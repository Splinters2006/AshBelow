using UnityEngine;

namespace Slopgame
{
    public sealed class MainMenu : MonoBehaviour
    {
        private const int AshMotes = 70;
        private static readonly Color Ember = new Color(1f, 0.46f, 0.2f);

        public DungeonRun Run { get; set; }
        private bool selecting, shopping, coop, settings, codex;
        private Vector2 heroScroll;
        private CharacterDefinition lockedCharacter;
        private string specimenPin = "";
        private bool incorrectPin;

        public void RequestUnlock(CharacterDefinition character)
        {
            lockedCharacter = character;
            specimenPin = "";
            incorrectPin = false;
        }
        // Leaving a party asks for a second click so a stray press does not drop you out of the lobby.
        private float leaveConfirmUntil;
        private Texture2D gradient, glow;
        private readonly AshShop shop = new AshShop();
        private readonly CoopMenu coopMenu = new CoopMenu();
        private readonly SettingsMenu settingsMenu = new SettingsMenu();
        private readonly Encyclopedia encyclopedia = new Encyclopedia();
        public void ResetPage(bool showCoop = false) { lockedCharacter = null; specimenPin = ""; selecting = false; shopping = false; settings = false; codex = false; coop = showCoop; leaveConfirmUntil = 0f; settingsMenu.Cancel(); }
        public void ShowCoop() => ResetPage(true);

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
                if (lockedCharacter != null) { DrawSpecimenLock(); return; }
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
            string title = settings ? "SETTINGS" : shopping ? "ASH SHOP" : codex ? "ENCYCLOPEDIA" : coop ? "CO-OP" : "ASH / BELOW";
            // The encyclopedia and settings pages need the room, so they get a smaller title.
            bool compact = settings || codex;
            int size = compact ? 40 : 66;
            float y = compact ? 80 : 88, height = compact ? 56 : 94, glow = compact ? 1.5f : 2f;
            // Soft ember glow and a hard drop shadow give the title some depth.
            float flicker = 0.1f + Mathf.PerlinNoise(t * 1.4f, 0.3f) * 0.12f;
            DungeonUi.Label(new Rect(65 - glow, y - glow, 1100, height), title, size, new Color(Ember.r, Ember.g, Ember.b, flicker));
            DungeonUi.Label(new Rect(65 + glow, y + glow, 1100, height), title, size, new Color(Ember.r, Ember.g, Ember.b, flicker));
            DungeonUi.Label(new Rect(65 + glow * 2, y + glow * 2.5f, 1100, height), title, size, new Color(0f, 0f, 0f, 0.6f));
            DungeonUi.Label(new Rect(65, y, 1100, height), title, size);
            DungeonUi.Label(compact ? new Rect(70, 138, 1100, 30) : new Rect(70, 188, 1100, 42), settings ? "Resize the menus and HUD, fade the HUD, toggle autofire and rebind every action. Changes save instantly."
                : shopping ? "Spend the ash you carry home. Grow stronger with every descent."
                : codex ? "Everything you have met in the ash. Unfound entries stay hidden until a descent turns them up."
                : coop ? "Descend with up to three friends. Fallen heroes rise again on the next floor."
                : selecting ? "Choose your hero. Shape your build. Claim the relics below." : "Nine heroes. Two relic abilities. One life in the ash.", compact ? 17 : 20, DungeonUi.Muted);

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
            DungeonUi.Panel(new Rect(70, 262, 610, 190), DungeonUi.PanelColor);
            DungeonUi.Panel(new Rect(70, 262, 4, 190), DungeonUi.Teal);
            DungeonUi.Label(new Rect(102, 286, 530, 40), "POWER HAS A PRICE", 27, DungeonUi.Teal);
            DungeonUi.Label(new Rect(102, 336, 550, 110), "Charge your attacks. Read the enemy.\nEvery fifth floor, face an arena guardian.\nTake its artifact and choose your own power.", 20, DungeonUi.Muted);
            DrawHeroParade(new Rect(70, 470, 610, 116));

            DungeonUi.Label(new Rect(755, 244, 420, 20), "MENU", 12, DungeonUi.Muted);
            if (DungeonUi.Button("chooseClass", new Rect(755, 266, 420, 64), "Choose your hero", AbilityCatalog.Gold)) selecting = true;
            if (DungeonUi.Button("coop", new Rect(755, 342, 205, 48), "Co-op", AbilityCatalog.Gold)) coop = true;
            if (DungeonUi.Button("encyclopedia", new Rect(970, 342, 205, 48), "Encyclopedia", AbilityCatalog.Gold)) codex = true;
            if (DungeonUi.Button("shop", new Rect(755, 402, 205, 48), "Ash shop", DungeonUi.Teal)) shopping = true;
            if (DungeonUi.Button("settings", new Rect(970, 402, 205, 48), "Settings", DungeonUi.Teal)) settings = true;
            if (DungeonUi.Button("quit", new Rect(755, 462, 420, 48), "Quit", DungeonUi.Muted))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            if (DungeonUi.Button("debugMode", new Rect(755, 548, 420, 38),
                DebugMode.Enabled ? "Debug admin mode: ON  (F1)" : "Debug admin mode: OFF  (F1)",
                DebugMode.Enabled ? DungeonHud.DebugColor : DungeonUi.Muted)) DebugMode.Toggle();
        }

