using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.Audio
{
    /// <summary>Loads variants from <c>Resources/Audio/&lt;EVENT&gt;/</c> on first use; random pick with no immediate repeat.</summary>
    public sealed class AudioClipLibrary
    {
        public const string Root = "Audio/";
        readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        readonly Dictionary<string, int> _last = new Dictionary<string, int>();
        readonly System.Random _rng = new System.Random(7);

        /// <summary>All variants of <paramref name="evt"/> (empty array when none).</summary>
        public AudioClip[] Variants(string evt)
        {
            if (!_clips.TryGetValue(evt, out var arr))
            {
                arr = Resources.LoadAll<AudioClip>(Root + evt);
                System.Array.Sort(arr, (a, b) => string.CompareOrdinal(a.name, b.name));
                _clips[evt] = arr;
            }
            return arr;
        }

        /// <summary>A random variant that differs from the previous pick when possible.</summary>
        public AudioClip Pick(string evt)
        {
            var arr = Variants(evt);
            if (arr.Length == 0) return null;
            if (arr.Length == 1) return arr[0];
            _last.TryGetValue(evt, out int prev);
            int i = _rng.Next(arr.Length - 1);
            if (i >= prev) i++;
            _last[evt] = i;
            return arr[i];
        }

        /// <summary>The stem whose name ends with <paramref name="suffix"/> (e.g. "_base", "_lead").</summary>
        public AudioClip Stem(string evt, string suffix)
        {
            foreach (var c in Variants(evt)) if (c.name.EndsWith(suffix)) return c;
            return null;
        }

        public float RandomRange(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
    }
}
