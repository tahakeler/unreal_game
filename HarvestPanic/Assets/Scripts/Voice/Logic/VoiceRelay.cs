using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.Voice
{
    /// <summary>What the host does with one voice frame for one listener.</summary>
    public enum VoiceRoute : byte { Deliver = 0, Self = 1, Muted = 2, OutOfRange = 3 }

    /// <summary>
    /// Host relay rules (adr-voice-chat section 3). Pure so EditMode tests pin them: never echo to the speaker,
    /// honour the listener's mute list, drop listeners beyond range + margin. When either head position is unknown
    /// (player object not spawned yet) the frame is delivered and the receiver plays it non-spatially.
    /// </summary>
    public static class VoiceRelay
    {
        public static VoiceRoute Decide(ulong speaker, ulong listener, bool listenerMutedSpeaker, bool havePositions,
            Vector3 speakerPos, Vector3 listenerPos, float range, float margin)
        {
            if (speaker == listener) return VoiceRoute.Self;
            if (listenerMutedSpeaker) return VoiceRoute.Muted;
            if (havePositions && !VoiceMath.InRelayRange(speakerPos, listenerPos, range, margin)) return VoiceRoute.OutOfRange;
            return VoiceRoute.Deliver;
        }
    }

    /// <summary>Host-side mute lists: listener -> speakers that listener muted. Replaced wholesale by HP_VoiceMute.</summary>
    public sealed class VoiceMuteTable
    {
        public const int MaxEntries = 16;
        readonly Dictionary<ulong, HashSet<ulong>> _byListener = new Dictionary<ulong, HashSet<ulong>>();

        public void Set(ulong listener, IEnumerable<ulong> muted)
        {
            if (!_byListener.TryGetValue(listener, out var set)) _byListener[listener] = set = new HashSet<ulong>();
            set.Clear();
            if (muted == null) return;
            foreach (var id in muted)
            {
                if (set.Count >= MaxEntries) break;
                if (id != listener) set.Add(id);
            }
        }

        public bool IsMuted(ulong listener, ulong speaker) => _byListener.TryGetValue(listener, out var set) && set.Contains(speaker);

        public int CountFor(ulong listener) => _byListener.TryGetValue(listener, out var set) ? set.Count : 0;

        /// <summary>Forgets <paramref name="client"/> as a listener and as a muted speaker (disconnect).</summary>
        public void Remove(ulong client)
        {
            _byListener.Remove(client);
            foreach (var set in _byListener.Values) set.Remove(client);
        }

        public void Clear() => _byListener.Clear();
    }

    /// <summary>Host: voice loudness -> Noise Event at most once per interval per speaker (GDD proximity-voice rule 5).</summary>
    public sealed class VoiceNoiseGate
    {
        readonly Dictionary<ulong, double> _last = new Dictionary<ulong, double>();

        public bool ShouldEmit(ulong speaker, float loudness, double now, float threshold, float interval)
        {
            if (float.IsNaN(loudness) || loudness < threshold) return false;
            if (_last.TryGetValue(speaker, out double t) && now - t < interval) return false;
            _last[speaker] = now;
            return true;
        }

        public void Forget(ulong speaker) => _last.Remove(speaker);
        public void Clear() => _last.Clear();
    }
}
