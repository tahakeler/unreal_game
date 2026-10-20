namespace HarvestPanic.Core
{
    /// <summary>
    /// First-person motion-comfort values written by Frontend UI from player settings and read by the Player camera
    /// (PlayerView). Client-local presentation only. Defaults leave tuning untouched. Add-only contract.
    /// </summary>
    public static class CameraComfort
    {
        /// <summary>Base field of view in degrees; 0 or less = use the tuning asset value.</summary>
        public static float BaseFov;
        /// <summary>Head-bob amplitude multiplier (0 = off).</summary>
        public static float BobScale = 1f;
        /// <summary>Impact-shake multiplier (0 = off).</summary>
        public static float ShakeScale = 1f;
        /// <summary>Sprint FOV kick multiplier (0 = off; reduced motion).</summary>
        public static float FovKickScale = 1f;

        /// <summary>Restores defaults (tests).</summary>
        public static void Reset() { BaseFov = 0f; BobScale = ShakeScale = FovKickScale = 1f; }
    }
}
