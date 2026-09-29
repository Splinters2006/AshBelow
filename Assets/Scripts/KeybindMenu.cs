using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The controls page of the main menu. Click a binding, then press a key or mouse button to rebind it
    /// (clicking the highlighted binding itself binds that mouse button). ESC cancels; DELETE or BACKSPACE clears.
    /// </summary>
    public sealed class KeybindMenu
    {
        private const float Top = 256, RowStep = 52, ColumnStep = 580;
        private int capturingAction = -1, capturingSlot;

        public bool IsCapturing => capturingAction >= 0;
        public void Cancel() => capturingAction = -1;

        private static int PerColumn => (KeyBindings.Actions.Length + 1) / 2;

        private static Rect RowRect(int index) =>
            new Rect(70 + index / PerColumn * ColumnStep, Top + index % PerColumn * RowStep, 560, 46);

        private static Rect SlotRect(int index, int slot)
        {
            Rect row = RowRect(index);
            return new Rect(row.x + 220 + slot * 168, row.y + 5, 158, 36);
        }

        public void Draw()
        {
            if (IsCapturing) Capture(Event.current);
            var actions = KeyBindings.Actions;
            for (int i = 0; i < actions.Length; i++)
            {
                Rect row = RowRect(i);
                DungeonUi.Panel(row, DungeonUi.PanelColor);
                DungeonUi.Label(new Rect(row.x + 18, row.y, 200, row.height), KeyBindings.ActionName(actions[i]), 17, DungeonUi.Text, TextAnchor.MiddleLeft);
                for (int slot = 0; slot < KeyBindings.Slots; slot++)
                {
                    bool active = capturingAction == i && capturingSlot == slot;
                    string text = active ? "Press a key..." : KeyBindings.KeyName(KeyBindings.Get(actions[i], slot));
                    Color accent = active ? AbilityCatalog.Gold : slot == 0 ? DungeonUi.Teal : DungeonUi.Muted;
                    if (DungeonUi.Button($"bind{i}.{slot}", SlotRect(i, slot), text, accent) && !active)
                    {
                        capturingAction = i;
                        capturingSlot = slot;
                    }
                }
            }
            DungeonUi.Label(new Rect(70, 568, 1140, 24), IsCapturing
                ? "Press a key or mouse button  (click the gold binding to bind a mouse button).   ESC  cancel     DELETE / BACKSPACE  clear"
                : "Click a binding to change it. Every action has a main and an alternate binding. F1 always toggles debug mode.",
                14, IsCapturing ? AbilityCatalog.Gold : DungeonUi.Muted);
        }

        private void Capture(Event e)
        {
            var action = KeyBindings.Actions[capturingAction];
            // F1 is reserved for debug mode.
            if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None && e.keyCode != KeyCode.F1)
            {
                if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) KeyBindings.Set(action, capturingSlot, KeyCode.None);
                else if (e.keyCode != KeyCode.Escape) KeyBindings.Set(action, capturingSlot, e.keyCode);
                capturingAction = -1;
                e.Use();
            }
            else if (e.type == EventType.MouseDown)
            {
                // A click elsewhere cancels (and still reaches whatever was clicked).
                if (!SlotRect(capturingAction, capturingSlot).Contains(e.mousePosition)) { capturingAction = -1; return; }
                if (e.button <= 4) KeyBindings.Set(action, capturingSlot, KeyCode.Mouse0 + e.button);
                capturingAction = -1;
                e.Use();
            }
        }
    }
}
