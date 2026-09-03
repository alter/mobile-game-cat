using System;
using System.IO;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 60-shell-build/08: where the save lives. `Core.GameSave` decides
    /// what a save *is* — a string — and knows nothing of files or of Unity;
    /// this is the only place that touches the disk.
    ///
    /// Written on every move (DECISIONS.md D12), so it is written hundreds of
    /// times per level. iOS can kill the process at any point, including inside
    /// a write, so the file is replaced atomically: a fully-written temporary
    /// file is moved over the old one. A half-written save would be worse than
    /// no save, because it would swallow the position that was still good.
    /// </summary>
    public static class SaveFile
    {
        private const string FileName = "board.save";
        private const string TempName = "board.save.tmp";

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
                // An unreadable save is the same as no save: the player loses a
                // pile, not the app.
                Debug.LogWarning($"[SaveFile] read failed, starting fresh: {e.Message}");
                return null;
            }
        }

        public static void Write(string text)
        {
            try
            {
                File.WriteAllText(TempPath, text);
                // File.Copy is not a move: it truncates and rewrites the
                // destination in place, so a kill mid-copy leaves exactly the
                // half-written file this class exists to avoid. File.Replace
                // (or Move, when there is nothing yet to replace) renames the
                // temp file onto the target, which the filesystem commits as
                // one step.
                if (File.Exists(Path))
                    File.Replace(TempPath, Path, null);
                else
                    File.Move(TempPath, Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveFile] write failed: {e.Message}");
            }
        }

        /// <summary>
        /// Throw the position away.
        ///
        /// Not quite a delete since 60-shell-build/28: whatever
        /// <see cref="Core.GameSave.Residue"/> says must outlive a position is
        /// written back in its place — today that is one line saying the first
        /// lesson has been played. A loss on the first pile used to delete the
        /// whole file, and the replay then taught the player to tap three boards
        /// all over again, which is the one thing that task's SCOPE rules out.
        /// The residue is not a resumable save and `GameSave.Read` rejects it,
        /// so every caller asking for a position still gets a fresh board,
        /// exactly as it did when the file was simply gone.
        /// </summary>
        public static void Clear()
        {
            try
            {
                var residue = Core.GameSave.Residue(Read());
                if (residue != null)
                {
                    Write(residue);
                    return;
                }
                if (File.Exists(Path)) File.Delete(Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveFile] delete failed: {e.Message}");
            }
        }
    }
}
