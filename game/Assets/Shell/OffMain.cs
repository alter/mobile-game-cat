using System;
using System.Threading;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 60-shell-build/19: the one place work that must not hold the main
    /// thread actually runs.
    ///
    /// <para><b>What it was called for.</b> The photograph path was five native
    /// calls made one after another from the main thread:
    /// <see cref="CatVision.Recognise"/>, <see cref="CatVision.Silhouette"/>
    /// (twice — once for the subject box, once for the coat),
    /// <see cref="CatPhoto.Prepare"/> and <see cref="CatMarks.Measure"/>. The
    /// Java side already runs its own work properly and already has ceilings on
    /// it — 30 s per analyse, 12 s for the module fetch — and C# stood in front
    /// of every one of them with <c>CallStatic</c>. Three analyse calls on one
    /// photograph is a minute and a half of main thread in the worst case, and
    /// Android is entitled to kill an application for a fraction of that. The
    /// player saw the other half of it: a still frame with a bar that had
    /// stopped moving, which is what a hung application looks like because it
    /// is what a hung application IS.</para>
    ///
    /// <para><b>Why a thread each and not a pool.</b> Five calls per
    /// photograph, one photograph per session. A pooled thread would save a few
    /// hundred microseconds of thread creation and cost the guarantee below:
    /// the JNI attachment is per thread, and a pool hands the same thread to
    /// somebody else's work afterwards.</para>
    ///
    /// <para><b>AttachCurrentThread is not optional.</b>
    /// <c>AndroidJavaClass</c> and <c>AndroidJavaObject</c> reach the VM
    /// through JNI, and JNI has no notion of a thread it has not been
    /// introduced to — a call from an unattached thread does not throw a
    /// managed exception, it takes the process down with a native abort, which
    /// under IL2CPP arrives as a tombstone and no C# stack. So every thread
    /// this class starts attaches first and detaches in a <c>finally</c>,
    /// including the failure path. Off Android the pair is compiled out: the
    /// iOS calls are <c>DllImport</c>, which has no such rule, and the editor
    /// has no VM at all.</para>
    ///
    /// <para><b>What may NOT go through here.</b> Anything touching the engine:
    /// <see cref="Texture2D"/>, the scene, and — the one that is easy to miss
    /// because it looks like a plain string — <see cref="Application"/>'s
    /// properties, <c>persistentDataPath</c> among them. Read those on the main
    /// thread and hand the value in; <see cref="CatCoat"/> does exactly that
    /// for its dump path.</para>
    /// </summary>
    public static class OffMain
    {
        /// <summary>
        /// A piece of work in flight. The main thread polls
        /// <see cref="Done"/> once a frame from a coroutine and reads
        /// <see cref="Value"/> after it turns true.
        ///
        /// <para>Two threads share this object and no lock guards it, which is
        /// safe for one reason worth writing down: <see cref="_done"/> is
        /// <c>volatile</c>, the worker writes the result BEFORE it writes
        /// <c>_done</c>, and the reader reads <c>_done</c> before the result. A
        /// volatile write publishes everything written before it and a volatile
        /// read sees everything published before the write it observed, so a
        /// reader that sees <c>Done</c> sees a fully-written
        /// <see cref="Value"/>. Reversing either order breaks it silently and
        /// only on a device.</para>
        /// </summary>
        public sealed class Call<T>
        {
            private volatile bool _done;
            private T _value;
            private Exception _fault;

            public bool Done => _done;

            /// <summary>
            /// The answer, once <see cref="Done"/>. <c>default</c> when the
            /// work threw — every caller on this path already has a meaning for
            /// a default answer (a <see cref="VisionAnswer"/> with
            /// <c>ok</c> false, a null crop, an empty box), so a fault does not
            /// need a branch of its own unless the caller wants one.
            /// </summary>
            public T Value => _value;

            /// <summary>What the work threw, or null. The type is all that may
            /// be logged — a native error string can name a player's file, the
            /// rule <see cref="CatVision"/> and <see cref="CatCoat"/> already
            /// follow.</summary>
            public Exception Fault => _fault;

            internal void Finish(T value, Exception fault)
            {
                _value = value;
                _fault = fault;
                _done = true;       // last, and volatile: see the note above
            }
        }

        /// <summary>
        /// Start <paramref name="work"/> on a thread of its own and hand back
        /// something the caller can poll a frame at a time.
        /// </summary>
        /// <param name="what">
        /// A few words for the log. Never the input and never a path.
        /// </param>
        public static Call<T> Run<T>(Func<T> work, string what)
        {
            var call = new Call<T>();
            if (work == null)
            {
                call.Finish(default, null);
                return call;
            }

            var thread = new Thread(() =>
            {
                var value = default(T);
                Exception fault = null;
#if UNITY_ANDROID && !UNITY_EDITOR
                AndroidJNI.AttachCurrentThread();
#endif
                try
                {
                    value = work();
                }
                catch (Exception e)
                {
                    fault = e;
                    Debug.LogWarning($"[OffMain] {what} threw {e.GetType().Name}");
                }
                finally
                {
#if UNITY_ANDROID && !UNITY_EDITOR
                    // In the finally, so a throw does not leave the VM holding
                    // a reference to a thread that has gone.
                    AndroidJNI.DetachCurrentThread();
#endif
                    call.Finish(value, fault);
                }
            })
            {
                // Background, so a call still waiting on a 30-second native
                // ceiling cannot keep the process alive after the player has
                // left.
                IsBackground = true,
                Name = "off-main " + what,
            };

            try
            {
                thread.Start();
            }
            catch (Exception e)
            {
                // A thread that will not start is a device out of room, not a
                // reason to lose the photograph — and a Call that never
                // finishes would hang the coroutine polling it forever, which
                // is worse than the block this class was written to remove. So
                // it runs here instead, on the main thread, exactly as it did
                // before this task.
                Debug.LogWarning($"[OffMain] {what} could not get a thread " +
                                 $"({e.GetType().Name}); running it here");
                try { call.Finish(work(), null); }
                catch (Exception inner) { call.Finish(default, inner); }
            }

            return call;
        }
    }
}
