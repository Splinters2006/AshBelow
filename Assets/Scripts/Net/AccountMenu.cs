using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The account page of the main menu: sign in or sign up, then manage the display name, password, cloud save and the
    /// account itself. Accounts are optional; without one, progress simply stays on this PC.
    /// </summary>
    public sealed class AccountMenu
    {
        private string username = "", password = "", currentPassword = "", newPassword = "", displayName = "";
        // The account the page's fields were filled for, so they refill after switching accounts.
        private string shownFor;
        // Deleting the account asks for a second click so a stray press cannot remove it.
        private float deleteConfirmUntil;

        /// <summary>Clears typed passwords whenever the page is left.</summary>
        public void Cancel()
        {
            password = currentPassword = newPassword = "";
            deleteConfirmUntil = 0f;
        }

        public void Draw(DungeonRun run)
        {
            var account = run.Account;
            var sync = run.CloudSync;
            // Changing who is signed in mid-party would pull the relay out from under the other players.
            bool inParty = run.Coop.Session.State != NetState.Offline;
            if (account.IsSignedIn && sync.IsChoosing) DrawChoice(run, sync);
            else if (account.IsSignedIn) DrawSignedIn(account, sync, inParty);
            else DrawSignedOut(account, inParty);
            string status = inParty && !account.IsSignedIn ? "Leave your co-op party to sign in." : account.Status;
            if (!string.IsNullOrEmpty(status))
                DungeonUi.Label(new Rect(360, 606, 850, 40), status, 15, account.StatusIsError ? AbilityCatalog.Gold : DungeonUi.Teal);
        }

        private void DrawSignedOut(PlayerAccount account, bool inParty)
        {
            shownFor = null;
            bool enabled = !account.IsBusy && !inParty;
            DungeonUi.Panel(new Rect(70, 250, 540, 336), DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(100, 270, 480, 24), "SIGN IN  /  CREATE AN ACCOUNT", 14, AbilityCatalog.Gold);
            DungeonUi.Label(new Rect(100, 304, 480, 20), "USERNAME", 12, DungeonUi.Muted);
            username = DungeonUi.TextField("accountUser", new Rect(100, 326, 480, 46), username, PlayerAccount.MaxUsername);
            DungeonUi.Label(new Rect(100, 386, 480, 20), "PASSWORD", 12, DungeonUi.Muted);
            password = DungeonUi.PasswordField("accountPassword", new Rect(100, 408, 480, 46), password, PlayerAccount.MaxPassword);
            if (DungeonUi.Button("accountSignIn", new Rect(100, 476, 230, 52), "Sign in", AbilityCatalog.Gold, enabled))
            { account.SignIn(username, password); password = ""; }
            if (DungeonUi.Button("accountSignUp", new Rect(350, 476, 230, 52), "Create account", DungeonUi.Teal, enabled))
            { account.SignUp(username, password); password = ""; }
            DungeonUi.Label(new Rect(100, 540, 480, 40), "Forgot it? Passwords cannot be recovered yet, so keep it somewhere safe.", 13, DungeonUi.Muted);

            DungeonUi.Panel(new Rect(650, 250, 560, 336), DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(680, 270, 500, 24), "WHY SIGN IN", 14, DungeonUi.Teal);
            DungeonUi.Label(new Rect(680, 302, 500, 150),
                "Your ash, upgrades, unlocks and encyclopedia are saved to your account and follow you to any PC.\n\n"
                + "Your display name goes with you into co-op parties.\n\n"
                + "No account? Nothing changes: progress stays on this PC.", 16, DungeonUi.Muted);
            DungeonUi.Label(new Rect(680, 470, 500, 20), "RULES", 12, DungeonUi.Muted);
            DungeonUi.Label(new Rect(680, 492, 500, 80),
                "Username: 3-20 letters, numbers or . - @ _\nPassword: 8-30 characters with an upper and lower case letter, a number and a symbol.", 14, DungeonUi.Muted);
        }

        private void DrawSignedIn(PlayerAccount account, CloudProgressSync sync, bool inParty)
        {
            if (shownFor != account.Username)
            {
                shownFor = account.Username;
                displayName = account.DisplayName ?? "";
                Cancel();
            }
            bool idle = !account.IsBusy;
            DungeonUi.Panel(new Rect(70, 250, 540, 336), DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(100, 270, 480, 24), "SIGNED IN AS", 14, DungeonUi.Muted);
            DungeonUi.Label(new Rect(100, 292, 480, 44), account.Username, 30, AbilityCatalog.Gold);
            DungeonUi.Label(new Rect(100, 346, 480, 20), "DISPLAY NAME  /  SHOWN IN CO-OP", 12, DungeonUi.Muted);
            displayName = DungeonUi.TextField("accountName", new Rect(100, 368, 330, 46), displayName, PlayerAccount.MaxDisplayName);
            bool renamed = NetSession.CleanName(displayName) != (account.DisplayName ?? "");
            if (DungeonUi.Button("accountRename", new Rect(444, 368, 136, 46), "Save", DungeonUi.Teal, idle && renamed)) account.Rename(displayName);

            DungeonUi.Label(new Rect(100, 432, 480, 20), "CLOUD SAVE", 12, DungeonUi.Muted);
            string state = sync.IsBusy ? "Syncing…" : sync.Status ?? (sync.IsLinked ? "Progress is synced." : "Not synced yet.");
            DungeonUi.Label(new Rect(100, 452, 480, 50), state, 15, sync.IsLinked ? DungeonUi.Text : AbilityCatalog.Gold);
            if (DungeonUi.Button("accountSignOut", new Rect(100, 518, 230, 48), "Sign out", DungeonUi.Muted, idle && !inParty && !sync.IsBusy))
                sync.SignOutAfterUpload();
            if (DungeonUi.Button("accountSync", new Rect(350, 518, 230, 48), "Sync now", AbilityCatalog.Gold, idle && !sync.IsBusy)) sync.SyncNow();

            DungeonUi.Panel(new Rect(650, 250, 560, 336), DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(680, 270, 500, 24), "CHANGE PASSWORD", 14, DungeonUi.Teal);
            currentPassword = DungeonUi.PasswordField("accountCurrent", new Rect(680, 302, 500, 44), currentPassword, PlayerAccount.MaxPassword, 18);
            if (currentPassword.Length == 0 && GUI.GetNameOfFocusedControl() != "accountCurrent")
                DungeonUi.Label(new Rect(694, 302, 480, 44), "CURRENT PASSWORD", 16, DungeonUi.Muted * 0.7f, TextAnchor.MiddleLeft);
            newPassword = DungeonUi.PasswordField("accountNew", new Rect(680, 356, 500, 44), newPassword, PlayerAccount.MaxPassword, 18);
            if (newPassword.Length == 0 && GUI.GetNameOfFocusedControl() != "accountNew")
                DungeonUi.Label(new Rect(694, 356, 480, 44), "NEW PASSWORD", 16, DungeonUi.Muted * 0.7f, TextAnchor.MiddleLeft);
            if (DungeonUi.Button("accountPassword", new Rect(680, 412, 240, 46), "Change password", DungeonUi.Teal, idle))
            {
                account.ChangePassword(currentPassword, newPassword);
                currentPassword = newPassword = "";
            }

            DungeonUi.Label(new Rect(680, 476, 500, 24), "DELETE ACCOUNT", 14, AbilityCatalog.Gold);
            bool confirming = Time.unscaledTime < deleteConfirmUntil;
            if (DungeonUi.Button("accountDelete", new Rect(680, 506, 240, 48), confirming ? "Confirm delete?" : "Delete account",
                confirming ? AbilityCatalog.Gold : DungeonUi.Muted, idle && !inParty && !sync.IsBusy))
            {
                if (!confirming) deleteConfirmUntil = Time.unscaledTime + 3f;
                else
                {
                    deleteConfirmUntil = 0f;
                    account.DeleteAccount(sync.DeleteCloudCopy);
                }
            }
            DungeonUi.Label(new Rect(936, 502, 250, 60), "Removes the account and its cloud save. This PC keeps its progress.", 13, DungeonUi.Muted);
        }

        private static void DrawChoice(DungeonRun run, CloudProgressSync sync)
        {
            DungeonUi.Panel(new Rect(70, 250, 1140, 336), DungeonUi.PanelColor);
            DungeonUi.Label(new Rect(100, 270, 1080, 24), "CHOOSE YOUR PROGRESS", 14, AbilityCatalog.Gold);
            DungeonUi.Label(new Rect(100, 298, 1080, 44), "This account already has progress from another PC. Keep one; the other is replaced for good.", 17, DungeonUi.Text);
            DrawSave(new Rect(100, 354, 520, 120), "THIS PC", run.Progress.Describe(), DungeonUi.Teal);
            DrawSave(new Rect(660, 354, 520, 120), "YOUR ACCOUNT", PermanentProgress.Describe(sync.CloudJson), AbilityCatalog.Gold);
            if (DungeonUi.Button("accountKeepLocal", new Rect(100, 494, 520, 52), "Keep this PC's progress", DungeonUi.Teal, !sync.IsBusy)) sync.KeepLocal();
            if (DungeonUi.Button("accountKeepCloud", new Rect(660, 494, 520, 52), "Use the account's progress", AbilityCatalog.Gold, !sync.IsBusy)) sync.KeepCloud();
        }

        private static void DrawSave(Rect rect, string title, string summary, Color accent)
        {
            DungeonUi.Panel(rect, new Color(0.02f, 0.03f, 0.045f, 0.95f));
            DungeonUi.Panel(new Rect(rect.x, rect.y, 4, rect.height), accent);
            DungeonUi.Label(new Rect(rect.x + 22, rect.y + 14, rect.width - 40, 24), title, 14, accent);
            DungeonUi.Label(new Rect(rect.x + 22, rect.y + 38, rect.width - 40, 80), summary.Replace("  /  ", "\n"), 16, DungeonUi.Text);
        }
    }
}
