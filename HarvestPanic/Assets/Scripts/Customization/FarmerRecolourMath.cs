using UnityEngine;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// CPU mirror of HarvestPanic/Characters/FarmerRecolour (keep the two in step; EditMode tests compare them):
    /// per zone c = texel + mask * s * (tint - ref), s = clamp(lum(texel) / lum(ref), 0, 1.6), all linear colour.
    /// A texel is s * ref + painted detail, so a zone becomes s * tint + detail; tint == ref is the identity.
    /// </summary>
    public static class FarmerRecolourMath
    {
        /// <summary>Rec. 601 luma weights, as the shader uses.</summary>
        public static float Luminance(Vector3 c) => 0.299f * c.x + 0.587f * c.y + 0.114f * c.z;

        /// <summary>One zone applied to <paramref name="c"/> (running colour) for a texel of luminance <paramref name="lum"/>.</summary>
        public static Vector3 Zone(Vector3 c, float lum, Vector4 refColour, Vector3 tint, float mask)
        {
            float shade = Mathf.Clamp(lum / Mathf.Max(refColour.w, 0.02f), 0f, 1.6f);
            return c + Mathf.Clamp01(mask) * shade * (tint - (Vector3)refColour);
        }

        /// <summary>
        /// Recoloured linear albedo. <paramref name="mask"/> = (overalls, shirt, boots/gloves, skin), <paramref name="regions"/>.y
        /// = band, .z = hair; <paramref name="refs"/> and <paramref name="tints"/> in FarmerVisual zone order
        /// (overalls, shirt, boots/gloves, skin, hair, band).
        /// </summary>
        public static Vector3 Apply(Vector3 texel, Vector4 mask, Vector4 regions, Vector4[] refs, Vector3[] tints)
        {
            float lum = Luminance(texel);
            var c = texel;
            c = Zone(c, lum, refs[FarmerVisual.ZonePrimary], tints[FarmerVisual.ZonePrimary], mask.x);
            c = Zone(c, lum, refs[FarmerVisual.ZoneSecondary], tints[FarmerVisual.ZoneSecondary], mask.y);
            c = Zone(c, lum, refs[FarmerVisual.ZoneAccent], tints[FarmerVisual.ZoneAccent], mask.z);
            c = Zone(c, lum, refs[FarmerVisual.ZoneSkin], tints[FarmerVisual.ZoneSkin], mask.w);
            c = Zone(c, lum, refs[FarmerVisual.ZoneBand], tints[FarmerVisual.ZoneBand], regions.y);
            c = Zone(c, lum, refs[FarmerVisual.ZoneHair], tints[FarmerVisual.ZoneHair], regions.z);
            return new Vector3(Mathf.Clamp01(c.x), Mathf.Clamp01(c.y), Mathf.Clamp01(c.z));
        }
    }
}
