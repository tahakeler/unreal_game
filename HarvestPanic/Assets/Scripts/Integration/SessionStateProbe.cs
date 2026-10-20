using System.IO;
using System.Text;
using HarvestPanic.AI;
using HarvestPanic.Customization;
using HarvestPanic.Economy;
using HarvestPanic.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarvestPanic.Integration
{
    /// <summary>
    /// Automation-only (needs -netResult and -netHost/-netJoin): once the game scene is live, appends one
    /// "state ..." line per second to the result file so the two-process test can see what each peer observes
    /// (critters, goods, economy phase and wallet, replicated players with their cosmetic loadouts).
    /// Lives in its own assembly because Networking may not reference AI, Economy or Customization.
    /// </summary>
    public sealed class SessionStateProbe : MonoBehaviour
    {
        string _path, _scene;
        float _next;
        bool _host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string path = SessionService.Arg("-netResult");
            if (string.IsNullOrEmpty(path) || !(SessionService.HasArg("-netHost") || SessionService.Arg("-netJoin") != null)) return;
            var go = new GameObject("[SessionStateProbe]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            var p = go.AddComponent<SessionStateProbe>();
            p._path = path;
            p._host = SessionService.HasArg("-netHost");
            p._scene = SessionService.Arg("-netGameScene") ?? "Muddlebury";
        }

        void Update()
        {
            if (Time.realtimeSinceStartup < _next || SceneManager.GetActiveScene().name != _scene) return;
            _next = Time.realtimeSinceStartup + 1f;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;
            var eco = EconomyDirector.Instance;
            var sb = new StringBuilder($"state role={(_host ? "host" : "client")} critters={CritterRegistry.All.Count} goods={FindObjectsByType<Goods>(FindObjectsSortMode.None).Length}");
            sb.Append(eco != null ? $" economy=1 phase={eco.Phase} wallet={eco.Balance}" : " economy=0");
            var players = new StringBuilder();
            foreach (var l in FarmerLoadoutSync.Active)
            {
                var v = l.Loadout;
                players.Append(players.Length > 0 ? ";" : "").Append($"owner{l.OwnerClientId}:body{v.Body}/hat{v.Hat}/face{v.Face}/back{v.Back}");
            }
            sb.Append($" farmers={FarmerLoadoutSync.Active.Count} loadouts={players}");
            try { File.AppendAllText(_path, $"[{Time.realtimeSinceStartup:F2}] {sb}\n"); } catch { /* best effort */ }
        }
    }
}
