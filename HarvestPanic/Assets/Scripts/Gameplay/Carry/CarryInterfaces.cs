using System;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>Optional veto on grabbing (e.g. players may only be grabbed while knocked out).</summary>
    public interface IGrabGate
    {
        /// <summary>True when <paramref name="clientId"/> may grab this now.</summary>
        bool CanBeGrabbedBy(ulong clientId);
    }

    /// <summary>
    /// A carryable whose physics is simulated by its own owner (player bodies, the wagon). The server-side
    /// <see cref="CarryBody"/> tracks grips but applies no forces and reports no impacts; the body moves itself.
    /// </summary>
    public interface ISelfSimulatedCarryable { }

    /// <summary>A held tool that reacts to the Use button.</summary>
    public interface IUsableTool
    {
        /// <summary>Server: Use pressed (true) or released (false) by <paramref name="user"/>.</summary>
        void ServerUse(bool down, PlayerCarry user);

        /// <summary>Prompt shown while holding, e.g. "RMB: Shake (7 left)".</summary>
        string UsePrompt { get; }
    }

    /// <summary>
    /// Optional on a held tool that wants the Interact (E) press while held (the lasso ties itself to a post).
    /// Owner side: when <see cref="InteractPrompt"/> is non-empty, E calls <see cref="ServerInteract"/> instead of
    /// putting the tool away.
    /// </summary>
    public interface IToolInteract
    {
        /// <summary>Owner: prompt for E ("E: Tie to post"), or null/empty to keep the default E action.</summary>
        string InteractPrompt { get; }

        /// <summary>Server: E pressed by <paramref name="user"/>. Returns false to fall back to the default (put away).</summary>
        bool ServerInteract(PlayerCarry user);
    }

    /// <summary>
    /// Optional on a self-simulated carryable (the wagon): replaces the carry-speed formula for the players holding it,
    /// so pushing a 120 kg cart is paced by the cart's own top speed instead of "lifting" 120 kg.
    /// </summary>
    public interface ICarryLoadOverride
    {
        /// <summary>Server: movement multiplier for each holder with <paramref name="holders"/> players attached.</summary>
        float CarrierSpeedMultiplier(int holders);
    }

    /// <summary>Optional custom E prompts for unusual carryables (wagon handles).</summary>
    public interface ICustomGrabPrompt
    {
        /// <summary>Prompt while aiming at it with free hands.</summary>
        string GrabPrompt { get; }
        /// <summary>Prompt while holding it.</summary>
        string HoldPrompt { get; }
    }

    /// <summary>
    /// A held tool or vehicle that drives a custom first-person arm pose (read on the owner from replicated state).
    /// Return <see cref="Gameplay.ArmPose.Tool"/> (tools) or <see cref="Gameplay.ArmPose.Idle"/> for "no override".
    /// </summary>
    public interface IToolArmPose
    {
        /// <summary>Pose to show now.</summary>
        ArmPose ArmPose { get; }
    }

    /// <summary>
    /// Server-side hand-over hook: raised right before something takes physical control of a networked body
    /// (a player grip in <see cref="CarryBody.TryAddGrip"/>, a lasso tether, a wagon loading cargo). Whoever held it
    /// before (a wagon slot, another lasso's yank) lets go so the new owner starts from a clean physics state.
    /// </summary>
    public static class CarryHooks
    {
        /// <summary>Server: (<c>body</c>, <c>claimant</c>) where claimant is the <see cref="PlayerCarry"/>, lasso or wagon.</summary>
        public static event Action<NetworkObject, object> Claiming;

        /// <summary>Server: announce that <paramref name="claimant"/> is taking <paramref name="body"/>.</summary>
        public static void RaiseClaiming(NetworkObject body, object claimant)
        {
            if (body != null) Claiming?.Invoke(body, claimant);
        }
    }
}
