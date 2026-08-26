using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 60-shell-build/10: a click and a tap on every successful placement,
    /// something stronger on a match. Only the good moments — a failed tap and
    /// the lose screen stay quiet, on purpose (SCOPE).
    ///
    /// The sounds are synthesised here rather than loaded from files. The game
    /// has no audio assets yet and this is the shape of the feedback, not its
    /// final voice: two short percussive blips, a low one for a placement and a
    /// brighter two-note one for a match. Replace both with real recordings
    /// when the art pass produces them — the call sites do not change.
    /// </summary>
    public sealed class Feedback : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private static Feedback _instance;
        private AudioSource _source;
        private AudioClip _place;
        private AudioClip _match;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CatHaptics_place();
        [DllImport("__Internal")] private static extern void CatHaptics_match();
        [DllImport("__Internal")] private static extern void CatHaptics_prepare();
#endif

        public static Feedback Attach(GameObject host)
        {
            if (_instance != null) return _instance;
            _instance = host.AddComponent<Feedback>();
            return _instance;
        }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _place = Blip("place", 220f, 0.07f, 0f);
            _match = Blip("match", 520f, 0.16f, 780f);
#if UNITY_IOS && !UNITY_EDITOR
            CatHaptics_prepare();
#endif
        }

        /// <summary>One item landed on the shelf.</summary>
        public static void Place()
        {
            if (_instance == null) return;
            _instance._source.PlayOneShot(_instance._place, 0.35f);
#if UNITY_IOS && !UNITY_EDITOR
            CatHaptics_place();
#endif
        }

        /// <summary>Three of a kind matched and left the shelf.</summary>
        public static void Match()
        {
            if (_instance == null) return;
            _instance._source.PlayOneShot(_instance._match, 0.5f);
#if UNITY_IOS && !UNITY_EDITOR
            CatHaptics_match();
#endif
        }

        /// <summary>
        /// A short percussive blip: a sine that decays fast enough to read as a
        /// tap rather than a tone. <paramref name="secondTone"/> above zero adds
        /// a rising second note, which is what makes the match cue distinct from
        /// the placement one without being louder.
        /// </summary>
        private static AudioClip Blip(string name, float hz, float seconds, float secondTone)
        {
            var samples = new float[Mathf.RoundToInt(SampleRate * seconds)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / samples.Length;
                float decay = Mathf.Exp(-14f * progress);
                float value = Mathf.Sin(2f * Mathf.PI * hz * t);
                if (secondTone > 0f && progress > 0.45f)
                    value = Mathf.Sin(2f * Mathf.PI * secondTone * t);
                samples[i] = value * decay;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
