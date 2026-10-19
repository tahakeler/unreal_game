using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace HarvestPanic.AI
{
    /// <summary>
    /// Visuals for a critter on every peer: drives the Animator <c>Speed</c> float on the authority (synced to clients by
    /// NetworkAnimator) and adds procedural beats on the model child from the replicated state: the Alert tell hop,
    /// carried wriggle, stunned wobble and PlayDead roll.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CritterPresentation : NetworkBehaviour
    {
        /// <summary>Animator float parameter: horizontal speed in m/s.</summary>
        public const string SpeedParam = "Speed";

        [SerializeField, Tooltip("Child holding the mesh (procedural offsets are applied here)")] Transform model;
        [SerializeField] Animator animator;
        [SerializeField, Min(0)] float tellHopHeight = 0.12f;
        [SerializeField, Min(0)] float speedDamp = 0.1f;

        static readonly int SpeedHash = Animator.StringToHash(SpeedParam);
        CritterBrain _brain;
        NavMeshAgent _agent;
        Vector3 _modelPos;
        Quaternion _modelRot;
        Vector3 _lastPos;
        float _stateStart;
        CritterState _shown;

        /// <summary>Configures references (editor builder).</summary>
        public void Configure(Transform modelRoot, Animator anim) { model = modelRoot; animator = anim; }

        void Awake()
        {
            _brain = GetComponent<CritterBrain>();
            _agent = GetComponent<NavMeshAgent>();
            if (model != null) { _modelPos = model.localPosition; _modelRot = model.localRotation; }
            _lastPos = transform.position;
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var s = _brain != null ? _brain.State : CritterState.Idle;
            if (s != _shown) { _shown = s; _stateStart = Time.time; }
            float t = Time.time - _stateStart;

            bool authority = !IsSpawned || IsServer;
            if (animator != null && authority)
            {
                float speed;
                if (_agent != null && _agent.enabled) speed = new Vector3(_agent.velocity.x, 0f, _agent.velocity.z).magnitude;
                else { Vector3 d = transform.position - _lastPos; d.y = 0f; speed = d.magnitude / dt; }
                if (s == CritterState.Carried || s == CritterState.Stunned) speed = 0f;
                animator.SetFloat(SpeedHash, speed, speedDamp, dt);
            }
            _lastPos = transform.position;

            if (model == null) return;
            Vector3 pos = _modelPos;
            Quaternion rot = _modelRot;
            float tell = _brain != null ? _brain.Tuning.AlertTellSeconds : 0.4f;
            switch (s)
            {
                case CritterState.Alert when t < tell:
                    pos.y += Mathf.Sin(t / tell * Mathf.PI) * tellHopHeight;
                    break;
                case CritterState.Carried:
                    rot = Quaternion.Euler(Mathf.Sin(Time.time * 23f) * 12f, Mathf.Sin(Time.time * 17f) * 18f, Mathf.Sin(Time.time * 29f) * 10f) * rot;
                    break;
                case CritterState.Stunned:
                    rot = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 9f) * 15f) * rot;
                    break;
                case CritterState.Cornered when _brain != null && _brain.Personality != null && _brain.Personality.CorneredResponse == CorneredResponse.PlayDead:
                    rot = Quaternion.Euler(0f, 0f, Mathf.Min(1f, t * 4f) * 90f) * rot;
                    break;
            }
            model.localPosition = pos;
            model.localRotation = rot;
        }
    }
}
