using UnityEngine;

namespace HarvestPanic.World
{
    /// <summary>Cosmetic constant rotation (windmill sails, weather vane). Client-side only.</summary>
    public sealed class Spinner : MonoBehaviour
    {
        [SerializeField] Vector3 axis = Vector3.forward;
        [SerializeField] float degreesPerSecond = 25f;
        /// <summary>Editor setup.</summary>
        public void Configure(Vector3 localAxis, float speed) { axis = localAxis; degreesPerSecond = speed; }
        void Update() => transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
