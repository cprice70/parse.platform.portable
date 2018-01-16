using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Parse.Internal;
using Parse.Internal.Object.Controller;
using Parse.Internal.Object.State;
using Parse.Internal.Operation;
using Parse.Internal.Session.Controller;
using Parse.Internal.User.Controller;
using Parse.Internal.Utilities;
using Parse.Public;
using static NUnit.Framework.Assert;

namespace parse.platform.test
{
    [TestFixture]
    public class UserTests
    {
        [SetUp]
        public void SetUp()
        {
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();
        }

        [TearDown]
        public void TearDown()
        {
            ParseCorePlugins.Instance = null;
        }

        [Test]
        public void TestRemoveFields()
        {
            IObjectState state = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"username", "kevin"},
                    {"name", "andrew"}
                }
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            Throws<ArgumentException>(() => user.Remove("username"));
            DoesNotThrow(() => user.Remove("name"));
            False(user.ContainsKey("name"));
        }

        [Test]
        public void TestSessionTokenGetter()
        {
            IObjectState state = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"username", "kevin"},
                    {"sessionToken", "se551onT0k3n"}
                }
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            AreEqual("se551onT0k3n", user.SessionToken);
        }

        [Test]
        public void TestUsernameGetterSetter()
        {
            IObjectState state = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"username", "kevin"},
                }
            };
            
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            AreEqual("kevin", user.Username);
            user.Username = "ilya";
            AreEqual("ilya", user.Username);
        }

        [Test]
        public void TestPasswordGetterSetter()
        {
            IObjectState state = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"username", "kevin"},
                    {"password", "hurrah"},
                }
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            AreEqual("hurrah", user.GetState()["password"]);
            user.Password = "david";
            NotNull(user.GetCurrentOperations()["password"]);
        }

        [Test]
        public void TestEmailGetterSetter()
        {
            IObjectState state = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"email", "james@parse.com"},
                    {"name", "andrew"},
                    {"sessionToken", "se551onT0k3n"}
                }
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            AreEqual("james@parse.com", user.Email);
            user.Email = "bryan@parse.com";
            AreEqual("bryan@parse.com", user.Email);
        }

        [Test]
        public void TestAuthDataGetter()
        {
            IObjectState state = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"email", "james@parse.com"},
                    {
                        "authData", new Dictionary<string, object>()
                        {
                            {
                                "facebook", new Dictionary<string, object>()
                                {
                                    {"sessionToken", "none"}
                                }
                            }
                        }
                    }
                }
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            AreEqual(1, user.GetAuthData().Count);
            IsInstanceOf<IDictionary<string, object>>(user.GetAuthData()["facebook"]);
        }

        [Test]
        public void TestGetUserQuery()
        {
            IsInstanceOf<ParseQuery<ParseUser>>(ParseUser.Query);
        }

        [Test]
        public void TestIsAuthenticated()
        {
            IObjectState state = new MutableObjectState
            {
                ObjectId = "wagimanPutraPetir",
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "llaKcolnu"}
                }
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            var mockCurrentUserController = new Mock<IParseCurrentUserController>();
            mockCurrentUserController.Setup(obj => obj.GetAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(user));
            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                CurrentUserController = mockCurrentUserController.Object
            };
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();

            True(user.IsAuthenticated);
        }

        [Test]
        public void TestIsAuthenticatedWithOtherParseUser()
        {
            IObjectState state = new MutableObjectState
            {
                ObjectId = "wagimanPutraPetir",
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "llaKcolnu"}
                }
            };
            IObjectState state2 = new MutableObjectState
            {
                ObjectId = "wagimanPutraPetir2",
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "llaKcolnu"}
                }
            };
            
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            var user2 = ParseObjectExtensions.FromState<ParseUser>(state2, "_User");
            var mockCurrentUserController = new Mock<IParseCurrentUserController>();
            mockCurrentUserController.Setup(obj => obj.GetAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(user));
            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                CurrentUserController = mockCurrentUserController.Object
            };
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();

            False(user2.IsAuthenticated);
        }

        [Test]
        [AsyncStateMachine(typeof(UserTests))]
        public Task TestSignUpWithInvalidServerData()
        {
            IObjectState state = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "llaKcolnu"}
                }
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");

            return user.SignUpAsync().ContinueWith(t =>
            {
                True(t.IsFaulted);
                if (t.Exception != null) IsInstanceOf<InvalidOperationException>(t.Exception.InnerException);
            });
        }

        [Test]
        [AsyncStateMachine(typeof(UserTests))]
        public Task TestSignUp()
        {
            IObjectState state = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "llaKcolnu"},
                    {"username", "ihave"},
                    {"password", "adream"}
                }
            };
            IObjectState newState = new MutableObjectState
            {
                ObjectId = "some0neTol4v4"
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            var mockController = new Mock<IParseUserController>();
            mockController.Setup(obj => obj.SignUpAsync(It.IsAny<IObjectState>(),
                It.IsAny<IDictionary<string, IParseFieldOperation>>(),
                It.IsAny<CancellationToken>())).Returns(Task.FromResult(newState));
            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                UserController = mockController.Object
            };
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();

            return user.SignUpAsync().ContinueWith(t =>
            {
                False(t.IsFaulted);
                False(t.IsCanceled);
                mockController.Verify(obj => obj.SignUpAsync(It.IsAny<IObjectState>(),
                    It.IsAny<IDictionary<string, IParseFieldOperation>>(),
                    It.IsAny<CancellationToken>()), Times.Exactly(1));
                False(user.IsDirty);
                AreEqual("ihave", user.Username);
                False(user.GetState().ContainsKey("password"));
                AreEqual("some0neTol4v4", user.ObjectId);
            });
        }

        [Test]
        [AsyncStateMachine(typeof(UserTests))]
        public Task TestLogIn()
        {
            IObjectState state = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>
                {
                    {"sessionToken", "llaKcolnu"},
                    {"username", "ihave"},
                    {"password", "adream"}
                }
            };
          //  IObjectState newState = new MutableObjectState
          //  {
          //      ObjectId = "some0neTol4v4"
          //  };

            var mockController = new Mock<IParseUserController>();
            mockController.Setup(obj => obj.LogInAsync("ihave",
                "adream",
                It.IsAny<CancellationToken>())).Returns(Task.FromResult(state));
            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                UserController = mockController.Object
            };

            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();

            return ParseUser.LogInAsync("ihave", "adream")
                .ContinueWith(t =>
                {
                    False(t.IsFaulted);
                    False(t.IsCanceled);
                    mockController.Verify(obj => obj.LogInAsync("ihave",
                        "adream",
                        It.IsAny<CancellationToken>()), Times.Exactly(1));

                    var user = t.Result;
                    False(user.IsDirty);
                    Null(user.Username);
                    AreEqual("some0neTol4v4", user.ObjectId);
                });
        }

        [Test]
        [AsyncStateMachine(typeof(UserTests))]
        public Task TestBecome()
        {
            IObjectState state = new MutableObjectState
            {
                ObjectId = "some0neTol4v4",
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "llaKcolnu"},
                    {"username", "Test"},
                    {"password", "Test"}
                }
            };
            var mockController = new Mock<IParseUserController>();
            mockController.Setup(obj => obj.GetUserAsync("llaKcolnu", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(state));
            
            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                UserController = mockController.Object
            };
            
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();

            return ParseUser.BecomeAsync("llaKcolnu")
                            .ContinueWith(t =>
            {
                False(t.IsFaulted);
                False(t.IsCanceled);
                mockController.Verify(obj => obj.GetUserAsync("llaKcolnu",
                    It.IsAny<CancellationToken>()), Times.Exactly(1));

                var user = t.Result;
                AreEqual("some0neTol4v4", user.ObjectId);
                AreEqual("llaKcolnu", user.SessionToken);
            });
        }

        [Test]
        [AsyncStateMachine(typeof(UserTests))]
        public Task TestLogOut()
        {
            IObjectState state = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "r:llaKcolnu"}
                }
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            var mockCurrentUserController = new Mock<IParseCurrentUserController>();
            mockCurrentUserController.Setup(obj => obj.GetAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(user));
            var mockSessionController = new Mock<IParseSessionController>();
            mockSessionController.Setup(c => c.IsRevocableSessionToken(It.IsAny<string>())).Returns(true);

            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                CurrentUserController = mockCurrentUserController.Object,
                SessionController = mockSessionController.Object
            };
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();

            return ParseUser.LogOutAsync().ContinueWith(t =>
            {
                False(t.IsFaulted);
                False(t.IsCanceled);
                mockCurrentUserController.Verify(obj => obj.LogOutAsync(It.IsAny<CancellationToken>()),
                    Times.Exactly(1));
                mockSessionController.Verify(obj => obj.RevokeAsync("r:llaKcolnu", It.IsAny<CancellationToken>()),
                    Times.Exactly(1));
            });
        }

        [Test]
        public void TestCurrentUser()
        {
            IObjectState state = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "llaKcolnu"}
                }
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            var mockCurrentUserController = new Mock<IParseCurrentUserController>();
            mockCurrentUserController.Setup(obj => obj.GetAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(user));
            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                CurrentUserController = mockCurrentUserController.Object,
            };
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();

            AreEqual(user, ParseUser.CurrentUser);
        }

        [Test]
        public void TestCurrentUserWithEmptyResult()
        {
            var mockCurrentUserController = new Mock<IParseCurrentUserController>();
            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                CurrentUserController = mockCurrentUserController.Object
            };
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();

            Null(ParseUser.CurrentUser);
        }

        [Test]
        [AsyncStateMachine(typeof(UserTests))]
        public Task TestRevocableSession()
        {
            IObjectState state = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "llaKcolnu"}
                }
            };
            IObjectState newState = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "r:llaKcolnu"}
                }
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            var mockSessionController = new Mock<IParseSessionController>();
            mockSessionController.Setup(obj => obj.UpgradeToRevocableSessionAsync("llaKcolnu",
                It.IsAny<CancellationToken>())).Returns(Task.FromResult(newState));
            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                SessionController = mockSessionController.Object
            };
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();

            return user.UpgradeToRevocableSessionAsync(CancellationToken.None).ContinueWith(t =>
            {
                False(t.IsFaulted);
                False(t.IsCanceled);
                mockSessionController.Verify(obj => obj.UpgradeToRevocableSessionAsync("llaKcolnu",
                    It.IsAny<CancellationToken>()), Times.Exactly(1));
                AreEqual("r:llaKcolnu", user.SessionToken);
            });
        }

        [Test]
        [AsyncStateMachine(typeof(UserTests))]
        public Task TestRequestPasswordReset()
        {
            var mockController = new Mock<IParseUserController>();
            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                UserController = mockController.Object
            };
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();

            return ParseUser.RequestPasswordResetAsync("gogo@parse.com").ContinueWith(t =>
            {
                False(t.IsFaulted);
                False(t.IsCanceled);
                mockController.Verify(obj => obj.RequestPasswordResetAsync("gogo@parse.com",
                    It.IsAny<CancellationToken>()), Times.Exactly(1));
            });
        }

        [Test]
        [AsyncStateMachine(typeof(UserTests))]
        public Task TestUserSave()
        {
            IObjectState state = new MutableObjectState
            {
                ObjectId = "some0neTol4v4",
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "llaKcolnu"},
                    {"username", "ihave"},
                    {"password", "adream"}
                }
            };
            IObjectState newState = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"Alliance", "rekt"}
                }
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            var mockObjectController = new Mock<IParseObjectController>();
            mockObjectController.Setup(obj => obj.SaveAsync(It.IsAny<IObjectState>(),
                It.IsAny<IDictionary<string, IParseFieldOperation>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>())).Returns(Task.FromResult(newState));
            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                ObjectController = mockObjectController.Object,
                CurrentUserController = new Mock<IParseCurrentUserController>().Object
            };
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();
            user["Alliance"] = "rekt";

            return user.SaveAsync().ContinueWith(t =>
            {
                False(t.IsFaulted);
                False(t.IsCanceled);
                mockObjectController.Verify(obj => obj.SaveAsync(It.IsAny<IObjectState>(),
                    It.IsAny<IDictionary<string, IParseFieldOperation>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()), Times.Exactly(1));
                False(user.IsDirty);
                AreEqual("ihave", user.Username);
                False(user.GetState().ContainsKey("password"));
                AreEqual("some0neTol4v4", user.ObjectId);
                AreEqual("rekt", user["Alliance"]);
            });
        }

        [Test]
        [AsyncStateMachine(typeof(UserTests))]
        public Task TestUserFetch()
        {
            IObjectState state = new MutableObjectState
            {
                ObjectId = "some0neTol4v4",
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "llaKcolnu"},
                    {"username", "ihave"},
                    {"password", "adream"}
                }
            };
            IObjectState newState = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"Alliance", "rekt"}
                }
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            var mockObjectController = new Mock<IParseObjectController>();
            mockObjectController.Setup(obj => obj.FetchAsync(It.IsAny<IObjectState>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>())).Returns(Task.FromResult(newState));
            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                ObjectController = mockObjectController.Object,
                CurrentUserController = new Mock<IParseCurrentUserController>().Object
            };
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();
            user["Alliance"] = "rekt";

            return user.FetchAsync().ContinueWith(t =>
            {
                False(t.IsFaulted);
                False(t.IsCanceled);
                mockObjectController.Verify(obj => obj.FetchAsync(It.IsAny<IObjectState>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()), Times.Exactly(1));
                True(user.IsDirty);
                AreEqual("ihave", user.Username);
                True(user.GetState().ContainsKey("password"));
                AreEqual("some0neTol4v4", user.ObjectId);
                AreEqual("rekt", user["Alliance"]);
            });
        }

        [Test]
        [AsyncStateMachine(typeof(UserTests))]
        public Task TestLink()
        {
            IObjectState state = new MutableObjectState
            {
                ObjectId = "some0neTol4v4",
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "llaKcolnu"}
                }
            };
            IObjectState newState = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"garden", "ofWords"}
                }
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            var mockObjectController = new Mock<IParseObjectController>();
            mockObjectController.Setup(obj => obj.SaveAsync(It.IsAny<IObjectState>(),
                It.IsAny<IDictionary<string, IParseFieldOperation>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>())).Returns(Task.FromResult(newState));
            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                ObjectController = mockObjectController.Object,
                CurrentUserController = new Mock<IParseCurrentUserController>().Object
            };
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();

            return user.LinkWithAsync("parse", new Dictionary<string, object>(), CancellationToken.None).ContinueWith(
                t =>
                {
                    False(t.IsFaulted);
                    False(t.IsCanceled);
                    mockObjectController.Verify(obj => obj.SaveAsync(It.IsAny<IObjectState>(),
                        It.IsAny<IDictionary<string, IParseFieldOperation>>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()), Times.Exactly(1));
                    False(user.IsDirty);
                    NotNull(user.GetAuthData());
                    NotNull(user.GetAuthData()["parse"]);
                    AreEqual("some0neTol4v4", user.ObjectId);
                    AreEqual("ofWords", user["garden"]);
                });
        }

        [Test]
        [AsyncStateMachine(typeof(UserTests))]
        public Task TestUnlink()
        {
            IObjectState state = new MutableObjectState
            {
                ObjectId = "some0neTol4v4",
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "llaKcolnu"},
                    {
                        "authData", new Dictionary<string, object>
                        {
                            {"parse", new Dictionary<string, object>()}
                        }
                    }
                }
            };
            IObjectState newState = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"garden", "ofWords"}
                }
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            var mockObjectController = new Mock<IParseObjectController>();
            mockObjectController.Setup(obj => obj.SaveAsync(It.IsAny<IObjectState>(),
                It.IsAny<IDictionary<string, IParseFieldOperation>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>())).Returns(Task.FromResult(newState));
            var mockCurrentUserController = new Mock<IParseCurrentUserController>();
            mockCurrentUserController.Setup(obj => obj.IsCurrent(user)).Returns(true);
            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                ObjectController = mockObjectController.Object,
                CurrentUserController = mockCurrentUserController.Object,
            };
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();

            return user.UnlinkFromAsync("parse", CancellationToken.None).ContinueWith(t =>
            {
                False(t.IsFaulted);
                False(t.IsCanceled);
                mockObjectController.Verify(obj => obj.SaveAsync(It.IsAny<IObjectState>(),
                    It.IsAny<IDictionary<string, IParseFieldOperation>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()), Times.Exactly(1));
                False(user.IsDirty);
                NotNull(user.GetAuthData());
                False(user.GetAuthData().ContainsKey("parse"));
                AreEqual("some0neTol4v4", user.ObjectId);
                AreEqual("ofWords", user["garden"]);
            });
        }

        [Test]
        [AsyncStateMachine(typeof(UserTests))]
        public Task TestUnlinkNonCurrentUser()
        {
            IObjectState state = new MutableObjectState
            {
                ObjectId = "some0neTol4v4",
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "llaKcolnu"},
                    {
                        "authData", new Dictionary<string, object>
                        {
                            {"parse", new Dictionary<string, object>()}
                        }
                    }
                }
            };
            IObjectState newState = new MutableObjectState
            {
                ServerData = new Dictionary<string, object>()
                {
                    {"garden", "ofWords"}
                }
            };
            var user = ParseObjectExtensions.FromState<ParseUser>(state, "_User");
            var mockObjectController = new Mock<IParseObjectController>();
            mockObjectController.Setup(obj => obj.SaveAsync(It.IsAny<IObjectState>(),
                It.IsAny<IDictionary<string, IParseFieldOperation>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>())).Returns(Task.FromResult(newState));
            var mockCurrentUserController = new Mock<IParseCurrentUserController>();
            mockCurrentUserController.Setup(obj => obj.IsCurrent(user)).Returns(false);
            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                ObjectController = mockObjectController.Object,
                CurrentUserController = mockCurrentUserController.Object,
            };
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();

            return user.UnlinkFromAsync("parse", CancellationToken.None).ContinueWith(t =>
            {
                False(t.IsFaulted);
                False(t.IsCanceled);
                mockObjectController.Verify(obj => obj.SaveAsync(It.IsAny<IObjectState>(),
                    It.IsAny<IDictionary<string, IParseFieldOperation>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()), Times.Exactly(1));
                False(user.IsDirty);
                NotNull(user.GetAuthData());
                True(user.GetAuthData().ContainsKey("parse"));
                Null(user.GetAuthData()["parse"]);
                AreEqual("some0neTol4v4", user.ObjectId);
                AreEqual("ofWords", user["garden"]);
            });
        }

        [Test]
        [AsyncStateMachine(typeof(UserTests))]
        public Task TestLogInWith()
        {
            IObjectState state = new MutableObjectState
            {
                ObjectId = "some0neTol4v4",
                ServerData = new Dictionary<string, object>()
                {
                    {"sessionToken", "llaKcolnu"}
                }
            };
            var mockController = new Mock<IParseUserController>();
            mockController.Setup(obj => obj.LogInAsync("parse",
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>())).Returns(Task.FromResult(state));

            ParseCorePlugins.Instance = new ParseCorePlugins
            {
                UserController = mockController.Object
            };
            ParseObject.RegisterSubclass<ParseUser>();
            ParseObject.RegisterSubclass<ParseSession>();

            return ParseUserExtensions.LogInWithAsync("parse", new Dictionary<string, object>(), CancellationToken.None)
                .ContinueWith(t =>
                {
                    False(t.IsFaulted);
                    False(t.IsCanceled);
                    mockController.Verify(obj => obj.LogInAsync("parse",
                        It.IsAny<IDictionary<string, object>>(),
                        It.IsAny<CancellationToken>()), Times.Exactly(1));

                    var user = t.Result;
                    NotNull(user.GetAuthData());
                    NotNull(user.GetAuthData()["parse"]);
                    AreEqual("some0neTol4v4", user.ObjectId);
                });
        }

        [Test]
        public void TestImmutableKeys()
        {
            var user = new ParseUser();
            var immutableKeys = new[]
            {
                "sessionToken", "isNew"
            };

            foreach (var key in immutableKeys)
            {
                Throws<InvalidOperationException>(() =>
                    user[key] = "1234567890"
                );

                Throws<InvalidOperationException>(() =>
                    user.Add(key, "1234567890")
                );

                Throws<InvalidOperationException>(() =>
                    user.AddRangeUniqueToList(key, new[] {"1234567890"})
                );

                Throws<InvalidOperationException>(() =>
                    user.Remove(key)
                );

                Throws<InvalidOperationException>(() =>
                    user.RemoveAllFromList(key, new[] {"1234567890"})
                );
            }

            // Other special keys should be good
            user["username"] = "username";
            user["password"] = "password";
        }
    }
}