namespace HarvestPanic.Core
{
    /// <summary>
    /// Conversion between Unity physics impulses and GDD "design units". Every GDD impulse threshold
    /// (bruise 400, ragdoll 900, KO 1800, plank 800, charge impulses) is in design units, which equal the Unity
    /// collision impulse (N*s) multiplied by <see cref="ImpulseScale"/>. Convert with
    /// <c>ImpactUnits.ToDesign(collision.impulse.magnitude)</c> before comparing to a threshold.
    /// </summary>
    public static class ImpactUnits
    {
        /// <summary>Design units per Unity N*s.</summary>
        public const float ImpulseScale = 10f;

        /// <summary>Unity impulse (N*s) to design units.</summary>
        public static float ToDesign(float unityImpulse) => unityImpulse * ImpulseScale;

        /// <summary>Design units to Unity impulse (N*s).</summary>
        public static float ToUnity(float designUnits) => designUnits / ImpulseScale;
    }
}
