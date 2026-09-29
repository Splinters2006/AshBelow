using System;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Paladin's blessing radius while the blessing charges: a gold ring with a progress arc that fills clockwise
    /// and flares once the blessing is ready. Shown for the local Paladin and for Paladin teammates alike, so allies
    /// can see where to stand. Draws in world units even though hero sprites are scaled.
    /// </summary>
    public sealed class BlessingChargeRing : MonoBehaviour
    {
        private const int Segments = 72;
        private FlameMesh mesh;
        private Func<bool> isCharging;
        private Func<float> amount;
        private float strength, shown;

        public static BlessingChargeRing Attach(Transform hero, Func<bool> isCharging, Func<float> amount)
        {
            var holder = new GameObject("Blessing charge ring");
            holder.transform.SetParent(hero, false);
            Vector3 scale = hero.lossyScale;
            holder.transform.localScale = new Vector3(Safe(scale.x), Safe(scale.y), 1f);
            var ring = holder.AddComponent<BlessingChargeRing>();
            ring.isCharging = isCharging;
            ring.amount = amount;
            ring.mesh = new FlameMesh(holder, 5);
            return ring;
        }

        private static float Safe(float scale) => Mathf.Abs(scale) > 0.0001f ? 1f / scale : 1f;

        private void LateUpdate()
        {
            bool charging = isCharging != null && isCharging();
            strength = Mathf.MoveTowards(strength, charging ? 1f : 0f, Time.deltaTime * (charging ? 10f : 6f));
            // Keep the last reading while fading out so the arc does not snap back to empty.
            if (charging) shown = Mathf.Clamp01(amount != null ? amount() : 0f);
            mesh.Begin();
            if (strength > 0f) Draw(shown, strength);
            mesh.Commit();
        }

        private void Draw(float charge, float alpha)
        {
            float radius = PaladinAttack.BlessingRadius, time = Time.time;
            bool ready = charge >= 1f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * (ready ? 12f : 4f));
            Color gold = AbilityCatalog.Gold;
            mesh.Disc(Vector2.zero, radius, FlameMesh.Alpha(gold, (0.03f + 0.07f * charge + (ready ? 0.06f * pulse : 0f)) * alpha),
                FlameMesh.Alpha(gold, (0.08f + 0.12f * charge) * alpha), Segments);
            mesh.Ring(Vector2.zero, radius, 0.06f, FlameMesh.Alpha(gold, Mathf.Lerp(0.25f, 0.8f, charge) * alpha), Segments);
            // The progress arc fills clockwise from the top.
            Arc(radius, 0.2f + (ready ? 0.08f * pulse : 0f), charge, FlameMesh.Alpha(ready ? FlameMesh.Core : gold, (0.75f + 0.25f * pulse) * alpha));
            float spin = time * 0.8f;
            for (int i = 0; i < 8; i++)
                mesh.Diamond(FlameMesh.Polar(spin + i * Mathf.PI / 4f, radius - 0.3f), ready ? 0.14f : 0.09f,
                    FlameMesh.Alpha(gold, (ready ? 0.9f : 0.35f + 0.4f * charge) * alpha));
        }

        private void Arc(float radius, float width, float fraction, Color color)
        {
            int count = Mathf.CeilToInt(Segments * fraction);
            if (count <= 0) return;
            float start = Mathf.PI * 0.5f, sweep = -Mathf.PI * 2f * fraction;
            float inner = radius - width * 0.5f, outer = radius + width * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float a0 = start + sweep * i / count, a1 = start + sweep * (i + 1) / count;
                mesh.Quad(FlameMesh.Polar(a0, inner), FlameMesh.Polar(a0, outer), FlameMesh.Polar(a1, outer), FlameMesh.Polar(a1, inner),
                    color, color, color, color);
            }
        }

        private void OnDestroy() { mesh?.Release(); }
    }
}
