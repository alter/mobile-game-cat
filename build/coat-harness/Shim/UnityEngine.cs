// A UnityEngine small enough to render a coat with, and no smaller.
//
// The point of the harness is that CoatBuilder.cs and CoatMasks.cs are the
// files Unity compiles, not copies of them, so anything this shim gets wrong
// shows up as a difference between the harness and the phone — the one failure
// mode that would make the whole exercise worse than useless. So the rules the
// device actually follows are written down here rather than guessed at:
//
//   * Texture2D.GetPixels32 returns rows BOTTOM-UP. Row 0 is the bottom of the
//     picture. Both CoatBuilder and CoatMasks depend on this — "rows run
//     bottom-up in a texture, so the head is the HIGH row index" is load-bearing
//     for the head box, the paws mask and the grime gradient. Png.cs flips on
//     load and on save so the arrays here are in Unity's order.
//   * Color32 holds the raw sRGB bytes. The cat silhouettes are imported with
//     sRGBTexture: 1 and textureCompression: 0, so GetPixels32 on the device
//     hands back exactly the bytes in the file, with no colour conversion.
//   * (byte)Mathf.Clamp(f, 0, 255) truncates towards zero, as in C#.
//
// Anything the coat pass does not use is absent rather than stubbed to a
// plausible-looking default. A missing member is a compile error, which is a
// question; a wrong default is a silent divergence, which is a bug.

