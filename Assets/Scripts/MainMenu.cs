using UnityEngine;

namespace Slopgame
{
    public sealed class MainMenu : MonoBehaviour
    {
        private const int AshMotes = 70;
        private static readonly Color Ember = new Color(1f, 0.46f, 0.2f);

        public DungeonRun Run { get; set; }
        private bool selecting, shopping, coop, settings, codex, account;
        private Vector2 heroScroll;
        // The move last hovered on the hero page (a guardian artifact, or the hero's charged attack, right click or R),
        // and the hero it belongs to. It starts on the charged attack, so the demo is never idle.
        private DemoKind previewKind;
        private AbilityDefinition previewed;
        private CharacterDefinition previewHero;
        // The little stage that plays the hovered move; it runs only while the hero page keeps asking for it.
        private AbilityDemo demo;
        private int demoWantedFrame = -1;
        // The hero page: a tall panel about the hero on the left, the demo and what it shows on the right.
        private const float PageTop = 178f, PageHeight = 402f;
        private static readonly Rect InfoRect = new Rect(368, PageTop, 410, PageHeight), DemoPanel = new Rect(786, PageTop, 424, PageHeight),
            DemoRect = new Rect(786, PageTop, 424, 270);
        /// <summary>True while the hero page is the one showing: it gets the smaller title, for the room.</summary>
        private bool OnHeroPage => selecting && !shopping && !coop && !settings && !codex && !account;
        private float beginConfirmUntil;

        // Leaving a party asks for a second click so a stray press does not drop you out of the lobby.
        private float leaveConfirmUntil;
        private Texture2D gradient, glow;
        private readonly AshShop shop = new AshShop();
        private readonly CoopMenu coopMenu = new CoopMenu();
        private readonly SettingsMenu settingsMenu = new SettingsMenu();
        private readonly Encyclopedia encyclopedia = new Encyclopedia();
        private readonly AccountMenu accountMenu = new AccountMenu();
        public void ResetPage(bool showCoop = false) { selecting = false; shopping = false; settings = false; codex = false; account = false; coop = showCoop; leaveConfirmUntil = 0f; settingsMenu.Cancel(); accountMenu.Cancel(); }
        public void ShowCoop() => ResetPage(true);

        private void LateUpdate()
        {
            // The hero page stopped drawing the demo (another page, another hero, or the descent began): close the stage.
            if (demo != null && demo.IsShowing && Time.frameCount - demoWantedFrame > 1) demo.Hide();
        }

        /// <summary>Plays the previewed move on the demo stage and draws it in its window, with a placeholder where there is nothing to play.</summary>
        private void DrawDemo(CharacterDefinition character)
        {
            DungeonUi.Panel(DemoRect, new Color(0.02f, 0.03f, 0.045f, 0.95f));
            if (!AbilityDemo.Supports(character.Weapon))
            {
                if (demo != null) demo.Hide();
                DungeonUi.Label(DemoRect, "No demos for this hero yet.", 15, DungeonUi.Muted, TextAnchor.MiddleCenter);
                return;
            }
            if (demo == null) demo = gameObject.AddComponent<AbilityDemo>();
            demo.Show(character, previewKind, previewed);
            demoWantedFrame = Time.frameCount;
            if (Event.current.type == EventType.Repaint && demo.Texture != null) GUI.DrawTexture(DemoRect, demo.Texture, ScaleMode.StretchToFill, false);
            DungeonUi.Label(new Rect(DemoRect.x + 8, DemoRect.y + 5, 120, 14), "DEMO", 10, DungeonUi.Muted);
        }

        private void OnDestroy()
        {
            if (gradient != null) Destroy(gradient);
            if (glow != null) Destroy(glow);
        }

