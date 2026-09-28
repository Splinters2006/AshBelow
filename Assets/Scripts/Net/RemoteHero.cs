using UnityEngine;

namespace Slopgame
{
    /// <summary>A teammate's hero as seen on this machine: smoothed position, facing, roll, shield and death state.</summary>
    public sealed class RemoteHero : MonoBehaviour
    {
        public ulong Id { get; private set; }
        public string PlayerName { get; private set; }
        public CharacterDefinition Character { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public bool IsAlive => Health > 0;
        public bool IsRolling => (flags & PlayerStateMessage.Rolling) != 0;
        public Vector2 Aim { get; private set; } = Vector2.right;
        private DungeonRun run;
        private SpriteRenderer body, details, shield;
        private Renderer[] renderers;
        private Vector2 target;
        private byte flags;
        private bool facingLeft, visible = true;

        public static RemoteHero Create(DungeonRun run, ulong id, string playerName, CharacterDefinition character, Vector2 position)
        {
            var body = DungeonVisuals.Create(playerName, run.transform, position, Vector2.one * 0.65f, character.Color, 4);
            var hero = body.gameObject.AddComponent<RemoteHero>();
            hero.run = run;
            hero.Id = id;
            hero.PlayerName = playerName;
            hero.Character = character;
            hero.body = body;
            hero.target = position;
            hero.Health = hero.MaxHealth = character.StartingHealth;
            hero.details = DungeonVisuals.DecorateHero(body.transform, character.Weapon, character.Color);
            hero.shield = DungeonVisuals.Create("Teammate shield", body.transform, position, new Vector2(0.14f, 1.3f), new Color(0.45f, 0.78f, 1f, 0.8f), 7);
            hero.shield.enabled = false;
            hero.renderers = body.GetComponentsInChildren<Renderer>(true);
            return hero;
        }

        public void Apply(PlayerStateMessage state)
        {
            target = state.Position;
            if (state.Aim.sqrMagnitude > 0.001f) Aim = state.Aim.normalized;
            flags = state.Flags;
            Health = (flags & PlayerStateMessage.Dead) != 0 ? 0 : Mathf.Max(1, (int)state.Health);
            MaxHealth = Mathf.Max(1, (int)state.MaxHealth);
            // Big jumps (a blink, a new floor) snap instead of sliding through walls.
            if (Vector2.Distance(transform.position, target) > 4f) transform.position = target;
        }

        public void Teleport(Vector2 position)
        {
            target = position;
            transform.position = position;
        }

        private void Update()
        {
            transform.position = Vector2.Lerp(transform.position, target, 1f - Mathf.Exp(-18f * Time.deltaTime));
            SetVisible(IsAlive);
            if (!IsAlive) return;
            bool blocking = (flags & PlayerStateMessage.Blocking) != 0;
            body.color = IsRolling ? new Color(0.4f, 0.65f, 1f)
                : (flags & PlayerStateMessage.Invulnerable) != 0 && Mathf.Repeat(Time.time * 8f, 1f) > 0.5f ? Color.white : Character.Color;
            if (Aim.x < -0.15f) facingLeft = true;
            else if (Aim.x > 0.15f) facingLeft = false;
            body.flipX = facingLeft;
            if (details != null) details.flipX = facingLeft;
            shield.enabled = blocking;
            if (blocking)
            {
                // Parent scale is 0.65, so offsets are in the hero's local units.
                shield.transform.localPosition = Aim * 0.95f;
                shield.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(Aim.y, Aim.x) * Mathf.Rad2Deg);
            }
        }

        private void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            foreach (var renderer in renderers) if (renderer != null && renderer != shield) renderer.enabled = value;
            if (!value) shield.enabled = false;
        }
    }
}