using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Color
    {
        public float r, g, b, a;

        public Color(float r, float g, float b, float a = 1f)
        {
            this.r = r; this.g = g; this.b = b; this.a = a;
        }

        public static Color white => new Color(1f, 1f, 1f, 1f);
        public static Color black => new Color(0f, 0f, 0f, 1f);

        // Unity multiplies all four channels, alpha included. Nothing in the
        // coat pass reads the alpha of a palette colour, but matching it costs
        // nothing and removes a thing to wonder about.
        public static Color operator *(Color c, float k)
            => new Color(c.r * k, c.g * k, c.b * k, c.a * k);

        public static Color Lerp(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t,
                             a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }

        public static implicit operator Color(Color32 c)
            => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
    }

    public struct Color32
    {
        public byte r, g, b, a;

        public Color32(byte r, byte g, byte b, byte a)
        {
            this.r = r; this.g = g; this.b = b; this.a = a;
        }

        // Deliberately the ONLY constructor, as in Unity. An `(int,int,int,int)`
        // overload alongside it is ambiguous for `new Color32(x, y, z, 255)`
        // where the first three are bytes — which is what CoatBuilder writes —
        // and worse, it would silently accept an out-of-range int where the real
        // Unity forces the caller's own cast and clamp to be visible.

        // Unity rounds each channel; it does not truncate. The difference is
        // half a level, but Color32.Lerp is used in Reshape on every pixel of a
        // narrowed body and a systematic half-level bias there would show up as
        // the harness and the device disagreeing about the mean.
        public static Color32 Lerp(Color32 a, Color32 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32(
                (byte)Mathf.RoundToInt(a.r + (b.r - a.r) * t),
                (byte)Mathf.RoundToInt(a.g + (b.g - a.g) * t),
                (byte)Mathf.RoundToInt(a.b + (b.b - a.b) * t),
                (byte)Mathf.RoundToInt(a.a + (b.a - a.a) * t));
        }

        public static implicit operator Color32(Color c)
            => new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255));
    }

    public static class Mathf
    {
        public const float PI = (float)Math.PI;

        public static float Clamp(float v, float lo, float hi)
            => v < lo ? lo : (v > hi ? hi : v);
        public static int Clamp(int v, int lo, int hi)
            => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;

        public static float Abs(float v) => Math.Abs(v);
        public static int Abs(int v) => Math.Abs(v);

        // Unity's RoundToInt is banker's rounding (Math.Round's default), not
        // away-from-zero. It matters where a radius is derived from a width.
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.ToEven);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Pow(float v, float p) => (float)Math.Pow(v, p);
        public static float Exp(float v) => (float)Math.Exp(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);

        public static bool Approximately(float a, float b)
            => Math.Abs(b - a) < Math.Max(1e-6f * Math.Max(Math.Abs(a), Math.Abs(b)), 1e-45f * 8f);
    }

    /// <summary>
    /// The device's log, on the console. Kept rather than silenced: the numbers
    /// CoatBuilder prints — how many pixels Deband lifted, what it scaled the
    /// value back by — are the only running commentary the pass has, and this
    /// harness exists to read them without an `adb logcat`.
    /// </summary>
    public static class Debug
    {
        public static void Log(object m) => Console.WriteLine($"       {m}");
        public static void LogWarning(object m) => Console.WriteLine($"  warn {m}");
        public static void LogError(object m) => Console.WriteLine($" ERROR {m}");
    }

    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp }
    public enum RenderTextureFormat { ARGB32 }
    public enum RenderTextureReadWrite { Default, Linear, sRGB }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h)
        {
            this.x = x; this.y = y; width = w; height = h;
        }
    }

    public class Object
    {
        public string name;
        public static void Destroy(Object o) { }
        public static void DestroyImmediate(Object o) { }
    }

    public class Texture2D : Object
    {
        private Color32[] _px;

        public int width { get; private set; }
        public int height { get; private set; }
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }

        /// <summary>
        /// True for everything the harness makes. On the device this is an
        /// import setting, and the three cat silhouettes carry `isReadable: 1`
        /// for the reason CoatBuilder.ReadPixels records at length: the other
        /// path stops the iOS simulator drawing anything for the rest of the
        /// run. The harness has no GPU and no import settings, so the honest
        /// answer here is the one the device gives for these files.
        /// </summary>
        public bool isReadable => true;

        public Texture2D(int w, int h, TextureFormat format = TextureFormat.RGBA32,
                         bool mipChain = false)
        {
            width = w; height = h;
            _px = new Color32[w * h];
        }

        public Color32[] GetPixels32() => (Color32[])_px.Clone();

        public void SetPixels32(Color32[] px)
        {
            if (px.Length != width * height)
                throw new ArgumentException($"{px.Length} pixels for {width}x{height}");
            _px = (Color32[])px.Clone();
        }

        public void Apply(bool updateMipmaps = true) { }

        /// <summary>Part of the blit path only; see the note at the foot of
        /// this file for why it refuses rather than approximates.</summary>
        public void ReadPixels(Rect source, int destX, int destY)
            => throw new NotSupportedException("no GPU in the coat harness");

        public bool LoadImage(byte[] data)
        {
            var img = Png.Decode(data);
            if (img == null) return false;
            width = img.Width; height = img.Height; _px = img.Pixels;
            return true;
        }

        public byte[] EncodeToPNG() => Png.Encode(_px, width, height);
    }

    /// <summary>
    /// Resources.Load, backed by a real directory. Pointed at
    /// game/Assets/Resources by the harness at start-up, so `Art/cat_2_short_base`
    /// finds the file Unity would find, and a mask that does not exist returns
    /// null here exactly as it does there — which is the branch CoatBuilder.MaskOf
    /// takes for all 27 of them today.
    /// </summary>
    public static class Resources
    {
        public static string Root;

        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        public static T Load<T>(string path) where T : class
        {
            if (typeof(T) != typeof(Texture2D)) return null;
            if (_cache.TryGetValue(path, out var hit)) return hit as T;

            var file = System.IO.Path.Combine(Root, path + ".png");
            Texture2D tex = null;
            if (System.IO.File.Exists(file))
            {
                var img = Png.Decode(System.IO.File.ReadAllBytes(file));
                if (img != null)
                {
                    tex = new Texture2D(img.Width, img.Height);
                    tex.SetPixels32(img.Pixels);
                    tex.name = System.IO.Path.GetFileNameWithoutExtension(file);
                }
            }
            _cache[path] = tex;
            return tex as T;
        }
    }

    /// <summary>
    /// Where the coat cache and the diagnostic switches live. The harness gets
    /// its own directory per run, so a coat cached by a previous run can never
    /// be mistaken for one this run built — which is the exact trap CoatVersion
    /// was added to close on the device.
    /// </summary>
    public static class Application
    {
        public static string persistentDataPath;
    }

    // ---------------------------------------------------------------
    // The GPU path, which this harness does not have
    // ---------------------------------------------------------------
    //
    // CoatBuilder.ReadPixels falls back to a RenderTexture blit when a texture
    // is not readable. Every texture here is readable, so the fallback is
    // unreachable — and it throws rather than returning something, because a
    // harness that quietly produced a different picture from the device on the
    // one path nobody can see would be worse than no harness.

    public class RenderTexture : Object
    {
        public static RenderTexture active { get; set; }

        public static RenderTexture GetTemporary(int w, int h, int depth,
                                                 RenderTextureFormat f,
                                                 RenderTextureReadWrite rw)
            => throw new NotSupportedException(
                "the coat harness has no GPU: a texture reached ReadPixels' blit " +
                "fallback, which means it was not readable — on the device that " +
                "path blanks the iOS simulator, so it is a failure here too");

        public static void ReleaseTemporary(RenderTexture rt) { }
    }

    public static class Graphics
    {
        public static void Blit(Texture2D src, RenderTexture dst)
            => throw new NotSupportedException("no GPU in the coat harness");
    }
}
