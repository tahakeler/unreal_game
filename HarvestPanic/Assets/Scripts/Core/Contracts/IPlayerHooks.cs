using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>
    /// Attachment points the Player prefab exposes so Customization can dress it without referencing
    /// the Player assembly. The Player team implements this on the player root; Customization finds it
    /// with <c>GetComponentInParent&lt;IPlayerHooks&gt;()</c>.
    /// </summary>
    public interface IPlayerHooks
    {
        /// <summary>Owning NGO client id.</summary>
        ulong ClientId { get; }

        /// <summary>True for the locally controlled player (first-person arms visible, body hidden).</summary>
        bool IsLocal { get; }

        /// <summary>Third-person body mesh (cosmetics swap its materials/meshes).</summary>
        Renderer BodyRenderer { get; }

        /// <summary>Animator driving the body.</summary>
        Animator BodyAnimator { get; }

        /// <summary>Root of the first-person arms (local player only; may be null).</summary>
        Transform FirstPersonArms { get; }

        /// <summary>Head bone attach point for hats and glasses.</summary>
        Transform HeadAttachPoint { get; }

        /// <summary>Plays an emote by id (e.g. "wave", "shout"); also generates the matching noise.</summary>
        void PlayEmote(string emoteId);

        /// <summary>Starts or ends the faint/ragdoll-collapse pose.</summary>
        void Faint(bool fainted);
    }
}
