using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The main menu's encyclopedia: every hero, talent, ability, guardian and world, revealed once it has turned up in
    /// any descent. Discoveries are saved in <see cref="PermanentProgress"/> under the stable ids built here. Debug admin
    /// mode (F1) reveals every entry for developers without writing anything to the save.
    /// </summary>
    public sealed class Encyclopedia
    {
        public static string HeroId(WeaponType weapon) => "hero:" + weapon;
        public static string TalentId(PowerupType type) => "talent:" + type;
        public static string AbilityId(AbilityType type) => "ability:" + type;
        public static string GuardianId(string title) => "guardian:" + title;
        public static string WorldId(int index) => "world:" + index;

        private enum Page { Heroes, Talents, Abilities, Guardians, Worlds }
        private static readonly string[] PageNames = { "Heroes", "Talents", "Abilities", "Guardians", "Worlds" };

        private struct Entry
        {
            public bool Found;
            public string Name, Tag, Description, Glyph;
            public Color Color;
        }

        private const float RowHeight = 92f, CardWidth = 549f;
        // Ten heroes share the filter row, so its labels are smaller than a normal button's (long names like Demoness wrapped).
        private const int FilterFont = 15;
        private const string Separator = "  ·  ";
        private Page page;
        // Talents and abilities can be narrowed to one hero. With filterAll off, a null hero means the general talents
        // any hero can take (they get their own section so they don't crowd the top of every hero's list).
        private WeaponType? heroFilter;
        private bool filterAll = true;
        private bool GeneralOnly => !filterAll && !heroFilter.HasValue;
        private Vector2 scroll;
        private readonly List<Entry> entries = new List<Entry>();

        public void Draw(DungeonRun run)
        {
            float step = 1152f / PageNames.Length;
            for (int i = 0; i < PageNames.Length; i++)
                if (DungeonUi.Button("codexTab" + i, new Rect(70 + i * step, 180, step - 12, 40), PageNames[i], (int)page == i ? AbilityCatalog.Gold : DungeonUi.Muted))
                {
                    page = (Page)i;
                    scroll = Vector2.zero;
                    // Abilities all belong to a hero, so the general section only exists for talents.
                    if (page != Page.Talents && GeneralOnly) filterAll = true;
                }

            bool filtered = page == Page.Talents || page == Page.Abilities;
            float listTop = 234;
            if (filtered)
            {
                DrawHeroFilter(run, new Rect(70, 232, 1140, 32));
                listTop = 276;
            }
            Build(run);
            bool revealAll = DebugMode.Enabled;
            if (revealAll)
                for (int i = 0; i < entries.Count; i++) { var entry = entries[i]; entry.Found = true; entries[i] = entry; }
            int found = 0;
            foreach (var entry in entries) if (entry.Found) found++;

            int rows = (entries.Count + 1) / 2;
            var area = new Rect(70, listTop, 1140, 586 - listTop);
            scroll = GUI.BeginScrollView(area, scroll, new Rect(0, 0, 1116, Mathf.Max(area.height - 4, rows * RowHeight)));
            for (int i = 0; i < entries.Count; i++)
                DrawEntry(new Rect(i % 2 * (CardWidth + 12), i / 2 * RowHeight, CardWidth, RowHeight - 10), entries[i]);
            if (entries.Count == 0) DungeonUi.Label(new Rect(0, 0, 1100, 40), "Nothing here for this hero.", 17, DungeonUi.Muted);
            GUI.EndScrollView();

            if (revealAll)
                DungeonUi.Label(new Rect(365, 608, 845, 30), "DEBUG ADMIN MODE  /  EVERY ENTRY REVEALED  /  F1 TO TURN OFF", 17, DungeonHud.DebugColor);
            else
                DungeonUi.Label(new Rect(365, 608, 845, 30), $"{PageNames[(int)page].ToUpperInvariant()}  {found} / {entries.Count} DISCOVERED", 17,
                    found == entries.Count && entries.Count > 0 ? AbilityCatalog.Gold : DungeonUi.Muted);
        }

        private void DrawHeroFilter(DungeonRun run, Rect row)
        {
            // "All" and "General" are short, so they get narrow buttons and the heroes share what is left.
            float x = row.x;
            if (DungeonUi.Button("codexAll", new Rect(x, row.y, 64, row.height), "All", filterAll ? DungeonUi.Teal : DungeonUi.Muted, size: FilterFont))
            { filterAll = true; heroFilter = null; scroll = Vector2.zero; }
            x += 72;
            if (page == Page.Talents)
            {
                if (DungeonUi.Button("codexGeneral", new Rect(x, row.y, 96, row.height), "General", GeneralOnly ? DungeonUi.Teal : DungeonUi.Muted, size: FilterFont))
                { filterAll = false; heroFilter = null; scroll = Vector2.zero; }
                x += 104;
            }
            float step = (row.xMax - x + 8) / Mathf.Max(1, run.Characters.Count);
            for (int i = 0; i < run.Characters.Count; i++)
            {
                var hero = run.Characters[i];
                bool selected = !filterAll && heroFilter == hero.Weapon;
                if (DungeonUi.Button("codexHero" + i, new Rect(x + i * step, row.y, step - 8, row.height), hero.DisplayName, selected ? hero.Color : DungeonUi.Muted, size: FilterFont))
                { filterAll = false; heroFilter = hero.Weapon; scroll = Vector2.zero; }
            }
        }

        private static void DrawEntry(Rect rect, Entry entry)
        {
            Color color = entry.Found ? entry.Color : DungeonUi.Muted;
            DungeonUi.Panel(rect, entry.Found ? DungeonUi.PanelColor : new Color(DungeonUi.PanelColor.r, DungeonUi.PanelColor.g, DungeonUi.PanelColor.b, 0.5f));
            DungeonUi.Panel(new Rect(rect.x, rect.y, 4, rect.height), entry.Found ? color : new Color(color.r, color.g, color.b, 0.3f));
            var badge = new Rect(rect.x + 16, rect.y + 14, 54, 54);
            DungeonUi.Panel(badge, new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f, entry.Found ? 1f : 0.5f));
            DungeonUi.Label(badge, entry.Found ? entry.Glyph : "?", entry.Found ? 22 : 26, entry.Found ? color : DungeonUi.Muted * 0.7f, TextAnchor.MiddleCenter);
            DungeonUi.Label(new Rect(rect.x + 86, rect.y + 10, 280, 24), entry.Found ? entry.Name : "???", 19, entry.Found ? DungeonUi.Text : DungeonUi.Muted);
            DungeonUi.Label(new Rect(rect.x + 336, rect.y + 13, rect.width - 350, 20), entry.Tag, 12, color, TextAnchor.UpperRight);
            DungeonUi.ScrollingText("codex" + entry.Tag + entry.Name, new Rect(rect.x + 86, rect.y + 38, rect.width - 100, rect.height - 42),
                entry.Found ? entry.Description : "Not yet found. Keep descending.", 14, DungeonUi.Muted);
        }

        private void Build(DungeonRun run)
        {
            entries.Clear();
            var progress = run.Progress;
            switch (page)
            {
                case Page.Heroes:
                    foreach (var hero in run.Characters)
                        entries.Add(new Entry
                        {
                            // Heroes who beat a guardian before the encyclopedia existed count as played.
                            Found = progress.IsDiscovered(HeroId(hero.Weapon)) || progress.GuardiansDefeatedAs(hero.Weapon) > 0,
                            Name = hero.DisplayName, Tag = $"{hero.StartingHealth} HP{Separator}{hero.StartingDamage} DMG{Separator}{hero.MoveSpeed:0.#} SPEED",
                            Description = hero.Description, Glyph = GlyphFor(hero.Weapon), Color = hero.Color
                        });
                    break;
                case Page.Talents:
                    foreach (var talent in PowerupCatalog.All)
                    {
                        if (!filterAll && talent.ClassWeapon != heroFilter) continue;
                        var tag = new List<string>();
                        // The hero is already picked in the filter (and shown by the card colour), so only the full list names it.
                        if (filterAll) tag.Add(talent.ClassWeapon.HasValue ? HeroName(run, talent.ClassWeapon.Value) : "ANY HERO");
                        if (talent.RequiredAbility != AbilityType.None) tag.Add("NEEDS " + AbilityCatalog.Get(talent.RequiredAbility)?.Name.ToUpperInvariant());
                        if (talent.MaxStacks < int.MaxValue && talent.MaxStacks > 1) tag.Add($"MAX {talent.MaxStacks}");
                        entries.Add(new Entry
                        {
                            Found = progress.IsDiscovered(TalentId(talent.Type)), Name = TalentName(talent), Tag = string.Join(Separator, tag),
                            Description = talent.Description,
                            Glyph = talent.ClassWeapon.HasValue ? GlyphFor(talent.ClassWeapon.Value) : "+",
                            Color = talent.ClassWeapon.HasValue ? HeroColor(run, talent.ClassWeapon.Value) : DungeonUi.Teal
                        });
                    }
                    break;
                case Page.Abilities:
                    foreach (var ability in AbilityCatalog.All)
                    {
                        if (!filterAll && ability.ClassWeapon != heroFilter) continue;
                        entries.Add(new Entry
                        {
                            Found = progress.IsDiscovered(AbilityId(ability.Type)), Name = ability.Name,
                            Tag = (filterAll ? HeroName(run, ability.ClassWeapon) + Separator : "") + $"{ability.Cooldown:0.#}s" + (ability.ShopUnlock ? Separator + "ASH SHOP" : ""),
                            Description = ability.Description, Glyph = ability.Glyph, Color = ability.Color
                        });
                    }
                    break;
                case Page.Guardians:
                    BuildGuardians(progress);
                    break;
                case Page.Worlds:
                    foreach (var world in WorldCatalog.All)
                        entries.Add(new Entry
                        {
                            Found = progress.IsDiscovered(WorldId(world.Index)) || progress.HasClearedWorld(world.Index), Name = world.Name,
                            Tag = $"WORLD {world.Index + 1}" + (world.IsWaveWorld ? Separator + "WAVES" : "") + (world.HighTech ? Separator + "HIGH TECH" : ""),
                            Description = $"Home of the {world.BasicName}, the {world.CasterName} and the {world.BruteName}."
                                + (progress.HasClearedWorld(world.Index) ? "  Cleared." : ""),
                            Glyph = (world.Index + 1).ToString(), Color = world.Accent
                        });
                    break;
            }
        }

        /// <summary>Every guardian in world order: three per world, named for the world's theme (a name is listed once).</summary>
        private void BuildGuardians(PermanentProgress progress)
        {
            var listed = new HashSet<string>();
            foreach (var world in WorldCatalog.All)
                for (int i = 0; i < 3; i++)
                {
                    string title = DungeonBoss.TitleFor((BossKind)((int)world.FirstGuardian + i), world.HighTech);
                    if (!listed.Add(title)) continue;
                    entries.Add(new Entry
                    {
                        Found = progress.IsDiscovered(GuardianId(title)), Name = title, Tag = $"WORLD {world.Index + 1}{Separator}GUARDIAN {i + 1}",
                        Description = $"Guards {(world.IsWaveWorld ? "level" : "floor")} {(i + 1) * 5} of {world.Name}.", Glyph = "!", Color = world.Accent
                    });
                }
        }

        /// <summary>Class talents are named "Hero: Talent" for the in-run picker; the card already shows the hero, so drop the prefix.</summary>
        private static string TalentName(PowerupDefinition talent)
        {
            int colon = talent.Name.IndexOf(": ");
            return talent.ClassWeapon.HasValue && colon >= 0 ? talent.Name.Substring(colon + 2) : talent.Name;
        }

        private static string HeroName(DungeonRun run, WeaponType weapon)
        {
            foreach (var hero in run.Characters) if (hero.Weapon == weapon) return hero.DisplayName.ToUpperInvariant();
            return weapon.ToString().ToUpperInvariant();
        }

        private static Color HeroColor(DungeonRun run, WeaponType weapon)
        {
            foreach (var hero in run.Characters) if (hero.Weapon == weapon) return hero.Color;
            return DungeonUi.Teal;
        }

        private static string GlyphFor(WeaponType weapon) => weapon switch
        {
            WeaponType.Shadow => "///", WeaponType.Staff => "*", WeaponType.Bow => ">", WeaponType.Daggers => "//", WeaponType.Fists => "[]",
            WeaponType.Tail => "~>", WeaponType.Coins => "$", WeaponType.Beam => "=>", WeaponType.Hammer => "T", _ => "+"
        };
    }
}
