using System.Collections.Generic;
using HarvestPanic.Core;
using HarvestPanic.Networking;
using NUnit.Framework;

namespace HarvestPanic.Tests.EditMode.Networking
{
    /// <summary>App ID resolution, connect-lobby parsing, payload versioning and join approval (version check, cap).</summary>
    public class SteamReadinessTests
    {
        static byte[] Payload(int proto, string build, string name) =>
            new ConnectionPayload { Protocol = proto, BuildVersion = build, PlayerName = name }.ToBytes();

        static ApprovalDecision Eval(byte[] p, int connected, int max = 4, bool locked = false, IEnumerable<string> taken = null) =>
            ConnectionApproval.Evaluate(p, false, connected, max, 1, "1.0", locked, taken ?? new string[0]);

        // ---- App ID ----
        [Test] public void AppId_DefaultsToConfig() => Assert.AreEqual(480u, SteamAppId.Resolve(480, null, null));
        [Test] public void AppId_ZeroConfigFallsBackToSpacewar() => Assert.AreEqual(SteamAppId.SpacewarTestAppId, SteamAppId.Resolve(0, "", " "));
        [Test] public void AppId_FileOverridesConfig() => Assert.AreEqual(2345670u, SteamAppId.Resolve(480, "2345670\r\n", null));
        [Test] public void AppId_ArgOverridesFile() => Assert.AreEqual(111u, SteamAppId.Resolve(480, "2345670", "111"));
        [TestCase("abc")] [TestCase("0")] [TestCase("-5")] [TestCase("99999999999")]
        public void AppId_InvalidFileIgnored(string text) => Assert.AreEqual(480u, SteamAppId.Resolve(480, text, null));
        [Test] public void AppId_FileFirstTokenOnly() => Assert.AreEqual(777u, SteamAppId.Resolve(480, " 777 # real id", null));

        // ---- Steam invite / launch args ----
        [Test] public void ConnectLobby_Parses()
        {
            Assert.IsTrue(SteamPlatform.TryParseConnectLobby("+connect_lobby 109775241000000001", out ulong id));
            Assert.AreEqual(109775241000000001UL, id);
            Assert.IsTrue(SteamPlatform.TryParseConnectLobby("-foo +connect_lobby  42 +bar", out id));
            Assert.AreEqual(42UL, id);
        }
        [TestCase(null)] [TestCase("")] [TestCase("+connect_lobby")] [TestCase("+connect_lobby x")] [TestCase("+connect_lobby 0")]
        public void ConnectLobby_RejectsBad(string s) => Assert.IsFalse(SteamPlatform.TryParseConnectLobby(s, out _));
        [Test] public void SteamIdKinds()
        {
            Assert.IsTrue(SteamPlatform.IsUserId(76561197960287930UL));
            Assert.IsFalse(SteamPlatform.IsLobbyId(76561197960287930UL));
            Assert.IsTrue(SteamPlatform.IsLobbyId(103582791429533753UL)); // universe 1, type 7 (chat)
        }

        // ---- Payload ----
        [Test] public void Payload_RoundTrips()
        {
            Assert.IsTrue(ConnectionPayload.TryParse(Payload(3, "1.2.3", "Cora"), out var p));
            Assert.AreEqual(3, p.Protocol); Assert.AreEqual("1.2.3", p.BuildVersion); Assert.AreEqual("Cora", p.PlayerName);
        }
        [Test] public void Payload_RejectsGarbage()
        {
            Assert.IsFalse(ConnectionPayload.TryParse(null, out _));
            Assert.IsFalse(ConnectionPayload.TryParse(new byte[] { 1, 2, 3, 4 }, out _));
            Assert.IsFalse(ConnectionPayload.TryParse(new byte[] { (byte)'H', (byte)'P', 1, 0 }, out _));
            Assert.IsFalse(ConnectionPayload.TryParse(new byte[ConnectionPayload.MaxBytes + 1], out _));
        }

        // ---- Approval ----
        [Test] public void Approval_MatchingVersionApproved() => Assert.IsTrue(Eval(Payload(1, "1.0", "Cora"), 1).Approved);
        [Test] public void Approval_BuildMismatchRejectedWithMessage()
        {
            var d = Eval(Payload(1, "0.9", "Cora"), 1);
            Assert.IsFalse(d.Approved);
            StringAssert.Contains("different version", d.Reason);
            StringAssert.Contains("v0.9", d.Reason); StringAssert.Contains("v1.0", d.Reason);
        }
        [Test] public void Approval_ProtocolMismatchRejected() => Assert.IsFalse(Eval(Payload(2, "1.0", "Cora"), 1).Approved);
        [Test] public void Approval_MalformedRejected() => Assert.AreEqual(ConnectionApproval.MalformedReason, Eval(new byte[] { 9 }, 1).Reason);
        [Test] public void Approval_VersionCheckedBeforeFull() => StringAssert.Contains("different version", Eval(Payload(1, "0.9", "X"), 4).Reason);
        [Test] public void Approval_FourthPlayerOkFifthRejected()
        {
            Assert.IsTrue(Eval(Payload(1, "1.0", "D"), 3).Approved);
            var d = Eval(Payload(1, "1.0", "E"), 4);
            Assert.IsFalse(d.Approved);
            Assert.AreEqual(ConnectionApproval.FullReason(4), d.Reason);
        }
        [Test] public void Approval_CapClampedToGameMax() =>
            Assert.AreEqual(ConnectionApproval.FullReason(GameInfo.MaxPlayers), Eval(Payload(1, "1.0", "E"), 4, max: 16).Reason);
        [Test] public void Approval_LowerHostCapEnforced() => Assert.AreEqual(ConnectionApproval.FullReason(2), Eval(Payload(1, "1.0", "C"), 2, max: 2).Reason);
        [Test] public void Approval_LockedRejected() => Assert.AreEqual(ConnectionApproval.LockedReason, Eval(Payload(1, "1.0", "C"), 1, locked: true).Reason);
        [Test] public void Approval_HostSelfAlwaysApproved() =>
            Assert.IsTrue(ConnectionApproval.Evaluate(null, true, 4, 4, 1, "1.0", true, new string[0]).Approved);
        [Test] public void Approval_RejoinWithSameNameAfterLeaveKeepsName() =>
            Assert.AreEqual("Cora", Eval(Payload(1, "1.0", "Cora"), 1, taken: new[] { "Hank" }).PlayerName);
        [Test] public void Approval_DuplicateNameMadeUnique() =>
            Assert.AreNotEqual("Cora", Eval(Payload(1, "1.0", "Cora"), 2, taken: new[] { "Hank", "Cora" }).PlayerName);
    }
}
