using UnityEngine;

namespace Slopgame
{
    public sealed class DungeonHud : MonoBehaviour
    {
        public static readonly Color DebugColor = new Color(1f, 0.38f, 0.62f);
        public DungeonRun Run { get; set; }
        private float displayedHealth = 1f, displayedBossHealth = 1f, modalFade;
        private bool showTalents, showAbilitiesTab;
        // The settings page behind the cog wheel; a solo descent pauses while it is open.
        private readonly SettingsMenu settingsMenu = new SettingsMenu();
        private bool showSettings, pausedForSettings;
        public bool SettingsOpen => showSettings;
        /// <summary>True while the settings page or the leave-the-descent confirmation is up: the hero ignores the keys.</summary>
        public bool CapturesInput => showSettings || confirmingMenu;
        // Going back to the main menu (or leaving the party) mid-descent asks first; a solo descent waits while it does.
        private bool confirmingMenu, pausedForConfirm;
        private static readonly Rect CogRect = new Rect(1216, 24, 40, 40);
        // Each group of the HUD over the floor is pinned to its own edge of the screen and pushed out toward it, so
        // the middle of the screen stays clear (see DungeonUi.AnchorMatrix).
        private static readonly Vector2 TopLeftNudge = new Vector2(-12f, -12f), TopNudge = new Vector2(0f, -12f),
            TopRightNudge = new Vector2(12f, -12f), BottomNudge = new Vector2(0f, 14f);
        private static void Pin(Vector2 anchor, Vector2 nudge = default) => DungeonUi.Anchor(GameSettings.HudScale, anchor, nudge);
        private static void PinTopLeft() => Pin(DungeonUi.TopLeft, TopLeftNudge);
        private static void PinTop() => Pin(DungeonUi.TopCenter, TopNudge);
        private static void PinTopRight() => Pin(DungeonUi.TopRight, TopRightNudge);
        private static void PinBottom() => Pin(DungeonUi.BottomCenter, BottomNudge);
        private static void PinCenter() => Pin(DungeonUi.Center);
        private static readonly Rect CornerButtonsRect = new Rect(1020, 24, 236, 40), TalentsRect = new Rect(922, 82, 334, 470);
        private Vector2 talentScroll, abilityScroll;
        // Co-op restart asks for a second click so a stray press does not throw away the party's run.
        private float restartConfirmUntil;
        // The world-cleared screen's travel map (the host's, in co-op) and whether it is showing.
        private readonly WorldMap travelMap = new WorldMap();
        private bool showTravelMap;
        // A talent or ability pick waits for a second, deliberate click so a stray press can't learn the wrong thing.
        private int pendingUpgrade = -1;
        private AbilityType pendingAbility = AbilityType.None;
        private static readonly Rect RestartRect = new Rect(896, 24, 112, 40);
        private static readonly Rect PurseRect = new Rect(900, 262, 356, 0);
        /// <summary>The purse panel: a header, the wares as a two-column grid of tiles, the safe's box when it has one, and a footer.</summary>
        private static Rect PurseArea(GamblerPurse purse)
            => new Rect(PurseRect.x, PurseRect.y, PurseRect.width, PurseHeader + PurseTileRows(purse) * PurseTileStep
                + (purse.HasSafe ? PurseSafeHeight + PurseTileGap : 0) + PurseFooter);
        private const float PurseHeader = 52, PurseFooter = 40, PurseTileHeight = 98, PurseTileGap = 8, PurseTileStep = PurseTileHeight + PurseTileGap,
            PursePadding = 16, PurseSafeHeight = 70;
        private static int PurseTileRows(GamblerPurse purse) => (purse.OnSale.Count + 1) / 2;
        private static readonly Rect ShopRect = new Rect(836, 84, 420, 476);
        private bool ShopOpen => Run.Shop != null && Run.Shop.IsOpen;
        /// <summary>The run just ended with the hero's fall still playing: the game-over screen waits for it.</summary>
        private bool DeathPending => !Run.IsPlaying && !Run.ChoosingArtifact && !Run.ChoosingUpgrade && !Run.WorldComplete
            && Run.Player != null && Run.Player.IsFalling;
        private bool CanRestartCoop => Run.IsNetworked && Run.IsPlaying;

        public bool BlocksPointer(Vector2 screenPosition)
        {
            // Everything clickable over the floor sits in the top-right group.
            Vector2 point = DungeonUi.ScreenToCanvas(screenPosition, GameSettings.HudScale, DungeonUi.TopRight, TopRightNudge);
            return !Run.IsPlaying || showSettings || confirmingMenu || CornerButtonsRect.Contains(point)
                || ((Run.CanSkipRoom || CanRestartCoop) && RestartRect.Contains(point))
                || (showTalents && TalentsRect.Contains(point))
                || (ShopOpen && ShopPanelRect(Run.Shop).Contains(point))
                || (!ShopOpen && Run.Player != null && Run.Player.Mechanic is GamblerPurse purse && purse.IsOpen && PurseArea(purse).Contains(point));
        }

        private void Update()
        {
            if (showSettings && (Run.IsInMainMenu || Run.Player == null)) { showSettings = false; settingsMenu.Cancel(); }
            if (confirmingMenu && (Run.IsInMainMenu || Run.Player == null)) { confirmingMenu = false; pausedForConfirm = false; }
            if (Run.Player == null) return;
            float smooth = 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime);
            displayedHealth = Mathf.Lerp(displayedHealth, Run.Player.Health / (float)Run.Player.MaxHealth, smooth);
            if (Run.Boss != null) displayedBossHealth = Mathf.Lerp(displayedBossHealth, Run.Boss.Enemy.Health / (float)Run.Boss.MaxHealth, smooth);
            else displayedBossHealth = 1f;
            if (!Run.WorldComplete) showTravelMap = false;
            if (!Run.ChoosingUpgrade || pendingUpgrade >= Run.UpgradeChoices.Count) pendingUpgrade = -1;
            if (!Run.ChoosingArtifact || Run.ArrangingAbilities) pendingAbility = AbilityType.None;
            modalFade = Mathf.MoveTowards(modalFade, Run.IsPlaying || DeathPending ? 0f : 1f, Time.unscaledDeltaTime * 5f);
        }

        private void OnGUI()
        {
            if (Run == null || Run.IsInMainMenu || Run.Player == null) return;
            Matrix4x4 previous = GUI.matrix;
            Color previousColor = GUI.color;
            try
            {
                PinCenter();
                // The settings page covers everything else, so nothing under it can be clicked.
                if (showSettings) { DrawSettings(); return; }
                var flash = ScreenFx.FlashColor;
                if (flash.a > 0f) DungeonUi.Panel(new Rect(-2000, -2000, 6000, 6000), flash);
                // The HUD over the floor is drawn at the player's opacity; the settings page and the screens below stay solid.
                GUI.color = new Color(1f, 1f, 1f, GameSettings.HudOpacity);
                DrawStatus();
                if (Run.Player == null) return;
                if (Run.IsNetworked) DrawTeam();
                DrawHotbar();
                var mechanic = Run.Player.Mechanic;
                PinTopRight();
                // The crystal shop takes the right-hand side while it is open. Nothing under the menu confirmation can be clicked.
                if (Run.IsPlaying && ShopOpen && !confirmingMenu) DrawShop(Run.Shop);
                else if (Run.IsPlaying && mechanic is GamblerPurse purse && purse.IsOpen && !confirmingMenu) DrawPurse(purse);
                if (!ShopOpen && showTalents && Run.IsPlaying && !confirmingMenu) DrawTalents();
                else if (!ShopOpen && Run.IsPlaying && Run.Minimap != null) Run.Minimap.Draw(new Rect(1026, 84, 224, 159));
                if (Run.IsPlaying || DeathPending)
                {
                    if (confirmingMenu) DrawMenuConfirm();
                    else DrawCog();
                    return;
                }
                GUI.color = previousColor;
                PinCenter();
                DungeonUi.Panel(new Rect(-2000, -2000, 6000, 6000), new Color(0.01f, 0.018f, 0.035f, 0.88f * modalFade));
                if (confirmingMenu) { DrawMenuConfirm(); return; }
                // Drawn over the dimmed floor, so the cog is there on every pick, world-cleared and game-over screen too.
                DrawCog();
                PinCenter();
                if (Run.ChoosingArtifact) DrawArtifacts();
                else if (Run.ChoosingUpgrade) DrawUpgrades();
                else if (Run.WorldComplete) DrawWorldComplete();
                else DrawDeath();
            }
            finally { GUI.color = previousColor; GUI.matrix = previous; }
        }

