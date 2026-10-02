using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Primal Rage: for as long as the Brawler rages, hackles of blood-red bristle off her back, the floor under her
    /// darkens and pounds with a heartbeat that quickens as the rage runs out, claws rake the air around her and
    /// flecks of blood are flung off across the floor. It opens on a roar (three shockwaves and one great claw mark
    /// torn across her) and ends with the hackles dropping and a breath of grey steam as she tires. Purely visual.
    /// </summary>
    public sealed class PrimalRageVfx : MonoBehaviour
    {
        private const float FadeOut = 0.5f, OpenTime = 0.55f, ClawEvery = 0.7f, ClawTime = 0.32f;
        private const int Hackles = 18, Flecks = 14;
        private const float Tau = Mathf.PI * 2f;
        private static readonly Color Blood = FlameMesh.Crimson, Dark = FlameMesh.Ember, Bone = new Color(1f, 0.9f, 0.82f);
        // Behind the hero (the floor, the glow, her hackles) and in front of her (claws, flecks, the roar).
        private FlameMesh back, front;
        private Transform hero;
        private float age, duration, heart;

        public static PrimalRageVfx Play(Transform root, Transform hero, float duration)
        {
            if (root == null || hero == null) return null;
            var effect = new GameObject("Primal Rage").AddComponent<PrimalRageVfx>();
            effect.transform.SetParent(root, false);
            effect.hero = hero;
            effect.duration = Mathf.Max(0.5f, duration);
            effect.back = new FlameMesh(effect.gameObject, 2);
            var near = new GameObject("Primal Rage (front)");
            near.transform.SetParent(effect.transform, false);
            effect.front = new FlameMesh(near, 9);
            return effect;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (hero == null || age >= duration + FadeOut) { Destroy(gameObject); return; }
            float appear = Mathf.Clamp01(age / 0.2f);
            float fade = appear * Mathf.Clamp01((duration + FadeOut - age) / FadeOut);
            // Her heart hammers faster as the rage burns down.
            heart += Time.deltaTime * Mathf.Lerp(1.5f, 2.8f, Mathf.Clamp01(age / duration));
            float phase = Mathf.Repeat(heart, 1f), beat = Mathf.Max(Thump(phase), Thump(phase - 0.22f));
            Vector2 center = hero.position;
            Color rage = HeroBuffs.RageColor;

            back.Begin();
            front.Begin();
            DrawGround(center, appear, fade, phase, beat, rage);
            back.Ellipse(center + Vector2.up * 0.1f, 0.9f, 1.05f, FlameMesh.Alpha(Blood, (0.2f + 0.2f * beat) * fade), FlameMesh.Alpha(Blood, 0f), 24);
            DrawHackles(center, appear, fade, beat, rage);
            DrawClaws(center, fade, rage);
            DrawFlecks(center, fade, rage);
            if (age < OpenTime) DrawRoar(center, age / OpenTime, rage);
            if (age > duration) DrawEnding(center, (age - duration) / FadeOut, rage);
            back.Commit();
            front.Commit();
        }

        /// <summary>One thump of the heart: a sharp kick that dies away.</summary>
        private static float Thump(float x) => x < 0f ? 0f : Mathf.Exp(-x * 9f);

        /// <summary>A dark stain under her feet, and a double ring thrown out by every heartbeat.</summary>
        private void DrawGround(Vector2 center, float appear, float fade, float phase, float beat, Color rage)
        {
            Vector2 feet = center + Vector2.down * 0.35f;
            back.Disc(feet, 1.5f * appear * (1f + 0.08f * beat), FlameMesh.Alpha(Dark, 0.6f * fade), FlameMesh.Alpha(Blood, 0f), 36);
            for (int i = 0; i < 2; i++)
            {
                float p = Mathf.Repeat(phase - i * 0.22f, 1f), rest = 1f - p;
                back.Ring(feet, 0.4f + 1.7f * p, 0.1f * rest + 0.01f, FlameMesh.Alpha(i == 0 ? rage : Blood, 0.8f * rest * rest * fade), 48);
            }
        }

        /// <summary>Her hackles: a mane of jagged spikes standing off her, tallest over her back, bristling with each beat.</summary>
        private void DrawHackles(Vector2 center, float appear, float fade, float beat, Color rage)
        {
            float frame = Mathf.Floor(age * 14f);
            Vector2 middle = center + Vector2.up * 0.1f;
            for (int i = 0; i < Hackles; i++)
            {
                float a = i * Tau / Hackles;
                float length = (0.35f + 0.45f * FlameMesh.Hash(i, frame)) * (1f + 0.5f * beat) * (0.7f + 0.5f * Mathf.Max(0f, Mathf.Sin(a))) * appear;
                Vector2 root = middle + new Vector2(Mathf.Cos(a) * 0.38f, Mathf.Sin(a) * 0.5f);
                Vector2 dir = FlameMesh.Polar(a + (FlameMesh.Hash(i + 0.3f, frame) - 0.5f) * 0.5f, 1f), across = Vector2.Perpendicular(dir) * 0.11f;
                Color foot = FlameMesh.Alpha(i % 2 == 0 ? Blood : Dark, 0.85f * fade);
                back.Triangle(root - across, root + dir * length, root + across, foot, FlameMesh.Alpha(rage, fade), foot);
            }
        }

        /// <summary>Three claws raked through the air beside her, at a new spot every time.</summary>
        private void DrawClaws(Vector2 center, float fade, Color rage)
        {
            float index = Mathf.Floor(age / ClawEvery), t = Mathf.Repeat(age, ClawEvery) / ClawTime;
            if (t >= 1f || age > duration) return;
            float a = FlameMesh.Hash(index, 1.9f) * Tau;
            Vector2 at = center + new Vector2(Mathf.Cos(a) * 0.95f, Mathf.Sin(a) * 0.75f + 0.1f);
            Vector2 dir = FlameMesh.Polar(a + Mathf.PI * 0.5f + (FlameMesh.Hash(index, 4.4f) - 0.5f) * 0.8f, 1f);
            Rake(at, dir, 0.9f, 0.16f, 0.07f, t, fade, rage);
        }

        /// <summary>Three tapering slashes side by side, torn open fast and then fading.</summary>
        private void Rake(Vector2 middle, Vector2 dir, float length, float spacing, float width, float t, float alpha, Color rage)
        {
            Vector2 across = Vector2.Perpendicular(dir);
            float torn = Mathf.Clamp01(t * 2.5f), reach = length * (1f - (1f - torn) * (1f - torn));
            alpha *= 1f - t;
            for (int claw = -1; claw <= 1; claw++)
            {
                // The middle claw is the longest.
                float scale = claw == 0 ? 1f : 0.8f;
                Vector2 start = middle + across * claw * spacing - dir * length * 0.5f * scale;
                Vector2 tip = start + dir * reach * scale;
                front.Triangle(start - across * width, tip, start + across * width, FlameMesh.Alpha(Blood, alpha), FlameMesh.Alpha(rage, alpha), FlameMesh.Alpha(Blood, alpha));
                front.Triangle(start - across * width * 0.4f, tip, start + across * width * 0.4f, FlameMesh.Alpha(Bone, alpha), FlameMesh.Alpha(Color.white, alpha), FlameMesh.Alpha(Bone, alpha));
            }
        }

        /// <summary>Flecks of blood flung out across the floor in low arcs.</summary>
        private void DrawFlecks(Vector2 center, float fade, Color rage)
        {
            Vector2 feet = center + Vector2.down * 0.3f;
            for (int i = 0; i < Flecks; i++)
            {
                float seed = FlameMesh.Hash(i, 1.3f);
                float phase = Mathf.Repeat(age * (0.9f + 0.6f * seed) + FlameMesh.Hash(i, 7.1f), 1f);
                float a = FlameMesh.Hash(i, 3.7f) * Tau, reach = 0.4f + 1.3f * phase;
                Vector2 at = feet + new Vector2(Mathf.Cos(a) * reach, Mathf.Sin(a) * reach * 0.5f + Mathf.Sin(phase * Mathf.PI) * 0.35f);
                front.Diamond(at, 0.05f + 0.03f * seed, FlameMesh.Alpha(i % 3 == 0 ? rage : Blood, (1f - phase) * fade));
            }
        }

        /// <summary>The roar that starts it: three shockwaves one after another and a great claw mark torn across her.</summary>
        private void DrawRoar(Vector2 center, float t, Color rage)
        {
            for (int i = 0; i < 3; i++)
            {
                float p = Mathf.Clamp01(t * 1.3f - i * 0.15f), rest = 1f - p;
                if (p <= 0f) continue;
                front.Ring(center, 0.4f + 4.2f * p, 0.22f * rest + 0.02f, FlameMesh.Alpha(i == 1 ? Bone : rage, rest), FlameMesh.Alpha(Blood, 0f), 64);
            }
            Rake(center + Vector2.up * 0.15f, new Vector2(0.6f, -0.8f), 3.4f, 0.42f, 0.2f, t, 1f, rage);
        }

        /// <summary>Spent: the last of the rage is drawn back in and leaves her in a breath of grey steam.</summary>
        private void DrawEnding(Vector2 center, float t, Color rage)
        {
            float rest = 1f - Mathf.Clamp01(t);
            Color steam = HeroBuffs.TiredColor;
            front.Ring(center, 0.2f + 2.2f * rest, 0.16f * rest + 0.02f, FlameMesh.Alpha(rage, rest), FlameMesh.Alpha(Blood, 0f), 56);
            for (int side = -1; side <= 1; side += 2)
                front.Disc(center + new Vector2(side * (0.2f + 0.3f * t), 0.4f + 0.9f * t), 0.3f + 0.5f * t, FlameMesh.Alpha(steam, 0.5f * rest), FlameMesh.Alpha(steam, 0f), 24);
        }

        private void OnDestroy()
        {
            back?.Release();
            front?.Release();
        }
    }
}
