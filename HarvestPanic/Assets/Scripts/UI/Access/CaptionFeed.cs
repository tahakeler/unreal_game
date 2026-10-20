using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.UI
{
    /// <summary>One on-screen caption line.</summary>
    public sealed class Caption
    {
        public string Text;
        public int Priority;
        public float ExpiresAt;
        public bool Danger;
    }

    /// <summary>
    /// Caption list rules (onboarding-accessibility edge case): at most <see cref="MaxLines"/> lines; a new caption replaces
    /// the oldest line of the lowest priority, and is dropped if every line outranks it. Identical text refreshes its line.
    /// </summary>
    public sealed class CaptionFeed
    {
        public int MaxLines = 3;
        public float Lifetime = 3f;
        readonly List<Caption> _lines = new List<Caption>();
        public IReadOnlyList<Caption> Lines => _lines;

        public bool Push(string text, int priority, float now, bool danger = false)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var l in _lines)
                if (l.Text == text) { l.ExpiresAt = now + Lifetime; return true; }
            if (_lines.Count >= MaxLines)
            {
                int victim = -1;
                for (int i = 0; i < _lines.Count; i++)
                    if (victim < 0 || _lines[i].Priority < _lines[victim].Priority) victim = i; // first = oldest of that priority
                if (_lines[victim].Priority > priority) return false;
                _lines.RemoveAt(victim);
            }
            _lines.Add(new Caption { Text = text, Priority = priority, ExpiresAt = now + Lifetime, Danger = danger });
            return true;
        }

        /// <summary>Drops expired lines; true when anything changed.</summary>
        public bool Expire(float now) => _lines.RemoveAll(l => l.ExpiresAt <= now) > 0;

        public void Clear() => _lines.Clear();

        /// <summary>Caption text for a noise, or null for sounds that carry no gameplay meaning (footsteps, plain talk).</summary>
        public static string Describe(in NoiseEvent e, out int priority, out bool danger)
        {
            priority = 1; danger = false;
            var t = e.Tags;
            if ((t & NoiseTag.PlankBreak) != 0) { priority = 3; danger = true; return "Plank snaps"; }
            if ((t & NoiseTag.Mandrake) != 0) { priority = 3; danger = true; return "Mandrake SHRIEKS"; }
            if ((t & NoiseTag.Scream) != 0) { priority = 2; return "Someone screams"; }
            if ((t & NoiseTag.Splash) != 0) { priority = 2; return "Splash"; }
            if ((t & NoiseTag.Squeal) != 0) { priority = 2; return "Pig squeals"; }
            if ((t & NoiseTag.Creak) != 0) { priority = 2; danger = true; return "Bridge creaks"; }
            if ((t & NoiseTag.WhistleLong) != 0) return "Long whistle";
            if ((t & NoiseTag.Whistle) != 0) return "Whistle";
            if ((t & NoiseTag.Metal) != 0) return "Metal clang";
            if ((t & NoiseTag.Food) != 0) return "Feed rattles";
            if ((t & NoiseTag.Squelch) != 0) return "Mud squelch";
            switch (e.Source)
            {
                case NoiseSource.Shout: priority = 2; return "Farmer hollers";
                case NoiseSource.Critter: return "Critter noises";
                case NoiseSource.Hazard: priority = 2; danger = true; return "Danger nearby";
                case NoiseSource.Impact: return e.Loudness >= 0.6f ? "Loud crash" : null;
                case NoiseSource.Vehicle: return "Wagon rattles";
                case NoiseSource.Tool: return "Tool clatters";
                case NoiseSource.Scripted: return null;
                default: return null; // Voice (proximity chat has its own subtitles), Footstep, Crop
            }
        }

        /// <summary>Direction hint relative to the listener: "&lt;", "&gt;", "^" (ahead) or "v" (behind); "" when close.</summary>
        public static string Direction(Vector3 listenerPos, Vector3 listenerForward, Vector3 source)
        {
            var d = source - listenerPos; d.y = 0f;
            if (d.sqrMagnitude < 4f) return "";
            listenerForward.y = 0f;
            if (listenerForward.sqrMagnitude < 1e-4f) listenerForward = Vector3.forward;
            float angle = Vector3.SignedAngle(listenerForward, d, Vector3.up);
            if (Mathf.Abs(angle) <= 45f) return "^";
            if (Mathf.Abs(angle) >= 135f) return "v";
            return angle > 0f ? ">" : "<";
        }
    }
}
