using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>Layer masks used every frame by the tools and the wagon, built once (LayerMask.GetMask allocates).</summary>
    internal static class Masks
    {
        static int _water = -1, _bridge = -1, _cargo = -1, _aim = -1, _ground = -1, _groundOrWater = -1;

        /// <summary>The river volume.</summary>
        public static int Water => _water != -1 ? _water : _water = LayerMask.GetMask(GameLayers.Water);
        /// <summary>Bridge planks.</summary>
        public static int Bridge => _bridge != -1 ? _bridge : _bridge = LayerMask.GetMask(GameLayers.Bridge);
        /// <summary>What can ride in the wagon bed.</summary>
        public static int Cargo => _cargo != -1 ? _cargo : _cargo = LayerMask.GetMask(GameLayers.Carryable, GameLayers.Critter);
        /// <summary>Lasso aim ray: everything solid.</summary>
        public static int Aim => _aim != -1 ? _aim : _aim = ~LayerMask.GetMask(GameLayers.Water, "Ignore Raycast", "UI", GameLayers.Interactable);
        /// <summary>Static ground for wheels (no bodies, no water).</summary>
        public static int Ground => _ground != -1 ? _ground : _ground = ~LayerMask.GetMask(GameLayers.Player, GameLayers.Carryable, GameLayers.Critter, GameLayers.Ragdoll, GameLayers.Interactable, GameLayers.Water, "Ignore Raycast");
        /// <summary>Ground for a dragged critter, including the river surface (queried with triggers on).</summary>
        public static int GroundOrWater => _groundOrWater != -1 ? _groundOrWater : _groundOrWater = ~LayerMask.GetMask(GameLayers.Player, GameLayers.Critter, GameLayers.Carryable, GameLayers.Ragdoll, GameLayers.Interactable, "Ignore Raycast", "UI");
    }
}
