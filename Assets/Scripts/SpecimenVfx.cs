using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Specimen's own effects: the chain whip cracking out in a wave and snapping at its tip, the Chain Cyclone
    /// sweeping round him, and the burst of a transformation.
    /// </summary>
    public sealed class SpecimenVfx : MonoBehaviour
    {
        private enum Kind { Lash, Cyclone }
        private Kind kind;
        private Transform owner;
        private Vector2 aim, anchor;
        private float reach, sweep, duration, born, seed;
        private Color color;
        private FlameMesh mesh;

        /// <summary>A lash: the chain flies out in a travelling wave, straightens, and cracks at its tip.</summary>
        public static void Lash(Transform root, Transform owner, Vector2 aim, float reach, Color color)
            => Create(root, owner, Kind.Lash, aim, reach, 0f, 0.16f, color);

        /// <summary>The chain swept round him (a full spin by default; Ankle Wrap sweeps a half circle low).</summary>
        public static void Cyclone(Transform root, Transform owner, Vector2 aim, float reach, Color color, float sweep = 360f)
            => Create(root, owner, Kind.Cyclone, aim, reach, sweep, sweep >= 360f ? 0.26f : 0.18f, color);

        private static void Create(Transform root, Transform owner, Kind kind, Vector2 aim, float reach, float sweep, float duration, Color color)
        {
            if (root == null || owner == null) return;
            var effect = new GameObject("Specimen " + kind).AddComponent<SpecimenVfx>();
            effect.transform.SetParent(root, false);
            effect.kind = kind;
            effect.owner = owner;
            effect.anchor = owner.position;
            effect.aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
            effect.reach = reach;
            effect.sweep = sweep;
            effect.duration = duration;
            effect.color = color;
            effect.born = Time.time;
            effect.seed = Random.value * 10f;
            effect.mesh = new FlameMesh(effect.gameObject, 7);
        }

        private void Update()
        {
            float t = (Time.time - born) / duration;
            if (t >= 1.25f) { Destroy(gameObject); return; }
            if (owner != null) anchor = owner.position;
            mesh.Begin();
            if (kind == Kind.Lash) DrawLash(Mathf.Clamp01(t), t > 1f ? 1f - (t - 1f) / 0.25f : 1f);
            else DrawCyclone(Mathf.Clamp01(t), t > 1f ? 1f - (t - 1f) / 0.25f : 1f);
            mesh.Commit();
        }

        private void DrawLash(float t, float fade)
        {
            // Out to full length over the first 60%, a wave rolling along it that dies away as it straightens.
            float extend = Mathf.Clamp01(t / 0.6f), length = reach * Mathf.SmoothStep(0f, 1f, extend);
            Vector2 side = Vector2.Perpendicular(aim);
            float wave = (1f - extend) * 0.35f;
            const int Links = 18;
            Vector2 previous = anchor + aim * 0.25f;
            for (int i = 1; i <= Links; i++)
            {
                float k = i / (float)Links;
                Vector2 point = anchor + aim * (0.25f + (length - 0.25f) * k) + side * Mathf.Sin(k * Mathf.PI * 2.2f + seed) * wave * k;
                Color link = FlameMesh.Alpha(i % 2 == 0 ? color : Color.Lerp(color, Color.black, 0.35f), fade);
                mesh.Bar(previous, (point - previous).normalized, Vector2.Distance(previous, point), 0.045f, link, link);
                previous = point;
            }
            // The crack at the tip.
            if (extend >= 0.95f)
            {
                float crack = Mathf.Clamp01((t - 0.57f) / 0.43f);
                mesh.Disc(previous, 0.12f + 0.25f * crack, FlameMesh.Alpha(Color.white, fade * (1f - crack)), FlameMesh.Alpha(SpecimenCatalog.Keen, 0f), 14);
                mesh.Ring(previous, 0.18f + 0.4f * crack, 0.04f, FlameMesh.Alpha(SpecimenCatalog.Keen, fade * (1f - crack)), 18);
            }
        }

        private void DrawCyclone(float t, float fade)
        {
            float start = Mathf.Atan2(aim.y, aim.x) - sweep * 0.5f * Mathf.Deg2Rad, head = start + sweep * Mathf.Deg2Rad * Mathf.SmoothStep(0f, 1f, t);
            // A fading trail of chain behind the leading edge, and the chain itself as a spoke.
            const int Trail = 14;
            for (int i = 0; i < Trail; i++)
            {
                float a = head - i * 0.12f, b = a - 0.12f;
                if (b < start) break;
                float alpha = fade * (1f - i / (float)Trail) * 0.7f;
                mesh.Quad(anchor + FlameMesh.Polar(a, reach * 0.35f), anchor + FlameMesh.Polar(a, reach), anchor + FlameMesh.Polar(b, reach),
                    anchor + FlameMesh.Polar(b, reach * 0.35f), FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(color, alpha), FlameMesh.Alpha(color, alpha), FlameMesh.Alpha(color, 0f));
            }
            Vector2 tip = anchor + FlameMesh.Polar(head, reach);
            mesh.Bar(anchor, (tip - anchor).normalized, reach, 0.045f, FlameMesh.Alpha(color, fade), FlameMesh.Alpha(color, fade));
            mesh.Disc(tip, 0.1f, FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(SpecimenCatalog.Keen, 0f), 12);
        }

        private void OnDestroy() => mesh?.Release();

        /// <summary>A change of body: a burst of the new form's colour, and a jolt of the screen for the bigger changes.</summary>
        public static void Transformation(DungeonRun run, Transform hero, SpecimenForm form, bool grown)
        {
            if (run == null || run.ProjectileRoot == null || hero == null) return;
            var root = run.ProjectileRoot;
            Vector2 at = hero.position;
            Color color = SpecimenCatalog.FormColor(form);
            float size = form == SpecimenForm.Behemoth ? (grown ? 2.4f : 2f) : 1.5f;
            HeroVfx.Pulse(root, at, size, FlameMesh.Alpha(color, 0.75f), 0.45f);
            CombatVfx.Ring(root, at, size * 0.8f, color, 0.4f);
            HeroVfx.Sparks(root, at, color, 24, 5f, 0.5f);
            HeroVfx.Motes(root, at, size * 0.5f, color, 18, 0.9f);
            CoopFx.Pulse(run, at, size, color, 0.45f);
            ScreenFx.Flash(FlameMesh.Alpha(color, 0.2f), 0.3f);
            ScreenFx.Shake(form == SpecimenForm.Behemoth ? 0.25f : 0.12f, 0.3f);
        }
    }
}
