using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A <see cref="FrozenEncasement"/> breaking apart: a white flash, a puff of frost, and shards of the block tumbling
    /// out and falling. Purely visual.
    /// </summary>
    public sealed class IceShatterVfx : MeshEffect
    {
        private const int Shards = 12;
        private Vector2 at, half;
        private float seed;

        /// <param name="half">The block's half size, so the shards fly from its real faces.</param>
        public static void Play(Transform root, Vector2 at, Vector2 half)
        {
            var effect = Spawn<IceShatterVfx>(root, 0.55f, 11);
            if (effect == null) return;
            effect.at = at;
            effect.half = half;
            effect.seed = Random.value * 100f;
            effect.Redraw();
        }

        protected override void Draw(float t)
        {
            float fade = 1f - t, flash = Mathf.Clamp01(1f - t / 0.15f), size = Mathf.Max(half.x, half.y);

            // Frost bursts out and settles.
            Mesh.Disc(at, size * (0.8f + 0.9f * EaseOut(t / 0.5f)), FlameMesh.Alpha(FrozenEncasement.Mist, 0.35f * fade), FlameMesh.Alpha(FrozenEncasement.Mist, 0f), 32);
            Mesh.Ring(at, size * (0.9f + 1.1f * EaseOut(t / 0.35f)), 0.08f * fade, FlameMesh.Alpha(FrozenEncasement.Light, 0.9f * fade), FlameMesh.Alpha(FrozenEncasement.Shade, 0f), 40);

            // Shards of the block tumble outward and fall.
            for (int i = 0; i < Shards; i++)
            {
                float s = FlameMesh.Hash(seed + i, 3.7f), angle = (i + s) / Shards * Mathf.PI * 2f;
                Vector2 outward = FlameMesh.Polar(angle, 1f);
                Vector2 start = at + Vector2.Scale(outward, half) * 0.6f;
                Vector2 shard = start + outward * (2.4f + 1.6f * s) * t * 0.5f + Vector2.down * 3.5f * t * t;
                float spin = angle + t * (s < 0.5f ? -9f : 9f);
                float length = size * (0.35f + 0.3f * s) * (1f - 0.5f * t);
                Mesh.Crystal(shard, FlameMesh.Polar(spin, 1f), length * 0.5f, length, seed + i, Color.white,
                    FrozenEncasement.Light, FrozenEncasement.Shade, FrozenEncasement.Deep, fade);
            }

            // A white crack of light as it breaks.
            if (flash > 0f) Mesh.Disc(at, size * 0.9f * flash, FlameMesh.Alpha(Color.white, 0.9f * flash), FlameMesh.Alpha(FrozenEncasement.Light, 0f), 24);
        }
    }
}
