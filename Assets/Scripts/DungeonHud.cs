using UnityEngine;

namespace Slopgame
{
    public sealed class DungeonHud : MonoBehaviour
    {
        public static readonly Color DebugColor = new Color(1f, 0.38f, 0.62f);
        public DungeonRun Run { get; set; }
        private float displayedHealth = 1f, displayedBossHealth = 1f, modalFade;
        private bool showTalents;
        // A relic waiting for the player to confirm it should overwrite an equipped one.
        private AbilityType pendingRelic = AbilityType.None;
        private int pendingSlot = -1;
        private Vector2 talentScroll;
        // Co-op restart asks for a second click so a stray press does not throw away the party's run.
        private float restartConfirmUntil;
        private static readonly Rect RestartRect = new Rect(896, 24, 112, 40);
        private static readonly Rect PurseRect = new Rect(900, 262, 356, 260);
        private static readonly Rect ShopRect = new Rect(876, 84, 380, 476);
        private bool ShopOpen => Run.Shop != null && Run.Shop.IsOpen;
        private bool CanRestartCoop => Run.IsNetworked && Run.IsPlaying;

        public bool BlocksPointer(Vector2 screenPosition)
        {
            float scale = Mathf.Min(Screen.width / DungeonUi.Width, Screen.height / DungeonUi.Height);
            Vector2 point = new Vector2(screenPosition.x - (Screen.width - DungeonUi.Width * scale) / 2f,
                Screen.height - screenPosition.y - (Screen.height - DungeonUi.Height * scale) / 2f) / scale;
            return !Run.IsPlaying || new Rect(1020, 24, 236, 40).Contains(point)
                || ((Run.CanSkipRoom || CanRestartCoop) && RestartRect.Contains(point))
                || (showTalents && new Rect(922, 82, 334, 470).Contains(point))
                || (ShopOpen && ShopRect.Contains(point))
                || (!ShopOpen && Run.Player != null && Run.Player.Mechanic is GamblerPurse purse && purse.IsOpen && PurseRect.Contains(point));
        }

        private void Update()
        {
            if (Run.Player == null) return;
            float smooth = 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime);
            displayedHealth = Mathf.Lerp(displayedHealth, Run.Player.Health / (float)Run.Player.MaxHealth, smooth);
            if (Run.Boss != null) displayedBossHealth = Mathf.Lerp(displayedBossHealth, Run.Boss.Enemy.Health / (float)Run.Boss.MaxHealth, smooth);
            else displayedBossHealth = 1f;
            if (!Run.ChoosingArtifact) pendingRelic = AbilityType.None;
            modalFade = Mathf.MoveTowards(modalFade, Run.IsPlaying ? 0f : 1f, Time.unscaledDeltaTime * 5f);
        }

        private void OnGUI()
        {
            if (Run == null || Run.IsInMainMenu || Run.Player == null) return;
            Matrix4x4 previous = DungeonUi.Begin();
            try
            {
                var flash = ScreenFx.FlashColor;
                if (flash.a > 0f) DungeonUi.Panel(new Rect(0, 0, 1280, 720), flash);
                DrawStatus();
                if (Run.Player == null) return;
                if (Run.IsNetworked) DrawTeam();
                DrawHotbar();
                var mechanic = Run.Player.Mechanic;
                // The crystal shop takes the right-hand side while it is open.
                if (Run.IsPlaying && ShopOpen) DrawShop(Run.Shop);
                else if (Run.IsPlaying && mechanic is GamblerPurse purse && purse.IsOpen) DrawPurse(purse);
                if (!ShopOpen && showTalents && Run.IsPlaying) DrawTalents();
                else if (!ShopOpen && Run.IsPlaying && Run.Minimap != null) Run.Minimap.Draw(new Rect(1026, 84, 224, 159));
                if (Run.IsPlaying) return;
                DungeonUi.Panel(new Rect(0, 0, 1280, 720), new Color(0.01f, 0.018f, 0.035f, 0.88f * modalFade));
                if (Run.ChoosingArtifact) DrawArtifacts();
                else if (Run.ChoosingUpgrade) DrawUpgrades();
                else DrawDeath();
            }
            finally { GUI.matrix = previous; }
        }

