using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>
    /// Physics/render layer names defined in ProjectSettings/TagManager.asset (owner: Networking team).
    /// Use the masks for raycasts and overlaps instead of literal layer numbers.
    /// </summary>
    public static class GameLayers
    {
        /// <summary>Layer name: player capsules.</summary>
        public const string Player = "Player";
        /// <summary>Layer name: critters.</summary>
        public const string Critter = "Critter";
        /// <summary>Layer name: goods, tools and other carryables.</summary>
        public const string Carryable = "Carryable";
        /// <summary>Layer name: bridge planks and rails.</summary>
        public const string Bridge = "Bridge";
        /// <summary>Layer name: interaction/sale triggers.</summary>
        public const string Interactable = "Interactable";
        /// <summary>Layer name: ragdolled player bodies.</summary>
        public const string Ragdoll = "Ragdoll";
        /// <summary>Layer name: river/water volumes (Unity built-in Water layer).</summary>
        public const string Water = "Water";

        /// <summary>Layer index by name; throws if missing so a broken TagManager fails loudly.</summary>
        public static int IndexOf(string layerName)
        {
            int i = LayerMask.NameToLayer(layerName);
            if (i < 0) throw new System.InvalidOperationException($"Layer '{layerName}' is not defined in TagManager.");
            return i;
        }

        /// <summary>Bit mask for the named layers.</summary>
        public static int MaskOf(params string[] layerNames) => LayerMask.GetMask(layerNames);
    }
}
