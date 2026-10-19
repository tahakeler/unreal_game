using UnityEngine;

namespace HarvestPanic.World
{
    /// <summary>Cosmetic: bobs floating props (lilies, debris) on the water surface. Pure client-side visual.</summary>
    public sealed class Bobber : MonoBehaviour
    {
        [SerializeField] float amplitude = 0.08f;
        [SerializeField] float speed = 1.6f;
        Vector3 _base;
        void Start() => _base = transform.localPosition;
        void Update() => transform.localPosition = _base + Vector3.up * Mathf.Sin(Time.time * speed + _base.x) * amplitude;
    }
}