        private void DrawStatus()
        {
            var player = Run.Player;
            var gambler = player.Weapon as GamblerAttack;
            // The Gambler's coins get their own row, so the panel (and everything under it) grows to fit.
            float coinRow = gambler != null ? 26f : 0f;
            DungeonUi.Panel(new Rect(24, 24, 292, 100 + coinRow), DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(42, 37, 250, 28), Run.SelectedCharacter.DisplayName.ToUpperInvariant(), 22, Run.SelectedCharacter.Color);
            DungeonUi.Label(new Rect(176, 41, 122, 22), $"{player.Crystals.Crystals} CRYSTALS", 14, CrystalPouch.CrystalColor, TextAnchor.UpperRight);
            DungeonUi.Label(new Rect(42, 73, 130, 22), DebugMode.Enabled ? "INFINITE HP" : $"{player.Health} / {player.MaxHealth} HP", 16, DebugMode.Enabled ? DebugColor : (Color?)null);
            DungeonUi.Label(new Rect(176, 73, 120, 22), $"WARD  {player.Powerups.ArmorCharges}", 14, DungeonUi.Muted, TextAnchor.UpperRight);
            if (gambler != null)
            {
                GUI.DrawTexture(new Rect(42, 101, 18, 18), DungeonVisuals.CoinSprite.texture);
                DungeonUi.Label(new Rect(66, 99, 232, 24), $"{gambler.Coins:N0} {(gambler.Coins == 1 ? "COIN" : "COINS")}", 16, GamblerAttack.Gold);
            }
            DungeonUi.Label(new Rect(24, 135 + coinRow, 300, 26), $"ASH  {Run.Progress.Ash}   /   +{Run.RunAshEarned} this run", 16, AbilityCatalog.Gold);
            if (!string.IsNullOrEmpty(Run.Progress.LastError))
                DungeonUi.Label(new Rect(24, 165 + coinRow, 310, 70), Run.Progress.LastError, 14, AbilityCatalog.Gold);
            DungeonUi.Bar(new Rect(42, 103 + coinRow, 256, 6), displayedHealth, Run.SelectedCharacter.Color);
            if (DebugMode.Enabled)
                DungeonUi.Label(new Rect(365, 4, 550, 22), "DEBUG ADMIN MODE  /  INVINCIBLE  ONE-HIT KILLS  NO COOLDOWNS  2X SPEED  /  F1", 12, DebugColor, TextAnchor.MiddleCenter);
            DungeonUi.Label(new Rect(405, 28, 470, 25), Run.InShop ? $"CRYSTAL SHOP  /  GUARDIAN OF FLOOR {Run.Floor + 1:00} AHEAD" : Run.IsBossFloor ? $"FLOOR {Run.Floor:00}  /  BOSS ARENA"
                : $"FLOOR {Run.Floor:00}  /  {Run.Enemies.Count} ENEMIES", 17, Run.InShop ? CrystalPouch.CrystalColor : AbilityCatalog.Gold, TextAnchor.MiddleCenter);
            if (Run.IsPlaying && Time.time < Run.WorldBannerUntil)
            {
                // Fades out over its last second.
                var world = Run.World;
                var accent = FlameMesh.Alpha(world.Accent, Mathf.Clamp01(Run.WorldBannerUntil - Time.time));
                DungeonUi.Label(new Rect(240, 190, 800, 30), $"WORLD {world.Index + 1}", 20, accent, TextAnchor.MiddleCenter);
                DungeonUi.Label(new Rect(240, 220, 800, 50), world.Name, 36, accent, TextAnchor.MiddleCenter);
            }
            DungeonUi.Label(new Rect(365, 59, 550, 40), Run.Objective, 17, DungeonUi.Text, TextAnchor.UpperCenter);
            if (Run.Boss != null && Run.Boss.Enemy.Health > 0)
            {
                // Kept small so it doesn't crowd the arena: name, a thin bar and the current attack.
                DungeonUi.Panel(new Rect(500, 100, 280, 36), DungeonUi.PanelColor);
                DungeonUi.Label(new Rect(506, 102, 268, 16), Run.Boss.Title, 11, AbilityCatalog.Gold, TextAnchor.MiddleCenter);
                bool untouchable = Run.Boss.IsInvulnerable;
                DungeonUi.Bar(new Rect(512, 122, 256, 4), displayedBossHealth, untouchable ? AbilityCatalog.Gold : new Color(0.94f, 0.3f, 0.38f));
                DungeonUi.Label(new Rect(440, 138, 400, 16), Run.Boss.Tell, 11, untouchable || Run.Boss.IsCharging ? AbilityCatalog.Gold : DungeonUi.Muted, TextAnchor.MiddleCenter);
            }
            if (Run.CanSkipRoom && DungeonUi.Button("skipRoom", RestartRect, "Skip room", DebugColor)) Run.DebugSkipRoom();
            if (CanRestartCoop)
            {
                // Guests vote; the host sees how many asked and can restart at once.
                var coop = Run.Coop;
                bool confirming = Time.unscaledTime < restartConfirmUntil;
                string label = confirming ? "Confirm?" : coop.VotedRestart ? $"Voted {coop.RestartVotes}/{coop.RestartVotesNeeded}"
                    : coop.RestartVotes > 0 ? $"Restart {coop.RestartVotes}/{coop.RestartVotesNeeded}" : coop.IsHost ? "Restart" : "Vote restart";
                if (DungeonUi.Button("coopRunRestart", RestartRect, label, confirming || coop.RestartVotes > 0 ? AbilityCatalog.Gold : DungeonUi.Muted, !coop.VotedRestart))
                {
                    if (confirming) { restartConfirmUntil = 0f; coop.VoteRestart(); }
                    else restartConfirmUntil = Time.unscaledTime + 3f;
                }
            }
            if (DungeonUi.Button("talents", new Rect(1020, 24, 112, 40), "Talents", DungeonUi.Teal)) showTalents = !showTalents;
            if (DungeonUi.Button("menu", new Rect(1144, 24, 112, 40), Run.IsNetworked ? "Leave" : "Menu", DungeonUi.Muted)) Run.ShowMainMenu();
        }

