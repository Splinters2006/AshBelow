#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Development builds only: several copies of the game play a scripted co-op descent over direct IP and log the
    /// outcome. Start one with <c>-coopSmoke host -coopPeers N</c> and N-1 more with <c>-coopSmoke join -coopPeers N</c>.
    /// Covers the lobby, shared floors, guest kills, boon and artifact votes, the boss floor, a guest leaving mid-run,
    /// enemy bolts, fallen heroes, the end of the run, returning to the lobby and the host quitting.
    /// </summary>
    public sealed class CoopSmokeTest : MonoBehaviour
    {
        private const ushort Port = 7788;
        private const float StepTimeout = 60f;
        private bool host, online;
        private int partySize = 2;
        private DungeonRun run;
        private NetSession session;
        private string role, shotFolder;

        /// <summary>With <c>-coopShots folder</c>, saves a screenshot for reviewing the co-op screens.</summary>
        private IEnumerator Snap(string name)
        {
            if (string.IsNullOrEmpty(shotFolder)) yield break;
            yield return new WaitForSecondsRealtime(0.6f);
            System.IO.Directory.CreateDirectory(shotFolder);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shotFolder, $"{role.ToLowerInvariant()}-{name}.png"));
            yield return new WaitForSecondsRealtime(0.4f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-coopSmoke");
            if (index < 0 || index + 1 >= args.Length) return;
            var test = new GameObject("Co-op smoke test").AddComponent<CoopSmokeTest>();
            test.host = args[index + 1] == "host";
            test.online = args[index + 1] == "online";
            int shots = Array.IndexOf(args, "-coopShots");
            if (shots >= 0 && shots + 1 < args.Length) test.shotFolder = args[shots + 1];
            int peers = Array.IndexOf(args, "-coopPeers");
            if (peers >= 0 && peers + 1 < args.Length && int.TryParse(args[peers + 1], out int count)) test.partySize = Mathf.Clamp(count, 2, NetSession.MaxPlayers);
        }

        private IEnumerator Start()
        {
            role = host ? "HOST" : "GUEST";
            yield return null;
            run = FindAnyObjectByType<DungeonRun>();
            session = run.Coop.Session;
            // Nested steps run here rather than as Unity coroutines, so a failure anywhere is caught and reported.
            var steps = new System.Collections.Generic.Stack<IEnumerator>();
            steps.Push(online ? ProbeOnline() : Play());
            while (steps.Count > 0)
            {
                object current;
                try
                {
                    if (!steps.Peek().MoveNext()) { steps.Pop(); continue; }
                    current = steps.Peek().Current;
                }
                catch (Exception error)
                {
                    Debug.LogError($"COOP_SMOKE_{role}_FAILED: {error.Message} (state {session.State}, status '{session.Status}', floor {run.Floor})");
                    Quit(1);
                    yield break;
                }
                if (current is IEnumerator nested) { steps.Push(nested); continue; }
                yield return current;
            }
            Debug.Log($"COOP_SMOKE_{role}_OK");
            yield return new WaitForSecondsRealtime(1f);
            Quit(0);
        }

        private static void Quit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }

        private IEnumerator Wait(string what, Func<bool> condition)
        {
            float until = Time.realtimeSinceStartup + StepTimeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until) throw new Exception("Timed out waiting for " + what);
                yield return null;
            }
            Debug.Log($"COOP_SMOKE {role} step: {what}");
        }

        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

        /// <summary>Roster position: 0 is the host, 1 does the fighting, the last guest leaves mid-run.</summary>
        private int Seat => session.Peers.ToList().FindIndex(peer => peer.Id == session.LocalId);

        /// <summary>Hosts through Unity Relay and reports the result: a join code, or the message a player would see.</summary>
        private IEnumerator ProbeOnline()
        {
            role = "ONLINE";
            session.HostOnline();
            yield return Wait("an online result", () => session.State == NetState.Lobby || session.State == NetState.Offline);
            Debug.Log($"COOP_SMOKE online result: state {session.State}, code '{session.JoinCode}', status '{session.Status}'");
            if (session.State == NetState.Lobby) Require(!string.IsNullOrEmpty(session.JoinCode), "Hosting online produced no join code.");
            else Require(!string.IsNullOrEmpty(session.Status), "A failed online host must explain why.");
            session.Leave();
        }

        private IEnumerator Play()
        {
            if (!string.IsNullOrEmpty(shotFolder)) { run.ShowCoopLobby(); yield return Snap("menu"); }
            DebugMode.Toggle();
            if (host)
            {
                session.RenameLocal("SmokeHost");
                session.HostDirect(Port);
            }
            else
            {
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0f, 1.5f));
                session.RenameLocal("SmokeGuest");
                session.JoinDirect("127.0.0.1", Port);
            }
            yield return Wait("the full party in the lobby", () => session.State == NetState.Lobby && session.Peers.Count == partySize
                && session.Peers.All(peer => peer.Name.StartsWith("Smoke")));
            int seat = Seat;
            role = seat == 0 ? "HOST" : "GUEST" + seat;
            session.SetLocalClass(seat % run.Characters.Count);
            yield return new WaitForSecondsRealtime(1f);
            yield return Snap("lobby");
            if (host) run.Coop.HostBeginRun();
            yield return Wait("floor 1 with every teammate", () => run.Floor == 1 && run.IsPlaying && run.Coop.RemoteHeroes.Count == partySize - 1);
            Debug.Log($"COOP_SMOKE layout {role} seed={run.Seed} enemies={run.Enemies.Count} party={run.PartySize} hp={run.Enemies[0].Health}");
            Require(run.PartySize == partySize, "Wrong party size.");
            Require(run.Enemies[0].Health == DungeonRun.ScaleHealth(2, partySize), "Enemy health is not scaled for the party.");
            Require(run.SelectedCharacter == run.Characters[seat % run.Characters.Count], "The hero picked in the lobby was not used.");
            Require(run.Coop.RemoteHeroes.All(hero => hero.Character == run.Characters[session.Peers.ToList().FindIndex(peer => peer.Id == hero.Id) % run.Characters.Count]),
                "A teammate appears as the wrong hero.");

            for (int floor = 1; floor <= 5; floor++)
            {
                int kills = run.Kills, enemies = run.Enemies.Count;
                if (seat == 1)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    run.Player.transform.position = (Vector2)run.Map.Centers[0] + Vector2.right;
                    if (floor == 1) yield return Snap("floor");
                    foreach (var enemy in run.Enemies.ToArray()) enemy.Hit(9999, enemy.transform.position + Vector3.left);
                }
                else if (floor == 1 && seat == 0)
                {
                    run.Player.transform.position = (Vector2)run.Map.Centers[0] + Vector2.left;
                    yield return Snap("floor");
                }
                yield return Wait($"floor {floor} cleared", () => run.Enemies.Count == 0);
                Require(run.Kills - kills == enemies, $"Floor {floor}: kills were not counted exactly once.");
                if (run.IsBossFloor)
                {
                    yield return Wait("the boss artifact", () => run.Artifact != null);
                    if (seat == 1) run.Coop.RequestInteract(CoopChoice.Artifact);
                    yield return Wait("the artifact choice opens", () => run.ChoosingArtifact);
                    var relic = AbilityCatalog.All.First(ability => ability.ClassWeapon == run.Player.ClassWeapon);
                    Require(run.ChooseArtifact(relic.Type, 0), "Could not claim a relic.");
                    Require(run.Player.Abilities.IsEquipped(relic.Type), "The relic was not equipped.");
                    yield return Wait("everyone claims a relic", () => !run.ChoosingArtifact && run.IsPlaying && run.Artifact == null);
                    if (seat == 1) run.Coop.RequestInteract(CoopChoice.Upgrade);
                }
                else
                {
                    if (seat == 1) run.Coop.RequestInteract(CoopChoice.Upgrade);
                    yield return Wait($"floor {floor} boon choice", () => run.ChoosingUpgrade);
                    if (seat == 0) yield return new WaitForSecondsRealtime(shotFolder != null ? 2.5f : 0.5f); // the host picks last; the others must wait
                    else if (floor == 1) yield return Snap("boons");
                    run.ChooseUpgrade(0);
                    if (seat != 0 && floor == 1) yield return Snap("waiting");
                }
                yield return Wait($"floor {floor + 1}", () => run.Floor == floor + 1 && run.IsPlaying);
            }

            if (partySize > 2)
            {
                if (seat == partySize - 1)
                {
                    run.ShowMainMenu();
                    Require(session.State == NetState.Offline && run.IsInMainMenu, "Leaving did not return to the menu.");
                    yield break;
                }
                yield return Wait("a guest leaves mid-run", () => run.Coop.RemoteHeroes.Count == partySize - 2 && session.Peers.Count == partySize - 1);
                Require(run.IsPlaying, "A guest leaving must not stop the run.");
            }

            if (host) EnemyProjectile.Spawn(run, run.ProjectileRoot, (Vector2)run.Map.Centers[run.Map.Centers.Count - 1], Vector2.up);
            else yield return Wait("a host bolt appears", () => FindObjectsByType<EnemyProjectile>().Any(bolt => bolt.Id > 0));
            yield return new WaitForSecondsRealtime(1f);

            DebugMode.Toggle();
            if (!host)
            {
                while (run.Player.Health > 0) { run.Player.Hit(); yield return new WaitForSecondsRealtime(1.1f); }
                Require(run.IsPlaying && !run.Coop.RunOver, "One fallen hero must not end the run.");
                yield return Snap("fallen");
            }
            else
            {
                yield return Wait("every guest falls", () => run.Coop.RemoteHeroes.All(hero => !hero.IsAlive));
                if (shotFolder != null) yield return new WaitForSecondsRealtime(2f);
                Require(run.IsPlaying && !run.Coop.RunOver, "The run must continue while the host lives.");
                while (run.Player.Health > 0) { run.Player.Hit(); yield return new WaitForSecondsRealtime(1.1f); }
            }
            yield return Wait("the whole party falls", () => run.Coop.RunOver && !run.IsPlaying);
            yield return Snap("over");

            yield return new WaitForSecondsRealtime(1f);
            if (host) run.Coop.HostReturnToLobby();
            yield return Wait("back in the party lobby", () => session.State == NetState.Lobby && run.IsInMainMenu);
            if (host)
            {
                yield return new WaitForSecondsRealtime(1f);
                run.ShowMainMenu();
                Require(session.State == NetState.Offline, "The host did not go offline.");
            }
            else yield return Wait("the host leaving is noticed", () => session.State == NetState.Offline && session.Status == "The host left the game.");
        }
    }
}
#endif
