using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The black iris between the party hall and the dungeon: in the hall's last moments it closes on the portal as the
    /// party is drawn in, and on arriving in a world it opens on the falling hero (see <see cref="HeroArrival"/>). Between
    /// floors it closes on the stairs and opens on the next floor (see <see cref="FloorTransition"/>).
    /// Drawn by <see cref="DungeonHud"/> under the rest of the HUD, so the countdown and the world banner show over it.
    /// </summary>
    public static class DescentIris
    {
        /// <summary>How long before the countdown runs out the hall starts pulling heroes into the portal.</summary>
        public const float PullTime = 1.2f;
        private static readonly Color Dark = new Color(0.008f, 0.012f, 0.024f, 1f);
        private static Texture2D hole;

        public static void Draw(DungeonRun run)
        {
            if (run.View == null || run.Player == null) return;
            float open;
            Vector2 center;
            if (run.IsInLobby && run.Hall != null && run.Coop != null)
            {
                float left = run.Coop.CountdownLeft;
                if (left < 0f || left >= PullTime) return;
                // Shuts a moment before the count ends, and quickest at the last.
                float t = Mathf.Clamp01((left - 0.15f) / (PullTime - 0.15f));
                open = t * t;
                center = run.Hall.Portal;
            }
            else
            {
                float floor = FloorTransition.Openness(out bool onStairs);
                open = Mathf.Min(HeroArrival.LocalOpenness, floor);
                if (open >= 1f) return;
                center = onStairs ? FloorTransition.Focus : (Vector2)run.Player.transform.position;
            }
            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Dark;
            float width = Screen.width, height = Screen.height;
            float radius = Mathf.Sqrt(width * width + height * height) * open;
            Vector3 screen = run.View.WorldToScreenPoint(center);
            Vector2 middle = new Vector2(screen.x, height - screen.y);
            var white = Texture2D.whiteTexture;
            if (radius < 2f) GUI.DrawTexture(new Rect(0, 0, width, height), white);
            else
            {
                var circle = new Rect(middle.x - radius, middle.y - radius, radius * 2f, radius * 2f);
                GUI.DrawTexture(circle, Hole);
                GUI.DrawTexture(new Rect(0, 0, width, Mathf.Max(0f, circle.yMin)), white);
                GUI.DrawTexture(new Rect(0, circle.yMax, width, Mathf.Max(0f, height - circle.yMax)), white);
                GUI.DrawTexture(new Rect(0, circle.yMin, Mathf.Max(0f, circle.xMin), circle.height), white);
                GUI.DrawTexture(new Rect(circle.xMax, circle.yMin, Mathf.Max(0f, width - circle.xMax), circle.height), white);
            }
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        /// <summary>A square of black with a soft-edged clear circle in the middle.</summary>
        private static Texture2D Hole
        {
            get
            {
                if (hole != null) return hole;
                const int Size = 256;
                hole = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var pixels = new Color32[Size * Size];
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float distance = new Vector2(x + 0.5f - Size / 2f, y + 0.5f - Size / 2f).magnitude / Size;
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.44f, 0.5f, distance))));
                }
                hole.SetPixels32(pixels);
                hole.Apply();
                return hole;
            }
        }
    }
}
