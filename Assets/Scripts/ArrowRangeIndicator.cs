using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Archer aiming guide: a faint line along the aim showing exactly how far a basic arrow will fly,
    /// stretching as the shot charges and stopping where a wall would stop the arrow.
    /// </summary>
    public sealed class ArrowRangeIndicator : MonoBehaviour
    {
        private static readonly Color Idle = new Color(0.95f, 1f, 0.65f, 0.22f);
        private static readonly Color Charged = new Color(1f, 0.8f, 0.25f, 0.75f);
        private static readonly Color Blocked = new Color(1f, 0.45f, 0.35f, 0.7f);
        private const float StartOffset = 0.45f;
        public DungeonPlayer Player { get; set; }
        private LineRenderer path, endTick, maxTick;
        private Material material;

        private void Awake()
        {
            material = new Material(Shader.Find("Sprites/Default")) { name = "Arrow range indicator" };
            path = CreateLine("Arrow range path", 0.045f);
            endTick = CreateLine("Arrow range end", 0.07f);
            maxTick = CreateLine("Arrow range full charge", 0.04f);
        }

        private LineRenderer CreateLine(string name, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.sortingOrder = 5;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        /// <summary>Distance a basic arrow travels along <paramref name="aim"/> before its range ends or a wall stops it.</summary>
        public static float FlightDistance(DungeonMap map, Vector2 origin, Vector2 aim, float range, out bool blocked)
        {
            blocked = false;
            if (map == null || aim.sqrMagnitude < 0.0001f) return range;
            aim.Normalize();
            const float step = 0.08f; // Matches PlayerProjectile's collision stepping.
            for (float travelled = step; travelled <= range + 0.0001f; travelled += step)
                if (!map.CanStand(origin + aim * Mathf.Min(travelled, range), 0.08f))
                {
                    blocked = true;
                    return Mathf.Max(0f, travelled - step);
                }
            return range;
        }

        private void LateUpdate()
        {
            bool show = Player != null && Player.Run != null && Player.Run.IsPlaying && !Player.IsRolling
                && Player.Charge != null && Player.Run.Map != null;
            path.enabled = endTick.enabled = maxTick.enabled = false;
            if (!show) return;

            float charge = Player.Charge.Amount;
            bool charging = Player.Charge.IsCharging;
            Vector2 origin = transform.position;
            Vector2 aim = Player.AimDirection.sqrMagnitude > 0.0001f ? Player.AimDirection.normalized : Vector2.right;
            float range = BowAttack.RangeForCharge(charge);
            float distance = FlightDistance(Player.Run.Map, origin, aim, range, out bool blocked);
            if (distance <= StartOffset + 0.05f && !blocked) return;

            float pulse = charging && charge >= 1f ? 0.85f + 0.15f * Mathf.Sin(Time.time * 14f) : 1f;
            Color tip = blocked ? Blocked : Color.Lerp(Idle, Charged, charging ? 0.35f + 0.65f * charge : 0f);
            tip.a *= pulse;
            Color tail = tip;
            tail.a = 0f;

            Vector2 start = origin + aim * Mathf.Min(StartOffset, distance);
            Vector2 end = origin + aim * distance;
            path.enabled = true;
            path.SetPosition(0, start);
            path.SetPosition(1, end);
            path.startColor = tail;
            path.endColor = tip;

            Vector2 side = Vector2.Perpendicular(aim) * (blocked ? 0.28f : 0.2f);
            endTick.enabled = true;
            endTick.SetPosition(0, end - side);
            endTick.SetPosition(1, end + side);
            endTick.startColor = endTick.endColor = tip;

            // While charging, a ghost tick shows where a full charge would reach.
            float full = BowAttack.RangeForCharge(1f);
            if (charging && charge < 1f && !blocked)
            {
                float fullDistance = FlightDistance(Player.Run.Map, origin, aim, full, out _);
                if (fullDistance > distance + 0.05f)
                {
                    Vector2 ghost = origin + aim * fullDistance;
                    Color faint = Charged;
                    faint.a = 0.25f;
                    maxTick.enabled = true;
                    maxTick.SetPosition(0, ghost - side * 0.7f);
                    maxTick.SetPosition(1, ghost + side * 0.7f);
                    maxTick.startColor = maxTick.endColor = faint;
                }
            }
        }

        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
