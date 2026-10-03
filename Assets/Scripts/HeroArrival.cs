using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A hero dropping into a new world: the screen opens from black (see <see cref="DescentIris"/>) while a copy of the
    /// hero plummets from above the camera down a column of the world's light, stretching as it speeds up over a
    /// growing shadow, and slams into the floor: a squash, a shockwave, thrown rubble and a scorched crater. The real
    /// hero stays hidden where it stands until then, and the local hero can neither act nor be hurt. Purely cosmetic
    /// and local: every machine plays it for its own view.
    /// </summary>
    public sealed class HeroArrival : MonoBehaviour
    {
        /// <summary>Black before the drop (it hides the floor being built), the fall itself, then the squash of the landing.</summary>
        public const float Hold = 0.3f, Fall = 0.7f, Settle = 0.28f;
        /// <summary>How long the screen takes to open, from the end of the hold.</summary>
        public const float Reveal = 0.75f;
        private const float DropHeight = 7f, StartScale = 3f;
        private static readonly Color Rubble = new Color(0.32f, 0.29f, 0.27f);

        private float startedAt;
        private Color accent;
        private bool local;
        private GameObject ghost;
        private SpriteRenderer[] ghostSprites;
        private Color[] ghostColors;
        private SpriteRenderer shadow, beam;
        private bool landed;
        private readonly List<Renderer> hidden = new List<Renderer>();

        /// <summary>When the local hero's arrival began, for the iris; negative when no arrival is playing.</summary>
        public static float LocalStartedAt { get; private set; } = -1f;
        /// <summary>The local hero's arrival in progress, if any.</summary>
        public static HeroArrival Local { get; private set; }

        /// <summary>Drops <paramref name="hero"/> in after <paramref name="delay"/>; <paramref name="local"/> also opens the screen.</summary>
        public static HeroArrival Play(GameObject hero, Color accent, bool local, float delay = 0f)
        {
            if (hero == null) return null;
            var previous = hero.GetComponent<HeroArrival>();
            if (previous != null) { previous.Land(); previous.enabled = false; Destroy(previous); }
            var arrival = hero.AddComponent<HeroArrival>();
            arrival.startedAt = Time.time + delay;
            arrival.accent = accent;
            arrival.local = local;
            arrival.ghost = DeathAnimation.CopyBody(hero.transform, hero.transform.parent, " arrival");
            arrival.ghostSprites = arrival.ghost.GetComponentsInChildren<SpriteRenderer>();
            arrival.ghostColors = new Color[arrival.ghostSprites.Length];
            for (int i = 0; i < arrival.ghostSprites.Length; i++)
            {
                arrival.ghostColors[i] = arrival.ghostSprites[i].color;
                // Above everything on the floor: it is coming down from over the camera.
                arrival.ghostSprites[i].sortingOrder += 40;
            }
            arrival.shadow = DungeonVisuals.Create("Arrival shadow", hero.transform.parent, hero.transform.position + Vector3.down * 0.45f,
                new Vector2(0.9f, 0.22f), Color.clear, 2);
            arrival.beam = DungeonVisuals.Create("Arrival beam", hero.transform.parent, hero.transform.position, Vector2.one, Color.clear, 30);
            arrival.beam.sprite = DungeonVisuals.GlowSprite;
            var player = hero.GetComponent<DungeonPlayer>();
            if (player != null)
            {
                player.Occupy(delay + Hold + Fall + Settle);
                player.Protect(delay + Hold + Fall + Settle + 0.6f);
            }
            if (local) { Local = arrival; LocalStartedAt = arrival.startedAt; }
            arrival.Animate();
            return arrival;
        }

        /// <summary>How open the local screen is, 0 (black) to 1 (clear); 1 when no arrival is playing.</summary>
        public static float LocalOpenness
        {
            get
            {
                if (LocalStartedAt < 0f) return 1f;
                float t = (Time.time - LocalStartedAt - Hold * 0.5f) / Reveal;
                if (t >= 1f) { LocalStartedAt = -1f; return 1f; }
                return t <= 0f ? 0f : 1f - (1f - t) * (1f - t) * (1f - t);
            }
        }

        /// <summary>Called when the run is torn down: no iris is left hanging over the menu.</summary>
        public static void ClearLocal() { LocalStartedAt = -1f; Local = null; }

        private void LateUpdate()
        {
            // Hidden every frame: a sprite the hero turns back on (a weapon, an aura) must not show before the landing.
            foreach (var renderer in GetComponentsInChildren<Renderer>())
                if (renderer.enabled) { renderer.enabled = false; hidden.Add(renderer); }
            Animate();
        }

        private void Animate()
        {
            float since = Time.time - startedAt - Hold;
            // A long frame can skip the squash; the impact still lands.
            if (since >= Fall + Settle) { if (!landed) Impact(); Land(); Destroy(this); return; }
            if (since >= Fall) { Squash((since - Fall) / Settle); return; }
            float t = Mathf.Clamp01(since / Fall);
            // Falling: slow at first, fast at the end, shrinking from near the camera down to the floor.
            float eased = t * t * t;
            Vector3 at = transform.position;
            ghost.transform.position = at + Vector3.up * (DropHeight * (1f - eased));
            // Stretched long by the speed of the drop.
            float stretch = 0.6f * t * t;
            float scale = Mathf.Lerp(StartScale, 1f, eased);
            ghost.transform.localScale = new Vector3(scale * (1f - stretch * 0.4f), scale * (1f + stretch), 1f);
            float alpha = Mathf.Clamp01(t * 4f);
            for (int i = 0; i < ghostSprites.Length; i++)
                if (ghostSprites[i] != null) ghostSprites[i].color = FlameMesh.Alpha(ghostColors[i], alpha);
            shadow.transform.position = at + Vector3.down * 0.45f;
            shadow.transform.localScale = new Vector3(Mathf.Lerp(0.2f, 1.1f, eased), Mathf.Lerp(0.06f, 0.28f, eased), 1f);
            shadow.color = new Color(0.01f, 0.02f, 0.04f, 0.55f * Mathf.Clamp01(t * 2f));
            // A column of the world's light marks the spot, narrowing onto it as the hero comes down.
            float height = DropHeight + 2f;
            beam.transform.position = at + Vector3.up * (height * 0.5f - 0.45f);
            beam.transform.localScale = new Vector3(Mathf.Lerp(1.4f, 0.5f, t), height, 1f);
            beam.color = FlameMesh.Alpha(accent, 0.35f * Mathf.Clamp01(t * 3f));
        }

        /// <summary>The hero flattened by the impact, springing back up with a small overshoot.</summary>
        private void Squash(float t)
        {
            if (!landed) Impact();
            float spring = Mathf.Exp(-6f * t) * Mathf.Cos(t * Mathf.PI * 3f);
            ghost.transform.position = transform.position;
            ghost.transform.localScale = new Vector3(1f + 0.45f * spring, 1f - 0.4f * spring, 1f);
        }

        /// <summary>The hero hits the floor: shockwave, rubble, sparks of the world's colour and a scorched crater.</summary>
        private void Impact()
        {
            landed = true;
            if (shadow != null) Destroy(shadow.gameObject);
            if (beam != null) Destroy(beam.gameObject);
            shadow = null;
            beam = null;
            for (int i = 0; i < ghostSprites.Length; i++)
                if (ghostSprites[i] != null) ghostSprites[i].color = ghostColors[i];
            Vector2 at = transform.position;
            Vector2 feet = at + Vector2.down * 0.4f;
            var parent = transform.parent;
            var crater = DungeonVisuals.Create("Arrival crater", parent, feet, new Vector2(2.8f, 1.3f), new Color(0.02f, 0.02f, 0.03f, 0.7f), 1);
            crater.sprite = DungeonVisuals.GlowSprite;
            crater.gameObject.AddComponent<FadingSprite>().Duration = 2.6f;
            var scorch = DungeonVisuals.Create("Arrival scorch", parent, feet, new Vector2(3.4f, 1.6f), FlameMesh.Alpha(accent, 0.5f), 2);
            scorch.sprite = DungeonVisuals.GlowSprite;
            scorch.gameObject.AddComponent<FadingSprite>().Duration = 1.1f;
            HeroVfx.Pulse(parent, at, 1.2f, new Color(1f, 1f, 1f, 0.8f), 0.25f);
            HeroVfx.Pulse(parent, at, 3.4f, accent, 0.6f);
            CombatVfx.Ring(parent, feet, 4.2f, accent, 0.7f);
            Debris.Burst(parent, feet, Rubble, 18, 5.5f, 6.5f, 0.16f, 1.6f);
            HeroVfx.Sparks(parent, feet, accent, 24, 9f, 0.5f);
            HeroVfx.Sparks(parent, feet, new Color(0.75f, 0.72f, 0.66f), 16, 6f, 0.55f, Vector2.right, 360f, 1.6f);
            HeroVfx.Motes(parent, at, 1.8f, accent, 16, 1.1f);
            if (!local) { ScreenFx.Shake(0.12f, 0.2f); return; }
            ScreenFx.Shake(0.5f, 0.45f);
            ScreenFx.Flash(FlameMesh.Alpha(accent, 0.55f), 0.7f);
        }

        /// <summary>Shows the real hero again and clears away the stand-in.</summary>
        private void Land()
        {
            if (ghost != null) Destroy(ghost);
            if (shadow != null) Destroy(shadow.gameObject);
            if (beam != null) Destroy(beam.gameObject);
            ghost = null;
            shadow = null;
            beam = null;
            foreach (var renderer in hidden) if (renderer != null) renderer.enabled = true;
            hidden.Clear();
            if (Local == this) Local = null;
        }

        private void OnDestroy()
        {
            // Torn down mid-fall (the run ended, the hero was rebuilt): nothing of it may linger.
            if (ghost != null || shadow != null || beam != null || hidden.Count > 0) Land();
        }
    }
}
