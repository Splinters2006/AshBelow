using UnityEngine;

namespace Slopgame
{
    public sealed class EnemyTactics : MonoBehaviour
    {
        private DungeonEnemy enemy;

        public Vector2 Direction(Vector2 target, bool visible, bool charging)
        {
            if (enemy == null) enemy = GetComponent<DungeonEnemy>();
            if (charging) return Vector2.zero;
            Vector2 position = transform.position;
            Vector2 offset = target - position;
            float distance = offset.magnitude;
            Vector2 desired = visible ? offset.normalized : enemy.Run.DirectionToPlayer(position);
            bool retreat = enemy.IsRanged && visible && distance < 3f;
            if (enemy.IsRanged && visible)
                desired = retreat ? -desired : distance < 5.5f ? Vector2.zero : desired;

            Vector2 separation = Vector2.zero;
            int nearby = 0, order = 0;
            DungeonEnemy leader = enemy;
            float nearest = distance;
            int ownIndex = enemy.Run.Enemies.IndexOf(enemy);
            for (int allyIndex = 0; allyIndex < enemy.Run.Enemies.Count; allyIndex++)
            {
                var ally = enemy.Run.Enemies[allyIndex];
                if (ally == enemy || ally.Health <= 0 || ally.Boss != null) continue;
                Vector2 away = position - (Vector2)ally.transform.position;
                if (away.sqrMagnitude < 1.44f)
                    separation += away.sqrMagnitude > 0.001f ? away.normalized * (1.2f - away.magnitude)
                        : (ownIndex < allyIndex ? Vector2.left : Vector2.right);
                if (ally.IsRanged || Vector2.Distance(ally.transform.position, target) > 6f) continue;
                nearby++;
                if (allyIndex < ownIndex) order++;
                float allyDistance = Vector2.Distance(ally.transform.position, target);
                if (allyDistance < nearest) { leader = ally; nearest = allyDistance; }
            }
            // The closest melee enemy presses the attack while its pack approaches from different sides.
            if (!enemy.IsRanged && visible && distance > 1.2f && distance < 6f && nearby > 0 && leader != enemy)
            {
                float angle = order * Mathf.PI * 2f / (nearby + 1);
                Vector2 flank = target + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.1f;
                if (enemy.Run.Map.CanStand(flank, enemy.MoveRadius) && enemy.Run.HasLineOfSight(position, flank))
                    desired = (flank - position).normalized;
            }
            desired = Vector2.ClampMagnitude(desired + separation * 1.5f, 1f);
            if (desired.sqrMagnitude < 0.001f) return Vector2.zero;

            // Look ahead in several directions so retreating casters slide along walls toward open space.
            Vector2 best = Vector2.zero;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8f;
                Vector2 candidate = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 probe = position;
                float clearance = 0f;
                for (int step = 0; step < 5; step++)
                {
                    Vector2 next = probe + candidate * 0.18f;
                    if (!enemy.Run.Map.CanStand(next, enemy.MoveRadius)) break;
                    probe = next;
                    clearance += 0.18f;
                }
                if (clearance < 0.18f) continue;
                float score = Vector2.Dot(candidate, desired.normalized) + clearance * 1.5f;
                if (retreat) score += (Vector2.Distance(probe, target) - distance) * 0.8f;
                if (score > bestScore) { bestScore = score; best = candidate; }
            }
            return best * desired.magnitude;
        }
    }
}