        private void OnGUI()
        {
            if (Run == null || !Run.IsInMainMenu) return;
            Matrix4x4 previous = DungeonUi.Begin(GameSettings.MenuScale);
            try
            {
                DrawBackdrop();
                DrawHeader();
                // The cog in the header opens the settings over whichever page is showing; Back returns to it.
                if (settings)
                {
                    settingsMenu.Draw();
                    if (DungeonUi.Button("settingsBack", new Rect(70, 598, 268, 48), "Back", DungeonUi.Muted)) { settingsMenu.Cancel(); settings = false; }
                    if (DungeonUi.Button("settingsReset", new Rect(860, 598, 350, 48), "Reset to defaults", DungeonUi.Teal)) settingsMenu.ResetToDefaults();
                }
                else if (shopping)
                {
                    shop.Draw(Run);
                    if (DungeonUi.Button("shopBack", new Rect(70, 598, 268, 48), "Back", DungeonUi.Muted)) shopping = false;
                }
                else if (account)
                {
                    accountMenu.Draw(Run);
                    if (DungeonUi.Button("accountBack", new Rect(70, 598, 268, 48), "Back", DungeonUi.Muted)) { accountMenu.Cancel(); account = false; }
                }
                else if (codex)
                {
                    encyclopedia.Draw(Run);
                    if (DungeonUi.Button("codexBack", new Rect(70, 598, 268, 48), "Back", DungeonUi.Muted)) codex = false;
                }
                else if (coop)
                {
                    coopMenu.Draw(Run);
                    bool inParty = Run.Coop.Session.State != NetState.Offline;
                    bool confirming = inParty && Time.unscaledTime < leaveConfirmUntil;
                    if (DungeonUi.Button("coopBack", new Rect(70, 598, 268, 48), confirming ? "Confirm?" : inParty ? "Leave party" : "Back", confirming ? AbilityCatalog.Gold : DungeonUi.Muted))
                    {
                        if (inParty && !confirming) leaveConfirmUntil = Time.unscaledTime + 3f;
                        else
                        {
                            leaveConfirmUntil = 0f;
                            CoopMenu.Leave(Run);
                            if (!inParty) coop = false;
                        }
                    }
                }
                else if (!selecting) DrawLanding();
                else DrawSelection();
                if (!string.IsNullOrEmpty(Run.Progress.LastError))
                    DungeonUi.Label(new Rect(70, 645, 1140, 24), Run.Progress.LastError, 14, AbilityCatalog.Gold);
                DungeonUi.Panel(new Rect(70, 666, 1140, 1), new Color(DungeonUi.Muted.r, DungeonUi.Muted.g, DungeonUi.Muted.b, 0.18f));
                DungeonUi.Label(new Rect(70, 676, 1140, 24), $"{KeyBindings.MovementLabel()}  move     HOLD / RELEASE {KeyBindings.Label(GameAction.Attack)}  attack     "
                    + $"{KeyBindings.Label(GameAction.Special)}  class skill     {KeyBindings.Label(GameAction.AbilityQ)} / {KeyBindings.Label(GameAction.AbilityE)}  relic abilities", 14, DungeonUi.Muted);
            }
            finally { GUI.matrix = previous; }
        }

        /// <summary>Dark gradient, a smouldering glow from below, and ash drifting down through rising embers.</summary>
        private void DrawBackdrop()
        {
            EnsureTextures();
            float t = Time.unscaledTime;
            DungeonUi.Panel(new Rect(-2000, -2000, 6000, 6000), DungeonUi.Background);
            if (Event.current.type != EventType.Repaint) return;
            Color previousColor = GUI.color;
            GUI.DrawTexture(new Rect(-400, 0, DungeonUi.Width + 800, DungeonUi.Height), gradient);
            float pulse = 0.8f + Mathf.Sin(t * 0.7f) * 0.2f;
            GUI.color = new Color(Ember.r, Ember.g, Ember.b, 0.2f * pulse);
            GUI.DrawTexture(new Rect(560, 360, 900, 620), glow);
            GUI.color = new Color(DungeonUi.Teal.r, DungeonUi.Teal.g, DungeonUi.Teal.b, 0.07f);
            GUI.DrawTexture(new Rect(-260, -260, 900, 640), glow);

            for (int i = 0; i < AshMotes; i++)
            {
                float seed = Hash(i), seed2 = Hash(i + 101), seed3 = Hash(i + 211);
                bool ember = i % 5 == 0;
                // Ash falls; every fifth mote is an ember rising and fading out near the top.
                float speed = ember ? 22f + seed2 * 26f : 10f + seed2 * 18f;
                float travel = Mathf.Repeat(seed3 * DungeonUi.Height + t * speed, DungeonUi.Height + 40f);
                float y = ember ? DungeonUi.Height + 20f - travel : travel - 20f;
                float x = seed * (DungeonUi.Width + 200f) - 100f + Mathf.Sin(t * (0.3f + seed2 * 0.4f) + i) * (18f + seed3 * 24f);
                float size = ember ? 2f + seed3 * 2f : 1.5f + seed * 2.5f;
                float alpha = ember ? Mathf.Clamp01(y / DungeonUi.Height) * (0.55f + Mathf.Sin(t * 5f + i) * 0.3f) : 0.1f + seed2 * 0.18f;
                GUI.color = ember ? new Color(Ember.r, Ember.g * 1.2f, Ember.b, alpha) : new Color(0.75f, 0.78f, 0.84f, alpha);
                GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);
            }
            GUI.color = previousColor;
        }