        private void DrawHotbar()
        {
            var player = Run.Player;
            // Every class but the Admin has a class mechanic slot, locked until it is bought in the Ash shop.
            bool mechanicSlot = player.ClassWeapon != WeaponType.Shadow;
            float left = mechanicSlot ? 164 : 260;
            Slot(new Rect(left, 596, 180, 78), KeyBindings.Label(GameAction.Special), DungeonUi.SpecialName(player.ClassWeapon), player.Weapon?.HeavyCooldownRemaining ?? 0f,
                DungeonUi.SpecialCooldown(player.ClassWeapon), Run.SelectedCharacter.Color);
            for (int i = 0; i < PlayerAbilities.SlotCount; i++)
            {
                var ability = AbilityCatalog.Get(player.Abilities.Equipped(i));
                Slot(new Rect(left + 192 + i * 192, 596, 180, 78), SlotKey(i), ability == null ? "Boss relic required" : ability.Name,
                    player.Abilities.CooldownRemaining(i), ability?.Cooldown ?? 1f, ability?.Color ?? DungeonUi.Muted, ability == null);
            }
            if (mechanicSlot) MechanicSlot(new Rect(left + 576, 596, 180, 78), player.Mechanic);
            Slot(new Rect(left + (mechanicSlot ? 768 : 576), 596, 180, 78), KeyBindings.Label(GameAction.Dodge), "Dodge", player.DodgeCooldownRemaining, DungeonPlayer.RollCooldown, DungeonUi.Teal);
            if (player.Blessing.BonusDamage > 0)
                DungeonUi.Label(new Rect(440, 505, 400, 24), $"BLESSED  +{player.Blessing.BonusDamage} DAMAGE  /  {player.Blessing.Remaining:0.0}s", 14, AbilityCatalog.Gold, TextAnchor.MiddleCenter);
            string buff = BuffStatus(player.Buffs);
            if (buff != null)
                DungeonUi.Label(new Rect(440, 475, 400, 24), buff, 14, player.Buffs.IsFurious ? HeroBuffs.FuryColor
                    : player.Buffs.IsAscended ? HeroBuffs.AscendColor : player.Buffs.IsRaging ? HeroBuffs.RageColor
                    : player.Buffs.IsTired ? HeroBuffs.TiredColor : HeroBuffs.EmpowerColor, TextAnchor.MiddleCenter);
            string boons = BoonStatus(player.Crystals);
            if (boons != null) DungeonUi.Label(new Rect(390, 445, 500, 24), boons, 14, CrystalPouch.CrystalColor, TextAnchor.MiddleCenter);
            if (player.Charge.IsCharging)
            {
                DungeonUi.Label(new Rect(440, 535, 400, 24), player.ClassWeapon == WeaponType.Hammer
                    ? player.Charge.Amount >= 1f ? "RELEASE TO BLESS ALLIES" : $"CHARGING BLESSING  {player.Charge.Amount:P0}"
                    : player.ClassWeapon == WeaponType.Fists
                    ? player.Charge.Amount >= 1f ? "RELEASE FOR A BARRAGE" : $"WINDING UP BARRAGE  {player.Charge.Amount:P0}"
                    : player.ClassWeapon == WeaponType.Tail && player.Buffs.IsAscended
                    ? player.Charge.Amount >= 1f ? "RELEASE FOR A TAIL WHIP" : $"WINDING UP TAIL WHIP  {player.Charge.Amount:P0}"
                    : player.ClassWeapon == WeaponType.Tail
                    ? player.Charge.Amount >= 1f ? "RELEASE TO STAB VITALS" : $"AIMING FOR VITALS  {player.Charge.Amount:P0}"
                    : player.ClassWeapon == WeaponType.Beam
                    ? player.Charge.Amount >= 1f ? "RELEASE TO FIRE A FULL RAY" : $"FOCUSING RAY  {player.Charge.Amount:P0}"
                    : player.Charge.Amount >= 1f ? "FULL CHARGE  /  RELEASE" : $"CHARGING  {player.Charge.Amount:P0}", 14, AbilityCatalog.Gold, TextAnchor.MiddleCenter);
                DungeonUi.Bar(new Rect(500, 568, 280, 5), player.Charge.Amount, AbilityCatalog.Gold);
            }
            if (player.Weapon is CyborgAttack cannon && cannon.IsCannonCharging)
            {
                float cannonCharge = cannon.IsOverclocked ? 1f : cannon.CannonCharge;
                DungeonUi.Label(new Rect(440, 535, 400, 24), cannonCharge >= 1f ? "RELEASE TO FIRE THE CANNON" : $"CHARGING PLASMA CANNON  {cannonCharge:P0}",
                    14, CyborgAttack.Plasma, TextAnchor.MiddleCenter);
                DungeonUi.Bar(new Rect(500, 568, 280, 5), cannonCharge, CyborgAttack.Plasma);
            }
            string attack = KeyBindings.Label(GameAction.Attack), interact = KeyBindings.Label(GameAction.Interact);
            DungeonUi.Label(new Rect(250, 690, 780, 22), player.ClassWeapon == WeaponType.Hammer
                ? $"{attack}  weak swipe     HOLD / RELEASE {attack}  bless allies     {interact}  interact"
                : player.ClassWeapon == WeaponType.Fists
                ? $"{attack}  jab     HOLD / RELEASE {attack}  punch barrage     {KeyBindings.Label(GameAction.Special)}  empower     {interact}  interact"
                : player.ClassWeapon == WeaponType.Coins
                ? $"{attack}  throw a coin     HOLD / RELEASE {attack}  charged throw     {KeyBindings.Label(GameAction.Special)}  coin volley     {(player.Mechanic != null ? KeyBindings.Label(GameAction.Mechanic) + "  purse     " : "")}{interact}  interact"
                : player.ClassWeapon == WeaponType.Tail
                ? $"{attack}  tail stab     HOLD / RELEASE {attack}  paralysing vital stab     {KeyBindings.Label(GameAction.Special)}  tail sweep     {interact}  interact"
                : player.ClassWeapon == WeaponType.Beam
                ? $"{attack}  plasma ray     HOLD / RELEASE {attack}  charged ray     HOLD / RELEASE {KeyBindings.Label(GameAction.Special)}  plasma cannon     {interact}  interact"
                : $"{KeyBindings.MovementLabel()}  move     HOLD / RELEASE {attack}  charge attack     {interact}  interact", 13, DungeonUi.Muted, TextAnchor.UpperCenter);
        }

