using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The player's Unity account: sign up and sign in with a username and password, a display name, password changes and
    /// account deletion. It also starts Unity Services, which co-op shares. The first copy of the game on a PC keeps its
    /// sign-in between launches; extra copies and test runs get a throwaway profile so they can sign in as other players.
    /// </summary>
    public sealed class PlayerAccount : MonoBehaviour
    {
        public const int MaxUsername = 20, MaxPassword = 30, MaxDisplayName = 16;
        private static readonly Regex UsernameRule = new Regex(@"^[A-Za-z0-9.\-@_]{3,20}$");
        // Kept for the developers: nobody can create these, though their owners still sign in to them as usual.
        private static readonly string[] ReservedUsernames = { "Splinters06", "tioqy" };
        private const float RestoreRetryDelay = 30f;

        /// <summary>Set before anything starts the services: false keeps test runs away from the player's saved sign-in.</summary>
        public static bool KeepSignIn { get; set; }
        private static string profile;
        private static FileStream profileLock;
        private static Task initializing;
        // The sign-in, sign-up or restore in flight; co-op waits for it rather than racing it with an anonymous sign-in.
        private static Task pending;

        /// <summary>The signed-in account's username, or null while signed out (an anonymous co-op sign-in is not an account).</summary>
        public string Username { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsSignedIn => Username != null;
        public bool IsBusy { get; private set; }
        public string Status { get; private set; }
        public bool StatusIsError { get; private set; }
        public event Action SignedIn, SignedOut;
        private bool hooked;
        private float restoreAt = -1f;

        /// <summary>Starts Unity Services once; a failed start is tried again on the next call.</summary>
        public static async Task EnsureServicesAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized) return;
            if (initializing == null) initializing = UnityServices.InitializeAsync(new InitializationOptions().SetProfile(PickProfile()));
            try { await initializing; }
            catch { initializing = null; throw; }
        }

        /// <summary>For co-op: makes sure some player is signed in, the account when there is one, otherwise an anonymous player.</summary>
        public static async Task EnsureSignedInAsync()
        {
            await EnsureServicesAsync();
            await Settle();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private static async Task Settle()
        {
            var waiting = pending;
            if (waiting == null) return;
            try { await waiting; }
            catch (Exception) { } // Whoever started it reports its failure.
        }

        private static string PickProfile()
        {
            if (profile != null) return profile;
            profile = "p" + Guid.NewGuid().ToString("N").Substring(0, 12);
            if (!KeepSignIn) return profile;
            try
            {
                // Held for the life of the process; a second copy of the game cannot open it and keeps the throwaway profile.
                profileLock = new FileStream(Path.Combine(Application.persistentDataPath, "account.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                profile = "main";
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
            return profile;
        }

        private void Start()
        {
            if (KeepSignIn) _ = Run(Restore(), null);
        }

        private void Update()
        {
            // The start-up sign-in could not reach Unity (offline, say): keep trying, so syncing starts once it can.
            if (restoreAt < 0f || Time.unscaledTime < restoreAt || IsBusy) return;
            restoreAt = -1f;
            if (!IsSignedIn) _ = Run(Restore(), null);
        }

        private void OnDestroy()
        {
            if (hooked && UnityServices.State == ServicesInitializationState.Initialized) AuthenticationService.Instance.Expired -= OnExpired;
        }

        /// <summary>Signs back in to the account used last time, if the saved sign-in is still good.</summary>
        private async Task Restore()
        {
            await EnsureServicesAsync();
            Hook();
            if (AuthenticationService.Instance.IsSignedIn || !AuthenticationService.Instance.SessionTokenExists) return;
            Report("Signing in…");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            await FinishSignIn();
            if (!IsSignedIn) Report(null);
        }

        public static string CheckUsername(string username)
        {
            if (!UsernameRule.IsMatch(username ?? "")) return "Usernames are 3-20 letters, numbers or . - @ _";
            foreach (string reserved in ReservedUsernames)
                if (string.Equals(username, reserved, StringComparison.OrdinalIgnoreCase)) return "That username is taken.";
            return null;
        }

        public static string CheckPassword(string password)
        {
            password ??= "";
            bool upper = false, lower = false, digit = false, symbol = false;
            foreach (char c in password)
            {
                if (char.IsUpper(c)) upper = true;
                else if (char.IsLower(c)) lower = true;
                else if (char.IsDigit(c)) digit = true;
                else symbol = true;
            }
            return password.Length >= 8 && password.Length <= MaxPassword && upper && lower && digit && symbol ? null
                : "Passwords are 8-30 characters with an upper and lower case letter, a number and a symbol.";
        }

        public void SignUp(string username, string password)
        {
            username = (username ?? "").Trim();
            string problem = CheckUsername(username) ?? CheckPassword(password);
            if (problem != null) { Report(problem, true); return; }
            _ = Run(SignInWith(() => AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password)), "Could not create the account");
        }

        public void SignIn(string username, string password)
        {
            username = (username ?? "").Trim();
            if (username.Length == 0 || string.IsNullOrEmpty(password)) { Report("Enter your username and password.", true); return; }
            _ = Run(SignInWith(() => AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password)), "Could not sign in");
        }

        private async Task SignInWith(Func<Task> signIn)
        {
            Report("Signing in…");
            await EnsureServicesAsync();
            Hook();
            // An anonymous co-op player steps aside; their saved session stays for the next anonymous sign-in.
            if (AuthenticationService.Instance.IsSignedIn) AuthenticationService.Instance.SignOut();
            await signIn();
            await FinishSignIn();
            if (!IsSignedIn) throw new InvalidOperationException("Signed in without an account.");
        }

        private async Task FinishSignIn()
        {
            var info = await AuthenticationService.Instance.GetPlayerInfoAsync();
            if (string.IsNullOrEmpty(info?.Username)) return;
            string name = null;
            try { name = await AuthenticationService.Instance.GetPlayerNameAsync(false); }
            catch (RequestFailedException error) { Debug.LogWarning("Could not load the display name: " + error.Message); }
            Username = info.Username;
            DisplayName = string.IsNullOrEmpty(name) ? null : StripTag(name);
            Report("Signed in as " + Username + ".");
            SignedIn?.Invoke();
        }

        public void SignOut()
        {
            if (!IsSignedIn || IsBusy) return;
            // Clearing the credentials stops the next launch from signing straight back in.
            AuthenticationService.Instance.SignOut(true);
            Forget("Signed out. Progress stays on this PC.");
        }

        public void ChangePassword(string current, string next)
        {
            if (!IsSignedIn) return;
            string problem = string.IsNullOrEmpty(current) ? "Enter your current password." : CheckPassword(next);
            if (problem != null) { Report(problem, true); return; }
            _ = Run(ChangePasswordAsync(current, next), "Could not change the password");
        }

        private async Task ChangePasswordAsync(string current, string next)
        {
            Report("Changing password…");
            await AuthenticationService.Instance.UpdatePasswordAsync(current, next);
            Report("Password changed.");
        }

        /// <summary>Sets the name other players see; Unity stores it with a #tag, which the game hides.</summary>
        public void Rename(string name)
        {
            if (!IsSignedIn) return;
            // Unity's player names cannot hold spaces.
            name = NetSession.CleanName(name).Replace(' ', '_');
            _ = Run(RenameAsync(name), "Could not change the display name");
        }

        private async Task RenameAsync(string name)
        {
            Report("Saving name…");
            DisplayName = StripTag(await AuthenticationService.Instance.UpdatePlayerNameAsync(name));
            Report("Display name saved.");
        }

        /// <summary>Deletes the account and everything Unity keeps for it. Progress on this PC stays.</summary>
        public void DeleteAccount(Func<Task> beforeDelete)
        {
            if (!IsSignedIn) return;
            _ = Run(DeleteAccountAsync(beforeDelete), "Could not delete the account");
        }

        private async Task DeleteAccountAsync(Func<Task> beforeDelete)
        {
            Report("Deleting account…");
            if (beforeDelete != null) await beforeDelete();
            await AuthenticationService.Instance.DeleteAccountAsync();
            AuthenticationService.Instance.SignOut(true);
            Forget("Account deleted. Progress stays on this PC.");
        }

        private void Hook()
        {
            if (hooked) return;
            hooked = true;
            AuthenticationService.Instance.Expired += OnExpired;
        }

        private void OnExpired() => Forget("Your sign-in expired. Sign in again to keep syncing.", true);

        private void Forget(string message, bool error = false)
        {
            bool was = IsSignedIn;
            Username = DisplayName = null;
            Report(message, error);
            if (was) SignedOut?.Invoke();
        }

        /// <summary>Runs one account action at a time, turning its failure into a message for the account page.</summary>
        private async Task Run(Task action, string failure)
        {
            IsBusy = true;
            pending = action;
            try { await action; }
            catch (Exception error)
            {
                Debug.LogWarning((failure ?? "Could not restore the sign-in") + ": " + error);
                if (failure == null && IsOffline(error))
                {
                    restoreAt = Time.unscaledTime + RestoreRetryDelay;
                    Report("Could not reach your account. Trying again shortly.", true);
                }
                else if (failure == null) Report(null);
                else Report(failure + ". " + Explain(error), true);
            }
            finally
            {
                if (pending == action) pending = null;
                IsBusy = false;
            }
        }

        private void Report(string message, bool error = false)
        {
            Status = message;
            StatusIsError = error;
        }

        private static string StripTag(string name)
        {
            int tag = name.LastIndexOf('#');
            return (tag > 0 ? name.Substring(0, tag) : name).Replace('_', ' ');
        }

        private static bool IsOffline(Exception error) => error is RequestFailedException failed && failed.ErrorCode == CommonErrorCodes.TransportError;

        private static string Explain(Exception error)
        {
            // Service start-up failures arrive wrapped in several layers of inner exceptions.
            if (error.ToString().Contains("UnityProjectNotLinkedException")) return "Accounts are not set up in this build yet (no Unity Cloud project).";
            // Username & Password has to be added as an identity provider in the Unity Cloud dashboard.
            if (error.Message.Contains("usernamepassword") && error.Message.Contains("not available"))
                return "Username sign-in is not enabled for this game yet.";
            if (error is RequestFailedException failed)
            {
                if (IsOffline(error)) return "Check your internet connection and try again.";
                if (failed.ErrorCode == AuthenticationErrorCodes.InvalidParameters) return "Check the username and password.";
                if (failed.ErrorCode == AuthenticationErrorCodes.ClientInvalidUserState) return "Another sign-in is still finishing. Try again in a moment.";
                if (!string.IsNullOrEmpty(failed.Message)) return failed.Message;
            }
            return "Please try again.";
        }
    }
}