        private void DrawHeader()
        {
            float t = Time.unscaledTime;
            DungeonUi.Panel(new Rect(70, 62, 36, 2), AbilityCatalog.Gold);
            DungeonUi.Label(new Rect(118, 52, 700, 25), "A ROGUELIKE DESCENT", 14, AbilityCatalog.Gold);
            string title = settings ? "SETTINGS" : shopping ? "ASH SHOP" : codex ? "ENCYCLOPEDIA" : account ? "ACCOUNT" : coop ? "CO-OP" : "ASH / BELOW";
            // The encyclopedia and settings pages need the room, so they get a smaller title.
            bool compact = settings || codex || OnHeroPage;
            int size = compact ? 40 : 66;
            float y = compact ? 80 : 88, height = compact ? 56 : 94, glow = compact ? 1.5f : 2f;
            // Soft ember glow and a hard drop shadow give the title some depth.
            float flicker = 0.1f + Mathf.PerlinNoise(t * 1.4f, 0.3f) * 0.12f;
            DungeonUi.Label(new Rect(65 - glow, y - glow, 1100, height), title, size, new Color(Ember.r, Ember.g, Ember.b, flicker));
            DungeonUi.Label(new Rect(65 + glow, y + glow, 1100, height), title, size, new Color(Ember.r, Ember.g, Ember.b, flicker));
            DungeonUi.Label(new Rect(65 + glow * 2, y + glow * 2.5f, 1100, height), title, size, new Color(0f, 0f, 0f, 0.6f));
            DungeonUi.Label(new Rect(65, y, 1100, height), title, size);
            DungeonUi.Label(compact ? new Rect(70, 138, 1100, 30) : new Rect(70, 188, 1100, 42), settings ? "Resize the menus and HUD, fade the HUD, switch autofire on or off for each hero and rebind every action. Changes save instantly."
                : shopping ? "Spend the ash you carry home. Grow stronger with every descent."
                : codex ? "Everything you have met in the ash. Unfound entries stay hidden until a descent turns them up."
                : account ? "Sign in to carry your ash, upgrades, unlocks and saved descents to any PC."
                : coop ? "Descend with up to three friends. Fallen heroes rise again on the next floor."
                : selecting ? "Choose your hero. Shape your build. Claim the relics below." : "Twelve heroes. Two relic abilities. One life in the ash.", compact ? 17 : 20, DungeonUi.Muted);

            // The settings cog sits beside the Ash on every page of the menu.
            if (DungeonUi.CogButton("menuCog", new Rect(936, 50, 44, 44), settings))
            {
                settingsMenu.Cancel();
                settings = !settings;
            }
            bool readOnly = Run.Progress.IsReadOnly;
            DungeonUi.Panel(new Rect(990, 50, 220, 44), DungeonUi.PanelColor);
            DungeonUi.Panel(new Rect(990, 91, 220, 3), readOnly ? DungeonUi.Muted : AbilityCatalog.Gold);
            DungeonUi.Label(new Rect(1008, 50, 90, 44), readOnly ? "SAVE" : "ASH", 13, DungeonUi.Muted, TextAnchor.MiddleLeft);
            DungeonUi.Label(new Rect(1040, 50, 152, 44), readOnly ? "UNAVAILABLE" : Run.Progress.Ash.ToString(), readOnly ? 16 : 25,
                readOnly ? DungeonUi.Muted : AbilityCatalog.Gold, TextAnchor.MiddleRight);
        }

