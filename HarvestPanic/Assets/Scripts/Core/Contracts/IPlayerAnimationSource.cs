namespace HarvestPanic.Core
{
    /// <summary>
    /// Gameplay state the third-person farmer body animates from (carry poses, throw wind-up, knocked down / get-up).
    /// Implemented by the Player root next to <see cref="IPlayerHooks"/>; read by Customization's FarmerVisual on every peer,
    /// so every member must be valid on remote copies (use replicated state). Locomotion speed and grounding are derived
    /// by the body from its own motion. Add-only contract.
    /// </summary>
    public interface IPlayerAnimationSource
    {
        /// <summary>Hands on the held object: 0 none, 1 one-handed, 2+ two-handed (replicated).</summary>
        int CarryHands { get; }

        /// <summary>True while holding a tool (net, bucket...) rather than goods (replicated).</summary>
        bool HoldingTool { get; }

        /// <summary>Throw wind-up 0..1 (owner only; 0 on remote copies).</summary>
        float ThrowCharge { get; }

        /// <summary>Ragdolled or knocked out: the body goes limp; when it turns false the body plays a get-up (replicated).</summary>
        bool IsDown { get; }
    }
}