        /// <summary>The key bound to relic slot 0 or 1.</summary>
        private static string SlotKey(int slot) => KeyBindings.Label(slot == 0 ? GameAction.AbilityQ : GameAction.AbilityE);

        private static string BuffStatus(HeroBuffs buffs)
        {
            if (buffs == null) return null;
            var parts = new System.Collections.Generic.List<string>();
            if (buffs.IsRaging) parts.Add($"PRIMAL RAGE  {buffs.RageRemaining:0.0}s");
            if (buffs.IsTired) parts.Add($"TIRED  {buffs.TiredRemaining:0.0}s");
            if (buffs.IsEmpowered) parts.Add($"EMPOWERED  {buffs.EmpowerRemaining:0.0}s");
            if (buffs.IsAscended) parts.Add($"ARCHDEMON'S TECHNIQUE  {buffs.AscendRemaining:0.0}s");
            if (buffs.IsFurious) parts.Add($"SUPER ANGRY  {buffs.FuryRemaining:0.0}s");
            if (buffs.JackpotDamage > 0) parts.Add($"JACKPOT  +{buffs.JackpotDamage} DAMAGE  {buffs.JackpotDamageRemaining:0.0}s");
            if (buffs.JackpotSpeed > 1f) parts.Add($"JACKPOT  x{buffs.JackpotSpeed:0.0} SPEED  {buffs.JackpotSpeedRemaining:0.0}s");
            return parts.Count == 0 ? null : string.Join("  /  ", parts);
        }

        /// <summary>The crystal shop's boons while they are active in the boss arena.</summary>
        private static string BoonStatus(CrystalPouch pouch)
        {
            if (pouch == null) return null;
            var parts = new System.Collections.Generic.List<string>();
            if (pouch.BonusDamage > 0) parts.Add($"WHETSTONE  +{pouch.BonusDamage} DAMAGE");
            if (pouch.SpeedMultiplier > 1f) parts.Add($"QUICKSILVER  +{pouch.SpeedMultiplier - 1f:P0} SPEED");
            return parts.Count == 0 ? null : string.Join("  /  ", parts);
        }

        private static void Slot(Rect rect, string key, string name, float cooldown, float total, Color accent, bool locked = false)
        {
            DungeonUi.Panel(rect, DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(rect.x + 14, rect.y + 10, 66, 22), key, 14, accent);
            DungeonUi.Label(new Rect(rect.x + 88, rect.y + 10, 76, 22), locked ? "LOCKED" : cooldown > 0f ? $"{cooldown:0.0}s" : "READY", 13,
                cooldown > 0 ? DungeonUi.Muted : accent, TextAnchor.UpperRight);
            DungeonUi.Label(new Rect(rect.x + 14, rect.y + 37, rect.width - 28, 28), name, locked ? 13 : 16, locked ? DungeonUi.Muted : DungeonUi.Text);
            DungeonUi.Bar(new Rect(rect.x + 12, rect.yMax - 7, rect.width - 24, 3), locked ? 0f : 1f - cooldown / total, accent);
        }