        private void DrawLanding()
        {
            var savedHero = Run.SavedRunHero;
            if (savedHero != null) DrawSavedRun(savedHero, Run.RunSave.Saved);
            else
            {
                DungeonUi.Panel(new Rect(70, 262, 610, 190), DungeonUi.PanelColor);
                DungeonUi.Panel(new Rect(70, 262, 4, 190), DungeonUi.Teal);
                DungeonUi.Label(new Rect(102, 286, 530, 40), "POWER HAS A PRICE", 27, DungeonUi.Teal);
                DungeonUi.Label(new Rect(102, 336, 550, 110), "Charge your attacks. Read the enemy.\nEvery fifth floor, face an arena guardian.\nTake its artifact and choose your own power.", 20, DungeonUi.Muted);
            }
            DrawHeroParade(new Rect(70, 470, 610, 116));

            DungeonUi.Label(new Rect(755, 244, 420, 20), "MENU", 12, DungeonUi.Muted);
            if (DungeonUi.Button("chooseClass", new Rect(755, 266, 420, 64), "Choose your hero", AbilityCatalog.Gold)) selecting = true;
            if (DungeonUi.Button("coop", new Rect(755, 342, 205, 48), "Co-op", AbilityCatalog.Gold)) coop = true;
            if (DungeonUi.Button("encyclopedia", new Rect(970, 342, 205, 48), "Encyclopedia", AbilityCatalog.Gold)) codex = true;
            if (DungeonUi.Button("shop", new Rect(755, 402, 205, 48), "Ash shop", DungeonUi.Teal)) shopping = true;
            if (DungeonUi.Button("settings", new Rect(970, 402, 205, 48), "Settings", DungeonUi.Teal)) settings = true;
            // Progress changed both here and on another PC since the last sync: the account page asks which to keep.
            bool choosing = Run.Account.IsSignedIn && Run.CloudSync.IsChoosing;
            if (DungeonUi.Button("account", new Rect(755, 462, 205, 48), choosing ? "Choose save" : AccountLabel(),
                choosing ? AbilityCatalog.Gold : DungeonUi.Teal)) account = true;
            if (DungeonUi.Button("quit", new Rect(970, 462, 205, 48), "Quit", DungeonUi.Muted))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            // Debug admin mode belongs to the developer accounts.
            if (Run.HasDeveloperAccess && DungeonUi.Button("debugMode", new Rect(755, 548, 420, 38),
                DebugMode.Enabled ? "Debug admin mode: ON  (F1)" : "Debug admin mode: OFF  (F1)",
                DebugMode.Enabled ? DungeonHud.DebugColor : DungeonUi.Muted)) DebugMode.Toggle();
        }

        /// <summary>The account's saved descent, in place of the landing blurb: where it was left, and a button to go on.</summary>
        private void DrawSavedRun(CharacterDefinition hero, RunSnapshot save)
        {
            DungeonUi.Panel(new Rect(70, 262, 610, 190), DungeonUi.PanelColor);
            DungeonUi.Panel(new Rect(70, 262, 4, 190), hero.Color);
            DungeonUi.Label(new Rect(102, 278, 530, 20), "YOUR DESCENT AWAITS", 13, AbilityCatalog.Gold);
            var world = WorldCatalog.ForFloor(save.floor);
            string where = save.inShop ? $"the crystal shop before floor {save.floor + 1}" : $"floor {save.floor}";
            DungeonUi.Label(new Rect(102, 300, 550, 40), $"{hero.DisplayName}  /  {world.Name}", 27, hero.Color);
            string saved = new System.DateTime(save.savedAt, System.DateTimeKind.Utc).ToLocalTime().ToString("d MMM, HH:mm");
            DungeonUi.Label(new Rect(102, 342, 550, 44), $"Left on {where}, which starts over when you return.  /  Saved {saved}", 15, DungeonUi.Muted);
            if (DungeonUi.Button("continueRun", new Rect(102, 392, 280, 46), "Continue descent", AbilityCatalog.Gold)) Run.ResumeRun();
            DungeonUi.Label(new Rect(398, 392, 260, 46), "A new descent replaces it.", 13, DungeonUi.Muted, TextAnchor.MiddleLeft);
        }

