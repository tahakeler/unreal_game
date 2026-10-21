using System;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Customization's <see cref="ICharacterCreator"/> (Core contract) for Frontend: the main menu opens it on first run and
    /// from "Customize"; the lobby opens it as the wardrobe (C). Registered at startup.
    /// </summary>
    public sealed class CharacterCreatorService : ICharacterCreator
    {
        static CharacterCreatorService _instance;

        /// <summary>The registered instance.</summary>
        public static CharacterCreatorService Instance => _instance ?? (_instance = new CharacterCreatorService());

        CharacterCreatorService() { WardrobeUI.Closed += c => Closed?.Invoke(c); }

        /// <inheritdoc/>
        public bool IsOpen => WardrobeUI.IsOpen || WardrobeUI.ClosedFrame == Time.frameCount;

        /// <summary>
        /// Batchmode (automated builds, editor test runs) never asks for the first-run creator unless a test opts in, so
        /// headless runs and other teams' menu tests are never blocked by a modal.
        /// </summary>
        public static bool AllowFirstRunInBatchMode { get; set; }

        /// <inheritdoc/>
        public bool NeedsFirstRun => !LoadoutStorage.FarmerCreated && (!Application.isBatchMode || AllowFirstRunInBatchMode);

        /// <inheritdoc/>
        public event Action<bool> Closed;

        /// <inheritdoc/>
        public bool Open(CharacterCreatorContext context)
        {
            if (WardrobeUI.IsOpen) return false;
            var local = FarmerLoadoutSync.Local;
            if (context == CharacterCreatorContext.Wardrobe)
                return local != null && WardrobeUI.Open(local, CharacterCreatorContext.Wardrobe) != null;
            if (local != null) return WardrobeUI.Open(local, context) != null; // a farmer exists (offline test scenes): dress it
            return WardrobeUI.OpenStandalone(CustomizationLink.Catalog, context) != null;
        }

        /// <summary>Registers the service if missing (idempotent).</summary>
        public static void EnsureRegistered()
        {
            if (Services.TryGet(out ICharacterCreator existing) && ReferenceEquals(existing, Instance)) return;
            Services.Register<ICharacterCreator>(Instance);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() => EnsureRegistered(); // Services is only cleared by tests; they call EnsureRegistered themselves

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _instance = null; AllowFirstRunInBatchMode = false; }
    }
}