        /// <summary>The class mechanic's slot: its charge or cooldown, or a lock until it is bought.</summary>
        private static void MechanicSlot(Rect rect, ClassMechanic mechanic)
        {
            string key = KeyBindings.Label(GameAction.Mechanic);
            DungeonUi.Panel(rect, DungeonUi.PanelColor);
            if (mechanic == null)
            {
                DungeonUi.Label(new Rect(rect.x + 14, rect.y + 10, 66, 22), key, 14, DungeonUi.Muted);
                DungeonUi.Label(new Rect(rect.x + 88, rect.y + 10, 76, 22), "LOCKED", 13, DungeonUi.Muted, TextAnchor.UpperRight);
                DungeonUi.Label(new Rect(rect.x + 14, rect.y + 37, rect.width - 28, 28), "Class mechanic: Ash shop", 13, DungeonUi.Muted);
                DungeonUi.Bar(new Rect(rect.x + 12, rect.yMax - 7, rect.width - 24, 3), 0f, DungeonUi.Muted);
                return;
            }
            bool ready = mechanic.Readiness >= 1f;
            DungeonUi.Label(new Rect(rect.x + 14, rect.y + 10, 50, 22), key, 14, mechanic.Color);
            DungeonUi.Label(new Rect(rect.x + 60, rect.y + 10, 106, 22), mechanic.Status, 13, ready ? mechanic.Color : DungeonUi.Muted, TextAnchor.UpperRight);
            DungeonUi.Label(new Rect(rect.x + 14, rect.y + 37, rect.width - 28, 28), mechanic.Name, 16, DungeonUi.Text);
            DungeonUi.Bar(new Rect(rect.x + 12, rect.yMax - 7, rect.width - 24, 3), mechanic.Readiness, mechanic.Color);
        }

        /// <summary>The Gambler's purse shop. Play goes on while it is open.</summary>
        private void DrawPurse(GamblerPurse purse)
        {
            var rect = PurseRect;
            DungeonUi.Panel(rect, DungeonUi.Background);
            DungeonUi.Label(new Rect(rect.x + 20, rect.y + 14, 220, 28), "THE PURSE", 22, GamblerAttack.Gold);
            DungeonUi.Label(new Rect(rect.x + 190, rect.y + 18, 146, 24), $"{purse.Coins?.Coins ?? 1:N0} COINS", 16, GamblerAttack.Gold, TextAnchor.UpperRight);
            for (int i = 0; i < GamblerPurse.Offers.Length; i++)
            {
                var offer = GamblerPurse.Offers[i];
                var row = new Rect(rect.x + 16, rect.y + 52 + i * 58, rect.width - 32, 52);
                bool affordable = purse.CanBuy(offer);
                if (DungeonUi.Button("purse" + i, new Rect(row.x, row.y, row.width, 32), $"{offer.Name}  /  {offer.Cost}c", GamblerAttack.Gold, affordable)) purse.Buy(offer);
                DungeonUi.Label(new Rect(row.x + 6, row.y + 34, row.width - 12, 18), offer.Description, 12, DungeonUi.Muted);
            }
            DungeonUi.Label(new Rect(rect.x + 20, rect.yMax - 36, rect.width - 40, 30),
                purse.LastResult ?? $"{KeyBindings.Label(GameAction.Mechanic)} closes the purse. Coins spent here leave your volley.", 13,
                purse.LastResult != null ? GamblerAttack.Gold : DungeonUi.Muted);
        }