        /// <summary>The landing button shows who is signed in, shortened to fit.</summary>
        private string AccountLabel()
        {
            string name = Run.Account.Username;
            if (name == null) return "Account";
            return name.Length > 12 ? name.Substring(0, 11) + "…" : name;
        }

        /// <summary>The roster standing in a bobbing line; clicking a hero jumps straight to them on the selection page.</summary>
        private void DrawHeroParade(Rect area)
        {
            DungeonUi.Panel(area, new Color(DungeonUi.PanelColor.r, DungeonUi.PanelColor.g, DungeonUi.PanelColor.b, 0.6f));
            var heroes = Run.AvailableCharacters;
            int count = heroes.Count;
            if (count == 0) return;
            float slot = Mathf.Min(64f, (area.width - 24f) / count);
            float startX = area.x + (area.width - slot * count) / 2f;
            string hoveredName = null;
            Color hoveredColor = DungeonUi.Muted;
            for (int i = 0; i < count; i++)
            {
                var hero = heroes[i];
                Rect cell = new Rect(startX + i * slot, area.y + 12, slot, 64);
                bool hovered = cell.Contains(Event.current.mousePosition);
                float bob = Mathf.Sin(Time.unscaledTime * 2.2f + i * 0.8f) * 2.5f - (hovered ? 5f : 0f);
                float size = Mathf.Min(slot - 8f, 52f) * (hovered ? 1.12f : 1f);
                Rect frame = new Rect(cell.center.x - size / 2f, cell.y + 58 - size + bob, size, size);
                // A small shadow on the "floor" keeps everyone grounded while they bob.
                DungeonUi.Panel(new Rect(cell.center.x - size * 0.3f, cell.y + 60, size * 0.6f, 3), new Color(0, 0, 0, 0.45f));
                DrawPortrait(frame, hero, hovered ? 1f : 0.82f);
                if (hovered) { hoveredName = hero.DisplayName; hoveredColor = hero.Color; }
                if (GUI.Button(cell, GUIContent.none, GUIStyle.none)) { Run.SelectCharacter(hero); selecting = true; }
            }
            DungeonUi.Label(new Rect(area.x, area.y + 84, area.width, 24), hoveredName ?? $"{count} HEROES AWAIT", 15,
                hoveredName != null ? hoveredColor : DungeonUi.Muted, TextAnchor.MiddleCenter);
        }

        private static void DrawPortrait(Rect frame, CharacterDefinition character, float brightness = 1f)
        {
            var portrait = HeroSprites.Body(character.Weapon);
            if (portrait == null)
            {
                string glyph = character.Weapon == WeaponType.Mutation ? "%" : character.Weapon == WeaponType.Staff ? "*" : character.Weapon == WeaponType.Bow ? ">" : character.Weapon == WeaponType.Daggers ? "//" : character.Weapon == WeaponType.Fists ? "[]" : character.Weapon == WeaponType.Tail ? "~>" : character.Weapon == WeaponType.Coins ? "$" : character.Weapon == WeaponType.Beam ? "=>" : character.Weapon == WeaponType.Scythe ? "?" : character.Weapon == WeaponType.Katana ? "/" : "+";
                DungeonUi.Label(frame, glyph, Mathf.RoundToInt(frame.height * 0.55f), character.Color, TextAnchor.MiddleCenter);
                return;
            }
            // Pixel-art portrait: tinted body layer, then the fixed-colour details on top.
            Color previousColor = GUI.color;
            GUI.color = new Color(character.Color.r * brightness, character.Color.g * brightness, character.Color.b * brightness, character.Color.a);
            GUI.DrawTexture(frame, portrait.texture, ScaleMode.ScaleToFit, true);
            var details = HeroSprites.Accent(character.Weapon);
            GUI.color = new Color(brightness, brightness, brightness, 1f);
            if (details != null) GUI.DrawTexture(frame, details.texture, ScaleMode.ScaleToFit, true);
            GUI.color = previousColor;
        }

