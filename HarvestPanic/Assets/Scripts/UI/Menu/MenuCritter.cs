using UnityEngine;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Menu-backdrop animal (visual model only, no AI or networking): grazes, ambles to a nearby spot, grazes again.
    /// An "escapee" also bolts across its dash line now and then (frontend-lobby title diorama: "a pig occasionally escapes
    /// across the yard"). Feet follow the copied terrain; the Animator's "Speed" float drives the walk cycle.
    /// </summary>
    public sealed class MenuCritter : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");

        [SerializeField] Animator animator;
        [SerializeField] Vector3 home;
        [SerializeField] float roam = 3.5f;
        [SerializeField] float walkSpeed = 0.9f;
        [SerializeField] bool escapee;
        [SerializeField] Vector3 dashFrom, dashTo;
        [SerializeField] float dashSpeed = 4.2f;
        [SerializeField] float dashEvery = 18f;
        [SerializeField] int seed = 1;

        System.Random _rng;
        Vector3 _target;
        float _wait, _nextDash;
        bool _dashing, _hasSpeed;

        /// <summary>Build-time setup (UiSceneBuilder).</summary>
        public void Configure(Animator anim, Vector3 homePos, float roamRadius, float speed, int rngSeed)
        { animator = anim; home = homePos; roam = roamRadius; walkSpeed = speed; seed = rngSeed; }

        public void ConfigureEscape(Vector3 from, Vector3 to, float speed, float every)
        { escapee = true; dashFrom = from; dashTo = to; dashSpeed = speed; dashEvery = every; }

        public bool IsDashing => _dashing;

        void Start()
        {
            _rng = new System.Random(seed);
            _target = transform.position;
            _wait = (float)_rng.NextDouble() * 3f;
            _nextDash = Time.time + dashEvery * (0.35f + (float)_rng.NextDouble() * 0.4f);
            if (animator != null)
                foreach (var p in animator.parameters) if (p.nameHash == SpeedId && p.type == AnimatorControllerParameterType.Float) _hasSpeed = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (escapee && !_dashing && Time.time >= _nextDash)
            {
                _dashing = true;
                transform.position = Grounded(dashFrom);
                _target = dashTo;
            }
            float speed = _dashing ? dashSpeed : walkSpeed;
            Vector3 pos = transform.position;
            Vector3 to = _target - pos; to.y = 0f;
            float moved = 0f;
            if (_wait > 0f && !_dashing) _wait -= dt;
            else if (to.magnitude > 0.08f)
            {
                var dir = to.normalized;
                moved = Mathf.Min(speed * dt, to.magnitude);
                pos += dir * moved;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-8f * dt));
                transform.position = Grounded(pos);
            }
            else if (_dashing)
            {
                _dashing = false;
                _nextDash = Time.time + dashEvery * (0.8f + (float)_rng.NextDouble() * 0.5f);
                transform.position = Grounded(home);
                _target = home;
                _wait = 1f;
            }
            else
            {
                _wait = 1.5f + (float)_rng.NextDouble() * 4f;
                float a = (float)_rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)_rng.NextDouble()) * roam;
                _target = home + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            }
            if (_hasSpeed) animator.SetFloat(SpeedId, dt > 0f ? moved / dt : 0f);
        }

        static Vector3 Grounded(Vector3 p)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var o = t.GetPosition(); var size = t.terrainData.size;
                if (p.x < o.x || p.z < o.z || p.x > o.x + size.x || p.z > o.z + size.z) continue;
                p.y = o.y + t.SampleHeight(p);
                return p;
            }
            return p;
        }
    }
}
