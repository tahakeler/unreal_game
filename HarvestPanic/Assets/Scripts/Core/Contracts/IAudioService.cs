using System;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>User-facing volume buses (sound-bible §4.6 settings sliders). Add-only.</summary>
    public enum AudioBus { Master, Music, Sfx, Voice, UI }

    /// <summary>
    /// Volume settings target (provider: Audio; consumers: Frontend settings UI). Values are linear 0..1
    /// and persist per machine. Audio is presentation only, so these calls are local and never networked.
    /// </summary>
    public interface IAudioSettingsSink
    {
        /// <summary>Current linear volume 0..1 of <paramref name="bus"/>.</summary>
        float GetBusVolume(AudioBus bus);

        /// <summary>Sets and persists the linear volume 0..1 of <paramref name="bus"/>.</summary>
        void SetBusVolume(AudioBus bus, float linear01);
    }

    /// <summary>
    /// Local (client-side) sound playback. Provider: Audio (<c>HarvestPanic.Audio</c>); callers: any team.
    /// Event names follow <c>design/audio/sfx-list.md</c> (<c>SFX_*</c>, <c>STG_*</c>, <c>UI_*</c>); unknown names are ignored.
    /// Never emits Noise Events and never changes gameplay state; safe to call on host and clients.
    /// </summary>
    public interface IAudioService : IAudioSettingsSink
    {
        /// <summary>Plays <paramref name="eventName"/> as a 3D one-shot at <paramref name="position"/>. Returns false if unknown or culled.</summary>
        bool PlayAt(string eventName, Vector3 position, float volumeScale = 1f);

        /// <summary>Plays <paramref name="eventName"/> as a 2D (non-spatial) one-shot, e.g. UI. Returns false if unknown or culled.</summary>
        bool Play2D(string eventName, float volumeScale = 1f);

        /// <summary>Proximity-voice hook: true while any player within 15 m is speaking (drives music/critter ducking).</summary>
        void ReportVoiceActivity(bool anyNearbySpeaking);
    }

    /// <summary>
    /// Read-only day-loop phase for presentation (music). Provider: the integration bridge over the
    /// Economy day clock; consumer: Audio. <see cref="PhaseName"/> is the day-loop phase name
    /// ("None", "Morning", "Workday", "Dusk", "Payout", "Sunday", "Evicted"); valid on host and clients.
    /// </summary>
    public interface IDayPhaseInfo
    {
        /// <summary>Current phase name (day-loop §3 Phases).</summary>
        string PhaseName { get; }

        /// <summary>Seconds left in the current phase.</summary>
        float PhaseRemaining { get; }
    }
}
