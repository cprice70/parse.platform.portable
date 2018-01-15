using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Parse.Internal.Config.Controller;
using Parse.Public;
using Parse.Internal.Utilities;
using Parse.Internal;
using Parse.Internal.User.Controller;

namespace ParseTest
{
    [TestFixture]
	public class ConfigTests {
		private IParseConfigController MockedConfigController {
			get {
				var mockedConfigController = new Mock<IParseConfigController>();
				var mockedCurrentConfigController = new Mock<IParseCurrentConfigController>();

				ParseConfig theConfig = ParseConfigExtensions.Create(new Dictionary<string, object> {{
					"params", new Dictionary<string, object> {{
						 "testKey", "testValue"
					}}
				}});

				mockedCurrentConfigController.Setup(
					obj => obj.GetCurrentConfigAsync()
				).Returns(Task.FromResult(theConfig));

				mockedConfigController.Setup(obj => obj.CurrentConfigController)
            .Returns(mockedCurrentConfigController.Object);

        var tcs = new TaskCompletionSource<ParseConfig>();
        tcs.TrySetCanceled();

        mockedConfigController.Setup(obj => obj.FetchConfigAsync(It.IsAny<string>(),
            It.Is<CancellationToken>(ct => ct.IsCancellationRequested))).Returns(tcs.Task);

				mockedConfigController.Setup(obj => obj.FetchConfigAsync(It.IsAny<string>(),
            It.Is<CancellationToken>(ct => !ct.IsCancellationRequested))).Returns(Task.FromResult(theConfig));

				return mockedConfigController.Object;
			}
		}

		[SetUp]
		public void SetUp() {
      ParseCorePlugins.Instance = new ParseCorePlugins {
        ConfigController = MockedConfigController,
        CurrentUserController = new Mock<IParseCurrentUserController>().Object
      };
		}

		[TearDown]
		public void TearDown() {
			ParseCorePlugins.Instance = null;
		}

		[Test]
		public void TestCurrentConfig() {
			ParseConfig config = ParseConfig.CurrentConfig;
            if (config.TryGetValue("testKey", out string result) == true)
                Assert.AreEqual("testValue", result);
			Assert.AreEqual("testValue", config.Get<string>("testKey"));
		}

		[Test]
		public void TestToJSON() {
			ParseConfig config1 = ParseConfig.CurrentConfig;
			IDictionary<string, object> expectedJson = new Dictionary<string, object> {{
				"params", new Dictionary<string, object> {{
					"testKey", "testValue"
				}}
			}};

			Assert.AreEqual(((IJsonConvertible)config1).ToJson(), expectedJson);
		}

		[Test]
		[AsyncStateMachine(typeof(ConfigTests))]
		public Task TestGetConfig() {
			return ParseConfig.GetAsync().ContinueWith(t => {
                if (t.Result.TryGetValue("testKey", out string result) == true)
                    Assert.AreEqual("testValue", result);
				Assert.AreEqual("testValue", t.Result.Get<string>("testKey"));
			});
		}

		[Test]
		[AsyncStateMachine(typeof(ConfigTests))]
		public Task TestGetConfigCancel() {
			CancellationTokenSource tokenSource = new CancellationTokenSource();
			tokenSource.Cancel();
			return ParseConfig.GetAsync(tokenSource.Token).ContinueWith(t => {
				Assert.True(t.IsCanceled);
			});
		}
	}
}