        /// <summary>The crystal merchant's wares. Play goes on while it is open.</summary>
        private void DrawShop(CrystalShop shop)
        {
            var rect = ShopRect;
            var pouch = Run.Player.Crystals;
            DungeonUi.Panel(rect, DungeonUi.Background);
            DungeonUi.Label(new Rect(rect.x + 20, rect.y + 14, 220, 28), "CRYSTAL SHOP", 22, CrystalPouch.CrystalColor);
            DungeonUi.Label(new Rect(rect.x + 200, rect.y + 18, 160, 24), $"{pouch.Crystals} CRYSTALS", 16, CrystalPouch.CrystalColor, TextAnchor.UpperRight);
            for (int i = 0; i < shop.Stock.Count; i++)
            {
                var offer = shop.Stock[i];
                var row = new Rect(rect.x + 16, rect.y + 52 + i * 58, rect.width - 32, 52);
                bool maxed = offer.Powerup.HasValue && !Run.Player.Powerups.CanTake(offer.Powerup.Value);
                string price = maxed ? "maxed" : $"{shop.Cost(offer)} crystals";
                if (DungeonUi.Button("shop" + i, new Rect(row.x, row.y, row.width, 32), $"{offer.Name}  /  {price}", offer.Color, shop.CanBuy(offer)))
                    shop.Buy(offer);
                string description = offer.Category == CrystalShop.Category.Relic ? "Relic: " + offer.Description : offer.Description;
                DungeonUi.Label(new Rect(row.x + 6, row.y + 34, row.width - 12, 18), description, 12, DungeonUi.Muted);
            }
            if (shop.RerollsLeft > 0 && DungeonUi.Button("shopReroll", new Rect(rect.x + 16, rect.y + 52 + shop.Stock.Count * 58, rect.width - 32, 28),
                    $"Reroll wares  /  {shop.RerollsLeft} free", new Color(0.85f, 0.7f, 1f), Run.IsPlaying))
                shop.Reroll();
            var boons = new System.Collections.Generic.List<string>();
            if (pouch.PendingWards > 0) boons.Add($"+{pouch.PendingWards} wards");
            if (pouch.PendingDamage > 0) boons.Add($"+{pouch.PendingDamage} damage");
            if (pouch.PendingSwiftness > 0) boons.Add($"+{pouch.PendingSwiftness * CrystalPouch.SwiftnessPerBoon:P0} speed");
            DungeonUi.Label(new Rect(rect.x + 20, rect.y + 404, rect.width - 40, 22),
                boons.Count > 0 ? "For the guardian:  " + string.Join("   ", boons) : "Boons last the boss fight, relics the whole run.", 13, boons.Count > 0 ? AbilityCatalog.Gold : DungeonUi.Muted);
            DungeonUi.Label(new Rect(rect.x + 20, rect.yMax - 44, rect.width - 40, 36),
                shop.LastResult ?? $"{KeyBindings.Label(GameAction.Interact)} or walking away closes the shop. Crystals carry over to later shops.", 13,
                shop.LastResult != null ? CrystalPouch.CrystalColor : DungeonUi.Muted);
        }

        private void DrawTalents()
        {
            DungeonUi.Panel(new Rect(922, 82, 334, 470), DungeonUi.Background);
            DungeonUi.Label(new Rect(946, 103, 288, 30), "YOUR BUILD", 24, DungeonUi.Teal);
            DungeonUi.Label(new Rect(946, 141, 288, 48), $"Physical crit {Run.Player.Powerups.PhysicalCritChance:P0}\nElemental effect {Run.Player.Powerups.CritChance:P0}", 15, DungeonUi.Muted);
            int count = 0;
            foreach (var power in PowerupCatalog.All) if (Run.Player.Powerups.Count(power.Type) > 0) count++;
            talentScroll = GUI.BeginScrollView(new Rect(944, 205, 290, 324), talentScroll, new Rect(0, 0, 268, Mathf.Max(310, count * 45)));
            int row = 0;
            foreach (var power in PowerupCatalog.All)
            {
                int rank = Run.Player.Powerups.Count(power.Type);
                if (rank == 0) continue;
                DungeonUi.Label(new Rect(0, row++ * 45, 260, 42), $"{power.Name}  /  {rank}", 16);
            }
            if (row == 0) DungeonUi.Label(new Rect(0, 0, 260, 80), "Clear floors to earn talents.\nBoss relics unlock active abilities.", 16, DungeonUi.Muted);
            GUI.EndScrollView();
        }

        private static void ModalTitle(string eyebrow, string title, string subtitle)
        {
            DungeonUi.Label(new Rect(160, 112, 960, 25), eyebrow, 14, AbilityCatalog.Gold, TextAnchor.MiddleCenter);
            DungeonUi.Label(new Rect(120, 153, 1040, 64), title, 42, DungeonUi.Text, TextAnchor.MiddleCenter);
            DungeonUi.Label(new Rect(220, 226, 840, 56), subtitle, 18, DungeonUi.Muted, TextAnchor.UpperCenter);
        }

