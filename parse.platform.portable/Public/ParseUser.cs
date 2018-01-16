// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Parse.Internal;
using Parse.Internal.Authentication;
using Parse.Internal.Object.State;
using Parse.Internal.User.Controller;
using Parse.Internal.Utilities;

namespace Parse.Public
{
    /// <inheritdoc />
    /// <summary>
    /// Represents a user for a Parse application.
    /// </summary>
    [ParseClassName("_User")]
    public class ParseUser : ParseObject
    {
        private static readonly IDictionary<string, IParseAuthenticationProvider> AuthProviders =
            new Dictionary<string, IParseAuthenticationProvider>();

        private static readonly HashSet<string> ReadOnlyKeys = new HashSet<string>
        {
            "sessionToken",
            "isNew"
        };

        private static IParseUserController UserController => ParseCorePlugins.Instance.UserController;

        private static IParseCurrentUserController CurrentUserController =>
            ParseCorePlugins.Instance.CurrentUserController;

        private static readonly object IsAutoUserEnabledMutex = new object();
        private static bool _autoUserEnabled;

        /// <summary>
        /// Whether the ParseUser has been authenticated on this device. Only an authenticated
        /// ParseUser can be saved and deleted.
        /// </summary>
        public bool IsAuthenticated
        {
            get
            {
                lock (LockObject)
                {
                    return SessionToken != null &&
                           CurrentUser != null &&
                           CurrentUser.ObjectId == ObjectId;
                }
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Removes a key from the object's data if it exists.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <exception cref="T:System.ArgumentException">Cannot remove the username key.</exception>
        public override void Remove(string key)
        {
            if (key == "username")
            {
                throw new ArgumentException("Cannot remove the username key.");
            }

            base.Remove(key);
        }

        protected override bool IsKeyMutable(string key)
        {
            return !ReadOnlyKeys.Contains(key);
        }

        internal override void HandleSave(IObjectState serverState)
        {
            base.HandleSave(serverState);

            SynchronizeAllAuthData();
            CleanupAuthData();

            MutateState(mutableClone => { mutableClone.ServerData.Remove("password"); });
        }

        public string SessionToken
        {
            get
            {
                if (State.ContainsKey("sessionToken"))
                {
                    return State["sessionToken"] as string;
                }

                return null;
            }
        }

        internal static string CurrentSessionToken
        {
            get
            {
                var sessionTokenTask = GetCurrentSessionTokenAsync();
                sessionTokenTask.Wait();
                return sessionTokenTask.Result;
            }
        }

        private static Task<string> GetCurrentSessionTokenAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return CurrentUserController.GetCurrentSessionTokenAsync(cancellationToken);
        }

        private Task SetSessionTokenAsync(string newSessionToken)
        {
            return SetSessionTokenAsync(newSessionToken, CancellationToken.None);
        }

        private Task SetSessionTokenAsync(string newSessionToken, CancellationToken cancellationToken)
        {
            MutateState(mutableClone => { mutableClone.ServerData["sessionToken"] = newSessionToken; });

            return SaveCurrentUserAsync(this, cancellationToken);
        }

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        [ParseFieldName("username")]
        public string Username
        {
            get => GetProperty<string>(nameof(Username));
            set => SetProperty(value);
        }

        /// <summary>
        /// Sets the password.
        /// </summary>
        [ParseFieldName("password")]
        public string Password
        {
            get => GetProperty<string>(nameof(Password));
            set => SetProperty(value);
        }

        /// <summary>
        /// Sets the email address.
        /// </summary>
        [ParseFieldName("email")]
        public string Email
        {
            get => GetProperty<string>(nameof(Email));
            set => SetProperty(value);
        }

        private Task SignUpAsync(Task toAwait, CancellationToken cancellationToken)
        {
            if (AuthData == null)
            {
                // TODO (hallucinogen): make an Extension of Task to create Task with exception/canceled.
                if (string.IsNullOrEmpty(Username))
                {
                    var tcs = new TaskCompletionSource<object>();
                    tcs.TrySetException(new InvalidOperationException("Cannot sign up user with an empty name."));
                    return tcs.Task;
                }

                if (string.IsNullOrEmpty(Password))
                {
                    var tcs = new TaskCompletionSource<object>();
                    tcs.TrySetException(new InvalidOperationException("Cannot sign up user with an empty password."));
                    return tcs.Task;
                }
            }

            if (!string.IsNullOrEmpty(ObjectId))
            {
                var tcs = new TaskCompletionSource<object>();
                tcs.TrySetException(new InvalidOperationException("Cannot sign up a user that already exists."));
                return tcs.Task;
            }

            var currentOperations = StartSave();

            return toAwait.OnSuccess(_ => UserController.SignUpAsync(State, currentOperations, cancellationToken))
                .Unwrap()
                .ContinueWith(t =>
                {
                    if (t.IsFaulted || t.IsCanceled)
                    {
                        HandleFailedSave(currentOperations);
                    }
                    else
                    {
                        var serverState = t.Result;
                        HandleSave(serverState);
                    }

                    return t;
                }, cancellationToken).Unwrap().OnSuccess(_ => SaveCurrentUserAsync(this, cancellationToken)).Unwrap();
        }

        /// <summary>
        /// Signs up a new user. This will create a new ParseUser on the server and will also persist the
        /// session on disk so that you can access the user using <see cref="CurrentUser"/>. A username and
        /// password must be set before calling SignUpAsync.
        /// </summary>
        public Task SignUpAsync()
        {
            return SignUpAsync(CancellationToken.None);
        }

        /// <summary>
        /// Signs up a new user. This will create a new ParseUser on the server and will also persist the
        /// session on disk so that you can access the user using <see cref="CurrentUser"/>. A username and
        /// password must be set before calling SignUpAsync.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        private Task SignUpAsync(CancellationToken cancellationToken)
        {
            return TaskQueue.Enqueue(toAwait => SignUpAsync(toAwait, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Logs in a user with a username and password. On success, this saves the session to disk so you
        /// can retrieve the currently logged in user using <see cref="CurrentUser"/>.
        /// </summary>
        /// <param name="authType">The type of auth.</param>
        /// <param name="authData">The data to login with.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The newly logged-in user.</returns>
        public static async Task<ParseUser> LogInAsync(string authType, Dictionary<string, object> authData,
            CancellationToken token)
        {
            await UserController.LogInAsync(authType, authData, token).OnSuccess(t =>
            {
                var user = FromState<ParseUser>(t.Result, "_User");
                return SaveCurrentUserAsync(user, token).OnSuccess(_ => user);
            }).Unwrap();
            return null;
        }

        /// <summary>
        /// Logs in a user with a username and password. On success, this saves the session to disk so you
        /// can retrieve the currently logged in user using <see cref="CurrentUser"/>.
        /// </summary>
        /// <param name="username">The username to log in with.</param>
        /// <param name="password">The password to log in with.</param>
        /// <returns>The newly logged-in user.</returns>
        public static Task<ParseUser> LogInAsync(string username, string password)
        {
            return LogInAsync(username, password, CancellationToken.None);
        }

        /// <summary>
        /// Logs in a user with a username and password. On success, this saves the session to disk so you
        /// can retrieve the currently logged in user using <see cref="CurrentUser"/>.
        /// </summary>
        /// <param name="username">The username to log in with.</param>
        /// <param name="password">The password to log in with.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The newly logged-in user.</returns>
        private static Task<ParseUser> LogInAsync(string username, string password, CancellationToken cancellationToken)
        {
            return UserController.LogInAsync(username, password, cancellationToken)
                .OnSuccess(t =>
                {
                    var user = FromState<ParseUser>(t.Result, "_User");
                    return SaveCurrentUserAsync(user, cancellationToken).OnSuccess(_ => user);
                }).Unwrap();
        }

        /// <summary>
        /// Logs in a user with a username and password. On success, this saves the session to disk so you
        /// can retrieve the currently logged in user using <see cref="CurrentUser"/>.
        /// </summary>
        /// <param name="sessionToken">The session token to authorize with</param>
        /// <returns>The user if authorization was successful</returns>
        public static Task<ParseUser> BecomeAsync(string sessionToken)
        {
            return BecomeAsync(sessionToken, CancellationToken.None);
        }

        /// <summary>
        /// Logs in a user with a username and password. On success, this saves the session to disk so you
        /// can retrieve the currently logged in user using <see cref="CurrentUser"/>.
        /// </summary>
        /// <param name="sessionToken">The session token to authorize with</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The user if authorization was successful</returns>
        private static Task<ParseUser> BecomeAsync(string sessionToken, CancellationToken cancellationToken)
        {
            return UserController.GetUserAsync(sessionToken, cancellationToken)
                .OnSuccess(t =>
                {
                    var user = FromState<ParseUser>(t.Result, "_User");
                    return SaveCurrentUserAsync(user, cancellationToken).OnSuccess(_ => user);
                }).Unwrap();
        }

        protected override Task SaveAsync(Task toAwait, CancellationToken cancellationToken)
        {
            lock (LockObject)
            {
                if (ObjectId == null)
                {
                    throw new InvalidOperationException("You must call SignUpAsync before calling SaveAsync.");
                }

                return base.SaveAsync(toAwait, cancellationToken)
                    .OnSuccess(_ =>
                        !CurrentUserController.IsCurrent(this)
                            ? Task.FromResult(0)
                            : SaveCurrentUserAsync(this, cancellationToken)).Unwrap();
            }
        }

        internal override Task<ParseObject> FetchAsyncInternal(Task toAwait, CancellationToken cancellationToken)
        {
            return base.FetchAsyncInternal(toAwait, cancellationToken).OnSuccess(t =>
            {
                return !CurrentUserController.IsCurrent(this)
                    ? Task.FromResult(t.Result)
                    : SaveCurrentUserAsync(this, cancellationToken)
                        .OnSuccess(_ => t.Result);
                // If this is already the current user, refresh its state on disk.
            }).Unwrap();
        }

        /// <summary>
        /// Logs out the currently logged in user session. This will remove the session from disk, log out of
        /// linked services, and future calls to <see cref="CurrentUser"/> will return <c>null</c>.
        /// </summary>
        /// <remarks>
        /// Typically, you should use <see cref="LogOutAsync()"/>, unless you are managing your own threading.
        /// </remarks>
        public static void LogOut()
        {
            // TODO (hallucinogen): this will without a doubt fail in Unity. But what else can we do?
            LogOutAsync().Wait();
        }

        /// <summary>
        /// Logs out the currently logged in user session. This will remove the session from disk, log out of
        /// linked services, and future calls to <see cref="CurrentUser"/> will return <c>null</c>.
        /// </summary>
        /// <remarks>
        /// This is preferable to using <see cref="LogOut()"/>, unless your code is already running from a
        /// background thread.
        /// </remarks>
        public static Task LogOutAsync()
        {
            return LogOutAsync(CancellationToken.None);
        }

        /// <summary>
        /// Logs out the currently logged in user session. This will remove the session from disk, log out of
        /// linked services, and future calls to <see cref="CurrentUser"/> will return <c>null</c>.
        ///
        /// This is preferable to using <see cref="LogOut()"/>, unless your code is already running from a
        /// background thread.
        /// </summary>
        private static Task LogOutAsync(CancellationToken cancellationToken)
        {
            return GetCurrentUserAsync().OnSuccess(t =>
            {
                LogOutWithProviders();

                var user = t.Result;
                return user == null
                    ? Task.FromResult(0)
                    : user.TaskQueue.Enqueue(toAwait => user.LogOutAsync(toAwait, cancellationToken),
                        cancellationToken);
            }).Unwrap();
        }

        private Task LogOutAsync(Task toAwait, CancellationToken cancellationToken)
        {
            var oldSessionToken = SessionToken;
            if (oldSessionToken == null)
            {
                return Task.FromResult(0);
            }

            // Cleanup in-memory session.
            MutateState(mutableClone => { mutableClone.ServerData.Remove("sessionToken"); });
            var revokeSessionTask = ParseSession.RevokeAsync(oldSessionToken, cancellationToken);
            return Task.WhenAll(revokeSessionTask, CurrentUserController.LogOutAsync(cancellationToken));
        }

        private static void LogOutWithProviders()
        {
            foreach (var provider in AuthProviders.Values)
            {
                provider.Deauthenticate();
            }
        }

        /// <summary>
        /// Gets the currently logged in ParseUser with a valid session, either from memory or disk
        /// if necessary.
        /// </summary>
        public static ParseUser CurrentUser
        {
            get
            {
                var userTask = GetCurrentUserAsync();
                // TODO (hallucinogen): this will without a doubt fail in Unity. How should we fix it?
                userTask.Wait();
                return userTask.Result;
            }
        }

        /// <summary>
        /// Gets the currently logged in ParseUser with a valid session, either from memory or disk
        /// if necessary, asynchronously.
        /// </summary>
        internal static Task<ParseUser> GetCurrentUserAsync()
        {
            return GetCurrentUserAsync(isAutomaticUserEnabled(), CancellationToken.None);
        }

        /// <summary>
        /// Gets the currently logged in ParseUser with a valid session, either from memory or disk
        /// if necessary, asynchronously.
        /// </summary>
        private static Task<ParseUser> GetCurrentUserAsync(bool AutomaticUserEnabled,
            CancellationToken cancellationToken)
        {
            return CurrentUserController.GetAsync(cancellationToken);
        }

        private static Task SaveCurrentUserAsync(ParseUser user)
        {
            return SaveCurrentUserAsync(user, CancellationToken.None);
        }

        private static Task SaveCurrentUserAsync(ParseUser user, CancellationToken cancellationToken)
        {
            return CurrentUserController.SetAsync(user, cancellationToken);
        }

        internal static void ClearInMemoryUser()
        {
            CurrentUserController.ClearFromMemory();
        }

        public static void EnableAutomaticUser()
        {
            lock (IsAutoUserEnabledMutex)
            {
                _autoUserEnabled = true;
            }
        }

        /* package */
        static void disableAutomaticUser()
        {
            lock (IsAutoUserEnabledMutex)
            {
                _autoUserEnabled = false;
            }
        }

        /* package */
        static bool isAutomaticUserEnabled()
        {
            lock (IsAutoUserEnabledMutex)
            {
                return _autoUserEnabled;
            }
        }

        /// <summary>
        /// Constructs a <see cref="ParseQuery{ParseUser}"/> for ParseUsers.
        /// </summary>
        public static ParseQuery<ParseUser> Query => new ParseQuery<ParseUser>();

        #region Legacy / Revocable Session Tokens

        private static readonly object isRevocableSessionEnabledMutex = new object();
        private static bool isRevocableSessionEnabled;

        /// <summary>
        /// Tells server to use revocable session on LogIn and SignUp, even when App's Settings
        /// has "Require Revocable Session" turned off. Issues network request in background to
        /// migrate the sessionToken on disk to revocable session.
        /// </summary>
        /// <returns>The Task that upgrades the session.</returns>
        public static Task EnableRevocableSessionAsync()
        {
            return EnableRevocableSessionAsync(CancellationToken.None);
        }

        /// <summary>
        /// Tells server to use revocable session on LogIn and SignUp, even when App's Settings
        /// has "Require Revocable Session" turned off. Issues network request in background to
        /// migrate the sessionToken on disk to revocable session.
        /// </summary>
        /// <returns>The Task that upgrades the session.</returns>
        private static Task EnableRevocableSessionAsync(CancellationToken cancellationToken)
        {
            lock (isRevocableSessionEnabledMutex)
            {
                isRevocableSessionEnabled = true;
            }

            return GetCurrentUserAsync(isAutomaticUserEnabled(), cancellationToken).OnSuccess(t =>
            {
                var user = t.Result;
                return user.UpgradeToRevocableSessionAsync(cancellationToken);
            });
        }

        internal static void DisableRevocableSession()
        {
            lock (isRevocableSessionEnabledMutex)
            {
                isRevocableSessionEnabled = false;
            }
        }

        internal static bool IsRevocableSessionEnabled
        {
            get
            {
                lock (isRevocableSessionEnabledMutex)
                {
                    return isRevocableSessionEnabled;
                }
            }
        }

        internal Task UpgradeToRevocableSessionAsync()
        {
            return UpgradeToRevocableSessionAsync(CancellationToken.None);
        }

        internal Task UpgradeToRevocableSessionAsync(CancellationToken cancellationToken)
        {
            return TaskQueue.Enqueue(toAwait => UpgradeToRevocableSessionAsync(toAwait, cancellationToken),
                cancellationToken);
        }

        private Task UpgradeToRevocableSessionAsync(Task toAwait, CancellationToken cancellationToken)
        {
            var sessionToken = SessionToken;

            return toAwait.OnSuccess(_ => ParseSession.UpgradeToRevocableSessionAsync(sessionToken, cancellationToken))
                .Unwrap()
                .OnSuccess(t => SetSessionTokenAsync(t.Result, cancellationToken)).Unwrap();
        }

        #endregion

        /// <summary>
        /// Requests a password reset email to be sent to the specified email address associated with the
        /// user account. This email allows the user to securely reset their password on the Parse site.
        /// </summary>
        /// <param name="email">The email address associated with the user that forgot their password.</param>
        public static Task RequestPasswordResetAsync(string email)
        {
            return RequestPasswordResetAsync(email, CancellationToken.None);
        }

        /// <summary>
        /// Requests a password reset email to be sent to the specified email address associated with the
        /// user account. This email allows the user to securely reset their password on the Parse site.
        /// </summary>
        /// <param name="email">The email address associated with the user that forgot their password.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private static Task RequestPasswordResetAsync(string email,
            CancellationToken cancellationToken)
        {
            return UserController.RequestPasswordResetAsync(email, cancellationToken);
        }

        /// <summary>
        /// Gets the authData for this user.
        /// </summary>
        internal IDictionary<string, IDictionary<string, object>> AuthData
        {
            get => TryGetValue<IDictionary<string, IDictionary<string, object>>>(
                "authData", out var authData)
                ? authData
                : null;
            private set => this["authData"] = value;
        }

        private static IParseAuthenticationProvider GetProvider(string providerName)
        {
            return AuthProviders.TryGetValue(providerName, out var provider) ? provider : null;
        }

        /// <summary>
        /// Removes null values from authData (which exist temporarily for unlinking)
        /// </summary>
        private void CleanupAuthData()
        {
            lock (LockObject)
            {
                if (!CurrentUserController.IsCurrent(this))
                {
                    return;
                }

                var authData = AuthData;

                if (authData == null)
                {
                    return;
                }

                foreach (var pair in new Dictionary<string, IDictionary<string, object>>(authData))
                {
                    if (pair.Value == null)
                    {
                        authData.Remove(pair.Key);
                    }
                }
            }
        }

        /// <summary>
        /// Synchronizes authData for all providers.
        /// </summary>
        private void SynchronizeAllAuthData()
        {
            lock (LockObject)
            {
                var authData = AuthData;

                if (authData == null)
                {
                    return;
                }

                foreach (var pair in authData)
                {
                    SynchronizeAuthData(GetProvider(pair.Key));
                }
            }
        }

        private void SynchronizeAuthData(IParseAuthenticationProvider provider)
        {
            var restorationSuccess = false;
            lock (LockObject)
            {
                var authData = AuthData;
                if (authData == null || provider == null)
                {
                    return;
                }

                IDictionary<string, object> data;
                if (authData.TryGetValue(provider.AuthType, out data))
                {
                    restorationSuccess = provider.RestoreAuthentication(data);
                }
            }

            if (!restorationSuccess)
            {
                this.UnlinkFromAsync(provider.AuthType, CancellationToken.None);
            }
        }

        internal Task LinkWithAsync(string authType, IDictionary<string, object> data,
            CancellationToken cancellationToken)
        {
            return TaskQueue.Enqueue(toAwait =>
            {
                var authData = AuthData ?? (AuthData = new Dictionary<string, IDictionary<string, object>>());
                authData[authType] = data;
                AuthData = authData;
                return SaveAsync(cancellationToken);
            }, cancellationToken);
        }

        internal Task LinkWithAsync(string authType, CancellationToken cancellationToken)
        {
            var provider = GetProvider(authType);
            return provider.AuthenticateAsync(cancellationToken)
                .OnSuccess(t => LinkWithAsync(authType, t.Result, cancellationToken))
                .Unwrap();
        }

        /// <summary>
        /// Unlinks a user from a service.
        /// </summary>
        internal Task UnlinkFromAsync(string authType, CancellationToken cancellationToken)
        {
            return LinkWithAsync(authType, null, cancellationToken);
        }

        /// <summary>
        /// Checks whether a user is linked to a service.
        /// </summary>
        internal bool IsLinked(string authType)
        {
            lock (LockObject)
            {
                return AuthData != null && AuthData.ContainsKey(authType) && AuthData[authType] != null;
            }
        }

        internal static Task<ParseUser> LogInWithAsync(string authType,
            IDictionary<string, object> data,
            CancellationToken cancellationToken)
        {
            ParseUser user = null;

            return UserController.LogInAsync(authType, data, cancellationToken).OnSuccess(t =>
            {
                user = FromState<ParseUser>(t.Result, "_User");

                lock (user.LockObject)
                {
                    if (user.AuthData == null)
                    {
                        user.AuthData = new Dictionary<string, IDictionary<string, object>>();
                    }

                    user.AuthData[authType] = data;
                    user.SynchronizeAllAuthData();
                }

                return SaveCurrentUserAsync(user, cancellationToken);
            }).Unwrap().OnSuccess(t => user);
        }

        internal static Task<ParseUser> LogInWithAsync(string authType,
            CancellationToken cancellationToken)
        {
            var provider = GetProvider(authType);
            return provider.AuthenticateAsync(cancellationToken)
                .OnSuccess(authData => LogInWithAsync(authType, authData.Result, cancellationToken))
                .Unwrap();
        }
    }
}