        private void DrawSelection()
        {
            // The hero list scrolls (mouse wheel or scrollbar) so it never runs into the Back button.
            var heroes = Run.AvailableCharacters;
            int count = heroes.Count;
            heroScroll = GUI.BeginScrollView(new Rect(70, PageTop, 292, PageHeight), heroScroll, new Rect(0, 0, 268, Mathf.Max(PageHeight, count * 46 - 6)));
            for (int i = 0; i < count; i++)
            {
                var hero = heroes[i];
                Rect rect = new Rect(0, i * 46, 268, 40);
                bool selected = hero == Run.SelectedCharacter;
                if (DungeonUi.Button("class" + i, rect, hero.DisplayName + (selected ? "  /  SELECTED" : ""), selected ? hero.Color : DungeonUi.Muted)) Run.SelectCharacter(hero);
            }
            GUI.EndScrollView();
            var character = Run.SelectedCharacter;
            // A newly chosen hero starts on their charged attack.
            if (previewHero != character) { previewHero = character; previewKind = DemoKind.Charged; previewed = null; }
            var permanent = new PermanentBonuses(Run.Progress, character.Weapon);
            DungeonUi.Panel(InfoRect, DungeonUi.PanelColor);
            DungeonUi.Panel(new Rect(InfoRect.x, InfoRect.y, 4, InfoRect.height), character.Color);
            if (Event.current.type == EventType.Repaint)
            {
                Color previousColor = GUI.color;
                GUI.color = new Color(character.Color.r, character.Color.g, character.Color.b, 0.18f);
                GUI.DrawTexture(new Rect(InfoRect.x - 24, InfoRect.y - 24, 200, 200), glow);
                GUI.color = previousColor;
            }
            float left = InfoRect.x + 20, width = InfoRect.width - 36;
            DungeonUi.Panel(new Rect(left, PageTop + 16, 92, 92), new Color(character.Color.r * 0.25f, character.Color.g * 0.25f, character.Color.b * 0.25f));
            float bob = Mathf.Sin(Time.unscaledTime * 2f) * 2f;
            DrawPortrait(new Rect(left + 8, PageTop + 24 + bob, 76, 76), character);
            DungeonUi.Label(new Rect(left + 106, PageTop + 14, width - 106, 44), character.DisplayName, 30, character.Color);
            DrawStat(new Rect(left + 106, PageTop + 62, 86, 32), "HP", character.StartingHealth + permanent.Health);
            DrawStat(new Rect(left + 196, PageTop + 62, 86, 32), "DMG", character.StartingDamage + permanent.Damage);
            DrawStat(new Rect(left + 286, PageTop + 62, 86, 32), "SPEED", character.MoveSpeed + permanent.Speed);
            DungeonUi.Label(new Rect(left, PageTop + 120, width, 104), character.Description, 14);
            DrawMoves(character, left, width);
            DungeonUi.Panel(DemoPanel, DungeonUi.PanelColor);
            DrawDemo(character);
            DrawMoveCaption(character);
            if (DungeonUi.Button("back", new Rect(70, 598, 268, 48), "Back", DungeonUi.Muted)) selecting = false;
            // Autofire is switched per hero: here for the one on the page (the settings page lists them all).
            bool autofire = GameSettings.AutofireFor(character.Weapon);
            if (DungeonUi.Button("heroAutofire", new Rect(560, 598, 280, 48), autofire ? "Autofire: ON" : "Autofire: OFF", autofire ? AbilityCatalog.Gold : DungeonUi.Muted, size: 17))
                GameSettings.SetAutofire(character.Weapon, !autofire);
            // A saved descent is replaced by the new one, so the first click asks.
            bool replacing = Run.SavedRunHero != null;
            bool confirming = replacing && Time.unscaledTime < beginConfirmUntil;
            if (DungeonUi.Button("begin", new Rect(860, 598, 350, 48), confirming ? "Replace your saved descent?" : "Begin descent",
                confirming ? AbilityCatalog.Gold : character.Color, size: confirming ? 16 : 18))
            {
                if (replacing && !confirming) beginConfirmUntil = Time.unscaledTime + 3f;
                else { beginConfirmUntil = 0f; Run.Restart(); }
            }
        }

