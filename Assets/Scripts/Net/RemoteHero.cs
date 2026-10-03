using UnityEngine;

namespace Slopgame
{
    /// <summary>A teammate's hero as seen on this machine: smoothed position, facing, roll, shield and death state.</summary>
    public sealed class RemoteHero : MonoBehaviour
    {
        public ulong Id { get; private set; }
        public string PlayerName { get; private set; }
        public int NameColor { get; private set; }
        public int Badge { get; private set; }
        public CharacterDefinition Character { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public bool IsAlive => Health > 0;
        public bool IsRolling => (flags & PlayerStateMessage.Rolling) != 0;
        public bool IsEmpowered => (flags & PlayerStateMessage.Empowered) != 0;
        public bool IsBlessed => (moreFlags & PlayerStateMessage.Blessed) != 0;
        public bool IsVeiled => (moreFlags & PlayerStateMessage.Veiled) != 0;
        public bool IsCharging => (flags & PlayerStateMessage.Charging) != 0;
        /// <summary>Shield Taunt: enemies target this teammate first.</summary>
        public bool IsTaunting => IsAlive && (moreFlags & PlayerStateMessage.Taunting) != 0;
        /// <summary>The Specimen's form (0 frail, 1 Behemoth, 2 Edge, 3 rampaging Behemoth); 0 for every other hero.</summary>
        private int FormBits => Character != null && Character.Weapon == WeaponType.Mutation
            ? (moreFlags & PlayerStateMessage.SpecimenFormMask) >> PlayerStateMessage.SpecimenFormShift : 0;
        /// <summary>A Behemoth teammate is a big target: enemies near him go for him first.</summary>
        public bool IsBigTarget => IsAlive && (FormBits == 1 || FormBits == 3);
        /// <summary>When this teammate last fell (Heavenly Host revives whoever has been down longest).</summary>
        public float DiedAt { get; private set; }
        public float ChargeAmount { get; private set; }
        public Vector2 Aim { get; private set; } = Vector2.right;
        private DungeonRun run;
        private SpriteRenderer body, details, shield;
        private Renderer[] renderers;
        private Vector2 target;
        private byte flags, moreFlags;
        private bool facingLeft, visible = true;
        private int lookKey;

        public static RemoteHero Create(DungeonRun run, ulong id, string playerName, int nameColor, int badge, CharacterDefinition character, Vector2 position)
        {
            var body = DungeonVisuals.Create(playerName, run.transform, position, Vector2.one * 0.65f, character.Color, 4);
            var hero = body.gameObject.AddComponent<RemoteHero>();
            hero.run = run;
            hero.Id = id;
            hero.PlayerName = playerName;
            hero.NameColor = nameColor;
            hero.Badge = badge;
            hero.Character = character;
            hero.body = body;
            hero.target = position;
            hero.Health = hero.MaxHealth = character.StartingHealth;
            hero.details = DungeonVisuals.DecorateHero(body.transform, character.Weapon, character.Color);
            hero.shield = DungeonVisuals.Create("Teammate shield", body.transform, position, new Vector2(0.14f, 1.3f), new Color(0.45f, 0.78f, 1f, 0.8f), 7);
            hero.shield.enabled = false;
            BlessingSparkles.Attach(body.transform, () => hero.IsAlive && hero.IsBlessed);
            if (character.Weapon == WeaponType.Hammer)
                BlessingChargeRing.Attach(body.transform, () => hero.IsAlive && hero.IsCharging && !hero.IsRolling, () => hero.ChargeAmount);
            hero.renderers = body.GetComponentsInChildren<Renderer>(true);
            return hero;
        }

        public void Apply(PlayerStateMessage state)
        {
            target = state.Position;
            if (state.Aim.sqrMagnitude > 0.001f) Aim = state.Aim.normalized;
            flags = state.Flags;
            moreFlags = state.MoreFlags;
            ChargeAmount = state.Charge / 255f;
            bool wasAlive = IsAlive;
            Health = (flags & PlayerStateMessage.Dead) != 0 ? 0 : Mathf.Max(1, (int)state.Health);
            if (wasAlive && !IsAlive)
            {
                DiedAt = Time.time;
                if (run != null && run.ProjectileRoot != null) DeathAnimation.Play(transform, run.ProjectileRoot, DeathAnimation.HeroDuration);
            }
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
            if (Character.Weapon == WeaponType.Mutation) ApplySpecimenLook();
            bool blocking = (flags & PlayerStateMessage.Blocking) != 0;
            body.color = IsRolling ? new Color(0.4f, 0.65f, 1f)
                : IsTaunting || FormBits == 3 ? HeroBuffs.AngryTint(Character.Color)
                : (flags & PlayerStateMessage.Invulnerable) != 0 && Mathf.Repeat(Time.time * 8f, 1f) > 0.5f ? Color.white
                : HeroBuffs.Tint(Character.Color, (flags & PlayerStateMessage.Empowered) != 0,
                    (flags & PlayerStateMessage.Raging) != 0, (flags & PlayerStateMessage.Tired) != 0,
                    (moreFlags & PlayerStateMessage.Ascended) != 0, (moreFlags & PlayerStateMessage.Furious) != 0);
            if (Aim.x < -0.15f) facingLeft = true;
            else if (Aim.x > 0.15f) facingLeft = false;
            body.flipX = facingLeft;
            if (details != null) details.flipX = facingLeft;
            // A teammate in Shadow Veil shows as a faint shadow.
            if (IsVeiled)
            {
                float alpha = 0.3f + 0.08f * Mathf.Sin(Time.time * 6f);
                body.color = new Color(body.color.r, body.color.g, body.color.b, alpha);
                if (details != null) details.color = new Color(details.color.r, details.color.g, details.color.b, alpha);
            }
            else if (details != null && details.color.a < 1f) details.color = new Color(details.color.r, details.color.g, details.color.b, 1f);
            // A taunting Knight shows no shield, just his fury (see the body tint above).
            blocking &= !IsTaunting;
            shield.enabled = blocking;
            if (blocking)
            {
                // Parent scale is 0.65, so offsets are in the hero's local units.
                shield.transform.localPosition = Aim * 0.95f;
                shield.transform.localScale = new Vector3(0.14f, 1.3f, 1f);
                shield.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(Aim.y, Aim.x) * Mathf.Rad2Deg);
            }
        }

        /// <summary>A Specimen teammate wears his current form's sprite, at its size.</summary>
        private void ApplySpecimenLook()
        {
            int key = moreFlags & (PlayerStateMessage.SpecimenFormMask | PlayerStateMessage.SpecimenGrown);
            if (key == lookKey) return;
            lookKey = key;
            int form = FormBits;
            bool grown = (moreFlags & PlayerStateMessage.SpecimenGrown) != 0;
            var shape = form == 1 || form == 3 ? SpecimenForm.Behemoth : form == 2 ? SpecimenForm.Edge : SpecimenForm.Frail;
            body.sprite = HeroSprites.SpecimenBody(shape);
            if (details != null) details.sprite = HeroSprites.SpecimenAccent(shape);
            float scale = form == 3 ? SpecimenAttack.RampageScale : form == 1 ? (grown ? SpecimenAttack.ColossusScale : SpecimenAttack.BehemothScale) : 1f;
            transform.localScale = new Vector3(0.65f * scale, 0.65f * scale, 1f);
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
