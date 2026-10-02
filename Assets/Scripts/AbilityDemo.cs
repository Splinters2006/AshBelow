using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>Which of a hero's moves the demo stage plays: a guardian artifact, or one of the three every hero has.</summary>
    public enum DemoKind { Ability, Charged, Heavy, Mechanic }

    /// <summary>
    /// The hero select screen's move demo: a little stage far from the dungeon, watched by its own camera, where a
    /// stand-in hero performs the hovered move against stand-in enemies on a loop. Wherever a move is a pure effect
    /// the demo replays the real one (the same ones co-op shows teammates); thrown and planted things, which need a
    /// live run, are stood in for by simple copies built from the same sprites and sparks. Nothing here deals damage
    /// or touches the run. The menu draws <see cref="Texture"/>. Each class's demos live in their own file.
    /// </summary>
    public sealed partial class AbilityDemo : MonoBehaviour
    {
        public const int PixelWidth = 848, PixelHeight = 540;
        public const float Aspect = PixelWidth / (float)PixelHeight;
        // Far enough from any floor that the dungeon's camera never sees the stage, nor this one the dungeon.
        private static readonly Vector2 Stage = new Vector2(1000f, -1000f);
        private static readonly Color Floor = new Color(0.035f, 0.055f, 0.08f), Dust = new Color(0.75f, 0.68f, 0.58f, 0.8f);

        /// <summary>A stand-in enemy: it only wears the tints and marks the real ones would, and slides when struck.</summary>
        internal sealed class Dummy
        {
            public SpriteRenderer Body, Skull, Flame, Blood;
            public Color Tint;
            public float ParalyzedUntil, StunnedUntil, FrozenUntil, ChilledUntil, RootedUntil, FearedUntil, CursedUntil, BurningUntil, BleedingUntil, FlashUntil;
            public Vector2 Velocity;
            public bool Dead;
            public Vector2 Position { get => Body.transform.position; set => Body.transform.position = value; }
            public bool IsHeld => Time.time < ParalyzedUntil || Time.time < StunnedUntil || Time.time < FrozenUntil || Time.time < RootedUntil || Time.time < FearedUntil;
            public void Flash() => FlashUntil = Time.time + 0.12f;
            public void Paralyze(float seconds) { Flash(); ParalyzedUntil = Time.time + seconds; }
            public void Stun(float seconds) { Flash(); StunnedUntil = Time.time + seconds; }
            public void Freeze(float seconds) { Flash(); FrozenUntil = Time.time + seconds; }
            public void Chill(float seconds) { Flash(); ChilledUntil = Time.time + seconds; }
            public void Root(float seconds) { Flash(); RootedUntil = Time.time + seconds; }
            public void Fear(float seconds) { Flash(); FearedUntil = Time.time + seconds; }
            public void Curse(float seconds) => CursedUntil = Time.time + seconds;
            public void Burn(float seconds) => BurningUntil = Time.time + seconds;
            public void Bleed(float seconds) => BleedingUntil = Time.time + seconds;
            /// <summary>Every hold lets go at once.</summary>
            public void Release() => ParalyzedUntil = StunnedUntil = FrozenUntil = RootedUntil = FearedUntil = 0f;
            /// <summary>A blow from <paramref name="from"/>: a white flash, and a shove of about <paramref name="knockback"/> units.</summary>
            public void Hit(Vector2 from, float knockback = 0.3f)
            {
                Flash();
                Vector2 away = Position - from;
                if (knockback > 0f && away.sqrMagnitude > 0.0001f) Velocity = away.normalized * knockback * 8f;
            }
        }

        internal const float DummyRadius = 0.38f;
        private readonly List<Dummy> dummies = new List<Dummy>();
        private Camera view;
        private RenderTexture texture;
        // Effects draw in world space under a root that sits at the origin, like the run's own.
        private Transform fx;
        private SpriteRenderer hero, heroDetails;
        private CharacterDefinition character;
        private AbilityDefinition ability;
        private DemoKind kind;
        private bool showing;
        // How many times the current demo has played: the ones with a roll of the dice show a different outcome each time.
        private int loops;
        private Coroutine script;
        private Color heroTint;
        private float heroTintUntil, heroVeiledUntil;

        public Texture Texture => texture;
        public bool IsShowing => showing;

        /// <summary>Whether this hero's moves have demos to play (the Specimen's three forms have none yet).</summary>
        public static bool Supports(WeaponType weapon) => weapon != WeaponType.Mutation;

        /// <summary>Plays one of <paramref name="character"/>'s moves on a loop (it carries on if it is already the one playing).</summary>
        public void Show(CharacterDefinition character, DemoKind kind, AbilityDefinition ability = null)
        {
            if (character == null || !Supports(character.Weapon) || (kind == DemoKind.Ability && ability == null)) { Hide(); return; }
            if (showing && this.character == character && this.kind == kind && this.ability == ability) return;
            Hide();
            this.character = character;
            this.kind = kind;
            this.ability = ability;
            showing = true;
            loops = 0;
            if (texture == null) texture = new RenderTexture(PixelWidth, PixelHeight, 16) { name = "Ability demo", filterMode = FilterMode.Bilinear };
            if (view == null)
            {
                view = new GameObject("Ability demo camera").AddComponent<Camera>();
                view.orthographic = true;
                view.clearFlags = CameraClearFlags.SolidColor;
                view.backgroundColor = Floor;
                view.targetTexture = texture;
                view.transform.position = new Vector3(Stage.x, Stage.y, -10f);
            }
            view.enabled = true;
            script = StartCoroutine(Loop());
        }

        public void Hide()
        {
            // The loop and every helper it started (bolts in flight and the like).
            StopAllCoroutines();
            script = null;
            showing = false;
            Clear();
            if (view != null) view.enabled = false;
        }

        private void OnDestroy()
        {
            Hide();
            if (view != null) Destroy(view.gameObject);
            if (texture != null) { texture.Release(); Destroy(texture); }
        }

        // ---------------------------------------------------------------- the stage

        private void Clear()
        {
            dummies.Clear();
            heroTintUntil = heroVeiledUntil = 0f;
            if (fx != null) Destroy(fx.gameObject);
            fx = null;
            hero = heroDetails = null;
        }

        /// <summary>A point on the stage, <paramref name="x"/> right and <paramref name="y"/> up of its centre.</summary>
        private static Vector2 At(float x, float y) => Stage + new Vector2(x, y);

        /// <summary>
        /// Sweeps the stage and sets it again: the camera shows <paramref name="halfWidth"/> units either side of the
        /// centre, the hero stands at <paramref name="heroAt"/> and one dummy at each of <paramref name="enemies"/>
        /// (all offsets from the centre).
        /// </summary>
        private void Set(float halfWidth, Vector2 heroAt, params Vector2[] enemies)
        {
            Clear();
            fx = new GameObject("Ability demo stage").transform;
            view.orthographicSize = halfWidth / Aspect;
            // A patch of lit floor, so the effects have ground to land on.
            DungeonVisuals.Create("Demo floor", fx, Stage, new Vector2(80f, 60f), new Color(0.07f, 0.09f, 0.13f), 0);
            hero = DungeonVisuals.Create("Demo hero", fx, Stage + heroAt, Vector2.one * 0.65f, character.Color, 4);
            heroDetails = DungeonVisuals.DecorateHero(hero.transform, character.Weapon, character.Color);
            foreach (var at in enemies) AddDummy(at);
        }

        private Dummy AddDummy(Vector2 at)
        {
            Color tint = WorldCatalog.All[0].BasicTint;
            var body = DungeonVisuals.Create("Demo enemy", fx, Stage + at, Vector2.one * 0.6f, tint, 3);
            body.sprite = DungeonVisuals.EnemySprite(false, false);
            var dummy = new Dummy { Body = body, Tint = tint };
            dummy.Skull = Marker(body.transform, DungeonVisuals.SkullSprite, new Vector2(0f, 1.4f), Vector2.one * 0.42f, HeroBuffs.AscendColor, 10);
            dummy.Flame = Marker(body.transform, DungeonVisuals.FlameSprite, new Vector2(0f, 0.95f), new Vector2(0.28f, 0.4f), new Color(1f, 0.4f, 0.16f), 9);
            dummy.Blood = Marker(body.transform, DungeonVisuals.BloodDropSprite, new Vector2(0f, 0.95f), new Vector2(0.24f, 0.31f), DungeonEnemy.BleedColor, 9);
            dummies.Add(dummy);
            return dummy;
        }

        /// <summary>A teammate standing on the stage: the Knight, for the moves that are about allies.</summary>
        private SpriteRenderer AddAlly(Vector2 at)
        {
            Color color = new Color(0.45f, 0.72f, 1f);
            var ally = DungeonVisuals.Create("Demo ally", fx, Stage + at, Vector2.one * 0.65f, color, 4);
            DungeonVisuals.DecorateHero(ally.transform, WeaponType.Sword, color);
            return ally;
        }

        private static SpriteRenderer Marker(Transform owner, Sprite sprite, Vector2 offset, Vector2 size, Color color, int order)
        {
            var marker = DungeonVisuals.Create("Demo marker", owner, owner.position, size, color, order);
            marker.sprite = sprite;
            marker.transform.localPosition = offset;
            marker.enabled = false;
            return marker;
        }

        private Vector2 HeroAt { get => hero.transform.position; set => hero.transform.position = value; }
        private Dummy Nearest(Vector2 point)
        {
            Dummy best = null;
            foreach (var dummy in dummies)
                if (!dummy.Dead && (best == null || Vector2.Distance(point, dummy.Position) < Vector2.Distance(point, best.Position))) best = dummy;
            return best;
        }

        /// <summary>The living dummies any part of whose body lies in the cone.</summary>
        private List<Dummy> InCone(Vector2 origin, Vector2 aim, float reach, float coneDegrees)
        {
            var found = new List<Dummy>();
            foreach (var dummy in dummies)
                if (!dummy.Dead && SwordAttack.OverlapsCone(dummy.Position - origin, aim, reach, coneDegrees, DummyRadius)) found.Add(dummy);
            return found;
        }

        /// <summary>The living dummies in the rectangle ahead.</summary>
        private List<Dummy> InLane(Vector2 origin, Vector2 aim, float length, float halfWidth)
        {
            var found = new List<Dummy>();
            foreach (var dummy in dummies)
                if (!dummy.Dead && BrawlerAttack.InRectangle(dummy.Position - origin, aim, length, halfWidth, DummyRadius)) found.Add(dummy);
            return found;
        }

        /// <summary>The living dummies within <paramref name="radius"/> of a point.</summary>
        private List<Dummy> Within(Vector2 center, float radius)
        {
            var found = new List<Dummy>();
            foreach (var dummy in dummies)
                if (!dummy.Dead && Vector2.Distance(center, dummy.Position) <= radius + DummyRadius) found.Add(dummy);
            return found;
        }

        private void Face(Vector2 aim)
        {
            if (Mathf.Abs(aim.x) < 0.01f) return;
            hero.flipX = aim.x < 0f;
            if (heroDetails != null) heroDetails.flipX = aim.x < 0f;
        }

        /// <summary>Washes the hero in <paramref name="color"/> for a while, the way a buff tints the real one.</summary>
        private void TintHero(Color color, float seconds) { heroTint = color; heroTintUntil = Time.time + seconds; }

        /// <summary>The hero fades to a shade for a while, the way a veiled or intangible one does.</summary>
        private void Veil(float seconds) => heroVeiledUntil = Time.time + seconds;

        private void Update()
        {
            if (hero != null)
            {
                float alpha = Time.time < heroVeiledUntil ? 0.35f : 1f;
                hero.color = FlameMesh.Alpha(Time.time < heroTintUntil
                    ? Color.Lerp(character.Color, heroTint, 0.4f + 0.2f * Mathf.Sin(Time.time * 9f)) : character.Color, alpha);
                if (heroDetails != null) heroDetails.color = new Color(1f, 1f, 1f, alpha);
            }
            foreach (var dummy in dummies)
            {
                if (dummy.Body == null) continue;
                float now = Time.time;
                dummy.Body.color = now < dummy.FlashUntil ? Color.white
                    : now < dummy.ParalyzedUntil ? DemonessAttack.ParalyzedTint(now)
                    : now < dummy.StunnedUntil ? Color.Lerp(DungeonEnemy.StunnedTint, Color.white, 0.5f + 0.5f * Mathf.Sin(now * 14f))
                    : now < dummy.RootedUntil ? DungeonEnemy.RootedTint
                    : now < dummy.FrozenUntil ? DungeonEnemy.FrozenTint
                    : now < dummy.FearedUntil ? Color.Lerp(dummy.Tint, ReaperAttack.Soul, 0.45f + 0.25f * Mathf.Sin(now * 18f))
                    : now < dummy.ChilledUntil ? AbilityCatalog.Ice : dummy.Tint;
                // A frightened enemy turns its back and shivers on the spot.
                dummy.Body.flipX = now < dummy.FearedUntil;
                dummy.Skull.enabled = now < dummy.CursedUntil;
                dummy.Skull.transform.localPosition = new Vector2(0f, 1.4f + 0.06f * Mathf.Sin(now * 4f));
                dummy.Flame.enabled = now < dummy.BurningUntil;
                dummy.Flame.transform.localScale = new Vector3(0.28f, 0.4f, 1f) * (1f + 0.12f * Mathf.Sin(now * 12f));
                bool bleeding = now < dummy.BleedingUntil;
                dummy.Blood.enabled = bleeding;
                if (bleeding)
                {
                    float fall = Mathf.Clamp01((Mathf.Repeat(now * 1.4f, 1f) - 0.5f) / 0.5f);
                    dummy.Blood.transform.localPosition = new Vector2(dummy.Flame.enabled ? -0.32f : 0f, 0.95f - 0.3f * fall * fall);
                    dummy.Blood.color = FlameMesh.Alpha(DungeonEnemy.BleedColor, 1f - fall * fall);
                }
                if (dummy.Velocity.sqrMagnitude > 0.0001f)
                {
                    dummy.Position += dummy.Velocity * Time.deltaTime;
                    dummy.Velocity = Vector2.MoveTowards(dummy.Velocity, Vector2.zero, 40f * Time.deltaTime);
                }
            }
        }

        // ---------------------------------------------------------------- shared moves

        private static WaitForSeconds Wait(float seconds) => new WaitForSeconds(seconds);

        /// <summary>The flourish every artifact casts with: a ring, a pulse and sparks in its colour.</summary>
        private void Cast()
        {
            if (ability == null) return;
            CombatVfx.Ring(fx, HeroAt, 0.65f, ability.Color);
            HeroVfx.Pulse(fx, HeroAt, 1.1f, ability.Color, 0.35f);
            HeroVfx.Sparks(fx, HeroAt, ability.Color, 10, 3.5f, 0.35f);
        }

        /// <summary>Every dummy that is free to move walks toward <paramref name="goal"/>.</summary>
        private void Advance(Vector2 goal, float speed)
        {
            foreach (var dummy in dummies)
                if (!dummy.Dead && !dummy.IsHeld && Vector2.Distance(dummy.Position, goal) > 0.9f)
                    dummy.Position = Vector2.MoveTowards(dummy.Position, goal, speed * Time.deltaTime);
        }

        /// <summary>The dummies walk at the hero for <paramref name="seconds"/>.</summary>
        private IEnumerator Approach(float seconds, float speed = 1.2f)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) { Advance(HeroAt, speed); yield return null; }
        }

        /// <summary>The hero runs (or dashes) to <paramref name="goal"/>; <paramref name="each"/> is told where she is every frame.</summary>
        private IEnumerator MoveHero(Vector2 goal, float speed, Action<Vector2> each = null)
        {
            Face(goal - HeroAt);
            while (Vector2.Distance(HeroAt, goal) > 0.01f)
            {
                HeroAt = Vector2.MoveTowards(HeroAt, goal, speed * Time.deltaTime);
                each?.Invoke(HeroAt);
                yield return null;
            }
        }

        /// <summary>Carries <paramref name="thing"/> from one spot to another along an arc <paramref name="height"/> high.</summary>
        private static IEnumerator Arc(Transform thing, Vector2 from, Vector2 to, float seconds, float height)
        {
            for (float t = 0f; t < seconds && thing != null; t += Time.deltaTime)
            {
                float progress = t / seconds;
                thing.position = Vector2.Lerp(from, to, progress) + Vector2.up * Mathf.Sin(progress * Mathf.PI) * height;
                yield return null;
            }
            if (thing != null) thing.position = to;
        }

        /// <summary>The dummy drops: it bursts in a puff of its own colour and is gone for the rest of the loop.</summary>
        private void Kill(Dummy dummy)
        {
            if (dummy.Dead) return;
            dummy.Dead = true;
            HeroVfx.Sparks(fx, dummy.Position, dummy.Tint, 12, 4.2f, 0.4f);
            HeroVfx.Pulse(fx, dummy.Position, 0.8f, AbilityCatalog.Gold, 0.3f);
            dummy.Body.gameObject.SetActive(false);
        }

        private SpriteRenderer Sprite(string name, Sprite sprite, Vector2 at, Vector2 size, Color color, int order)
        {
            var renderer = DungeonVisuals.Create(name, fx, at, size, color, order);
            if (sprite != null) renderer.sprite = sprite;
            return renderer;
        }

        /// <summary>A custom drawing that lives for <paramref name="life"/> seconds; <paramref name="draw"/> is given its mesh and age every frame.</summary>
        private DemoMesh Draw(float life, int order, Action<FlameMesh, float> draw) => DemoMesh.Create(fx, life, order, draw);

        /// <summary>
        /// Sends <paramref name="body"/> flying along <paramref name="direction"/>. It stops in the first dummy it touches
        /// (or passes through them all when <paramref name="pierce"/> is set), telling <paramref name="onHit"/> each time.
        /// </summary>
        private DemoShot Shoot(SpriteRenderer body, Vector2 direction, float speed, float range, float radius = 0.12f, bool pierce = false,
            Action<Dummy, Vector2> onHit = null, Action<Vector2> onEnd = null)
        {
            var shot = body.gameObject.AddComponent<DemoShot>();
            shot.Dummies = dummies;
            shot.Direction = direction.normalized;
            shot.Speed = speed;
            shot.Remaining = range;
            shot.Radius = radius;
            shot.Pierce = pierce;
            shot.OnHit = onHit;
            shot.OnEnd = onEnd;
            shot.Aim();
            return shot;
        }

        /// <summary>An arrow like the Archer's: it sticks in the first dummy it meets with a puff of splinters.</summary>
        private DemoShot Arrow(Vector2 from, Vector2 direction, float range, Color tint, Action<Dummy, Vector2> onHit = null)
        {
            var arrow = DungeonVisuals.CreateArrow("Demo arrow", fx, from, 0.6f, tint, 6);
            CombatVfx.Trail(arrow.gameObject, new Color(tint.r, tint.g, tint.b, 0.8f), 0.07f, 0.1f);
            return Shoot(arrow, direction, PlayerProjectile.ArrowSpeed, range, 0.12f, false, (dummy, at) =>
            {
                dummy.Hit(at - direction, 0.3f);
                HeroVfx.Sparks(fx, at, new Color(0.85f, 0.85f, 0.7f), 5, 2.5f, 0.2f, -direction, 140f, 0.7f);
                onHit?.Invoke(dummy, at);
            });
        }

        /// <summary>A flipped coin like the Gambler's.</summary>
        private DemoShot Coin(Vector2 from, Vector2 direction, float range)
        {
            var coin = DungeonVisuals.CreateCoin("Demo coin", fx, from, 0.3f, 6);
            CombatVfx.Trail(coin.gameObject, new Color(GamblerAttack.Gold.r, GamblerAttack.Gold.g, GamblerAttack.Gold.b, 0.8f), 0.14f, 0.16f);
            var shot = Shoot(coin, direction, PlayerProjectile.CoinSpeed, range, 0.14f, false, (dummy, at) =>
            {
                dummy.Hit(at - direction, 0.3f);
                HeroVfx.Sparks(fx, at, GamblerAttack.Gold, 7, 3f, 0.25f, -direction, 140f, 0.8f);
            });
            shot.Turn = false;
            shot.Flip = 14f;
            return shot;
        }

        /// <summary>The wedge a swing will cover, filling as the charge builds (drawn every frame by a charge preview).</summary>
        private static void Cone(FlameMesh mesh, Vector2 origin, Vector2 aim, float reach, float coneDegrees, Color fill)
        {
            float cone = coneDegrees * Mathf.Deg2Rad, start = Mathf.Atan2(aim.y, aim.x) - cone * 0.5f;
            const int Segments = 20;
            for (int i = 0; i < Segments; i++)
                mesh.Triangle(origin, origin + FlameMesh.Polar(start + cone * i / Segments, reach),
                    origin + FlameMesh.Polar(start + cone * (i + 1) / Segments, reach), fill, fill, fill);
        }

        /// <summary>The lane a punch or stab will cover.</summary>
        private static void Lane(FlameMesh mesh, Vector2 origin, Vector2 aim, float length, float halfWidth, Color fill)
            => mesh.Bar(origin, aim.normalized, length, halfWidth * 2f, fill, fill);

        /// <summary>
        /// The hero holds a charge for <paramref name="seconds"/>: <paramref name="preview"/> draws what the attack will
        /// cover as the charge (0 to 1) fills, the way the real hit previews do.
        /// </summary>
        private IEnumerator Charge(float seconds, Action<FlameMesh, float> preview)
        {
            var mesh = Draw(seconds, 5, (m, age) => preview(m, Mathf.Clamp01(age / (seconds * 0.85f))));
            float nextMote = 0f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (t >= nextMote) { nextMote = t + 0.12f; HeroVfx.Motes(fx, HeroAt, 0.45f, character.Color, 2, 0.4f); }
                yield return null;
            }
            if (mesh != null) Destroy(mesh.gameObject);
        }

        /// <summary>How a charge preview's colour fills: faint in the hero's colour, then pulsing gold once full.</summary>
        private Color ChargeFill(float charge, Color color) => charge >= 1f
            ? new Color(AbilityCatalog.Gold.r, AbilityCatalog.Gold.g, AbilityCatalog.Gold.b, 0.3f + 0.1f * Mathf.Sin(Time.time * 14f))
            : new Color(color.r, color.g, color.b, Mathf.Lerp(0.1f, 0.28f, charge));

        // ---------------------------------------------------------------- the loop

        private IEnumerator Loop()
        {
            while (true)
            {
                IEnumerator demo = character.Weapon switch
                {
                    WeaponType.Sword => Knight(),
                    WeaponType.Bow => Archer(),
                    WeaponType.Staff => Wizard(),
                    WeaponType.Daggers => Assassin(),
                    WeaponType.Hammer => Paladin(),
                    WeaponType.Fists => Brawler(),
                    WeaponType.Tail => Demoness(),
                    WeaponType.Coins => Gambler(),
                    WeaponType.Beam => Augment(),
                    WeaponType.Scythe => Reaper(),
                    WeaponType.Katana => Samurai(),
                    _ => null
                };
                if (demo == null) { yield return Wait(1f); continue; }
                yield return demo;
                loops++;
                // A breath between loops, so the last frame of one does not run straight into the next.
                yield return Wait(0.5f);
            }
        }
    }

    /// <summary>A demo's custom drawing: one mesh redrawn every frame by a callback until its time is up.</summary>
    public sealed class DemoMesh : MonoBehaviour
    {
        private FlameMesh mesh;
        private Action<FlameMesh, float> draw;
        private float age, life;

        public static DemoMesh Create(Transform root, float life, int order, Action<FlameMesh, float> draw)
        {
            if (root == null) return null;
            var effect = new GameObject("Demo drawing").AddComponent<DemoMesh>();
            // The owner stays at the origin: FlameMesh draws in its local space.
            effect.transform.SetParent(root, false);
            effect.mesh = new FlameMesh(effect.gameObject, order);
            effect.life = life;
            effect.draw = draw;
            effect.Redraw();
            return effect;
        }

        private void Redraw()
        {
            mesh.Begin();
            draw(mesh, age);
            mesh.Commit();
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= life) { Destroy(gameObject); return; }
            Redraw();
        }

        private void OnDestroy() => mesh?.Release();
    }

    /// <summary>A demo's stand-in projectile: it flies straight (or homes on a dummy), and reports what it runs into.</summary>
    public sealed class DemoShot : MonoBehaviour
    {
        internal List<AbilityDemo.Dummy> Dummies;
        internal AbilityDemo.Dummy Target;
        internal Action<AbilityDemo.Dummy, Vector2> OnHit;
        internal Action<Vector2> OnEnd;
        public Vector2 Direction;
        public float Speed, Remaining, Radius, Spin, Flip, TurnRate;
        /// <summary>Whether the sprite points along its flight.</summary>
        public bool Pierce, Turn = true;
        private readonly HashSet<AbilityDemo.Dummy> struck = new HashSet<AbilityDemo.Dummy>();
        private Vector3 scale;
        private float age;
        private bool ended;

        public void Aim()
        {
            scale = transform.localScale;
            if (Turn) transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg);
        }

        /// <summary>Lets the shot pass through <paramref name="dummy"/> without striking it.</summary>
        internal void Skip(AbilityDemo.Dummy dummy) => struck.Add(dummy);

        private void Update()
        {
            if (ended) return;
            age += Time.deltaTime;
            if (Target != null && !Target.Dead && Target.Body != null && TurnRate > 0f)
            {
                Vector2 wanted = (Target.Position - (Vector2)transform.position).normalized;
                float angle = Mathf.MoveTowardsAngle(Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg,
                    Mathf.Atan2(wanted.y, wanted.x) * Mathf.Rad2Deg, TurnRate * Time.deltaTime);
                Direction = FlameMesh.Polar(angle * Mathf.Deg2Rad, 1f);
            }
            float step = Mathf.Min(Speed * Time.deltaTime, Remaining);
            transform.position += (Vector3)(Direction * step);
            Remaining -= step;
            if (Turn) transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg);
            else if (Spin != 0f) transform.rotation = Quaternion.Euler(0f, 0f, age * Spin);
            // A flipped coin shows its face and its edge in turn.
            if (Flip > 0f) transform.localScale = new Vector3(scale.x, scale.y * (0.15f + 0.85f * Mathf.Abs(Mathf.Cos(age * Flip))), scale.z);
            Vector2 at = transform.position;
            if (Dummies != null)
                foreach (var dummy in Dummies)
                {
                    if (dummy.Dead || dummy.Body == null || struck.Contains(dummy) || Vector2.Distance(at, dummy.Position) > Radius + AbilityDemo.DummyRadius) continue;
                    struck.Add(dummy);
                    OnHit?.Invoke(dummy, at);
                    if (!Pierce) { End(); return; }
                }
            if (Remaining <= 0f) End();
        }

        public void End()
        {
            if (ended) return;
            ended = true;
            OnEnd?.Invoke(transform.position);
            Destroy(gameObject);
        }
    }
}
