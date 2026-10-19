using System.Collections.Generic;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Counts ragdolls inside a rolling window: the 3rd within 20 s is a knockout (player-character rule 8).
    /// </summary>
    public sealed class KnockoutTracker
    {
        readonly Queue<float> _times = new Queue<float>();
        readonly int _count;
        readonly float _window;

        /// <summary>Creates a tracker for <paramref name="count"/> ragdolls inside <paramref name="window"/> seconds.</summary>
        public KnockoutTracker(int count, float window) { _count = count; _window = window; }

        /// <summary>Records a ragdoll at <paramref name="now"/>; returns true if this one makes a knockout.</summary>
        public bool RegisterRagdoll(float now)
        {
            while (_times.Count > 0 && now - _times.Peek() > _window) _times.Dequeue();
            _times.Enqueue(now);
            if (_times.Count < _count) return false;
            _times.Clear();
            return true;
        }

        /// <summary>Forgets every recorded ragdoll.</summary>
        public void Reset() => _times.Clear();
    }

    /// <summary>
    /// Grip slip rule (physics-carry rule 3): stretched beyond SlipDistance for SlipTime seconds breaks the grip.
    /// </summary>
    public sealed class SlipTracker
    {
        readonly float _distance;
        readonly float _time;
        float _over;

        /// <summary>Creates a tracker.</summary>
        public SlipTracker(float slipDistance, float slipTime) { _distance = slipDistance; _time = slipTime; }

        /// <summary>Feeds the current stretch; returns true when the grip slips.</summary>
        public bool Tick(float stretch, float dt)
        {
            _over = stretch > _distance ? _over + dt : 0f;
            return _over >= _time;
        }
    }

    /// <summary>Rest detector for ragdoll get-up (speed below threshold for a duration).</summary>
    public sealed class RestTracker
    {
        readonly float _speed;
        readonly float _time;
        float _still;

        /// <summary>Creates a tracker.</summary>
        public RestTracker(float restSpeed, float restTime) { _speed = restSpeed; _time = restTime; }

        /// <summary>True once the body has been slow for long enough.</summary>
        public bool AtRest => _still >= _time;

        /// <summary>Feeds the body speed.</summary>
        public void Tick(float speed, float dt) => _still = speed < _speed ? _still + dt : 0f;

        /// <summary>Starts over.</summary>
        public void Reset() => _still = 0f;
    }
}
