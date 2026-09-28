using UnityEngine;

namespace Slopgame
{
    public enum EnemyHitRegion { Front, Side, Back }

    public sealed class EnemyFacing : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float turnSpeed = 180f;
        public Vector2 Direction { get; private set; } = Vector2.down;
        private Transform marker;

        private void Start()
        {
            marker = DungeonVisuals.Create("Facing marker", transform, transform.position,
                new Vector2(0.2f, 0.13f), new Color(0.15f, 0.1f, 0.12f), 7).transform;
            UpdateMarker();
        }

        public void TurnToward(Vector2 desired, float deltaTime)
        {
            if (desired.sqrMagnitude < 0.0001f || deltaTime <= 0) return;
            float current = Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg;
            float target = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
            float angle = Mathf.MoveTowardsAngle(current, target, turnSpeed * deltaTime) * Mathf.Deg2Rad;
            Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            UpdateMarker();
        }

        public EnemyHitRegion RegionFrom(Vector2 source)
        {
            Vector2 offset = source - (Vector2)transform.position;
            if (offset.sqrMagnitude < 0.0001f) return EnemyHitRegion.Side;
            float dot = Vector2.Dot(Direction, offset.normalized);
            if (dot >= 0.5f) return EnemyHitRegion.Front;
            if (dot <= -0.5f) return EnemyHitRegion.Back;
            return EnemyHitRegion.Side;
        }

        public bool IsBehind(Vector2 source) => RegionFrom(source) == EnemyHitRegion.Back;
        public bool IsInFront(Vector2 source) => RegionFrom(source) == EnemyHitRegion.Front;

        private void UpdateMarker()
        {
            if (marker == null) return;
            marker.localPosition = Direction * 0.36f;
            marker.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg);
        }
    }
}
