using System.Collections.Generic;

namespace HarvestPanic.Networking
{
    /// <summary>
    /// Server-side ordering of joins against NGO scene switches (pure, EditMode tested; driven by SessionService).
    /// <para>
    /// Why: NGO 2.x synchronises a newly approved client at once, even while the server is mid scene load. The joiner's
    /// Synchronize then already lists the scene being loaded, and when the server finishes it also sends that scene's
    /// Load event to every connected client, joiner included. The joiner loads the scene twice ("Server Scene Handle
    /// (...) already exist!") and never gets its player. The mirror case, a switch started while a joiner is still
    /// synchronising, interleaves two loads on that joiner.
    /// </para>
    /// Rules:
    /// <list type="number">
    /// <item>A join that arrives while a server scene load is running or a switch is queued is held (NGO approval
    /// Pending) and released once the server has finished loading (its own LoadComplete; the Load event has then
    /// already gone out to the clients that were connected). A join held longer than <see cref="MaxHoldSeconds"/> is
    /// refused with a retry reason instead of timing out in NGO.</item>
    /// <item>A switch requested while approved joiners are still synchronising is queued until they finish. A joiner
    /// older than <see cref="MaxJoinerWaitSeconds"/> no longer blocks (a stuck client must not freeze the host).</item>
    /// </list>
    /// </summary>
    public sealed class SceneTransitionGate
    {
        /// <summary>Seconds a join may be held before it is refused (keep below NGO ClientConnectionBufferTimeout).</summary>
        public float MaxHoldSeconds = 15f;
        /// <summary>Seconds a queued switch waits for one joiner to finish synchronising.</summary>
        public float MaxJoinerWaitSeconds = 20f;
        /// <summary>Safety: a server load not reported finished after this long stops blocking joins.</summary>
        public float MaxSceneLoadSeconds = 130f;

        readonly Dictionary<ulong, float> _held = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, float> _joining = new Dictionary<ulong, float>();
        readonly List<ulong> _scratch = new List<ulong>();
        bool _loading;
        float _loadingSince;
        string _queued;
        float _queuedSince;

        /// <summary>True while the server is loading a scene (between its Load and LoadComplete).</summary>
        public bool ServerLoading => _loading;
        /// <summary>Scene waiting for joiners to finish synchronising (null when none).</summary>
        public string QueuedScene => _queued;
        /// <summary>Joins currently held.</summary>
        public int HeldCount => _held.Count;
        /// <summary>Approved joiners that have not finished synchronising.</summary>
        public int JoiningCount => _joining.Count;
        /// <summary>True when a new join must be held instead of approved now.</summary>
        public bool ShouldHoldJoins => _loading || _queued != null;

        /// <summary>True when <paramref name="clientId"/>'s join is being held.</summary>
        public bool IsHeld(ulong clientId) => _held.ContainsKey(clientId);

        /// <summary>Records a held join.</summary>
        public void Hold(ulong clientId, float now)
        {
            if (!_held.ContainsKey(clientId)) _held[clientId] = now;
        }

        /// <summary>A join was approved; the client now synchronises until <see cref="Synchronized"/>.</summary>
        public void Approved(ulong clientId, float now)
        {
            _held.Remove(clientId);
            _joining[clientId] = now;
        }

        /// <summary>The client finished its initial synchronisation.</summary>
        public void Synchronized(ulong clientId) => _joining.Remove(clientId);

        /// <summary>The client left (held, joining or connected).</summary>
        public void Disconnected(ulong clientId)
        {
            _held.Remove(clientId);
            _joining.Remove(clientId);
        }

        /// <summary>The server started loading a scene.</summary>
        public void ServerLoadStarted(float now)
        {
            _loading = true;
            _loadingSince = now;
        }

        /// <summary>The server finished loading (its Load event is out to every connected client).</summary>
        public void ServerLoadFinished() => _loading = false;

        /// <summary>
        /// Asks to switch to <paramref name="scene"/>. True: start it now. False: it is queued (replacing any queued
        /// switch) and <see cref="Tick"/> hands it back when the joiners are done.
        /// </summary>
        public bool RequestSwitch(string scene, float now)
        {
            if (_queued == null && !_loading && !JoinersBlock(now)) return true;
            if (_queued == null) _queuedSince = now;
            _queued = scene;
            return false;
        }

        /// <summary>
        /// Advances the gate. Fills <paramref name="release"/> (approve now) and <paramref name="refuse"/> (held too
        /// long) and returns the queued scene to start now, if any. While a switch is about to start nothing is
        /// released: those joins wait for that load too.
        /// </summary>
        public string Tick(float now, List<ulong> release, List<ulong> refuse)
        {
            release.Clear();
            refuse.Clear();
            if (_loading && now - _loadingSince > MaxSceneLoadSeconds) _loading = false;

            string start = null;
            if (_queued != null && !_loading && !JoinersBlock(now))
            {
                start = _queued;
                _queued = null;
            }

            if (_held.Count == 0) return start;
            _scratch.Clear();
            _scratch.AddRange(_held.Keys);
            bool hold = start != null || ShouldHoldJoins;
            foreach (var id in _scratch)
            {
                if (!hold) release.Add(id);
                else if (now - _held[id] >= MaxHoldSeconds) refuse.Add(id);
            }
            foreach (var id in refuse) _held.Remove(id);
            foreach (var id in release) _held.Remove(id);
            return start;
        }

        /// <summary>Forgets everything (session start/end).</summary>
        public void Reset()
        {
            _held.Clear();
            _joining.Clear();
            _loading = false;
            _queued = null;
        }

        bool JoinersBlock(float now)
        {
            foreach (var since in _joining.Values)
                if (now - since < MaxJoinerWaitSeconds) return true;
            return false;
        }
    }
}
