using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// The farm's hitching post (networked prop spawned by <see cref="WagonBootstrapper"/>): a lasso can be tied to its
    /// <see cref="LassoTiePoint"/> to park a lassoed animal (physics-carry rule 7, hitch length 3 m).
    /// </summary>
    public sealed class HitchingPost : MonoBehaviour
    {
        /// <summary>The knot point on top of the post.</summary>
        public LassoTiePoint Tie => GetComponentInChildren<LassoTiePoint>();
    }
}