        /// <summary>One chip in the hero's rows of moves: hovering it puts that move on the demo stage. True when it is clicked.</summary>
        private bool MoveChip(string id, Rect chip, string text, int size, Color color, DemoKind kind, AbilityDefinition ability, bool locked)
        {
            if (chip.Contains(Event.current.mousePosition)) { previewKind = kind; previewed = ability; }
            bool clicked = DungeonUi.Button(id, chip, "", locked ? DungeonUi.Muted : color);
            DungeonUi.Label(locked ? new Rect(chip.x, chip.y, chip.width, 22) : chip, text, locked ? size - 3 : size, locked ? DungeonUi.Muted : color, TextAnchor.MiddleCenter);
            if (locked) DungeonUi.Label(new Rect(chip.x, chip.y + 19, chip.width, 12), "LOCKED", 9, AbilityCatalog.Gold, TextAnchor.MiddleCenter);
            if (previewKind == kind && previewed == ability) DungeonUi.Panel(new Rect(chip.x, chip.y, chip.width, 2), locked ? AbilityCatalog.Gold : color);
            return clicked;
        }

        /// <summary>
        /// The hero's moves as two rows of chips: the three every hero has (charged attack, right click and R), then
        /// their guardian artifacts. Hovering one plays it; clicking a locked artifact opens the Ash shop on its unlock.
        /// </summary>
        private void DrawMoves(CharacterDefinition character, float left, float width)
        {
            var weapon = character.Weapon;
            float y = PageTop + 232;
            DungeonUi.Label(new Rect(left, y, width, 14), "ATTACKS", 11, DungeonUi.Muted);
            float third = (width - 12) / 3f;
            MoveChip("moveCharged", new Rect(left, y + 16, third, 34), HeroMoves.ChargedName(weapon), 12, character.Color, DemoKind.Charged, null, false);
            MoveChip("moveHeavy", new Rect(left + third + 6, y + 16, third, 34), DungeonUi.SpecialName(weapon), 12, character.Color, DemoKind.Heavy, null, false);
            var mechanic = PermanentUpgradeCatalog.Get(PermanentUpgradeCatalog.MechanicId(weapon));
            MoveChip("moveMechanic", new Rect(left + (third + 6) * 2, y + 16, third, 34), mechanic != null ? mechanic.Name : "Class mechanic", 12, character.Color,
                DemoKind.Mechanic, null, mechanic != null && Run.Progress.Rank(mechanic.Id) <= 0);

            y += 60;
            DungeonUi.Label(new Rect(left, y, width, 14), $"ABILITIES   /   guardian artifacts fill {KeyBindings.Label(GameAction.AbilityQ)} and {KeyBindings.Label(GameAction.AbilityE)}", 11, DungeonUi.Muted);
            const int PerRow = 6;
            float chipWidth = (width - (PerRow - 1) * 6) / PerRow;
            bool anyLocked = false;
            int index = 0;
            foreach (var ability in AbilityCatalog.All)
            {
                if (ability.ClassWeapon != weapon) continue;
                bool locked = IsLocked(ability);
                anyLocked |= locked;
                var chip = new Rect(left + (index % PerRow) * (chipWidth + 6), y + 16 + (index / PerRow) * 38, chipWidth, 34);
                index++;
                if (MoveChip("preview_" + ability.Type, chip, ability.Glyph, 16, ability.Color, DemoKind.Ability, ability, locked) && locked)
                {
                    shop.ShowAbility(ability);
                    shopping = true;
                }
            }
            DungeonUi.Label(new Rect(left, InfoRect.yMax - 24, width, 16), anyLocked ? "Hover a move to watch it. Click a locked ability to find it in the Ash shop." : "Hover a move to watch it.", 11, DungeonUi.Muted);
        }

