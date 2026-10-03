using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The co-op party hall: the guardians' open arena, where the party runs around together before the descent. A ring of
    /// hero statues swaps the local hero, the portal in the middle readies them up, and a row of training dummies stands
    /// along the south wall to warm up on. Lives on the hall's level root, so it goes when the descent begins.
    /// </summary>
    public sealed class PartyHall : MonoBehaviour
    {
        public const float StatueRing = 6.5f, PortalRange = 1.4f;
        public const int Dummies = 3;

        public DungeonRun Run { get; private set; }
        public IReadOnlyList<HeroStatue> Statues => statues;
        public Vector2 Center { get; private set; }
        /// <summary>Where heroes appear: just south of the portal.</summary>
        public Vector2 Spawn => Center + Vector2.down * 2.5f;
        public Vector2 Portal => Center;
        private readonly List<HeroStatue> statues = new List<HeroStatue>();
        private StairVisual portal;
        private float nextMote;

        /// <summary>Stands the statues, the portal and the dummies in <paramref name="level"/> around the arena's middle.</summary>
        public static PartyHall Build(DungeonRun run, Transform level, Vector2 center)
        {
            var hall = level.gameObject.AddComponent<PartyHall>();
            hall.Run = run;
            hall.Center = center;
            hall.portal = StairVisual.CreatePortal(level, center, AbilityCatalog.Gold);
            // Every hero this player may take, evenly round the ring and starting straight above the portal.
            var heroes = new List<int>();
            for (int i = 0; i < run.Characters.Count; i++)
                if (!run.IsCharacterLocked(run.Characters[i])) heroes.Add(i);
            for (int slot = 0; slot < heroes.Count; slot++)
            {
                float angle = (90f - 360f * slot / heroes.Count) * Mathf.Deg2Rad;
                var position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * StatueRing;
                hall.statues.Add(HeroStatue.Create(level, position, run.Characters[heroes[slot]], heroes[slot]));
            }
            float south = DungeonMap.Arena.yMin + 1.5f;
            for (int i = 0; i < Dummies; i++)
                TrainingDummy.Place(run, level, new Vector2(center.x + (i - (Dummies - 1) / 2f) * 4f, south));
            return hall;
        }

        /// <summary>The statue the hero stands at, if any (the closest one when two are in reach).</summary>
        public HeroStatue StatueAt(Vector2 point)
        {
            HeroStatue best = null;
            foreach (var statue in statues)
                if (statue.IsNear(point) && (best == null || Vector2.Distance(point, statue.transform.position) < Vector2.Distance(point, best.transform.position)))
                    best = statue;
            return best;
        }

        public bool AtPortal(Vector2 point) => Vector2.Distance(point, Portal) < PortalRange;

        /// <summary>What the hero can do where they stand, for the HUD's objective line.</summary>
        public string Prompt
        {
            get
            {
                var player = Run.Player;
                if (player == null) return "";
                string key = KeyBindings.Label(GameAction.Interact);
                var statue = StatueAt(player.transform.position);
                if (statue != null)
                    return statue.Character == Run.SelectedCharacter ? $"You are the {statue.Character.DisplayName}" : $"Become the {statue.Character.DisplayName}  /  {key}";
                if (AtPortal(player.transform.position))
                    return Run.Coop.Session.LocalReady ? $"Ready!  Step back in to cancel  /  {key}" : $"Ready up  /  {key}";
                if (Run.Coop.CountdownLeft >= 0f) return "Everyone is ready. Un-ready at the portal to wait";
                return Run.Coop.Session.LocalReady ? "Ready. The descent begins when the whole party is"
                    : "Pick a hero at the statues, then step into the portal to ready up";
            }
        }

        private void Update()
        {
            var player = Run != null ? Run.Player : null;
            if (player == null || !Run.IsInLobby) return;
            var session = Run.Coop.Session;
            // The roster is the truth: a pick made elsewhere (a test, a future menu) still swaps the hero standing here.
            var picked = Run.Characters[Mathf.Clamp(session.LocalClassIndex, 0, Run.Characters.Count - 1)];
            if (picked != Run.SelectedCharacter && Run.SwapLobbyHero(picked)) player = Run.Player;
            Vector2 at = player.transform.position;
            var near = StatueAt(at);
            foreach (var statue in statues) statue.Refresh(statue == near, statue.Character == Run.SelectedCharacter);
            portal.SetUnlocked(session.LocalReady);
            float left = Run.Coop.CountdownLeft;
            if (left >= 0f && left < DescentIris.PullTime) { PullIntoPortal(player, left); return; }
            if (Run.HudCapturesInput || !PlayerInput.Interact) return;
            if (near != null)
            {
                if (near.Character != Run.SelectedCharacter && Run.SwapLobbyHero(near.Character)) near.Flash();
            }
            else if (AtPortal(at)) session.SetLocalReady(!session.LocalReady);
        }

        /// <summary>
        /// The countdown's last moments: the portal flares and draws the hero in, faster the nearer the descent, while
        /// the screen closes on it (see <see cref="DescentIris"/>). Each player pulls their own hero, so the party sees
        /// everyone go in together.
        /// </summary>
        private void PullIntoPortal(DungeonPlayer player, float left)
        {
            player.Occupy(0.1f);
            float pull = 1f - left / DescentIris.PullTime;
            player.transform.position = Vector2.MoveTowards(player.transform.position, Portal, Time.unscaledDeltaTime * (2f + 14f * pull * pull));
            if (Time.unscaledTime < nextMote) return;
            nextMote = Time.unscaledTime + 0.18f;
            HeroVfx.Motes(transform, Portal, 1.6f, AbilityCatalog.Gold, 8, 0.6f);
            HeroVfx.Pulse(transform, Portal, 1.2f + 1.6f * pull, FlameMesh.Alpha(AbilityCatalog.Gold, 0.6f), 0.4f);
        }
    }
}
