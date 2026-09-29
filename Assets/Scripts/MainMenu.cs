using UnityEngine;

namespace Slopgame
{
    public sealed class MainMenu : MonoBehaviour
    {
        public DungeonRun Run { get; set; }
        private bool selecting, shopping, coop, controls;
        private Vector2 heroScroll;
        private readonly AshShop shop = new AshShop();
        private readonly CoopMenu coopMenu = new CoopMenu();
        private readonly KeybindMenu keybindMenu = new KeybindMenu();
        public void ResetPage(bool showCoop = false) { selecting = false; shopping = false; controls = false; coop = showCoop; keybindMenu.Cancel(); }
        public void ShowCoop() => ResetPage(true);

        private void OnGUI()
        {
            if (Run == null || !Run.IsInMainMenu) return;
            Matrix4x4 previous = DungeonUi.Begin();
            try
            {
                DungeonUi.Panel(new Rect(-2000, -2000, 6000, 6000), DungeonUi.Background);
                for (int i = 0; i < 8; i++)
                {
                    float offset = Mathf.Sin(Time.unscaledTime * 0.2f + i) * 20f;
                    DungeonUi.Panel(new Rect(700 + i * 45 + offset, 70 + i * 65, 130, 2), new Color(0.21f, 0.3f, 0.33f, 0.22f));
                }
                DungeonUi.Label(new Rect(70, 52, 900, 25), "A ROGUELIKE DESCENT", 14, AbilityCatalog.Gold);
                DungeonUi.Label(new Rect(65, 88, 1100, 94), shopping ? "ASH SHOP" : controls ? "CONTROLS" : coop ? "CO-OP" : "ASH / BELOW", 66);
                DungeonUi.Label(new Rect(70, 188, 1100, 42), shopping ? "Spend the ash you carry home. Grow stronger with every descent."
                    : controls ? "Rebind every action to the keys and mouse buttons you like. Changes save instantly."
                    : coop ? "Descend with up to three friends. Fallen heroes rise again on the next floor."
                    : selecting ? "Choose your hero. Shape your build. Claim the relics below." : "Nine heroes. Two relic abilities. One life in the ash.", 20, DungeonUi.Muted);
                DungeonUi.Label(new Rect(930, 55, 280, 32), Run.Progress.IsReadOnly ? "SAVE UNAVAILABLE" : $"{Run.Progress.Ash} ASH", 23, AbilityCatalog.Gold, TextAnchor.UpperRight);
                if (shopping)
                {
                    shop.Draw(Run);
                    if (DungeonUi.Button("shopBack", new Rect(70, 598, 268, 48), "Back", DungeonUi.Muted)) shopping = false;
                }
                else if (controls)
                {
                    keybindMenu.Draw();
                    if (DungeonUi.Button("controlsBack", new Rect(70, 598, 268, 48), "Back", DungeonUi.Muted)) { keybindMenu.Cancel(); controls = false; }
                    if (DungeonUi.Button("controlsReset", new Rect(860, 598, 350, 48), "Reset to defaults", DungeonUi.Teal)) { keybindMenu.Cancel(); KeyBindings.ResetToDefaults(); }
                }
                else if (coop)
                {
                    coopMenu.Draw(Run);
                    bool inParty = Run.Coop.Session.State != NetState.Offline;
                    if (DungeonUi.Button("coopBack", new Rect(70, 598, 268, 48), inParty ? "Leave party" : "Back", DungeonUi.Muted))
                    {
                        CoopMenu.Leave(Run);
                        if (!inParty) coop = false;
                    }
                }
                else if (!selecting)
                {
                    DungeonUi.Panel(new Rect(70, 275, 610, 258), DungeonUi.PanelColor);
                    DungeonUi.Label(new Rect(102, 307, 530, 55), "POWER HAS A PRICE", 29, DungeonUi.Teal);
                    DungeonUi.Label(new Rect(102, 374, 530, 130), "Charge your attacks. Read the enemy.\nEvery fifth floor, face an arena guardian.\nTake its artifact and choose your own power.", 22, DungeonUi.Muted);
                    if (DungeonUi.Button("chooseClass", new Rect(755, 290, 420, 60), "Choose your hero", AbilityCatalog.Gold)) selecting = true;
                    if (DungeonUi.Button("coop", new Rect(755, 362, 420, 48), "Co-op", AbilityCatalog.Gold)) coop = true;
                    if (DungeonUi.Button("shop", new Rect(755, 422, 205, 48), "Ash shop", DungeonUi.Teal)) shopping = true;
                    if (DungeonUi.Button("controls", new Rect(970, 422, 205, 48), "Controls", DungeonUi.Teal)) controls = true;
                    if (DungeonUi.Button("debugMode", new Rect(755, 545, 420, 42),
                        DebugMode.Enabled ? "Debug admin mode: ON  (F1)" : "Debug admin mode: OFF  (F1)",
                        DebugMode.Enabled ? DungeonHud.DebugColor : DungeonUi.Muted)) DebugMode.Toggle();
                    if (DungeonUi.Button("quit", new Rect(755, 482, 420, 48), "Quit", DungeonUi.Muted))
                    {
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#else
                        Application.Quit();
#endif
                    }
                }
                else DrawSelection();
                if (!string.IsNullOrEmpty(Run.Progress.LastError))
                    DungeonUi.Label(new Rect(70, 645, 1140, 24), Run.Progress.LastError, 14, AbilityCatalog.Gold);
                DungeonUi.Label(new Rect(70, 672, 1100, 24), $"{KeyBindings.MovementLabel()}  move     HOLD / RELEASE {KeyBindings.Label(GameAction.Attack)}  attack     "
                    + $"{KeyBindings.Label(GameAction.Special)}  class skill     {KeyBindings.Label(GameAction.AbilityQ)} / {KeyBindings.Label(GameAction.AbilityE)}  relic abilities", 14, DungeonUi.Muted);
            }
            finally { GUI.matrix = previous; }
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
                if (DungeonUi.Button("class" + i, rect, hero.DisplayName + (selected ? "  /  SELECTED" : ""), selected ? hero.Color : DungeonUi.Muted)) Run.SelectCharacter(hero);
            }
            GUI.EndScrollView();
            var character = Run.SelectedCharacter;
            var permanent = new PermanentBonuses(Run.Progress, character.Weapon);
            DungeonUi.Panel(new Rect(368, 268, 842, 312), DungeonUi.PanelColor);
            DungeonUi.Panel(new Rect(402, 302, 104, 104), new Color(character.Color.r * 0.25f, character.Color.g * 0.25f, character.Color.b * 0.25f));
            string glyph = character.Weapon == WeaponType.Shadow ? "///" : character.Weapon == WeaponType.Staff ? "*" : character.Weapon == WeaponType.Bow ? ">" : character.Weapon == WeaponType.Daggers ? "//" : character.Weapon == WeaponType.Fists ? "[]" : character.Weapon == WeaponType.Tail ? "~>" : character.Weapon == WeaponType.Coins ? "$" : character.Weapon == WeaponType.Beam ? "=>" : "+";
            var portrait = character.Weapon == WeaponType.Shadow ? DungeonVisuals.ShadowHeroSprite : HeroSprites.Body(character.Weapon);
            if (portrait != null)
            {
                // Pixel-art portrait: tinted body layer, then the fixed-colour details on top.
                Rect frame = new Rect(412, 312, 84, 84);
                Color previousColor = GUI.color;
                GUI.color = character.Color;
                GUI.DrawTexture(frame, portrait.texture, ScaleMode.ScaleToFit, true);
                var details = HeroSprites.Accent(character.Weapon);
                GUI.color = Color.white;
                if (details != null) GUI.DrawTexture(frame, details.texture, ScaleMode.ScaleToFit, true);
                GUI.color = previousColor;
            }
            else DungeonUi.Label(new Rect(402, 302, 104, 104), glyph, 58, character.Color, TextAnchor.MiddleCenter);
            DungeonUi.Label(new Rect(536, 300, 630, 52), character.DisplayName, 38, character.Color);
            DungeonUi.Label(new Rect(538, 361, 610, 32), $"{character.StartingHealth + permanent.Health} HP     {character.StartingDamage + permanent.Damage} DAMAGE     {character.MoveSpeed + permanent.Speed:0.#} SPEED", 16, DungeonUi.Muted);
            DungeonUi.Label(new Rect(402, 434, 766, 80), character.Description, 20);
            DungeonUi.Label(new Rect(402, 535, 766, 28), $"{KeyBindings.Label(GameAction.Special)}  {DungeonUi.SpecialName(character.Weapon)}     /     {KeyBindings.Label(GameAction.AbilityQ)} + {KeyBindings.Label(GameAction.AbilityE)} unlock from boss artifacts", 16, character.Color);
            if (DungeonUi.Button("back", new Rect(70, 598, 268, 48), "Back", DungeonUi.Muted)) selecting = false;
            if (DungeonUi.Button("begin", new Rect(860, 598, 350, 48), "Begin descent", character.Color)) Run.Restart();
        }
    }
}