        /// <summary>Asks before a descent in progress is left for the main menu (solo, it waits while asked).</summary>
        private void AskToLeave()
        {
            confirmingMenu = true;
            pausedForConfirm = Run.IsPlaying && !Run.IsNetworked && Time.timeScale > 0f;
            if (pausedForConfirm) Time.timeScale = 0f;
        }

        private void CloseMenuConfirm()
        {
            confirmingMenu = false;
            if (pausedForConfirm && Run.IsPlaying) Time.timeScale = 1f;
            pausedForConfirm = false;
        }

        /// <summary>The leave-the-descent confirmation, over everything else.</summary>
        private void DrawMenuConfirm()
        {
            PinCenter();
            GUI.color = Color.white;
            bool party = Run.IsNetworked;
            bool saved = Run.CanSuspend;
            string question = party ? "Leave the party?" : "Return to the main menu?";
            string detail = party
                ? "You leave the descent and the party goes on without you. The Ash you earned is kept."
                : saved && Run.WorldComplete ? "Your descent is saved to your account. Continue it from the main menu, on this PC or another, in the next world."
                : saved ? "Your descent is saved to your account. Continue it from the main menu, on this PC or another: this floor starts over."
                : Run.WorldComplete ? "This descent ends here. The Ash you earned is kept. Sign in to an account to save descents instead."
                : "This descent ends here: your talents, abilities and crystals are lost. The Ash you earned is kept. Sign in to an account to save descents instead.";
            var answer = ConfirmPick(question, detail, AbilityCatalog.Gold, party ? "Leave party" : saved ? "Save and leave" : "Main menu");
            if (answer == true) { CloseMenuConfirm(); Run.ShowMainMenu(); }
            else if (answer == false) CloseMenuConfirm();
        }

        private void DrawCog()
        {
            PinTopRight();
            if (!DungeonUi.CogButton("hudCog", CogRect)) return;
            showSettings = true;
            // A solo descent stands still while the settings are open; a co-op one cannot.
            pausedForSettings = Run.IsPlaying && !Run.IsNetworked;
            if (pausedForSettings) Time.timeScale = 0f;
        }

        private void CloseSettings()
        {
            settingsMenu.Cancel();
            showSettings = false;
            if (pausedForSettings && Run.IsPlaying) Time.timeScale = 1f;
            pausedForSettings = false;
        }

        /// <summary>The same settings page as the main menu's, over the descent.</summary>
        private void DrawSettings()
        {
            DungeonUi.Panel(new Rect(-2000, -2000, 6000, 6000), DungeonUi.Background);
            DungeonUi.Label(new Rect(70, 52, 700, 25), pausedForSettings ? "THE DESCENT IS PAUSED" : Run.IsNetworked && Run.IsPlaying ? "THE DESCENT GOES ON AROUND YOU" : "THE DESCENT WAITS", 14, AbilityCatalog.Gold);
            DungeonUi.Label(new Rect(65, 80, 1100, 56), "SETTINGS", 40);
            DungeonUi.Label(new Rect(70, 138, 1100, 30), "Resize the menus and HUD, fade the HUD, switch autofire on or off for each hero and rebind every action. Changes save instantly.", 17, DungeonUi.Muted);
            settingsMenu.Draw();
            if (DungeonUi.Button("hudSettingsBack", new Rect(70, 598, 268, 48), "Back", DungeonUi.Muted)) CloseSettings();
            if (DungeonUi.Button("hudSettingsReset", new Rect(860, 598, 350, 48), "Reset to defaults", DungeonUi.Teal)) settingsMenu.ResetToDefaults();
            PinTopRight();
            if (DungeonUi.CogButton("hudCogClose", CogRect, true)) CloseSettings();
        }

