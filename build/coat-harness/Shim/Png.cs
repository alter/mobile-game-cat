// PNG in and PNG out, in about two hundred lines, so the harness needs no
// package and runs on a machine with nothing installed but dotnet.
//
// Only what the cat art actually is, is supported: 8 bits per channel,
// non-interlaced, colour type 6 (RGBA), 2 (RGB), 0 (grey) or 4 (grey+alpha).
// A file outside that returns null rather than a half-decoded picture — the
// three silhouettes are all 1024x1024 RGBA8, and art delivered later that is
// not should stop the harness rather than be silently mangled by it.
//
// Rows are flipped on the way in and on the way out. PNG stores the top row
// first; Unity's GetPixels32 hands back the bottom row first. Every "the head
// is the HIGH row index" comment in CoatBuilder and CoatMasks rests on that,
// so getting the flip wrong here would turn the head box into a tail box and
// the harness would confidently render something the phone never would.

using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace UnityEngine
{
    public sealed class PngImage
    {
        public int Width;
        public int Height;
        public Color32[] Pixels;   // bottom-up, as Unity hands them back
    }

    public static class Png
    {
        private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public static PngImage Decode(byte[] data)
        {
            if (data == null || data.Length < 8) return null;
            for (int i = 0; i < 8; i++) if (data[i] != Signature[i]) return null;

            int w = 0, h = 0, bitDepth = 0, colourType = 0, interlace = 0;
            var idat = new MemoryStream();

            int p = 8;
            while (p + 8 <= data.Length)
            {
                int len = ReadBE32(data, p);
                string type = System.Text.Encoding.ASCII.GetString(data, p + 4, 4);
                int body = p + 8;
                if (body + len > data.Length) return null;

                if (type == "IHDR")
                {
                    w = ReadBE32(data, body);
                    h = ReadBE32(data, body + 4);
                    bitDepth = data[body + 8];
                    colourType = data[body + 9];
                    interlace = data[body + 12];
                }
                else if (type == "IDAT")
                {
                    idat.Write(data, body, len);
                }
                else if (type == "IEND")
                {
                    break;
                }

                p = body + len + 4;   // + CRC
            }

            if (w <= 0 || h <= 0 || bitDepth != 8 || interlace != 0) return null;

            int channels = colourType switch
            {
                0 => 1,   // grey
                2 => 3,   // rgb
                4 => 2,   // grey + alpha
                6 => 4,   // rgba
                _ => 0,
            };
            if (channels == 0) return null;

            byte[] raw = Inflate(idat.ToArray());
            int stride = w * channels;
            if (raw.Length < (stride + 1) * h) return null;

            var px = new Color32[w * h];
            var previous = new byte[stride];
            var current = new byte[stride];

            for (int y = 0; y < h; y++)
            {
                int at = y * (stride + 1);
                byte filter = raw[at];
                Buffer.BlockCopy(raw, at + 1, current, 0, stride);
                Unfilter(filter, current, previous, channels, stride);

                // PNG's row 0 is the top; Unity's row 0 is the bottom.
                int row = (h - 1 - y) * w;
                for (int x = 0; x < w; x++)
                {
                    int c = x * channels;
                    byte r, g, b, a;
                    switch (channels)
                    {
                        case 1: r = g = b = current[c]; a = 255; break;
                        case 2: r = g = b = current[c]; a = current[c + 1]; break;
                        case 3: r = current[c]; g = current[c + 1]; b = current[c + 2]; a = 255; break;
                        default: r = current[c]; g = current[c + 1]; b = current[c + 2]; a = current[c + 3]; break;
                    }
                    px[row + x] = new Color32(r, g, b, a);
                }

                var swap = previous; previous = current; current = swap;
            }

            return new PngImage { Width = w, Height = h, Pixels = px };
        }

        public static byte[] Encode(Color32[] px, int w, int h)
        {
            int stride = w * 4;
            var raw = new byte[(stride + 1) * h];
            for (int y = 0; y < h; y++)
            {
                int at = y * (stride + 1);
                raw[at] = 0;   // filter None: the file is written once and read
                               // by eye, so a smaller file is worth nothing
                int row = (h - 1 - y) * w;   // flip back to PNG's top-down order
                for (int x = 0; x < w; x++)
                {
                    var c = px[row + x];
                    int o = at + 1 + x * 4;
                    raw[o] = c.r; raw[o + 1] = c.g; raw[o + 2] = c.b; raw[o + 3] = c.a;
                }
            }

            var outStream = new MemoryStream();
            outStream.Write(Signature, 0, 8);

            var ihdr = new byte[13];
            WriteBE32(ihdr, 0, w);
            WriteBE32(ihdr, 4, h);
            ihdr[8] = 8;    // bit depth
            ihdr[9] = 6;    // colour type: RGBA
            ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
            WriteChunk(outStream, "IHDR", ihdr);
            WriteChunk(outStream, "IDAT", Deflate(raw));
            WriteChunk(outStream, "IEND", Array.Empty<byte>());
            return outStream.ToArray();
        }

        // ---------------------------------------------------------------

        private static void Unfilter(byte filter, byte[] cur, byte[] prev, int bpp, int stride)
        {
            switch (filter)
            {
                case 0: break;
                case 1:
                    for (int i = bpp; i < stride; i++)
                        cur[i] = (byte)(cur[i] + cur[i - bpp]);
                    break;
                case 2:
                    for (int i = 0; i < stride; i++)
                        cur[i] = (byte)(cur[i] + prev[i]);
                    break;
                case 3:
                    for (int i = 0; i < stride; i++)
                    {
                        int left = i >= bpp ? cur[i - bpp] : 0;
                        cur[i] = (byte)(cur[i] + ((left + prev[i]) >> 1));
                    }
                    break;
                case 4:
                    for (int i = 0; i < stride; i++)
                    {
                        int a = i >= bpp ? cur[i - bpp] : 0;
                        int b = prev[i];
                        int c = i >= bpp ? prev[i - bpp] : 0;
                        int q = a + b - c;
                        int pa = Math.Abs(q - a), pb = Math.Abs(q - b), pc = Math.Abs(q - c);
                        int pick = (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c);
                        cur[i] = (byte)(cur[i] + pick);
                    }
                    break;
                default:
                    throw new InvalidDataException($"png filter {filter}");
            }
        }

        private static byte[] Inflate(byte[] zlib)
        {
            // ZLibStream arrived in .NET 6; before it the two-byte header had to
            // be skipped by hand and the Adler checksum ignored. Using the real
            // thing means a corrupt file is reported rather than silently
            // truncated.
            using var input = new MemoryStream(zlib);
            using var z = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            z.CopyTo(output);
            return output.ToArray();
        }

        private static byte[] Deflate(byte[] raw)
        {
            using var output = new MemoryStream();
            using (var z = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
                z.Write(raw, 0, raw.Length);
            return output.ToArray();
        }

        private static void WriteChunk(Stream s, string type, byte[] body)
        {
            var header = new byte[4];
            WriteBE32(header, 0, body.Length);
            s.Write(header, 0, 4);

            var typed = new byte[4 + body.Length];
            for (int i = 0; i < 4; i++) typed[i] = (byte)type[i];
            Buffer.BlockCopy(body, 0, typed, 4, body.Length);
            s.Write(typed, 0, typed.Length);

            var crc = new byte[4];
            WriteBE32(crc, 0, unchecked((int)Crc32(typed)));
            s.Write(crc, 0, 4);
        }

        private static int ReadBE32(byte[] d, int at)
            => (d[at] << 24) | (d[at + 1] << 16) | (d[at + 2] << 8) | d[at + 3];

        private static void WriteBE32(byte[] d, int at, int v)
        {
            d[at] = (byte)(v >> 24); d[at + 1] = (byte)(v >> 16);
            d[at + 2] = (byte)(v >> 8); d[at + 3] = (byte)v;
        }

        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }

        private static uint Crc32(byte[] data)
        {
            uint c = 0xFFFFFFFFu;
            foreach (var b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }
    }
}
