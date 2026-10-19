using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarvestPanic.Core;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Optional-feature bridge. ISessionService is the contract; things it does not (yet) cover are discovered on the registered
    /// provider by name, so Networking can add them without a UI change:
    ///   string TransportName { get; }                         ("Steam" / "LAN")
    ///   IEnumerable DiscoveredHosts { get; }                  (items with Name, Address, Players, MaxPlayers)
    ///   void RefreshDiscovery()
    ///   void SendChat(string text);  event Action&lt;string,string&gt; OnChat (sender, text)
    ///   void Kick(ulong clientId)
    /// Wardrobe: a Core interface named IWardrobeUI (registered in Services) with void Open(); resolved by type name.
    /// Every call is null-safe and returns false when the feature is absent.
    /// </summary>
    public static class SessionAccess
    {
        const BindingFlags Pub = BindingFlags.Public | BindingFlags.Instance;

        public static bool TryGetSession(out ISessionService svc) => Services.TryGet(out svc);

        public static string TransportLabel(object svc)
        {
            if (svc == null) return "OFFLINE";
            var p = svc.GetType().GetProperty("TransportName", Pub);
            if (p != null && p.GetValue(svc) is string s && !string.IsNullOrWhiteSpace(s)) return s.ToUpperInvariant();
            return svc.GetType().Name.IndexOf("Steam", StringComparison.OrdinalIgnoreCase) >= 0 ? "STEAM" : "LAN";
        }

        public static bool SupportsDiscovery(object svc) => svc?.GetType().GetProperty("DiscoveredHosts", Pub) != null;

        public static List<LanHost> GetHosts(object svc)
        {
            var list = new List<LanHost>();
            var p = svc?.GetType().GetProperty("DiscoveredHosts", Pub);
            if (p == null || !(p.GetValue(svc) is IEnumerable en)) return list;
            foreach (var item in en)
            {
                if (item == null) continue;
                var t = item.GetType();
                list.Add(new LanHost
                {
                    Name = Str(item, t, "Name"),
                    Address = Str(item, t, "Address"),
                    Players = Int(item, t, "Players"),
                    MaxPlayers = Int(item, t, "MaxPlayers") > 0 ? Int(item, t, "MaxPlayers") : 4,
                });
            }
            return list;
        }

        public static void RefreshDiscovery(object svc) => svc?.GetType().GetMethod("RefreshDiscovery", Type.EmptyTypes)?.Invoke(svc, null);

        public static bool SupportsChat(object svc) => svc?.GetType().GetMethod("SendChat", new[] { typeof(string) }) != null;

        public static bool SendChat(object svc, string text)
        {
            var m = svc?.GetType().GetMethod("SendChat", new[] { typeof(string) });
            if (m == null) return false;
            m.Invoke(svc, new object[] { text });
            return true;
        }

        /// <summary>Subscribes to OnChat(sender, text). Returns an unsubscribe action or null when unsupported.</summary>
        public static Action SubscribeChat(object svc, Action<string, string> handler)
        {
            var ev = svc?.GetType().GetEvent("OnChat", Pub);
            if (ev == null || ev.EventHandlerType != typeof(Action<string, string>)) return null;
            ev.AddEventHandler(svc, handler);
            return () => ev.RemoveEventHandler(svc, handler);
        }

        public static bool SupportsKick(object svc) => svc?.GetType().GetMethod("Kick", new[] { typeof(ulong) }) != null;

        public static bool Kick(object svc, ulong clientId)
        {
            var m = svc?.GetType().GetMethod("Kick", new[] { typeof(ulong) });
            if (m == null) return false;
            m.Invoke(svc, new object[] { clientId });
            return true;
        }

        /// <summary>Opens the Steam friends overlay if the Steam transport assembly is present. False otherwise.</summary>
        public static bool TryOpenSteamOverlay()
        {
            try
            {
                var t = FindType("Steamworks.SteamFriends");
                var m = t?.GetMethod("OpenOverlay", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                if (m == null) return false;
                m.Invoke(null, new object[] { "friends" });
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Opens the Customization team's wardrobe if a Core interface named IWardrobeUI is registered. False otherwise.</summary>
        public static bool TryOpenWardrobe()
        {
            try
            {
                var iface = FindType("HarvestPanic.Core.IWardrobeUI") ?? FindTypeByName("IWardrobeUI");
                if (iface == null) return false;
                var tryGet = typeof(Services).GetMethod(nameof(Services.TryGet)).MakeGenericMethod(iface);
                var args = new object[] { null };
                if (!(bool)tryGet.Invoke(null, args) || args[0] == null) return false;
                var open = iface.GetMethod("Open", Type.EmptyTypes);
                if (open == null) return false;
                open.Invoke(args[0], null);
                return true;
            }
            catch (Exception) { return false; }
        }

        static Type FindType(string fullName)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        static Type FindTypeByName(string name)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!a.GetName().Name.StartsWith("HarvestPanic")) continue;
                foreach (var t in a.GetTypes()) if (t.Name == name && t.IsInterface) return t;
            }
            return null;
        }

        static string Str(object o, Type t, string n)
        {
            var f = t.GetField(n, Pub); if (f != null) return f.GetValue(o)?.ToString();
            return t.GetProperty(n, Pub)?.GetValue(o)?.ToString();
        }

        static int Int(object o, Type t, string n)
        {
            object v = t.GetField(n, Pub)?.GetValue(o) ?? t.GetProperty(n, Pub)?.GetValue(o);
            return v is int i ? i : 0;
        }
    }
}
