using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Networking
{
    /// <summary>
    /// LAN discovery over UDP broadcast. Poll-based (call <see cref="Tick"/> each frame, main thread only).
    /// Probe:  "HPDISC?1"  (client -> 255.255.255.255, subnet broadcasts and 127.0.0.1 on the discovery port)
    /// Reply:  "HPDISC!1" + JSON <see cref="Reply"/> (host -> prober, unicast).
    /// ~20 B probe every 1.5 s while the LAN tab is open; ~150 B reply per host.
    /// </summary>
    public sealed class LanDiscovery : IDisposable
    {
        const string ProbeText = "HPDISC?1";
        const string ReplyPrefix = "HPDISC!1";
        static readonly byte[] Probe = Encoding.ASCII.GetBytes(ProbeText);

        [Serializable]
        public class Reply
        {
            public string host; public string lobby; public int players; public int max;
            public int port; public string version; public bool inProgress;
        }

        readonly ushort _discoveryPort;
        UdpClient _responder;
        UdpClient _searcher;
        Func<Reply> _replyFactory;
        float _nextProbe;
        readonly List<DiscoveredSession> _found = new List<DiscoveredSession>();

        public float Interval = 1.5f;
        public float Expiry = 5f;

        /// <summary>Discovered hosts.</summary>
        public IReadOnlyList<DiscoveredSession> Sessions => _found;
        public event Action OnChanged;
        public bool IsResponding => _responder != null;
        public bool IsSearching => _searcher != null;

        public LanDiscovery(ushort discoveryPort) { _discoveryPort = discoveryPort; }

        /// <summary>Host side: answer probes with <paramref name="replyFactory"/>().</summary>
        public bool StartResponding(Func<Reply> replyFactory)
        {
            StopResponding();
            _replyFactory = replyFactory;
            try
            {
                var u = new UdpClient();
                u.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                u.Client.Bind(new IPEndPoint(IPAddress.Any, _discoveryPort));
                u.EnableBroadcast = true;
                _responder = u;
                return true;
            }
            catch (Exception e)
            {
                NetLog.Warn("disc-bind", $"LAN discovery responder could not bind UDP {_discoveryPort}: {e.Message}");
                return false;
            }
        }

        public void StopResponding()
        {
            _responder?.Close();
            _responder = null;
        }

        /// <summary>Client side: start probing.</summary>
        public bool StartSearching()
        {
            if (_searcher != null) return true;
            try
            {
                _searcher = new UdpClient(new IPEndPoint(IPAddress.Any, 0)) { EnableBroadcast = true };
                _nextProbe = 0f;
                return true;
            }
            catch (Exception e)
            {
                NetLog.Warn("disc-search", "LAN search failed to open a socket: " + e.Message);
                return false;
            }
        }

        public void StopSearching()
        {
            _searcher?.Close();
            _searcher = null;
            if (_found.Count > 0) { _found.Clear(); OnChanged?.Invoke(); }
        }

        /// <summary>Pumps sockets. <paramref name="now"/> = Time.realtimeSinceStartup.</summary>
        public void Tick(float now)
        {
            PumpResponder();
            if (_searcher == null) return;
            if (now >= _nextProbe) { _nextProbe = now + Interval; SendProbes(); }
            bool changed = PumpSearcher(now);
            for (int i = _found.Count - 1; i >= 0; i--)
                if (now - _found[i].LastSeen > Expiry) { _found.RemoveAt(i); changed = true; }
            if (changed) OnChanged?.Invoke();
        }

        void PumpResponder()
        {
            if (_responder == null) return;
            try
            {
                while (_responder.Available > 0)
                {
                    var ep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _responder.Receive(ref ep);
                    if (data.Length != Probe.Length || Encoding.ASCII.GetString(data) != ProbeText) continue;
                    var reply = _replyFactory?.Invoke();
                    if (reply == null) continue;
                    byte[] bytes = Encoding.UTF8.GetBytes(ReplyPrefix + JsonUtility.ToJson(reply));
                    _responder.Send(bytes, bytes.Length, ep);
                }
            }
            catch (SocketException e) { NetLog.Warn("disc-resp", "LAN discovery responder: " + e.Message); }
            catch (ObjectDisposedException) { }
        }

        bool PumpSearcher(float now)
        {
            bool changed = false;
            try
            {
                while (_searcher != null && _searcher.Available > 0)
                {
                    var ep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _searcher.Receive(ref ep);
                    if (data.Length > 1024) continue;
                    string s = Encoding.UTF8.GetString(data);
                    if (!s.StartsWith(ReplyPrefix, StringComparison.Ordinal)) continue;
                    Reply r;
                    try { r = JsonUtility.FromJson<Reply>(s.Substring(ReplyPrefix.Length)); } catch { continue; }
                    if (r == null || r.port <= 0 || r.port > 65535) continue;
                    changed |= Upsert(new DiscoveredSession
                    {
                        Address = ep.Address.ToString(), Port = (ushort)r.port,
                        HostName = PlayerNameSanitizer.Sanitize(r.host), LobbyName = PlayerNameSanitizer.Sanitize(r.lobby, 32),
                        Players = Mathf.Clamp(r.players, 0, GameInfo.MaxPlayers), MaxPlayers = Mathf.Clamp(r.max, 1, GameInfo.MaxPlayers),
                        Version = r.version, InProgress = r.inProgress, LastSeen = now,
                    });
                }
            }
            catch (SocketException) { /* ICMP unreachable echoes on some OSes: ignore */ }
            catch (ObjectDisposedException) { }
            return changed;
        }

        bool Upsert(DiscoveredSession s)
        {
            for (int i = 0; i < _found.Count; i++)
            {
                var f = _found[i];
                // The same host may answer on loopback and on its LAN IP: dedupe by host name + port, prefer non-loopback.
                bool same = (f.Address == s.Address && f.Port == s.Port) || (f.HostName == s.HostName && f.Port == s.Port && f.LobbyName == s.LobbyName && (IsLoopback(f.Address) || IsLoopback(s.Address)));
                if (!same) continue;
                bool visible = f.Players != s.Players || f.MaxPlayers != s.MaxPlayers || f.InProgress != s.InProgress;
                if (IsLoopback(s.Address) && !IsLoopback(f.Address)) s.Address = f.Address;
                _found[i] = s;
                return visible;
            }
            _found.Add(s);
            return true;
        }

        static bool IsLoopback(string a) => a == "127.0.0.1" || a == "::1";

        void SendProbes()
        {
            foreach (var addr in BroadcastTargets())
            {
                try { _searcher.Send(Probe, Probe.Length, new IPEndPoint(addr, _discoveryPort)); }
                catch (Exception e) { NetLog.Warn("disc-send" + addr, $"LAN probe to {addr} failed: {e.Message}"); }
            }
        }

        static List<IPAddress> BroadcastTargets()
        {
            var list = new List<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                        byte[] ip = ua.Address.GetAddressBytes(), mask = ua.IPv4Mask.GetAddressBytes();
                        if (mask.Length != 4) continue;
                        var b = new byte[4];
                        for (int i = 0; i < 4; i++) b[i] = (byte)(ip[i] | ~mask[i]);
                        var bc = new IPAddress(b);
                        if (!list.Contains(bc)) list.Add(bc);
                    }
                }
            }
            catch (Exception) { /* NetworkInformation unsupported on some platforms: global broadcast only */ }
            return list;
        }

        public void Dispose()
        {
            StopResponding();
            StopSearching();
        }
    }
}
