using System;

namespace HarvestPanic.Core
{
    /// <summary>Where the "Create your farmer" screen was opened from (changes its title and its Cancel button).</summary>
    public enum CharacterCreatorContext : byte
    {
        /// <summary>First launch from the main menu: no farmer has been confirmed on this machine yet.</summary>
        FirstRun = 0,
        /// <summary>Main menu "Customize" button (no session; edits the saved loadout).</summary>
        MainMenu = 1,
        /// <summary>Lobby / barn wardrobe for the spawned local farmer (Confirm goes through the server).</summary>
        Wardrobe = 2,
    }

    /// <summary>
    /// The character creator / wardrobe screen. Provider: Customization (registered at startup, survives scenes);
    /// consumers: Frontend main menu (first run, Customize), lobby (C key), pause overlay (owns Esc while open).
    /// Add-only contract.
    /// </summary>
    public interface ICharacterCreator
    {
        /// <summary>True while the screen is open, and for the frame it closed (so Esc is never handled twice).</summary>
        bool IsOpen { get; }

        /// <summary>True until a farmer has been confirmed on this machine (the main menu opens the creator once).</summary>
        bool NeedsFirstRun { get; }

        /// <summary>Opens the screen. Returns false when it cannot open now (already open, no catalog, no local farmer for Wardrobe).</summary>
        bool Open(CharacterCreatorContext context);

        /// <summary>Raised when the screen closes; true when the player confirmed.</summary>
        event Action<bool> Closed;
    }
}
