using System.Collections;
using UnityEngine;

namespace HarvestPanic.UI
{
    /// <summary>Visual dinner bell: swings when rung, droops with a sad wobble when a countdown is cancelled.</summary>
    public sealed class DinnerBell : MonoBehaviour
    {
        public static DinnerBell Instance { get; private set; }
        [SerializeField] Transform bell;
        Coroutine _swing;

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        public void Ring() => Swing(35f, 0.9f);
        public void Sad() => Swing(10f, 0.5f);

        void Swing(float amp, float time)
        {
            if (bell == null) return;
            if (_swing != null) StopCoroutine(_swing);
            _swing = StartCoroutine(Run(amp, time));
        }

        IEnumerator Run(float amp, float time)
        {
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                float k = 1f - t / time;
                bell.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 22f) * amp * k);
                yield return null;
            }
            bell.localRotation = Quaternion.identity;
        }
    }
}
