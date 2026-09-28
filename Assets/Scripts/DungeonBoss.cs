using UnityEngine;

namespace Slopgame
{
    [RequireComponent(typeof(DungeonEnemy))]
    public sealed class DungeonBoss : MonoBehaviour
    {
        public DungeonEnemy Enemy { get; private set; }
        public int MaxHealth { get; private set; }
        public string Title => Enemy.Run.Floor % 10 == 0 ? "THE CINDER SOVEREIGN" : "THE ASH WARDEN";
        public bool IsEnraged => Enemy.Health <= MaxHealth / 2;
        public string Tell => charging ? pattern % 2 == 0 ? "EMBER FAN - SIDESTEP" : "NOVA - KEEP MOVING" : IsEnraged ? "ENRAGED" : "GUARDIAN OF THE RELIC";
        private float readyAt, fireAt;
        private bool charging, dropped;
        private int pattern;
        private Vector2 lockedAim;
        private SpriteRenderer body;

        public void Initialize(DungeonRun run)
        {
            Enemy = GetComponent<DungeonEnemy>();
            Enemy.Run = run;
            Enemy.Boss = this;
            MaxHealth = 24 + run.Floor * 3;
            Enemy.Health = MaxHealth;
            Enemy.Speed = 1.5f;
            body = GetComponent<SpriteRenderer>();
            gameObject.name = Title;
            readyAt = Time.time + 2f;
            DungeonVisuals.DecorateBoss(transform);
        }

        private void Update()
        {
            if (!Enemy.Run.IsPlaying || Enemy.Health <= 0) return;
            Vector2 offset = Enemy.Run.Player.transform.position - transform.position;
            Enemy.Facing.TurnToward(offset, Time.deltaTime);
            body.color = Enemy.IsFlashing || charging ? Color.Lerp(AbilityCatalog.Gold, Color.white, 0.5f + Mathf.Sin(Time.time * 18f) * 0.5f)
                : IsEnraged ? new Color(1f, 0.26f, 0.28f) : new Color(0.65f, 0.3f, 0.55f);
            if (charging)
            {
                if (Time.time >= fireAt)
                {
                    int shots = pattern % 2 == 0 ? (IsEnraged ? 7 : 5) : (IsEnraged ? 16 : 12);
                    for (int i = 0; i < shots; i++)
                    {
                        float angle = pattern % 2 == 0 ? (i - (shots - 1) * 0.5f) * 14f : i * 360f / shots;
                        Vector2 direction = Quaternion.Euler(0, 0, angle) * lockedAim;
                        EnemyProjectile.Spawn(Enemy.Run, Enemy.Run.ProjectileRoot, (Vector2)transform.position + direction * 0.5f, direction);
                    }
                    pattern++;
                    charging = false;
                    readyAt = Time.time + (IsEnraged ? 1.1f : 1.65f);
                }
            }
            else if (Time.time >= readyAt)
            {
                charging = true;
                lockedAim = offset.sqrMagnitude > 0.01f ? offset.normalized : Vector2.down;
                fireAt = Time.time + 0.85f;
                CombatVfx.Ring(Enemy.Run.ProjectileRoot, transform.position, 1.5f, AbilityCatalog.Gold, 0.85f);
            }
            else if (offset.magnitude > 2f)
                transform.position = Enemy.Run.Map.Move(transform.position, offset.normalized * Enemy.Speed * Enemy.MoveMultiplier * Time.deltaTime);
            if (offset.magnitude < 1.05f) Enemy.Run.Player.Hit();
        }

        public void Defeated()
        {
            if (dropped) return;
            dropped = true;
            foreach (var bolt in Enemy.Run.ProjectileRoot.GetComponentsInChildren<EnemyProjectile>())
            {
                bolt.gameObject.SetActive(false);
                Destroy(bolt.gameObject);
            }
            Enemy.Run.DropArtifact(transform.position);
        }
    }
}