        private void DrawStatus()
        {
            var player = Run.Player;
            var gambler = player.Weapon as GamblerAttack;
            // The Gambler's coins get their own row, so the panel (and everything under it) grows to fit.
            var reaper = player.Weapon as ReaperAttack;
            var specimen = player.Weapon as SpecimenAttack;
            float coinRow = gambler != null || reaper != null || specimen != null ? 26f : 0f;
            PinTopLeft();
            DungeonUi.Panel(new Rect(24, 24, 292, 100 + coinRow), DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(42, 37, 250, 28), Run.SelectedCharacter.DisplayName.ToUpperInvariant(), 22, Run.SelectedCharacter.Color);
            DungeonUi.Label(new Rect(176, 41, 122, 22), $"{player.Crystals.Crystals} CRYSTALS", 14, CrystalPouch.CrystalColor, TextAnchor.UpperRight);
            DungeonUi.Label(new Rect(42, 73, 130, 22), DebugMode.Enabled ? "INFINITE HP" : $"{player.Health} / {player.MaxHealth} HP", 16, DebugMode.Enabled ? DebugColor : (Color?)null);
            DungeonUi.Label(new Rect(176, 73, 120, 22), $"WARD  {player.Powerups.ArmorCharges}", 14, DungeonUi.Muted, TextAnchor.UpperRight);
            if (gambler != null)
            {
                GUI.DrawTexture(new Rect(42, 101, 18, 18), DungeonVisuals.CoinSprite.texture);
                DungeonUi.Label(new Rect(66, 99, 232, 24), $"{gambler.Coins:N0} {(gambler.Coins == 1 ? "COIN" : "COINS")}" + (gambler.Cards > 0 ? $"  /  {gambler.Cards} CARDS" : ""), 16, GamblerAttack.Gold);
            }
            if (player.Weapon is CyborgAttack augment && player.Permanent.HasPassive(WeaponType.Beam))
            {
                GUI.DrawTexture(new Rect(42, 101, 18, 18), ScrapPickup.ScrapSprite.texture);
                DungeonUi.Label(new Rect(66, 99, 232, 24), $"{augment.Scrap} / {ScrapPickup.PerHeal} SCRAP", 16, ScrapPickup.Metal);
            }
            if (reaper != null)
            {
                GUI.DrawTexture(new Rect(42, 101, 18, 18), SoulWisp.Sprite.texture);
                // Souls just spent flash red beside the count, which blinks red with them.
                float spent = 1f - Mathf.Clamp01((Time.time - reaper.LastSpentAt) / ReaperAttack.SpentNoticeTime);
                bool blink = spent > 0f && Mathf.Repeat(Time.time * 8f, 1f) < 0.5f && spent > 0.6f;
                DungeonUi.Label(new Rect(66, 99, 232, 24), $"{reaper.Souls} {(reaper.Souls == 1 ? "SOUL" : "SOULS")}", 16, blink ? DungeonPlayer.HurtColor : ReaperAttack.Soul);
                if (spent > 0f)
                    DungeonUi.Label(new Rect(176, 97, 122, 26), $"-{reaper.LastSpent} {(reaper.LastSpent == 1 ? "SOUL" : "SOULS")}", 18, FlameMesh.Alpha(DungeonPlayer.HurtColor, Mathf.Clamp01(spent * 2f)), TextAnchor.UpperRight);
            }
            if (specimen != null) DrawSpecimenRow(specimen);
            DungeonUi.Label(new Rect(24, 135 + coinRow, 300, 26), $"ASH  {Run.Progress.Ash}   /   +{Run.RunAshEarned} this run", 16, AbilityCatalog.Gold);
            if (!string.IsNullOrEmpty(Run.Progress.LastError))
                DungeonUi.Label(new Rect(24, 165 + coinRow, 310, 70), Run.Progress.LastError, 14, AbilityCatalog.Gold);
            DungeonUi.Bar(new Rect(42, 103 + coinRow, 256, 6), displayedHealth, Run.SelectedCharacter.Color);
            PinTop();
            if (DebugMode.Enabled)
                DungeonUi.Label(new Rect(365, 4, 550, 22), "DEBUG ADMIN MODE  /  INVINCIBLE  ONE-HIT KILLS  NO COOLDOWNS  2X SPEED  /  F1", 12, DebugColor, TextAnchor.MiddleCenter);
            // Wave worlds count waves (1-15) rather than floors.
            string where = Run.World.IsWaveWorld ? $"LEVEL {Run.LevelNumber:00}" : $"FLOOR {Run.Floor:00}";
            string guardian = Run.World.IsWaveWorld ? $"LEVEL {Run.LevelNumber + 1:00}" : $"FLOOR {Run.Floor + 1:00}";
            if (Run.IsWaveFloor && !Run.WavesPending) where += $"  /  WAVE {Run.CurrentWave}/{Run.WavesThisLevel}";
            DungeonUi.Label(new Rect(405, 28, 470, 25), Run.InShop ? $"CRYSTAL SHOP  /  GUARDIAN OF {guardian} AHEAD" : Run.IsBossFloor ? $"{where}  /  BOSS ARENA"
                : $"{where}  /  {Run.Enemies.Count} ENEMIES", 17, Run.InShop ? CrystalPouch.CrystalColor : AbilityCatalog.Gold, TextAnchor.MiddleCenter);
            if (Run.IsPlaying && Time.time < Run.WorldBannerUntil)
            {
                // Fades out over its last second.
                var world = Run.World;
                var accent = FlameMesh.Alpha(world.Accent, Mathf.Clamp01(Run.WorldBannerUntil - Time.time));
                DungeonUi.Label(new Rect(240, 190, 800, 30), world.IsWaveWorld ? $"WORLD {world.Index + 1}  /  WAVES" : $"WORLD {world.Index + 1}", 20, accent, TextAnchor.MiddleCenter);
                DungeonUi.Label(new Rect(240, 220, 800, 50), world.Name, 36, accent, TextAnchor.MiddleCenter);
            }
            else if (Run.IsPlaying && Run.IsWaveFloor && Time.time < Run.WaveBannerUntil)
            {
                // A new wave has arrived; fades out over its last second.
                var accent = FlameMesh.Alpha(Run.World.Accent, Mathf.Clamp01(Run.WaveBannerUntil - Time.time));
                DungeonUi.Label(new Rect(240, 196, 800, 50), $"WAVE {Run.CurrentWave} / {Run.WavesThisLevel}", 36, accent, TextAnchor.MiddleCenter);
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
            PinTopRight();
            // Nothing up here can be clicked while the menu confirmation is open.
            if (confirmingMenu) return;
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
            // Build and Menu share the corner with the settings cog (see DrawCog).
            if (DungeonUi.Button("talents", new Rect(1020, 24, 92, 40), "Build", DungeonUi.Teal)) showTalents = !showTalents;
            if (DungeonUi.Button("menu", new Rect(1118, 24, 92, 40), Run.IsNetworked ? "Leave" : "Menu", DungeonUi.Muted)) AskToLeave();
        }

        private void DrawHotbar()
        {
            PinBottom();
            var player = Run.Player;
            // Every class has a class mechanic slot, locked until it is bought in the Ash shop.
            const bool mechanicSlot = true;
            float left = 164;
            var specimen = player.Weapon as SpecimenAttack;
            Slot(new Rect(left, 596, 180, 78), KeyBindings.Label(GameAction.Special), specimen != null ? specimen.SpecialName : DungeonUi.SpecialName(player.ClassWeapon),
                player.Weapon?.HeavyCooldownRemaining ?? 0f, specimen != null ? specimen.SpecialCooldownTime : DungeonUi.SpecialCooldown(player.ClassWeapon), Run.SelectedCharacter.Color);
            for (int i = 0; i < PlayerAbilities.SlotCount; i++)
            {
                var ability = AbilityCatalog.Get(player.Abilities.Equipped(i));
                // The Specimen's path abilities sleep until he is in their form.
                if (ability != null && specimen != null && !specimen.CanUseAbility(ability.Type))
                {
                    Slot(new Rect(left + 192 + i * 192, 596, 180, 78), SlotKey(i), $"{ability.Name}  /  {SpecimenFormTag(ability.Type).ToLowerInvariant()}",
                        0f, 1f, ability.Color, true, "ASLEEP");
                    continue;
                }
                Slot(new Rect(left + 192 + i * 192, 596, 180, 78), SlotKey(i), ability == null ? "Boss relic required" : ability.Name,
                    player.Abilities.CooldownRemaining(i), ability?.Cooldown ?? 1f, ability?.Color ?? DungeonUi.Muted, ability == null);
            }
            if (mechanicSlot) MechanicSlot(new Rect(left + 576, 596, 180, 78), player.Mechanic);
            if (player.Powerups.Count(PowerupType.Zeal) > 0) ZealMeter(new Rect(left, 562, 180, 28), player.Powerups.Zeal);
            Slot(new Rect(left + (mechanicSlot ? 768 : 576), 596, 180, 78), KeyBindings.Label(GameAction.Dodge), "Dodge", player.DodgeCooldownRemaining, DungeonPlayer.RollCooldown, DungeonUi.Teal);
            if (player.Blessing.BonusDamage > 0)
                DungeonUi.Label(new Rect(440, 505, 400, 24), $"BLESSED  +{player.Blessing.BonusDamage} DAMAGE  /  {player.Blessing.Remaining:0.0}s", 14, AbilityCatalog.Gold, TextAnchor.MiddleCenter);
            string buff = BuffStatus(player.Buffs);
            if (buff != null)
                DungeonUi.Label(new Rect(440, 475, 400, 24), buff, 14, player.Buffs.IsIncarnate ? HeroBuffs.IncarnateColor : player.Buffs.IsFurious ? HeroBuffs.FuryColor
                    : player.Buffs.IsAscended || player.Buffs.IsRuneEmpowered ? HeroBuffs.AscendColor : player.Buffs.IsRaging ? HeroBuffs.RageColor
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
                    : player.ClassWeapon == WeaponType.Scythe
                    ? player.Charge.Amount >= 1f ? "RELEASE TO HARVEST SOULS" : $"RAISING THE SCYTHE  {player.Charge.Amount:P0}"
                    : player.ClassWeapon == WeaponType.Katana
                    ? player.Charge.Amount >= 1f ? "RELEASE FOR A FLURRY" : $"GATHERING A FLURRY  {player.Charge.Amount:P0}"
                    : specimen != null && specimen.Form == SpecimenForm.Behemoth && !specimen.IsRampaging
                    ? player.Charge.Amount >= 1f ? "RELEASE FOR AN AXE KICK" : $"RAISING HIS HEEL  {player.Charge.Amount:P0}"
                    : specimen != null && specimen.Form == SpecimenForm.Edge
                    ? player.Charge.Amount >= 1f ? "RELEASE FOR A CHAIN CYCLONE" : $"WHIRLING THE CHAIN  {player.Charge.Amount:P0}"
                    : specimen != null && specimen.Form == SpecimenForm.Frail
                    ? player.Charge.Amount >= 1f ? "RELEASE FOR A SNAP KICK" : $"WINDING UP A KICK  {player.Charge.Amount:P0}"
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
            if (player.Weapon is ReaperAttack skulls && skulls.IsSkullCharging)
            {
                int tier = ReaperAttack.SkullTier(skulls.SkullCharge, DebugMode.Enabled ? 3 : skulls.Purse);
                int price = skulls.Cost(tier);
                string souls = price == 0 ? "FREE" : price == 1 ? "1 SOUL" : $"{price} SOULS";
                DungeonUi.Label(new Rect(440, 535, 400, 24), (tier >= 3 ? "RELEASE: 3 FEAR SKULLS  /  " : tier == 2 ? "RELEASE: FEAR SKULL  /  " : "RELEASE: SKULL  /  ") + souls,
                    14, ReaperAttack.Soul, TextAnchor.MiddleCenter);
                DungeonUi.Bar(new Rect(500, 568, 280, 5), skulls.SkullCharge, ReaperAttack.Soul);
            }
            if (specimen != null && specimen.IsGuarding)
            {
                DungeonUi.Label(new Rect(440, 535, 400, 24), $"ARM GUARD  {specimen.GuardRemaining:0.0}s  /  FORCE {specimen.Force}/{specimen.ForceCapacity}  /  RELEASE TO REPEL",
                    14, SpecimenCatalog.Amber, TextAnchor.MiddleCenter);
                DungeonUi.Bar(new Rect(500, 568, 280, 5), specimen.GuardRemaining / Mathf.Max(0.01f, specimen.GuardDuration), SpecimenCatalog.Amber);
            }
            string attack = KeyBindings.Label(GameAction.Attack), interact = KeyBindings.Label(GameAction.Interact);
            DungeonUi.Label(new Rect(250, 686, 780, 20), player.ClassWeapon == WeaponType.Hammer
                ? $"{attack}  weak swipe     HOLD / RELEASE {attack}  bless allies     {interact}  interact"
                : player.ClassWeapon == WeaponType.Fists
                ? $"{attack}  jab     HOLD / RELEASE {attack}  punch barrage     {KeyBindings.Label(GameAction.Special)}  empower     {interact}  interact"
                : player.ClassWeapon == WeaponType.Coins
                ? $"{attack}  throw a coin     HOLD / RELEASE {attack}  charged throw     {KeyBindings.Label(GameAction.Special)}  coin volley     {(player.Mechanic != null ? KeyBindings.Label(GameAction.Mechanic) + "  purse     " : "")}{interact}  interact"
                : player.ClassWeapon == WeaponType.Tail
                ? $"{attack}  tail stab     HOLD / RELEASE {attack}  paralysing vital stab     {KeyBindings.Label(GameAction.Special)}  tail sweep     {interact}  interact"
                : player.ClassWeapon == WeaponType.Scythe
                ? $"{attack}  scythe sweep     HOLD / RELEASE {attack}  harvest souls     HOLD / RELEASE {KeyBindings.Label(GameAction.Special)}  soul skulls     {(player.Mechanic != null ? KeyBindings.Label(GameAction.Mechanic) + "  raise skeleton     " : "")}{interact}  interact"
                : player.ClassWeapon == WeaponType.Katana
                ? $"{attack}  katana slash     HOLD / RELEASE {attack}  flurry of slashes     {KeyBindings.Label(GameAction.Special)}  dash slash     {(player.Mechanic != null ? KeyBindings.Label(GameAction.Mechanic) + "  pose / sheathe     " : "")}{interact}  interact"
                : player.ClassWeapon == WeaponType.Beam
                ? $"{attack}  plasma ray     HOLD / RELEASE {attack}  charged ray     HOLD / RELEASE {KeyBindings.Label(GameAction.Special)}  plasma cannon     {interact}  interact"
                : specimen != null
                ? SpecimenHints(specimen, attack, interact, player.Mechanic != null)
                : $"{KeyBindings.MovementLabel()}  move     HOLD / RELEASE {attack}  charge attack     {interact}  interact", 13, DungeonUi.Muted, TextAnchor.UpperCenter);
        }

        /// <summary>
        /// The Specimen's row under his health: while frail, which way his talents are pulling him (Bulk on the left,
        /// Edge on the right, a marker sliding toward the path he leans to); in a form, its name and its numbers.
        /// </summary>
        private static void DrawSpecimenRow(SpecimenAttack specimen)
        {
            const float y = 99f;
            if (specimen.LockedPath == SpecimenPath.None && !specimen.IsSnapped)
            {
                int bulk = specimen.BulkPoints, edge = specimen.EdgePoints;
                DungeonUi.Label(new Rect(42, y, 90, 24), $"BULK {bulk}", 14, SpecimenCatalog.Amber);
                DungeonUi.Label(new Rect(208, y, 90, 24), $"EDGE {edge}", 14, SpecimenCatalog.Keen, TextAnchor.UpperRight);
                var track = new Rect(124, y + 9, 92, 4);
                DungeonUi.Panel(track, DungeonUi.PanelColor);
                DungeonUi.Panel(new Rect(track.center.x - 1, track.y - 3, 2, 10), DungeonUi.Muted);
                float lean = Mathf.Clamp((edge - bulk) / (float)SpecimenCatalog.TransformPicks, -1f, 1f);
                Color marker = lean < 0f ? SpecimenCatalog.Amber : lean > 0f ? SpecimenCatalog.Keen : DungeonUi.Text;
                DungeonUi.Panel(new Rect(track.center.x + lean * (track.width * 0.5f - 4f) - 4f, track.y - 4, 8, 12), marker);
                return;
            }
            var form = specimen.Form;
            DungeonUi.Label(new Rect(42, y, 130, 24), SpecimenCatalog.FormName(form, specimen.IsGrown && !specimen.IsSnapped), 16, SpecimenCatalog.FormColor(form));
            string numbers = form == SpecimenForm.Behemoth ? $"FORCE {specimen.Force}/{specimen.ForceCapacity}"
                : $"CRIT +{specimen.Player.Powerups.FormCritBonus:P0}";
            DungeonUi.Label(new Rect(166, y + 1, 132, 24), numbers, 14, SpecimenCatalog.FormColor(form), TextAnchor.UpperRight);
        }

        /// <summary>The Specimen's path progress, shown over his talent choices.</summary>
        private static string SpecimenProgress(SpecimenAttack specimen)
        {
            if (specimen.LockedPath == SpecimenPath.None)
                return $"BULK {specimen.BulkPoints}/{SpecimenCatalog.TransformPicks}  /  EDGE {specimen.EdgePoints}/{SpecimenCatalog.TransformPicks}  -  three on one path transform him for the descent.";
            var form = SpecimenCatalog.FormOf(specimen.LockedPath);
            int points = SpecimenCatalog.Points(specimen.Player.Powerups, specimen.LockedPath);
            return specimen.IsGrown ? $"{SpecimenCatalog.FormName(form, true)}  /  fully grown."
                : $"{SpecimenCatalog.FormName(form, false)}  /  {points}/{SpecimenCatalog.GrowPicks} picks on his path to grow again.";
        }

        /// <summary>Which form one of the Specimen's abilities needs ("" for the ones that work in any form).</summary>
        private static string SpecimenFormTag(AbilityType type)
        {
            var path = SpecimenCatalog.PathOf(type);
            return path == SpecimenPath.None ? "" : SpecimenCatalog.FormName(SpecimenCatalog.FormOf(path), false);
        }

        /// <summary>Zeal stacks as a row of pips over the Paladin's special; a full row means the next blessing is doubled.</summary>
        private static void ZealMeter(Rect rect, int zeal)
        {
            bool full = zeal >= PlayerPowerups.ZealStacks;
            float pulse = full ? 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f) : 1f;
            DungeonUi.Label(new Rect(rect.x + 2, rect.y, 110, 16), full ? "ZEAL  x2 BLESSING" : $"ZEAL  {zeal}/{PlayerPowerups.ZealStacks}", 12,
                full ? FlameMesh.Alpha(AbilityCatalog.Gold, pulse) : DungeonUi.Muted);
            float gap = 3f, width = (rect.width - gap * (PlayerPowerups.ZealStacks - 1)) / PlayerPowerups.ZealStacks;
            for (int i = 0; i < PlayerPowerups.ZealStacks; i++)
                DungeonUi.Panel(new Rect(rect.x + i * (width + gap), rect.yMax - 8, width, 6),
                    i < zeal ? FlameMesh.Alpha(full ? FlameMesh.Core : AbilityCatalog.Gold, pulse) : DungeonUi.PanelColor);
        }

        private static string SpecimenHints(SpecimenAttack specimen, string attack, string interact, bool mechanic)
        {
            string special = KeyBindings.Label(GameAction.Special);
            string breaking = mechanic ? KeyBindings.Label(GameAction.Mechanic) + "  breaking point     " : "";
            switch (specimen.Form)
            {
                case SpecimenForm.Behemoth:
                    return specimen.IsRampaging
                        ? $"{attack}  ground pound     walk through enemies to trample them     {interact}  interact"
                        : $"{attack}  shove / stomp / pound     HOLD / RELEASE {attack}  axe kick     HOLD {special}  arm guard, release to repel     {breaking}{interact}  interact";
                case SpecimenForm.Edge:
                    return $"{attack}  lash (the tip always crits)     HOLD / RELEASE {attack}  chain cyclone     {special}  hook     {breaking}{interact}  interact";
                default:
                    return $"{attack}  palm strike     HOLD / RELEASE {attack}  snap kick     {special}  flinch guard     {breaking}{interact}  interact";
            }
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
            if (buffs.IsRuneEmpowered) parts.Add($"DEMONIC RUNE  {buffs.RuneRemaining:0.0}s");
            if (buffs.IsIncarnate) parts.Add($"AVATAR OF DEATH  x2 DAMAGE  {buffs.IncarnateRemaining:0.0}s");
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

        private static void Slot(Rect rect, string key, string name, float cooldown, float total, Color accent, bool locked = false, string lockedLabel = "LOCKED")
        {
            DungeonUi.Panel(rect, DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(rect.x + 14, rect.y + 10, 66, 22), key, 14, accent);
            DungeonUi.Label(new Rect(rect.x + 88, rect.y + 10, 76, 22), locked ? lockedLabel : cooldown > 0f ? $"{cooldown:0.0}s" : "READY", 13,
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

        /// <summary>The Gambler's purse shop: a tile per ware, then the safe in its own box. Play goes on while it is open.</summary>
        private void DrawPurse(GamblerPurse purse)
        {
            var rect = PurseArea(purse);
            DungeonUi.Panel(rect, DungeonUi.Background);
            DungeonUi.Label(new Rect(rect.x + 20, rect.y + 14, 220, 28), "THE PURSE", 22, GamblerAttack.Gold);
            int cards = purse.Coins?.Cards ?? 0;
            DungeonUi.Label(new Rect(rect.x + 150, rect.y + 18, 186, 24), cards > 0 ? $"{purse.Coins?.Coins ?? 1:N0} COINS  /  {cards} CARDS" : $"{purse.Coins?.Coins ?? 1:N0} COINS",
                cards > 0 ? 14 : 16, GamblerAttack.Gold, TextAnchor.UpperRight);
            var wares = purse.OnSale;
            float tileWidth = (rect.width - PursePadding * 2 - PurseTileGap) / 2f;
            for (int i = 0; i < wares.Count; i++)
            {
                var offer = wares[i];
                var tile = new Rect(rect.x + PursePadding + (i % 2) * (tileWidth + PurseTileGap), rect.y + PurseHeader + (i / 2) * PurseTileStep,
                    tileWidth, PurseTileHeight);
                bool affordable = purse.CanBuy(offer);
                // The whole tile is the button; its name, price and effect sit on top of it.
                if (DungeonUi.Button("purse" + i, tile, string.Empty, GamblerAttack.Gold, affordable)) purse.Buy(offer);
                DungeonUi.Label(new Rect(tile.x + 10, tile.y + 8, tile.width - 20, 20), offer.Name, 15, affordable ? DungeonUi.Text : DungeonUi.Muted);
                DungeonUi.Label(new Rect(tile.x + 10, tile.y + 28, tile.width - 20, 20), PurseTag(purse, offer), 16, affordable ? GamblerAttack.Gold : DungeonUi.Muted);
                DungeonUi.Label(new Rect(tile.x + 10, tile.y + 50, tile.width - 20, PurseTileHeight - 56), offer.Description, 11, DungeonUi.Muted);
            }
            if (purse.HasSafe)
            {
                // The safe: ten coins in per click, exactly half out per click, and interest from every guardian.
                var safe = new Rect(rect.x + PursePadding, rect.y + PurseHeader + PurseTileRows(purse) * PurseTileStep, rect.width - PursePadding * 2, PurseSafeHeight);
                float half = (safe.width - 24) / 2f;
                DungeonUi.Panel(safe, DungeonUi.PanelColor);
                DungeonUi.Label(new Rect(safe.x + 10, safe.y + 7, half, 18), $"THE SAFE  /  {purse.Safe:N0}c", 13, GamblerAttack.Gold);
                DungeonUi.Label(new Rect(safe.x + 14 + half, safe.y + 8, half, 18), $"x{purse.Interest:0.0#} PER GUARDIAN", 11, DungeonUi.Muted, TextAnchor.UpperRight);
                if (DungeonUi.Button("safeDeposit", new Rect(safe.x + 8, safe.y + 30, half, 32), $"Deposit {GamblerPurse.SafeDeposit}c", GamblerAttack.Gold, purse.CanDeposit, 15)) purse.Deposit();
                if (DungeonUi.Button("safeWithdraw", new Rect(safe.x + 16 + half, safe.y + 30, half, 32), $"Withdraw {purse.SafeWithdrawal:N0}c", GamblerAttack.Gold, purse.CanWithdraw, 15)) purse.Withdraw();
            }
            DungeonUi.Label(new Rect(rect.x + 20, rect.yMax - 34, rect.width - 40, 30),
                purse.LastResult ?? $"{KeyBindings.Label(GameAction.Mechanic)} closes the purse. Coins spent here leave your volley.", 12,
                purse.LastResult != null ? GamblerAttack.Gold : DungeonUi.Muted);
        }

        /// <summary>A purse tile's price line, or why it can't be bought when that isn't the coins.</summary>
        private static string PurseTag(GamblerPurse purse, GamblerPurse.Offer offer)
            => offer.Ware == GamblerPurse.Ware.Draught && purse.Player.Health >= purse.Player.MaxHealth ? "FULL HP" : $"{offer.Cost}c";

        /// <summary>Height of one ware's row: roomy while the stock fits, squeezed (never below 38) when it would run off the screen.</summary>
        private static float ShopRowStep(int wares)
            => Mathf.Clamp((DungeonUi.Height - 24f - ShopRect.y - ShopChrome) / Mathf.Max(1, wares), 38f, 58f);

        /// <summary>The shop panel's header, reroll slot and footer, around the rows of wares.</summary>
        private const float ShopChrome = 52f + 34f + 36f + 48f;

        /// <summary>The crystal shop panel, grown to fit however many wares it stocks (Black Market Pass adds one).</summary>
        private static Rect ShopPanelRect(CrystalShop shop)
        {
            int wares = shop != null ? shop.Stock.Count : 0;
            float height = Mathf.Max(ShopRect.height, ShopChrome + wares * ShopRowStep(wares));
            return new Rect(ShopRect.x, ShopRect.y, ShopRect.width, Mathf.Min(height, DungeonUi.Height - 24f - ShopRect.y));
        }

        /// <summary>The crystal merchant's wares. Play goes on while it is open.</summary>
        private void DrawShop(CrystalShop shop)
        {
            var rect = ShopPanelRect(shop);
            var pouch = Run.Player.Crystals;
            int wares = shop.Stock.Count;
            float step = ShopRowStep(wares);
            // Roomy cards give the description two lines; squeezed ones shrink everything to one.
            bool roomy = step >= 52f;
            Color crystal = CrystalPouch.CrystalColor;
            DungeonUi.Panel(rect, DungeonUi.Background);
            DungeonUi.Panel(new Rect(rect.x, rect.y, rect.width, 3), crystal);
            DungeonUi.Label(new Rect(rect.x + 20, rect.y + 13, 220, 28), "CRYSTAL SHOP", 22, crystal);
            // The purse: what there is to spend, set apart in its own chip.
            var chip = new Rect(rect.xMax - 156, rect.y + 12, 140, 28);
            DungeonUi.Panel(chip, DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(chip.x + 10, chip.y, 70, chip.height), "CRYSTALS", 11, DungeonUi.Muted, TextAnchor.MiddleLeft);
            DungeonUi.Label(new Rect(chip.x, chip.y, chip.width - 10, chip.height), pouch.Crystals.ToString(), 18, crystal, TextAnchor.MiddleRight);
            DungeonUi.Panel(new Rect(rect.x + 16, rect.y + 46, rect.width - 32, 1), new Color(DungeonUi.Muted.r, DungeonUi.Muted.g, DungeonUi.Muted.b, 0.25f));
            const float PriceWidth = 96f;
            for (int i = 0; i < wares; i++)
            {
                var offer = shop.Stock[i];
                // One card per ware: a strip in its colour, name and description on the left, the price to press on the right.
                var card = new Rect(rect.x + 16, rect.y + 52 + i * step, rect.width - 32, step - 6);
                bool maxed = offer.Powerup.HasValue && !Run.Player.Powerups.CanTake(offer.Powerup.Value);
                bool buyable = shop.CanBuy(offer);
                Color accent = buyable ? offer.Color : new Color(offer.Color.r, offer.Color.g, offer.Color.b, 0.35f);
                DungeonUi.Panel(card, DungeonUi.PanelColor);
                DungeonUi.Panel(new Rect(card.x, card.y, 4, card.height), accent);
                float textWidth = card.width - PriceWidth - 26f;
                string description = offer.Category == CrystalShop.Category.Relic ? "Relic: " + offer.Description : offer.Description;
                DungeonUi.Label(new Rect(card.x + 14, card.y + (roomy ? 5 : 1), textWidth, 18), offer.Name, roomy ? 15 : 13, buyable ? offer.Color : DungeonUi.Muted);
                DungeonUi.Label(new Rect(card.x + 14, card.y + (roomy ? 24 : 17), textWidth, card.height - (roomy ? 26 : 18)), description, roomy ? 11 : 10, DungeonUi.Muted);
                float priceHeight = Mathf.Min(34f, card.height - 8f);
                var price = new Rect(card.xMax - PriceWidth - 6, card.y + (card.height - priceHeight) / 2f, PriceWidth, priceHeight);
                if (DungeonUi.Button("shop" + i, price, maxed ? "MAXED" : $"{shop.Cost(offer)} cr", offer.Color, buyable, maxed ? 12 : 15))
                    shop.Buy(offer);
            }
            float below = rect.y + 52 + wares * step;
            if (shop.RerollsLeft > 0 && DungeonUi.Button("shopReroll", new Rect(rect.x + 16, below, rect.width - 32, 28),
                    $"Reroll wares  /  {shop.RerollsLeft} free", new Color(0.85f, 0.7f, 1f), Run.IsPlaying))
                shop.Reroll();
            var boons = new System.Collections.Generic.List<string>();
            if (pouch.PendingWards > 0) boons.Add($"+{pouch.PendingWards} wards");
            if (pouch.PendingDamage > 0) boons.Add($"+{pouch.PendingDamage} damage");
            if (pouch.PendingSwiftness > 0) boons.Add($"+{pouch.PendingSwiftness * CrystalPouch.SwiftnessPerBoon:P0} speed");
            // Always below the reroll slot, so a longer stock pushes it down instead of drawing over it.
            DungeonUi.Label(new Rect(rect.x + 20, Mathf.Max(rect.y + 404, below + 36), rect.width - 40, 22),
                boons.Count > 0 ? "For the guardian:  " + string.Join("   ", boons) : "Boons last the boss fight, relics the whole run.", 13, boons.Count > 0 ? AbilityCatalog.Gold : DungeonUi.Muted);
            DungeonUi.Label(new Rect(rect.x + 20, rect.yMax - 44, rect.width - 40, 36),
                shop.LastResult ?? $"{KeyBindings.Label(GameAction.Interact)} or walking away closes the shop. Crystals carry over to later shops.", 13,
                shop.LastResult != null ? CrystalPouch.CrystalColor : DungeonUi.Muted);
        }

        private void DrawTalents()
        {
            DungeonUi.Panel(new Rect(922, 82, 334, 470), DungeonUi.Background);
            if (DungeonUi.Button("buildTab", new Rect(940, 96, 144, 30), "Talents", DungeonUi.Teal, showAbilitiesTab)) showAbilitiesTab = false;
            if (DungeonUi.Button("abilitiesTab", new Rect(1094, 96, 144, 30), "Abilities", AbilityCatalog.Gold, !showAbilitiesTab)) showAbilitiesTab = true;
            if (showAbilitiesTab)
            {
                DungeonUi.Label(new Rect(946, 136, 288, 40), $"Choose which abilities sit on {SlotKey(0)} and {SlotKey(1)}.\nCooldowns stay with each ability.", 14, DungeonUi.Muted);
                DrawAbilityRows(new Rect(944, 184, 290, 350), ref abilityScroll);
                return;
            }
            DungeonUi.Label(new Rect(946, 136, 288, 48), $"Physical crit {Run.Player.Powerups.PhysicalCritChance:P0}\nElemental effect {Run.Player.Powerups.ElementalEffectChance:P0}", 15, DungeonUi.Muted);
            int count = 0;
            foreach (var power in PowerupCatalog.All) if (Run.Player.Powerups.Count(power.Type) > 0) count++;
            talentScroll = GUI.BeginScrollView(new Rect(944, 200, 290, 330), talentScroll, new Rect(0, 0, 268, Mathf.Max(316, count * 45)));
            int row = 0;
            foreach (var power in PowerupCatalog.All)
            {
                int rank = Run.Player.Powerups.Count(power.Type);
                if (rank == 0) continue;
                // The Specimen's talents from the path he did not take wither once he transforms.
                bool withered = Run.Player.Weapon is SpecimenAttack specimen && specimen.LockedPath != SpecimenPath.None
                    && SpecimenCatalog.PathOf(power.Type) == SpecimenCatalog.Other(specimen.LockedPath);
                DungeonUi.Label(new Rect(0, row++ * 45, 260, 42), withered ? $"{power.Name}  /  {rank}  (withered)" : $"{power.Name}  /  {rank}", 16, withered ? DungeonUi.Muted : (Color?)null);
            }
            if (row == 0) DungeonUi.Label(new Rect(0, 0, 260, 80), "Clear floors to earn talents.\nGuardians offer active abilities.", 16, DungeonUi.Muted);
            GUI.EndScrollView();
        }

        /// <summary>
        /// The abilities page: every ability learned this run with its rank, and buttons to put it on Q or E (an ability
        /// already on the other key swaps over).
        /// </summary>
        private void DrawAbilityRows(Rect area, ref Vector2 scroll)
        {
            var abilities = Run.Player.Abilities;
            var learned = new System.Collections.Generic.List<AbilityDefinition>(abilities.Learned);
            const float RowHeight = 58f;
            scroll = GUI.BeginScrollView(area, scroll, new Rect(0, 0, area.width - 22, Mathf.Max(area.height - 4, learned.Count * RowHeight)));
            float width = area.width - 22;
            for (int i = 0; i < learned.Count; i++)
            {
                var ability = learned[i];
                float y = i * RowHeight;
                DungeonUi.Panel(new Rect(0, y, width, RowHeight - 6), DungeonUi.PanelColor);
                DungeonUi.Label(new Rect(10, y + 4, 34, 44), ability.Glyph, 20, ability.Color, TextAnchor.MiddleCenter);
                DungeonUi.Label(new Rect(50, y + 4, width - 160, 24), ability.Name, 15);
                string formOnly = Run.Player.Weapon is SpecimenAttack && SpecimenFormTag(ability.Type) != "" ? $"  /  {SpecimenFormTag(ability.Type).ToLowerInvariant()}" : "";
                DungeonUi.Label(new Rect(50, y + 26, width - 160, 20), (abilities.Rank(ability.Type) >= PlayerAbilities.MaxRank ? $"Rank {abilities.Rank(ability.Type)}  /  MAX" : $"Rank {abilities.Rank(ability.Type)}") + formOnly,
                    12, abilities.Rank(ability.Type) >= PlayerAbilities.MaxRank ? AbilityCatalog.Gold : DungeonUi.Muted);
                for (int slot = 0; slot < PlayerAbilities.SlotCount; slot++)
                {
                    bool here = abilities.Equipped(slot) == ability.Type;
                    var key = new Rect(width - 104 + slot * 52, y + 8, 46, 36);
                    // The key it sits on is filled with the ability's colour (dark lettering); the other is a plain button to bind it.
                    if (here)
                    {
                        DungeonUi.Panel(key, ability.Color);
                        DungeonUi.Label(key, SlotKey(slot), 18, DungeonUi.Background, TextAnchor.MiddleCenter);
                    }
                    else if (DungeonUi.Button("equip" + ability.Type + slot, key, SlotKey(slot), DungeonUi.Muted))
                        abilities.Equip(ability.Type, slot);
                }
            }
            if (learned.Count == 0) DungeonUi.Label(new Rect(0, 0, width, 80), "No abilities learned yet.\nDefeat a guardian to choose one.", 15, DungeonUi.Muted);
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
            PinTopLeft();
            for (int i = 0; i < team.Count; i++)
            {
                var hero = team[i];
                if (hero == null) continue;
                Rect row = new Rect(24, 200 + i * 44, 292, 38);
                DungeonUi.Panel(row, DungeonUi.PanelColor);
                NameTag.Draw(new Rect(row.x + 14, row.y + 3, 152, 20), hero.PlayerName, hero.NameColor, hero.Badge, 14, titleBelow: false);
                DungeonUi.Label(new Rect(row.x + 170, row.y + 4, 108, 20), hero.IsAlive ? $"{hero.Health} / {hero.MaxHealth} HP" : "FALLEN", 13,
                    hero.IsAlive ? DungeonUi.Muted : new Color(1f, 0.4f, 0.4f), TextAnchor.UpperRight);
                DungeonUi.Bar(new Rect(row.x + 14, row.y + 27, row.width - 28, 4), hero.IsAlive ? hero.Health / (float)hero.MaxHealth : 0f, hero.Character.Color);
            }
            // Name tags float over teammates in the world, so they use the plain centred canvas.
            PinCenter();
            for (int i = 0; i < team.Count; i++)
            {
                var hero = team[i];
                if (hero == null || !hero.IsAlive || Run.View == null) continue;
                Vector3 screen = Run.View.WorldToScreenPoint(hero.transform.position + Vector3.up * 0.75f);
                if (screen.z < 0f) continue;
                Vector2 point = DungeonUi.ScreenToCanvas(screen, GameSettings.HudScale);
                // A developer's title sits under the name, so their tag starts a line higher.
                NameTag.Draw(new Rect(point.x - 90, point.y - (hero.Badge > 0 ? 36 : 24), 180, 34), hero.PlayerName, hero.NameColor, hero.Badge, 13, TextAnchor.UpperCenter);
            }
            PinTop();
            if (Run.Player.Health <= 0 && Run.IsPlaying)
            {
                var watched = Run.Coop.SpectateTarget;
                DungeonUi.Panel(new Rect(390, 104, 500, watched != null ? 104 : 64), DungeonUi.PanelColor);
                DungeonUi.Label(new Rect(400, 112, 480, 26), "YOU HAVE FALLEN", 20, new Color(1f, 0.45f, 0.45f), TextAnchor.MiddleCenter);
                DungeonUi.Label(new Rect(400, 138, 480, 22), "Your party fights on. You rise at half health next floor.", 14, DungeonUi.Muted, TextAnchor.MiddleCenter);
                if (watched != null)
                {
                    // Whom the camera follows; with more than one teammate standing, the arrows (or arrow keys) switch between them.
                    bool choice = Run.Coop.SpectateCount > 1 && !confirmingMenu;
                    DungeonUi.Label(new Rect(450, 168, 380, 30), "WATCHING  " + watched.PlayerName.ToUpperInvariant(), 15, watched.Character.Color, TextAnchor.MiddleCenter);
                    if (choice && DungeonUi.Button("spectatePrev", new Rect(404, 168, 40, 30), "<", DungeonUi.Teal)) Run.Coop.CycleSpectate(-1);
                    if (choice && DungeonUi.Button("spectateNext", new Rect(836, 168, 40, 30), ">", DungeonUi.Teal)) Run.Coop.CycleSpectate(1);
                    var e = Event.current;
                    if (choice && e.type == EventType.KeyDown && (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow))
                    {
                        Run.Coop.CycleSpectate(e.keyCode == KeyCode.RightArrow ? 1 : -1);
                        e.Use();
                    }
                }
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
            var specimen = Run.Player.Weapon as SpecimenAttack;
            ModalTitle("FLOOR CLEARED", "A moment of respite", specimen != null
                ? "Choose a talent. Restore 2 HP and descend deeper.\n" + SpecimenProgress(specimen) : "Choose a talent. Restore 2 HP and descend deeper.");
            for (int i = 0; i < Run.UpgradeChoices.Count; i++)
            {
                var power = Run.UpgradeChoices[i];
                Rect rect = new Rect(142 + i * 340, 300, 316, 330);
                int rank = Run.Player.Powerups.Count(power.Type) + 1;
                // The Specimen's talents say which path they feed.
                var path = specimen != null ? SpecimenCatalog.PathOf(power.Type) : SpecimenPath.None;
                string kind = path == SpecimenPath.Bulk ? "BULK TALENT" : path == SpecimenPath.Edge ? "EDGE TALENT" : power.ClassWeapon.HasValue ? "CLASS TALENT" : "TALENT";
                Color color = path == SpecimenPath.Bulk ? SpecimenCatalog.Amber : path == SpecimenPath.Edge ? SpecimenCatalog.Keen
                    : power.ClassWeapon.HasValue ? Run.SelectedCharacter.Color : DungeonUi.Teal;
                Card(rect, $"RANK {rank}  /  {kind}", power.Name, power.Description, color, "+");
                if (DungeonUi.Button("upgrade" + i, new Rect(rect.x + 24, rect.yMax - 60, 268, 40), "Choose talent", DungeonUi.Teal, pendingUpgrade < 0))
                    pendingUpgrade = i;
            }
            if (Run.CanRerollTalents && DungeonUi.Button("rerollTalents", new Rect(520, 650, 240, 36), "Reroll  /  Scholar's Reroll", AbilityCatalog.Gold, pendingUpgrade < 0))
                Run.RerollTalents();
            if (pendingUpgrade < 0) return;
            var chosen = Run.UpgradeChoices[pendingUpgrade];
            int chosenRank = Run.Player.Powerups.Count(chosen.Type) + 1;
            var answer = ConfirmPick(chosenRank > 1 ? $"Take {chosen.Name} to rank {chosenRank}?" : $"Take {chosen.Name}?", chosen.Description,
                chosen.ClassWeapon.HasValue ? Run.SelectedCharacter.Color : DungeonUi.Teal, "Take talent");
            if (answer == true) { int choice = pendingUpgrade; pendingUpgrade = -1; Run.ChooseUpgrade(choice); }
            else if (answer == false) pendingUpgrade = -1;
        }

        /// <summary>
        /// The confirmation over a talent or ability pick: true when confirmed, false when backed out of, null while
        /// it waits. Escape also backs out.
        /// </summary>
        private static bool? ConfirmPick(string question, string description, Color color, string confirmText)
        {
            DungeonUi.Panel(new Rect(-2000, -2000, 6000, 6000), new Color(0.01f, 0.018f, 0.035f, 0.7f));
            var panel = new Rect(390, 250, 500, 250);
            DungeonUi.Panel(panel, DungeonUi.Background);
            DungeonUi.Panel(new Rect(panel.x, panel.y, panel.width, 4), color);
            DungeonUi.Label(new Rect(panel.x + 24, panel.y + 22, panel.width - 48, 60), question, 24, DungeonUi.Text, TextAnchor.MiddleCenter);
            DungeonUi.ScrollingText("confirm" + question, new Rect(panel.x + 30, panel.y + 90, panel.width - 60, 80), description, 16, DungeonUi.Muted);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { Event.current.Use(); return false; }
            if (DungeonUi.Button("confirmBack", new Rect(panel.x + 24, panel.yMax - 64, 210, 42), "Back", DungeonUi.Muted)) return false;
            if (DungeonUi.Button("confirmYes", new Rect(panel.xMax - 234, panel.yMax - 64, 210, 42), confirmText, color)) return true;
            return null;
        }

        private void DrawArtifacts()
        {
            if (DrawWaiting()) return;
            if (Run.ArrangingAbilities) { DrawLoadout(); return; }
            var offers = Run.AbilityOffers;
            ModalTitle("GUARDIAN DEFEATED", "An artifact awakens", "Choose an ability to learn, or raise the rank of one you know. Then pick what sits on "
                + $"{SlotKey(0)} and {SlotKey(1)}.");
            const float Gap = 24f;
            float width = offers.Count >= 4 ? 280f : 316f;
            float left = (1280f - (offers.Count * width + (offers.Count - 1) * Gap)) / 2f;
            for (int i = 0; i < offers.Count; i++)
            {
                var ability = offers[i];
                Rect rect = new Rect(left + i * (width + Gap), 300, width, 340);
                int rank = Run.Player.Abilities.Rank(ability.Type);
                // The Specimen's path abilities only work in their own form.
                string form = Run.Player.Weapon is SpecimenAttack ? SpecimenFormTag(ability.Type) : "";
                string tag = form != "" ? (rank > 0 ? $"KNOWN  /  RANK {rank}  /  {form}" : $"{form} ABILITY  /  {ability.Cooldown:0}s")
                    : rank > 0 ? $"KNOWN  /  RANK {rank}" : $"NEW ABILITY  /  {ability.Cooldown:0}s COOLDOWN";
                Card(rect, tag, ability.Name, ability.Description, ability.Color, ability.Glyph);
                if (DungeonUi.Button("offer" + ability.Type, new Rect(rect.x + 24, rect.yMax - 60, rect.width - 48, 40),
                    rank + 1 >= PlayerAbilities.MaxRank ? $"Raise to rank {rank + 1}  /  MAX" : rank > 0 ? $"Raise to rank {rank + 1}" : "Learn", ability.Color, pendingAbility == AbilityType.None))
                    pendingAbility = ability.Type;
            }
            if (offers.Count == 0)
                DungeonUi.Label(new Rect(240, 380, 800, 60), $"You have mastered every ability this guardian could teach. Leave the artifact for {Run.LeftArtifactCrystals} crystals.", 20, DungeonUi.Muted, TextAnchor.MiddleCenter);
            if (DungeonUi.Button("leaveArtifact", new Rect(470, 661, 340, 35), $"Leave it  /  +{Run.LeftArtifactCrystals} crystals", CrystalPouch.CrystalColor,
                pendingAbility == AbilityType.None)) Run.LeaveArtifact();
            if (pendingAbility == AbilityType.None) return;
            var picked = AbilityCatalog.Get(pendingAbility);
            int known = Run.Player.Abilities.Rank(picked.Type);
            var answer = ConfirmPick(known + 1 >= PlayerAbilities.MaxRank ? $"Raise {picked.Name} to its max rank ({known + 1})?" : known > 0 ? $"Raise {picked.Name} to rank {known + 1}?" : $"Learn {picked.Name}?", picked.Description, picked.Color,
                known > 0 ? "Raise rank" : "Learn");
            if (answer == true) { var type = pendingAbility; pendingAbility = AbilityType.None; Run.PickAbility(type); }
            else if (answer == false) pendingAbility = AbilityType.None;
        }

        /// <summary>After the pick: every learned ability, and which sit on Q and E.</summary>
        private void DrawLoadout()
        {
            ModalTitle("ABILITIES", "Arrange your abilities", $"Every ability you've learned this run. Choose which sit on {SlotKey(0)} and {SlotKey(1)}; you can change this any time from the Talents panel.");
            var panel = new Rect(390, 290, 500, 330);
            DungeonUi.Panel(panel, DungeonUi.Background);
            DrawAbilityRows(new Rect(panel.x + 16, panel.y + 16, panel.width - 32, panel.height - 32), ref abilityScroll);
            if (DungeonUi.Button("loadoutDone", new Rect(520, 640, 240, 42), "Continue the descent", AbilityCatalog.Gold)) Run.FinishAbilityLoadout();
        }

        private void DrawWorldComplete()
        {
            bool decides = !Run.IsNetworked || Run.Coop.IsHost;
            if (showTravelMap) { DrawTravelMap(decides); return; }
            var world = Run.World;
            string ahead = Run.HasNextWorld ? "Ahead:  " + WorldCatalog.All[world.Index + 1].Name : "No world lies beyond. The descent goes on without end.";
            ModalTitle($"WORLD {world.Index + 1} CLEARED", world.Name,
                $"All three guardians have fallen  /  {Run.Kills} enemies defeated  /  +{Run.RunAshEarned} Ash this descent\n{ahead}");
            if (decides && DungeonUi.Button("worldNext", new Rect(450, 370, 380, 62), Run.HasNextWorld ? "Next world" : "Travel map", world.Accent))
            {
                travelMap.Selected = Run.HasNextWorld ? world.Index + 1 : world.Index;
                showTravelMap = true;
            }
            if (Run.IsNetworked)
            {
                if (Run.Coop.IsHost)
                {
                    if (DungeonUi.Button("worldLobby", new Rect(450, 450, 380, 50), "Back to the party", DungeonUi.Teal)) Run.Coop.HostReturnToLobby();
                }
                else
                {
                    // Teammates can look over the map while the host decides where the party goes.
                    DungeonUi.Label(new Rect(450, 326, 380, 36), "Waiting for the host to choose…", 18, DungeonUi.Muted, TextAnchor.MiddleCenter);
                    if (DungeonUi.Button("worldMapGuest", new Rect(450, 370, 380, 62), "World map", world.Accent))
                    {
                        travelMap.Selected = Run.HasNextWorld ? world.Index + 1 : world.Index;
                        showTravelMap = true;
                    }
                }
                if (DungeonUi.Button("worldLeave", new Rect(450, 520, 380, 44), "Leave party", DungeonUi.Muted)) AskToLeave();
                return;
            }
            if (DungeonUi.Button("worldMenu", new Rect(450, 450, 380, 50), "Main menu", DungeonUi.Muted)) AskToLeave();
        }

        /// <summary>
        /// The travel map between worlds: pick any world (nothing is locked yet) and travel there. Co-op teammates
        /// browse the same map, but only the host sends the party on.
        /// </summary>
        private void DrawTravelMap(bool decides)
        {
            var world = Run.World;
            // Below the dimmed floor label and objective, which sit at the top of the screen.
            DungeonUi.Label(new Rect(70, 112, 1140, 22), "TRAVEL MAP", 14, AbilityCatalog.Gold);
            DungeonUi.Label(new Rect(70, 134, 1140, 46), "Where does the descent go next?", 32, DungeonUi.Text);
            travelMap.Draw(Run, new Rect(70, 186, 1140, 396), world.Index, true);
            var target = WorldCatalog.All[travelMap.Selected];
            if (DungeonUi.Button("mapBack", new Rect(70, 598, 268, 48), "Back", DungeonUi.Muted)) showTravelMap = false;
            if (!decides)
            {
                DungeonUi.Label(new Rect(450, 598, 760, 48), "The host chooses where the party travels.", 18, DungeonUi.Muted, TextAnchor.MiddleCenter);
                return;
            }
            // Past the last world, the descent can also simply carry on.
            if (!Run.HasNextWorld && DungeonUi.Button("mapDescend", new Rect(450, 598, 380, 48), "Keep descending", world.Accent))
                Run.ContinueFromWorldComplete();
            if (DungeonUi.Button("mapTravel", new Rect(860, 598, 350, 48), $"Travel to world {travelMap.Selected + 1}", target.Accent))
                Run.TravelToWorld(travelMap.Selected);
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