        /// <summary>The roster standing in a bobbing line; clicking a hero jumps straight to them on the selection page.</summary>
        private void DrawHeroParade(Rect area)
        {
            DungeonUi.Panel(area, new Color(DungeonUi.PanelColor.r, DungeonUi.PanelColor.g, DungeonUi.PanelColor.b, 0.6f));
            int count = Run.Characters.Count;
            if (count == 0) return;
            float slot = Mathf.Min(64f, (area.width - 24f) / count);
            float startX = area.x + (area.width - slot * count) / 2f;
            string hoveredName = null;
            Color hoveredColor = DungeonUi.Muted;
            for (int i = 0; i < count; i++)
            {
                var hero = Run.Characters[i];
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
            int count = Run.Characters.Count;
            heroScroll = GUI.BeginScrollView(new Rect(70, 270, 292, 312), heroScroll, new Rect(0, 0, 268, Mathf.Max(312, count * 46 - 6)));
            for (int i = 0; i < count; i++)
            {
                var hero = Run.Characters[i];
                Rect rect = new Rect(0, i * 46, 268, 40);
                bool selected = hero == Run.SelectedCharacter;
                if (DungeonUi.Button("class" + i, rect, hero.DisplayName + (Run.IsCharacterLocked(hero) ? "  /  LOCKED" : selected ? "  /  SELECTED" : ""), selected ? hero.Color : DungeonUi.Muted)) Run.SelectCharacter(hero);
            }
            GUI.EndScrollView();
            var character = Run.SelectedCharacter;
            var permanent = new PermanentBonuses(Run.Progress, character.Weapon);
            DungeonUi.Panel(new Rect(368, 268, 842, 312), DungeonUi.PanelColor);
            DungeonUi.Panel(new Rect(368, 268, 4, 312), character.Color);
            if (Event.current.type == EventType.Repaint)
            {
                Color previousColor = GUI.color;
                GUI.color = new Color(character.Color.r, character.Color.g, character.Color.b, 0.18f);
                GUI.DrawTexture(new Rect(344, 244, 220, 220), glow);
                GUI.color = previousColor;
            }
            DungeonUi.Panel(new Rect(402, 302, 104, 104), new Color(character.Color.r * 0.25f, character.Color.g * 0.25f, character.Color.b * 0.25f));
            float bob = Mathf.Sin(Time.unscaledTime * 2f) * 2f;
            DrawPortrait(new Rect(412, 312 + bob, 84, 84), character);
            DungeonUi.Label(new Rect(536, 300, 630, 52), character.DisplayName, 38, character.Color);
            DrawStat(new Rect(538, 360, 120, 36), "HP", character.StartingHealth + permanent.Health);
            DrawStat(new Rect(666, 360, 120, 36), "DAMAGE", character.StartingDamage + permanent.Damage);
            DrawStat(new Rect(794, 360, 120, 36), "SPEED", character.MoveSpeed + permanent.Speed);
            DungeonUi.Label(new Rect(402, 434, 766, 80), character.Description, 20);
            DungeonUi.Label(new Rect(402, 535, 766, 28), $"{KeyBindings.Label(GameAction.Special)}  {DungeonUi.SpecialName(character.Weapon)}     /     {KeyBindings.Label(GameAction.AbilityQ)} + {KeyBindings.Label(GameAction.AbilityE)} unlock from boss artifacts", 16, character.Color);
            if (DungeonUi.Button("back", new Rect(70, 598, 268, 48), "Back", DungeonUi.Muted)) selecting = false;
            if (DungeonUi.Button("begin", new Rect(860, 598, 350, 48), "Begin descent", character.Color)) Run.Restart();
        }

        private void DrawSpecimenLock()
        {
            DungeonUi.Panel(new Rect(360, 230, 560, 300), DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(400, 254, 480, 48), "SPECIMEN / CONTAINED", 28, DungeonUi.Teal);
            DungeonUi.Label(new Rect(400, 310, 480, 36), incorrectPin ? "Incorrect PIN. Try again." : "Enter the four-digit containment PIN.", 18, incorrectPin ? AbilityCatalog.Gold : DungeonUi.Muted);
            specimenPin = DungeonUi.TextField("specimenPin", new Rect(400, 360, 480, 46), specimenPin, 4, 26);
            bool submit = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            bool cancel = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;
            if (submit || cancel) Event.current.Use();
            if (DungeonUi.Button("specimenCancel", new Rect(400, 444, 220, 48), "Back", DungeonUi.Muted) || cancel)
            {
                lockedCharacter = null;
                specimenPin = "";
                GUI.FocusControl(null);
                return;
            }
            if (DungeonUi.Button("specimenUnlock", new Rect(640, 444, 240, 48), "Release specimen", DungeonUi.Teal) || submit)
            {
                if (!Run.UnlockSpecimen(specimenPin)) { incorrectPin = true; specimenPin = ""; return; }
                Run.SelectCharacter(lockedCharacter);
                if (Run.Coop.Session.State == NetState.Lobby)
                    for (int i = 0; i < Run.Characters.Count; i++)
                        if (Run.Characters[i] == lockedCharacter) Run.Coop.Session.SetLocalClass(i);
                lockedCharacter = null;
                specimenPin = "";
                GUI.FocusControl(null);
            }
        }

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
