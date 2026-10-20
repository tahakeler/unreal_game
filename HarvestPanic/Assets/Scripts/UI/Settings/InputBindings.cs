using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HarvestPanic.UI
{
    /// <summary>One rebindable control: an action plus the default path that identifies the binding inside it.</summary>
    public readonly struct BindingDef
    {
        public readonly string Action, Label, DefaultPath, Group, CompositePart;
        public BindingDef(string action, string label, string group, string defaultPath, string compositePart = null)
        { Action = action; Label = label; Group = group; DefaultPath = defaultPath; CompositePart = compositePart; }
        public bool IsKeyboardMouse => Group == InputBindings.KbmGroup;
    }

    /// <summary>Saved user override: the binding of <see cref="Action"/> whose default is <see cref="Original"/> now uses <see cref="Path"/>.</summary>
    [Serializable]
    public struct BindingOverrideEntry { public string Action; public string Original; public string Path; }

    [Serializable]
    public sealed class BindingOverrideSet
    {
        public List<BindingOverrideEntry> Entries = new List<BindingOverrideEntry>();
    }

    /// <summary>
    /// Rebinding without referencing the Player assembly. The default table mirrors
    /// <c>Prefabs/Player/Input/PlayerControls.inputactions</c> (an EditMode test keeps them in sync). Overrides are keyed by
    /// (action name, default path), so they apply to every instance of the "Player" map, including the per-player copy
    /// PlayerInputReader instantiates. Look sensitivity and invert-Y are applied as processor overrides on the Look bindings.
    /// </summary>
    public static class InputBindings
    {
        public const string MapName = "Player";
        public const string KbmGroup = "KeyboardMouse", PadGroup = "Gamepad";
        const string StoreKey = "HarvestPanic.Bindings.v1";

        public static readonly BindingDef[] Defaults =
        {
            new BindingDef("Move", "Move forward", KbmGroup, "<Keyboard>/w", "up"),
            new BindingDef("Move", "Move back", KbmGroup, "<Keyboard>/s", "down"),
            new BindingDef("Move", "Move left", KbmGroup, "<Keyboard>/a", "left"),
            new BindingDef("Move", "Move right", KbmGroup, "<Keyboard>/d", "right"),
            new BindingDef("Move", "Move", PadGroup, "<Gamepad>/leftStick"),
            new BindingDef("Jump", "Jump", KbmGroup, "<Keyboard>/space"),
            new BindingDef("Jump", "Jump", PadGroup, "<Gamepad>/buttonSouth"),
            new BindingDef("Sprint", "Sprint", KbmGroup, "<Keyboard>/leftShift"),
            new BindingDef("Sprint", "Sprint", PadGroup, "<Gamepad>/leftStickPress"),
            new BindingDef("Crouch", "Crouch / sneak", KbmGroup, "<Keyboard>/leftCtrl"),
            new BindingDef("Crouch", "Crouch / sneak", PadGroup, "<Gamepad>/rightStickPress"),
            new BindingDef("Interact", "Grab / carry (hold)", KbmGroup, "<Keyboard>/e"),
            new BindingDef("Interact", "Grab / carry (hold)", PadGroup, "<Gamepad>/buttonWest"),
            new BindingDef("Throw", "Throw (hold to charge)", KbmGroup, "<Mouse>/leftButton"),
            new BindingDef("Throw", "Throw (hold to charge)", PadGroup, "<Gamepad>/rightTrigger"),
            new BindingDef("Use", "Use tool", KbmGroup, "<Mouse>/rightButton"),
            new BindingDef("Use", "Use tool", PadGroup, "<Gamepad>/leftTrigger"),
            new BindingDef("Holler", "Holler (shout)", KbmGroup, "<Keyboard>/h"),
            new BindingDef("Holler", "Holler (shout)", PadGroup, "<Gamepad>/buttonNorth"),
        };

        /// <summary>Extra default bindings in the asset that are not shown in the rebinding list.</summary>
        public static readonly (string action, string path)[] HiddenDefaults =
        {
            ("Look", "<Mouse>/delta"), ("Look", "<Gamepad>/rightStick"), ("Crouch", "<Keyboard>/c"),
        };

        static ISettingsStore _store = new PlayerPrefsSettingsStore(StoreKey);
        static BindingOverrideSet _set;
        static readonly List<InputActionMap> PendingMaps = new List<InputActionMap>();
        static bool _hooked;
        static InputActionAsset _template;

        /// <summary>Raised after any override changes (help screen and tips re-read labels).</summary>
        public static event Action Changed;

        public static BindingOverrideSet Current { get { if (_set == null) Load(); return _set; } }

        public static void UseStore(ISettingsStore store) { _store = store ?? new PlayerPrefsSettingsStore(StoreKey); _set = null; }

        public static BindingOverrideSet Load()
        {
            BindingOverrideSet loaded = null;
            if (_store.TryRead(out var json))
            {
                try { loaded = JsonUtility.FromJson<BindingOverrideSet>(json); }
                catch (Exception e) { Debug.LogWarning("[Bindings] Corrupt bindings ignored: " + e.Message); }
            }
            _set = loaded ?? new BindingOverrideSet();
            _set.Entries ??= new List<BindingOverrideEntry>();
            _set.Entries.RemoveAll(e => string.IsNullOrEmpty(e.Action) || string.IsNullOrEmpty(e.Original) || string.IsNullOrEmpty(e.Path));
            return _set;
        }

        public static void Save() => _store.Write(JsonUtility.ToJson(Current));

        /// <summary>Effective path for a default binding (override or default).</summary>
        public static string EffectivePath(in BindingDef def)
        {
            foreach (var e in Current.Entries) if (e.Action == def.Action && e.Original == def.DefaultPath) return e.Path;
            return def.DefaultPath;
        }

        /// <summary>Sets (or clears, when equal to the default) an override, persists and reapplies it everywhere.</summary>
        public static void SetOverride(string action, string original, string path)
        {
            var list = Current.Entries;
            list.RemoveAll(e => e.Action == action && e.Original == original);
            if (!string.IsNullOrEmpty(path) && path != original) list.Add(new BindingOverrideEntry { Action = action, Original = original, Path = path });
            Save();
            ApplyEverywhere();
            Changed?.Invoke();
        }

        public static void ResetAll()
        {
            Current.Entries.Clear();
            Save();
            ApplyEverywhere();
            Changed?.Invoke();
        }

        /// <summary>The other binding already using <paramref name="path"/> in the same group, if any (conflict warning).</summary>
        public static bool TryFindConflict(in BindingDef self, string path, out BindingDef other)
        {
            foreach (var d in Defaults)
            {
                if (d.Group != self.Group || (d.Action == self.Action && d.DefaultPath == self.DefaultPath)) continue;
                if (string.Equals(EffectivePath(d), path, StringComparison.OrdinalIgnoreCase)) { other = d; return true; }
            }
            other = default;
            return false;
        }

        /// <summary>Human label for a binding, e.g. "E", "Left Button", "Button West".</summary>
        public static string Display(in BindingDef def) => DisplayPath(EffectivePath(def));

        public static string DisplayPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "-";
            string s = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            return string.IsNullOrEmpty(s) ? path : s;
        }

        /// <summary>Label of the first binding for an action in a group (used by tips: "{Interact}").</summary>
        public static string ActionLabel(string action, bool gamepad)
        {
            string group = gamepad ? PadGroup : KbmGroup;
            foreach (var d in Defaults) if (d.Action == action && d.Group == group) return Display(d);
            return action;
        }

        // ---------------------------------------------------------------- application

        /// <summary>Applies overrides and look settings to one "Player" action map.</summary>
        public static void ApplyTo(InputActionMap map, GameSettings s)
        {
            if (map == null || map.name != MapName) return;
            foreach (var action in map.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var b = action.bindings[i];
                    if (b.isComposite) continue;
                    string over = null;
                    foreach (var e in Current.Entries) if (e.Action == action.name && e.Original == b.path) { over = e.Path; break; }
                    string processors = null;
                    if (action.name == "Look" && s != null) processors = LookProcessors(s.Sensitivity, s.InvertY);
                    if (over == null && processors == null) { if (b.hasOverrides) action.RemoveBindingOverride(i); continue; }
                    action.ApplyBindingOverride(i, new InputBinding { overridePath = over, overrideProcessors = processors });
                }
            }
        }

        /// <summary>Processor string for the Look bindings: uniform scale with optional Y inversion.</summary>
        public static string LookProcessors(float sensitivity, bool invertY)
        {
            float k = Mathf.Clamp(sensitivity, GameSettings.MinSensitivity, GameSettings.MaxSensitivity);
            float y = invertY ? -k : k;
            return string.Format(CultureInfo.InvariantCulture, "ScaleVector2(x={0:0.###},y={1:0.###})", k, y);
        }

        /// <summary>Reapplies to every enabled Player map plus the template used by the rebinding UI.</summary>
        public static void ApplyEverywhere()
        {
            var s = SettingsService.Current;
            var seen = new HashSet<InputActionMap>();
            foreach (var a in InputSystem.ListEnabledActions())
                if (a.actionMap != null && seen.Add(a.actionMap)) ApplyTo(a.actionMap, s);
            if (_template != null) ApplyTo(_template.FindActionMap(MapName), s);
        }

        /// <summary>Starts watching for newly enabled Player maps (called once by UiRuntime).</summary>
        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            InputSystem.onActionChange += OnActionChange;
            SettingsService.Changed += _ => ApplyEverywhere();
            ApplyEverywhere();
        }

        static void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionMapEnabled || !(obj is InputActionMap map) || map.name != MapName) return;
            if (!PendingMaps.Contains(map)) PendingMaps.Add(map); // applying inside the callback re-resolves mid-enable; defer
        }

        /// <summary>Applies deferred maps (UiRuntime.Update).</summary>
        public static void Pump()
        {
            if (PendingMaps.Count == 0) return;
            var s = SettingsService.Current;
            foreach (var m in PendingMaps) ApplyTo(m, s);
            PendingMaps.Clear();
        }

        /// <summary>Code-built copy of the Player map, used by the rebinding UI when no player exists (main menu).</summary>
        public static InputActionAsset Template
        {
            get
            {
                if (_template != null) return _template;
                _template = BuildTemplate();
                ApplyTo(_template.FindActionMap(MapName), SettingsService.Current);
                return _template;
            }
        }

        public static InputActionAsset BuildTemplate()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "HP_BindingTemplate";
            asset.hideFlags = HideFlags.HideAndDontSave;
            var map = asset.AddActionMap(MapName);
            var made = new Dictionary<string, InputAction>();
            InputAction Get(string name)
            {
                if (made.TryGetValue(name, out var a)) return a;
                var type = name == "Move" || name == "Look" ? InputActionType.Value : InputActionType.Button;
                a = map.AddAction(name, type);
                made[name] = a;
                return a;
            }
            var move = Get("Move");
            var comp = move.AddCompositeBinding("2DVector");
            foreach (var d in Defaults) if (d.Action == "Move" && d.CompositePart != null) comp.With(d.CompositePart, d.DefaultPath, d.Group);
            foreach (var d in Defaults) if (d.CompositePart == null) Get(d.Action).AddBinding(d.DefaultPath, groups: d.Group);
            foreach (var (action, path) in HiddenDefaults) Get(action).AddBinding(path);
            return asset;
        }

        /// <summary>Index of a default binding inside the template action.</summary>
        public static int TemplateIndex(InputAction action, in BindingDef def)
        {
            for (int i = 0; i < action.bindings.Count; i++)
                if (!action.bindings[i].isComposite && action.bindings[i].path == def.DefaultPath) return i;
            return -1;
        }
    }
}
