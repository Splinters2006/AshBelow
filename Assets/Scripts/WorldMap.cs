using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The travel map: every world as a node on a dotted trail. The main menu uses it to pick where a descent begins,
    /// and the world-cleared screen to pick where to travel next. Node positions come from
    /// <see cref="WorldDefinition.MapPosition"/>, so the layout is tweaked in <see cref="WorldCatalog"/>. Nothing is locked yet.
    /// </summary>
    public sealed class WorldMap
    {
        public const float NodeSize = 64f;
        /// <summary>Room kept around the nodes for their labels, in map pixels.</summary>
        private const float MarginX = 80f, MarginTop = 96f, MarginBottom = 70f;

        /// <summary>The highlighted world; clicking a node selects it.</summary>
        public int Selected { get; set; }

        /// <summary>
        /// Draws the map inside <paramref name="frame"/> (menu coordinates). <paramref name="current"/> marks where the
        /// hero stands, or -1 for none. Clicking a node selects it when <paramref name="interactive"/>.
        /// </summary>
        public void Draw(DungeonRun run, Rect frame, int current, bool interactive)
        {
            var worlds = WorldCatalog.All;
            Selected = Mathf.Clamp(Selected, 0, worlds.Length - 1);
            DungeonUi.Panel(frame, DungeonUi.PanelColor);
            DrawTrail(frame);
            for (int i = 0; i < worlds.Length; i++)
            {
                var world = worlds[i];
                Vector2 center = NodeCenter(frame, world);
                var node = new Rect(center.x - NodeSize / 2f, center.y - NodeSize / 2f, NodeSize, NodeSize);
                if (i == Selected) DungeonUi.Panel(Grow(node, 7f + 2f * Mathf.Sin(Time.unscaledTime * 4f)), FlameMesh.Alpha(AbilityCatalog.Gold, 0.9f));
                DungeonUi.Panel(Grow(node, 3f), world.Accent);
                DungeonUi.Panel(node, Color.Lerp(world.FloorB, world.Background, 0.3f));
                DrawEmblem(run, node, world);
                string tag = i == current ? "YOU ARE HERE" : world.IsPlaceholder ? "PLACEHOLDER" : "";
                if (tag.Length > 0)
                    DungeonUi.Label(new Rect(center.x - 80f, node.y - 24f, 160f, 16f), tag, 11,
                        i == current ? DungeonUi.Text : DungeonUi.Muted, TextAnchor.MiddleCenter);
                DungeonUi.Label(new Rect(center.x - 80f, node.yMax + 8f, 160f, 16f), "WORLD " + (i + 1), 11, world.Accent, TextAnchor.MiddleCenter);
                DungeonUi.Label(new Rect(center.x - 90f, node.yMax + 24f, 180f, 34f), world.Name, 13,
                    i == Selected ? DungeonUi.Text : DungeonUi.Muted, TextAnchor.UpperCenter);
                if (interactive && GUI.Button(node, GUIContent.none, GUIStyle.none)) Selected = i;
            }
            DrawDetails(run, frame, worlds[Selected]);
        }

        /// <summary>Where a world's node sits inside <paramref name="frame"/>.</summary>
        public static Vector2 NodeCenter(Rect frame, WorldDefinition world)
        {
            float x = frame.x + MarginX + (frame.width - 2f * MarginX) * Mathf.Clamp01(world.MapPosition.x);
            float y = frame.y + MarginTop + (frame.height - MarginTop - MarginBottom) * Mathf.Clamp01(world.MapPosition.y);
            return new Vector2(x, y);
        }

        /// <summary>A dotted trail from each world to the next, fading from one world's colour into the next's.</summary>
        private static void DrawTrail(Rect frame)
        {
            var worlds = WorldCatalog.All;
            for (int i = 0; i + 1 < worlds.Length; i++)
            {
                Vector2 from = NodeCenter(frame, worlds[i]), to = NodeCenter(frame, worlds[i + 1]);
                int dots = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(from, to) / 14f));
                for (int d = 1; d < dots; d++)
                {
                    float t = d / (float)dots;
                    Vector2 point = Vector2.Lerp(from, to, t);
                    DungeonUi.Panel(new Rect(point.x - 2f, point.y - 2f, 4f, 4f), FlameMesh.Alpha(Color.Lerp(worlds[i].Accent, worlds[i + 1].Accent, t), 0.6f));
                }
            }
        }

        /// <summary>The world's hero portrait in its node, or its number for the starting world.</summary>
        private static void DrawEmblem(DungeonRun run, Rect node, WorldDefinition world)
        {
            var hero = world.Hero.HasValue ? HeroFor(run, world.Hero.Value) : null;
            var body = hero != null ? HeroSprites.Body(hero.Weapon) : null;
            if (body == null)
            {
                DungeonUi.Label(node, (world.Index + 1).ToString(), 28, world.Accent, TextAnchor.MiddleCenter);
                return;
            }
            var frame = Grow(node, -10f);
            Color previous = GUI.color;
            GUI.color = hero.Color;
            GUI.DrawTexture(frame, body.texture, ScaleMode.ScaleToFit, true);
            var details = HeroSprites.Accent(hero.Weapon);
            GUI.color = Color.white;
            if (details != null) GUI.DrawTexture(frame, details.texture, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }

        /// <summary>The selected world's name, hero and floors, in the map's top-left corner.</summary>
        private static void DrawDetails(DungeonRun run, Rect frame, WorldDefinition world)
        {
            var hero = world.Hero.HasValue ? HeroFor(run, world.Hero.Value) : null;
            int first = WorldCatalog.FirstFloor(world.Index);
            string owner = hero != null ? $"{hero.DisplayName}'s world" : "The starting world";
            string status = world.IsPlaceholder ? "Placeholder: the Ash Below's enemies and guardians for now" : "Three guardians await";
            string layout = world.IsWaveWorld ? $"{WorldCatalog.FloorsPerWorld} levels of enemy waves, a guardian every 5" : $"floors {first}-{first + WorldCatalog.FloorsPerWorld - 1}";
            DungeonUi.Label(new Rect(frame.x + 24f, frame.y + 16f, frame.width - 48f, 30f), world.Name, 24, world.Accent);
            DungeonUi.Label(new Rect(frame.x + 24f, frame.y + 48f, frame.width - 48f, 22f), $"{owner}  /  {layout}  /  {status}", 14, DungeonUi.Muted);
        }

        private static CharacterDefinition HeroFor(DungeonRun run, WeaponType weapon)
        {
            if (run == null || run.Characters == null) return null;
            foreach (var character in run.Characters) if (character.Weapon == weapon) return character;
            return null;
        }

        private static Rect Grow(Rect rect, float amount) => new Rect(rect.x - amount, rect.y - amount, rect.width + 2f * amount, rect.height + 2f * amount);
    }
}
