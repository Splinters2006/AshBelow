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
            GUI.Label(new Rect(510, 75, 425, 125), $"Ward: {Run.Player.Powerups.ArmorCharges}    Crit: {Run.Player.Powerups.CritChance:P0}\n{Run.Player.Powerups.Summary}", text);
            GUI.Box(new Rect(12, 532, 930, 96), GUIContent.none);
            string heavyStatus = Run.Player.Weapon == null || Run.Player.Weapon.HeavyCooldownRemaining <= 0
                ? "READY" : $"{Run.Player.Weapon.HeavyCooldownRemaining:0.0}s";
            string special = Run.Player.ClassWeapon == WeaponType.Sword ? "Shield / reflect" : "Triple shot";
            string dodge = Run.Player.DodgeCooldownRemaining <= 0f ? "READY" : $"{Run.Player.DodgeCooldownRemaining:0.0}s";
            string charge = Run.Player.Charge.IsCharging ? $"Charge: {Run.Player.Charge.Amount:P0}  Damage: {Run.Player.Charge.Damage(Run.Player.Charge.Amount)}" : "Hold left click to charge; release to attack";
            GUI.Label(new Rect(28, 540, 900, 78), $"WASD: move    Mouse: aim    Space: dodge [{dodge}]    E: descend\n{charge}\nRight click: {special} [{heavyStatus}]", text);
            if (Run.Player.Charge.IsCharging)
            {
                GUI.Box(new Rect(330, 502, 300, 18), GUIContent.none);
                Color previousColor = GUI.color;
                GUI.color = Run.Player.Charge.Amount >= 1f ? new Color(1f, 0.8f, 0.25f) : new Color(0.4f, 1f, 0.85f);
                GUI.DrawTexture(new Rect(333, 505, 294 * Run.Player.Charge.Amount, 12), Texture2D.whiteTexture);
                GUI.color = previousColor;
            }
            if (Run.IsPlaying) return;
            GUI.Box(new Rect(220, 170, 520, 385), GUIContent.none);
            if (Run.ChoosingUpgrade)
            {
                GUI.Label(new Rect(250, 190, 465, 42), "A MOMENT OF RESPITE", title);
                GUI.Label(new Rect(250, 240, 465, 54), "Choose a boon. Every choice restores 2 HP.", text);
                for (int i = 0; i < Run.UpgradeChoices.Count; i++)
                {
                    var boon = Run.UpgradeChoices[i];
                    int rank = Run.Player.Powerups.Count(boon.Type) + 1;
                    if (GUI.Button(new Rect(250, 304 + i * 72, 460, 60), $"{boon.Name} (rank {rank})\n{boon.Description}", button))
                    {
                        Run.ChooseUpgrade(i);
                        break;
                    }
                }
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
