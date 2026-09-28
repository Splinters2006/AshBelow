using UnityEngine;

namespace Slopgame
{
    public sealed class DungeonHud : MonoBehaviour
    {
        public DungeonRun Run { get; set; }
        private GUIStyle title, text, button;

        private void OnGUI()
        {
            if (Run == null || Run.IsInMainMenu || Run.Player == null) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
                title.normal.textColor = new Color(0.4f, 1f, 0.85f);
                text = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { fontSize = 18, wordWrap = true };
            }
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * Mathf.Min(Screen.width / 960f, Screen.height / 640f));
            GUI.Box(new Rect(12, 12, 480, 138), GUIContent.none);
            GUI.Label(new Rect(28, 20, 440, 40), "ASH / BELOW", title);
            if (GUI.Button(new Rect(785, 20, 150, 40), "Main menu", button)) { Run.ShowMainMenu(); return; }
            GUI.Label(new Rect(28, 64, 445, 30), $"Floor {Run.Floor}    HP {Run.Player.Health}/{Run.Player.MaxHealth}    Enemies {Run.Enemies.Count}", text);
            GUI.Label(new Rect(28, 98, 445, 44), Run.Enemies.Count == 0
                ? "Find the gold stairs. Press E to descend."
                : "Clear the floor to unlock the stairs.", text);
            GUI.Box(new Rect(12, 560, 930, 68), GUIContent.none);
            string heavyStatus = Run.Player.Sword == null || Run.Player.Sword.HeavyCooldownRemaining <= 0
                ? "READY" : $"{Run.Player.Sword.HeavyCooldownRemaining:0.0}s";
            GUI.Label(new Rect(28, 568, 900, 52), $"WASD / Arrows: move    Mouse: aim    Left click: slash    Space: dodge    E: descend\nRight click: heavy swipe [{heavyStatus}]    Orange casters flash white before shooting.", text);
            if (Run.IsPlaying) return;
            GUI.Box(new Rect(220, 170, 520, 385), GUIContent.none);
            if (Run.ChoosingUpgrade)
            {
                GUI.Label(new Rect(250, 190, 465, 42), "A MOMENT OF RESPITE", title);
                GUI.Label(new Rect(250, 240, 465, 54), "Choose a boon. Every choice restores 2 HP.", text);
                if (GUI.Button(new Rect(250, 304, 460, 60), "Sharpen blade  /  +1 damage", button)) Run.ChooseUpgrade(0);
                if (GUI.Button(new Rect(250, 376, 460, 60), "Renew vitality  /  +2 max HP, full heal", button)) Run.ChooseUpgrade(1);
                if (GUI.Button(new Rect(250, 448, 460, 60), "Lighten step  /  +movement speed", button)) Run.ChooseUpgrade(2);
            }
            else
            {
                GUI.Label(new Rect(250, 198, 465, 42), "THE ASH TAKES YOU", title);
                GUI.Label(new Rect(250, 265, 460, 80), $"You reached floor {Run.Floor} and defeated {Run.Kills} ashling(s).\nRun seed: {Run.Seed}", text);
                if (GUI.Button(new Rect(250, 400, 460, 70), "Begin a new run", button)) Run.Restart();
            }
        }
    }
}
