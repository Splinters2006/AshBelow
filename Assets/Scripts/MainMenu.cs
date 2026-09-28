using UnityEngine;

namespace Slopgame
{
    public sealed class MainMenu : MonoBehaviour
    {
        public DungeonRun Run { get; set; }
        private bool selecting;
        private Vector2 scroll;
        private GUIStyle title, heading, text, button;

        public void ResetPage() { selecting = false; }

        private void OnGUI()
        {
            if (Run == null || !Run.IsInMainMenu) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 52, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                title.normal.textColor = new Color(0.4f, 1f, 0.85f);
                heading = new GUIStyle(title) { fontSize = 28, alignment = TextAnchor.MiddleLeft };
                text = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { fontSize = 22 };
            }
            float scale = Mathf.Min(Screen.width / 960f, Screen.height / 640f);
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 960 * scale) / 2, (Screen.height - 640 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            GUI.Label(new Rect(60, 35, 840, 90), "ASH / BELOW", title);
            if (!selecting)
            {
                GUI.Label(new Rect(285, 165, 420, 70), "Descend into the ash. Choose your hero.\nSee how far one life will take you.", text);
                if (GUI.Button(new Rect(290, 280, 380, 65), "Choose character", button)) selecting = true;
                if (GUI.Button(new Rect(290, 365, 380, 65), "Quit", button))
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }
                GUI.Label(new Rect(290, 490, 400, 40), "A roguelike dungeon crawler", text);
            }
            else
            {
                GUI.Label(new Rect(60, 140, 500, 50), "CHOOSE YOUR CHARACTER", heading);
                GUI.Box(new Rect(50, 205, 290, 295), GUIContent.none);
                scroll = GUI.BeginScrollView(new Rect(60, 215, 270, 275), scroll,
                    new Rect(0, 0, 245, Mathf.Max(270, Run.Characters.Count * 75 + 75)));
                for (int i = 0; i < Run.Characters.Count; i++)
                {
                    var character = Run.Characters[i];
                    string label = character.DisplayName + (Run.SelectedCharacter == character ? "  [Selected]" : "");
                    if (GUI.Button(new Rect(0, i * 75, 245, 60), label, button)) Run.SelectCharacter(character);
                }
                GUI.enabled = false;
                GUI.Button(new Rect(0, Run.Characters.Count * 75, 245, 60), "More heroes soon", text);
                GUI.enabled = true;
                GUI.EndScrollView();
                var selected = Run.SelectedCharacter;
                GUI.Box(new Rect(365, 205, 545, 295), GUIContent.none);
                var oldColor = GUI.color;
                GUI.color = selected.Color;
                GUI.DrawTexture(new Rect(390, 230, 54, 64), Texture2D.whiteTexture);
                GUI.color = oldColor;
                GUI.Label(new Rect(465, 225, 420, 50), selected.DisplayName, heading);
                GUI.Label(new Rect(465, 278, 420, 45), $"Health {selected.StartingHealth}    Damage {selected.StartingDamage}    Speed {selected.MoveSpeed:0.#}", text);
                GUI.Label(new Rect(390, 340, 495, 95), selected.Description, text);
                GUI.Label(new Rect(390, 440, 495, 55), selected.Weapon == WeaponType.Bow ? "Hold left: charge arrow (5-unit range)\nRight: triple shot (6s)" : "Hold left: charge a wider slash\nRight: reflect shield (2.8s)", text);
                if (GUI.Button(new Rect(50, 530, 220, 60), "Back", button)) selecting = false;
                if (GUI.Button(new Rect(570, 530, 340, 60), "Begin run", button)) Run.Restart();
            }
            GUI.matrix = previous;
        }
    }
}
