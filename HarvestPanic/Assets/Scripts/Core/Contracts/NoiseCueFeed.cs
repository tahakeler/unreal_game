using System;

namespace HarvestPanic.Core
{
    /// <summary>
    /// Client-side presentation feed of noise events (add-only, polish-2). <see cref="INoiseService.OnNoise"/> fires on
    /// the server only; this feed fires on EVERY peer for every accepted noise so captions, tips and local reactions
    /// work for clients too. Raised by the server's NoiseService (host/offline) and by Audio's AudioNetRelay on remote
    /// clients. Presentation only: never change gameplay state from it. The local player's own footsteps are not
    /// relayed back to their owner (played locally, predicted), so owners do not see their own footstep cues here.
    /// </summary>
    public static class NoiseCueFeed
    {
        /// <summary>Raised on every peer (main thread).</summary>
        public static event Action<NoiseEvent> OnNoiseCue;

        /// <summary>Number of cues raised in this process (tests/diagnostics).</summary>
        public static int RaisedCount { get; private set; }

        /// <summary>Raise a cue locally. Callers: NoiseService (server) and AudioNetRelay (remote clients).</summary>
        public static void Raise(in NoiseEvent e)
        {
            RaisedCount++;
            OnNoiseCue?.Invoke(e);
        }
    }
}