        /// <summary>Under the demo: the previewed move's name, how it is used and what it does.</summary>
        private void DrawMoveCaption(CharacterDefinition character)
        {
            var weapon = character.Weapon;
            string name, detail, description;
            Color color = character.Color;
            bool locked = false;
            if (previewKind == DemoKind.Ability && previewed != null)
            {
                locked = IsLocked(previewed);
                int cost = PermanentUpgradeCatalog.Get(previewed.UnlockId)?.Cost(0) ?? 0;
                name = previewed.Name;
                detail = locked ? $"LOCKED  /  {cost} Ash in the shop" : $"{previewed.Cooldown:0.#}s cooldown";
                description = previewed.Description;
                color = previewed.Color;
            }
            else if (previewKind == DemoKind.Heavy)
            {
                name = DungeonUi.SpecialName(weapon);
                detail = $"{KeyBindings.Label(GameAction.Special)}  /  {DungeonUi.SpecialCooldown(weapon):0.#}s cooldown";
                description = HeroMoves.HeavyDescription(weapon);
            }
            else if (previewKind == DemoKind.Mechanic)
            {
                var mechanic = PermanentUpgradeCatalog.Get(PermanentUpgradeCatalog.MechanicId(weapon));
                locked = mechanic != null && Run.Progress.Rank(mechanic.Id) <= 0;
                name = mechanic != null ? mechanic.Name : "Class mechanic";
                detail = KeyBindings.Label(GameAction.Mechanic) + (locked ? "  /  LOCKED  /  unlock it in the Ash shop" : "");
                description = mechanic != null ? mechanic.Description : "";
            }
            else
            {
                name = HeroMoves.ChargedName(weapon);
                detail = $"hold and release {KeyBindings.Label(GameAction.Attack)}";
                description = HeroMoves.ChargedDescription(weapon);
            }
            float left = DemoPanel.x + 14, width = DemoPanel.width - 28, y = DemoRect.yMax + 8;
            DungeonUi.Label(new Rect(left, y, width, 22), name, 17, locked ? AbilityCatalog.Gold : color);
            DungeonUi.Label(new Rect(left, y + 2, width, 20), detail, 12, locked ? AbilityCatalog.Gold : DungeonUi.Muted, TextAnchor.UpperRight);
            DungeonUi.Label(new Rect(left, y + 26, width, DemoPanel.yMax - y - 32), description, 12, DungeonUi.Muted);
        }

        private bool IsLocked(AbilityDefinition ability) => ability.ShopUnlock && Run.Progress.Rank(ability.UnlockId) <= 0;

        private static void DrawStat(Rect rect, string name, float value)
        {
            DungeonUi.Panel(rect, new Color(0.02f, 0.03f, 0.045f, 0.9f));
            DungeonUi.Label(new Rect(rect.x + 10, rect.y, 60, rect.height), name, 11, DungeonUi.Muted, TextAnchor.MiddleLeft);
            DungeonUi.Label(new Rect(rect.x, rect.y, rect.width - 10, rect.height), value.ToString("0.#"), 18, DungeonUi.Text, TextAnchor.MiddleRight);
        }

        private void EnsureTextures()
        {
            if (gradient == null)
            {
                // Night-blue at the top warming to a faint ash-red at the floor.
                gradient = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                for (int y = 0; y < 64; y++)
                {
                    float k = y / 63f;
                    gradient.SetPixel(0, y, Color.Lerp(new Color(0.09f, 0.035f, 0.03f, 0.9f), new Color(0.02f, 0.035f, 0.07f, 0f), Mathf.SmoothStep(0f, 1f, k)));
                }
                gradient.Apply();
            }
            if (glow == null)
            {
                const int size = 64;
                glow = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
                        float a = Mathf.Clamp01(1f - d);
                        glow.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                glow.Apply();
            }
        }

        private static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 127.1f + 311.7f) * 43758.5453f, 1f);
    }
}