        private static void Card(Rect rect, string tag, string title, string description, Color color, string glyph)
        {
            DungeonUi.Panel(rect, DungeonUi.PanelColor);
            DungeonUi.Panel(new Rect(rect.x + 24, rect.y + 24, 54, 54), new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f));
            DungeonUi.Label(new Rect(rect.x + 24, rect.y + 24, 54, 54), glyph, 28, color, TextAnchor.MiddleCenter);
            DungeonUi.Label(new Rect(rect.x + 94, rect.y + 41, rect.width - 115, 28), tag, 12, color);
            DungeonUi.Label(new Rect(rect.x + 24, rect.y + 102, rect.width - 48, 68), title, 26);
            // Long descriptions scroll instead of being cut off above the card's buttons.
            DungeonUi.ScrollingText("card" + title, new Rect(rect.x + 24, rect.y + 180, rect.width - 40, rect.height - 180 - 80), description, 17, DungeonUi.Muted);
        }

        /// <summary>Teammate health, their name tags in the world, and the fallen-hero banner.</summary>
        private void DrawTeam()
        {
            var team = Run.Coop.RemoteHeroes;
            for (int i = 0; i < team.Count; i++)
            {
                var hero = team[i];
                if (hero == null) continue;
                Rect row = new Rect(24, 200 + i * 44, 292, 38);
                DungeonUi.Panel(row, DungeonUi.PanelColor);
                DungeonUi.Label(new Rect(row.x + 14, row.y + 4, 170, 20), hero.PlayerName, 14, hero.Character.Color);
                DungeonUi.Label(new Rect(row.x + 170, row.y + 4, 108, 20), hero.IsAlive ? $"{hero.Health} / {hero.MaxHealth} HP" : "FALLEN", 13,
                    hero.IsAlive ? DungeonUi.Muted : new Color(1f, 0.4f, 0.4f), TextAnchor.UpperRight);
                DungeonUi.Bar(new Rect(row.x + 14, row.y + 27, row.width - 28, 4), hero.IsAlive ? hero.Health / (float)hero.MaxHealth : 0f, hero.Character.Color);
                if (!hero.IsAlive || Run.View == null) continue;
                Vector3 screen = Run.View.WorldToScreenPoint(hero.transform.position + Vector3.up * 0.75f);
                if (screen.z < 0f) continue;
                float scale = Mathf.Min(Screen.width / DungeonUi.Width, Screen.height / DungeonUi.Height);
                Vector2 point = new Vector2(screen.x - (Screen.width - DungeonUi.Width * scale) / 2f,
                    Screen.height - screen.y - (Screen.height - DungeonUi.Height * scale) / 2f) / scale;
                DungeonUi.Label(new Rect(point.x - 90, point.y - 24, 180, 20), hero.PlayerName, 13, hero.Character.Color, TextAnchor.MiddleCenter);
            }
            if (Run.Player.Health <= 0 && Run.IsPlaying)
            {
                DungeonUi.Panel(new Rect(390, 104, 500, 64), DungeonUi.PanelColor);
                DungeonUi.Label(new Rect(400, 112, 480, 26), "YOU HAVE FALLEN", 20, new Color(1f, 0.45f, 0.45f), TextAnchor.MiddleCenter);
                DungeonUi.Label(new Rect(400, 138, 480, 22), "Your party fights on. You rise again on the next floor.", 14, DungeonUi.Muted, TextAnchor.MiddleCenter);
            }
        }

        private bool DrawWaiting()
        {
            if (!Run.IsNetworked || !Run.Coop.WaitingForTeam) return false;
            int waiting = Run.Coop.WaitingCount;
            ModalTitle("CHOICE MADE", "Waiting for your party", waiting > 0 ? $"{waiting} {(waiting == 1 ? "hero is" : "heroes are")} still choosing." : "The descent continues when everyone has chosen.");
            return true;
        }

        private void DrawUpgrades()
        {
            if (DrawWaiting()) return;
            ModalTitle("FLOOR CLEARED", "A moment of respite", "Choose a talent. Restore 2 HP and descend deeper.");
            for (int i = 0; i < Run.UpgradeChoices.Count; i++)
            {
                var power = Run.UpgradeChoices[i];
                Rect rect = new Rect(142 + i * 340, 300, 316, 330);
                int rank = Run.Player.Powerups.Count(power.Type) + 1;
                Card(rect, $"RANK {rank}  /  {(power.ClassWeapon.HasValue ? "CLASS TALENT" : "TALENT")}", power.Name,
                    power.Description, power.ClassWeapon.HasValue ? Run.SelectedCharacter.Color : DungeonUi.Teal, "+");
                if (DungeonUi.Button("upgrade" + i, new Rect(rect.x + 24, rect.yMax - 60, 268, 40), "Choose talent", DungeonUi.Teal))
                { Run.ChooseUpgrade(i); return; }
            }
        }

        private void DrawArtifacts()
        {
            if (DrawWaiting()) { pendingRelic = AbilityType.None; return; }
            if (pendingRelic != AbilityType.None) { DrawReplaceConfirmation(); return; }
            ModalTitle("GUARDIAN DEFEATED", "An artifact awakens", $"Choose an active ability for your class. {SlotKey(0)} and {SlotKey(1)} hold two abilities. Choosing an equipped ability raises its rank.");
            int index = 0;
            foreach (var ability in AbilityCatalog.All)
            {
                if (ability.ClassWeapon != Run.Player.ClassWeapon) continue;
                Rect rect = new Rect(142 + index++ * 340, 300, 316, 340);
                bool equipped = Run.Player.Abilities.IsEquipped(ability.Type);
                int rank = Run.Player.Abilities.Rank(ability.Type);
                string binding = SlotKey(Run.Player.Abilities.Equipped(0) == ability.Type ? 0 : 1);
                Card(rect, equipped ? $"{binding} EQUIPPED  /  RANK {rank}" : $"ACTIVE  /  {ability.Cooldown:0}s COOLDOWN", ability.Name, ability.Description, ability.Color, ability.Glyph);
                if (equipped)
                {
                    string label = rank >= PlayerAbilities.MaxRank ? "Maximum rank" : $"Upgrade to rank {rank + 1}";
                    if (DungeonUi.Button("artifact" + ability.Type, new Rect(rect.x + 24, rect.yMax - 60, 268, 40), label, ability.Color, rank < PlayerAbilities.MaxRank))
                    { Run.ChooseArtifact(ability.Type, Run.Player.Abilities.Equipped(0) == ability.Type ? 0 : 1); return; }
                }
                else
                {
                    for (int slot = 0; slot < PlayerAbilities.SlotCount; slot++)
                    {
                        bool occupied = Run.Player.Abilities.Equipped(slot) != AbilityType.None;
                        if (!DungeonUi.Button("bind" + ability.Type + slot, new Rect(rect.x + 24 + slot * 140, rect.yMax - 60, 128, 40),
                            $"{(occupied ? "Replace" : "Bind to")} {SlotKey(slot)}", ability.Color)) continue;
                        // Overwriting an equipped relic unequips it, so ask first.
                        if (occupied) { pendingRelic = ability.Type; pendingSlot = slot; }
                        else Run.ChooseArtifact(ability.Type, slot);
                        return;
                    }
                }
            }
            if (index == 0)
                DungeonUi.Label(new Rect(240, 380, 800, 60), $"No relic answers to your class yet. Leave the artifact for {DungeonRun.LeftArtifactCrystals} crystals and descend.", 20, DungeonUi.Muted, TextAnchor.MiddleCenter);
            if (DungeonUi.Button("leaveArtifact", new Rect(470, 661, 340, 35), $"Leave it  /  +{DungeonRun.LeftArtifactCrystals} crystals", CrystalPouch.CrystalColor)) Run.LeaveArtifact();
        }

        private void DrawReplaceConfirmation()
        {
            var incoming = AbilityCatalog.Get(pendingRelic);
            var current = AbilityCatalog.Get(Run.Player.Abilities.Equipped(pendingSlot));
            if (incoming == null || current == null) { pendingRelic = AbilityType.None; return; }
            string key = SlotKey(pendingSlot);
            ModalTitle("REPLACE RELIC?", $"Replace {current.Name}?", $"{incoming.Name} will take the {key} slot and {current.Name} will be unequipped.");
            var panel = new Rect(390, 300, 500, 250);
            DungeonUi.Panel(panel, DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(panel.x + 24, panel.y + 28, 200, 24), $"{key}  NOW", 13, DungeonUi.Muted);
            DungeonUi.Label(new Rect(panel.x + 24, panel.y + 52, 210, 60), current.Name, 22, current.Color);
            DungeonUi.Label(new Rect(panel.x + 250, panel.y + 28, 230, 24), "BECOMES", 13, DungeonUi.Muted);
            DungeonUi.Label(new Rect(panel.x + 250, panel.y + 52, 230, 60), incoming.Name, 22, incoming.Color);
            DungeonUi.Label(new Rect(panel.x + 24, panel.y + 120, 452, 40), "Its rank is kept if a later relic lets you equip it again.", 15, DungeonUi.Muted);
            if (DungeonUi.Button("cancelReplace", new Rect(panel.x + 24, panel.yMax - 64, 214, 44), "Keep " + current.Name, DungeonUi.Muted))
                pendingRelic = AbilityType.None;
            else if (DungeonUi.Button("confirmReplace", new Rect(panel.xMax - 238, panel.yMax - 64, 214, 44), $"Replace {key}", incoming.Color))
            {
                AbilityType type = pendingRelic;
                int slot = pendingSlot;
                pendingRelic = AbilityType.None;
                Run.ChooseArtifact(type, slot);
            }
        }

        private void DrawDeath()
        {
            if (Run.IsNetworked)
            {
                ModalTitle("THE DESCENT ENDS", "The whole party has fallen", $"Floor {Run.Floor}  /  {Run.Kills} enemies defeated\nEvery hero keeps the Ash they earned.");
                if (Run.Coop.IsHost)
                {
                    if (DungeonUi.Button("coopRestart", new Rect(450, 370, 380, 62), "Begin another descent", AbilityCatalog.Gold)) Run.Coop.HostBeginRun();
                    if (DungeonUi.Button("coopLobby", new Rect(450, 450, 380, 50), "Back to the party", DungeonUi.Teal)) Run.Coop.HostReturnToLobby();
                }
                else DungeonUi.Label(new Rect(450, 380, 380, 40), "Waiting for the host…", 18, DungeonUi.Muted, TextAnchor.MiddleCenter);
                if (DungeonUi.Button("coopLeave", new Rect(450, 520, 380, 44), "Leave party", DungeonUi.Muted)) Run.ShowMainMenu();
                return;
            }
            ModalTitle("THE DESCENT ENDS", "The ash takes you", $"Floor {Run.Floor}  /  {Run.Kills} enemies defeated\nYour next descent begins with a clean slate.");
            if (DungeonUi.Button("restart", new Rect(450, 370, 380, 62), "Begin another descent", AbilityCatalog.Gold)) Run.Restart();
            if (DungeonUi.Button("deathMenu", new Rect(450, 450, 380, 50), "Choose another class", DungeonUi.Muted)) Run.ShowMainMenu();
        }
    }
}
