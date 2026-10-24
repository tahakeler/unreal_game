using System;

namespace HarvestPanic.Core
{
    /// <summary>How the local microphone decides when to transmit.</summary>
    public enum VoiceTransmitMode { OpenMic = 0, PushToTalk = 1 }

    /// <summary>
    /// Proximity voice chat (provider: Voice, <c>HarvestPanic.Voice</c>; consumers: Frontend settings / crew list, HUD).
    /// Client-local presentation and preferences; never changes gameplay state. Muting is local (the host is told so it
    /// stops relaying that speaker to you). Add-only (docs/architecture/adr-voice-chat.md).
    /// </summary>
    public interface IVoiceChat
    {
        /// <summary>Open mic (voice-activity threshold) or push-to-talk (default key V). Default push-to-talk; Frontend settings persist and apply it.</summary>
        VoiceTransmitMode Mode { get; set; }

        /// <summary>Open-mic voice-activity threshold as loudness 0..1 (0 = always on). Frontend settings persist and apply it.</summary>
        float OpenMicThreshold { get; set; }

        /// <summary>Local mic mute: nothing is captured or sent while true.</summary>
        bool MicMuted { get; set; }

        /// <summary>True while the local player is sending voice.</summary>
        bool IsTransmitting { get; }

        /// <summary>Smoothed local mic loudness 0..1 (settings meter).</summary>
        float LocalLoudness { get; }

        /// <summary>Active backend name for UI/debug: "Steam", "Microphone", "None".</summary>
        string BackendName { get; }

        /// <summary>True while <paramref name="clientId"/> is heard speaking by this machine.</summary>
        bool IsSpeaking(ulong clientId);

        /// <summary>True when this machine muted <paramref name="clientId"/>.</summary>
        bool IsMuted(ulong clientId);

        /// <summary>Mutes or unmutes <paramref name="clientId"/> for this machine only.</summary>
        void SetMuted(ulong clientId, bool muted);

        /// <summary>Raised on this machine when a remote player starts (true) or stops (false) being heard.</summary>
        event Action<ulong, bool> OnSpeakingChanged;
    }
}
