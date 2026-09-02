using System;
using System.IO;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 50-photo/09: where the cat's save lives — beside the board's,
    /// not in a second place with a second convention. Mirrors SaveFile.cs
    /// exactly: same directory, same atomic write through a temp file, same
    /// "log it and carry on" failure handling. `Core.CatSave` decides what
    /// the save *is* — a string, and how to parse one back without ever
    /// throwing; this is the only place that touches the disk for it.
    ///
    /// A missing or corrupt file is not this class's problem to solve: it
    /// hands back null exactly like SaveFile.Read() does, and the caller
    /// (GameBoot) falls back to Cat.Skipped — the same shape SaveResume
    /// uses for the board, so a launch never breaks over a save that will
    /// not parse.
    /// </summary>
    public static class CatSaveFile
    {
        private const string FileName = "cat.save";
        private const string TempName = "cat.save.tmp";

        private static string Dir => Application.persistentDataPath;
        public static string Path => System.IO.Path.Combine(Dir, FileName);
        private static string TempPath => System.IO.Path.Combine(Dir, TempName);

        /// <summary>The saved text, or null when there is nothing readable.</summary>
        public static string Read()
        {
            try
            {
                return File.Exists(Path) ? File.ReadAllText(Path) : null;
            }
            catch (Exception e)
            {
                // An unreadable cat save is the same as no save: she meets
                // the same cat again, not a crash on launch.
                Debug.LogWarning($"[CatSaveFile] read failed, starting fresh: {e.Message}");
                return null;
            }
        }

        public static void Write(string text)
        {
            try
            {
                File.WriteAllText(TempPath, text);
                // See SaveFile.Write: File.Copy truncates the destination in
                // place rather than moving onto it, so a kill mid-copy left a
                // half-written cat.save. File.Replace (Move when there is no
                // existing file to replace) renames instead, which the
                // filesystem commits as one step.
                if (File.Exists(Path))
                    File.Replace(TempPath, Path, null);
                else
                    File.Move(TempPath, Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CatSaveFile] write failed: {e.Message}");
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(Path)) File.Delete(Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CatSaveFile] delete failed: {e.Message}");
            }
        }
    }
}
