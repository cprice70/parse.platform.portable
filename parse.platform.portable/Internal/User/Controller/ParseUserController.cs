// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Parse.Internal.Command;
using Parse.Internal.Encoding;
using Parse.Internal.Object.State;
using Parse.Internal.Operation;
using Parse.Internal.Utilities;
using Parse.Public;

namespace Parse.Internal.User.Controller
{
    public class ParseUserController : IParseUserController
    {
        private readonly IParseCommandRunner _commandRunner;

        public ParseUserController(IParseCommandRunner commandRunner)
        {
            _commandRunner = commandRunner;
        }

        public Task<IObjectState> SignUpAsync(IObjectState state,
            IDictionary<string, IParseFieldOperation> operations,
            CancellationToken cancellationToken)
        {
            var objectJson = ParseObject.ToJsonObjectForSaving(operations);

            var command = new ParseCommand("classes/_User",
                method: "POST",
                data: objectJson);

            return _commandRunner.RunCommandAsync(command, cancellationToken: cancellationToken).OnSuccess(t =>
            {
                var serverState = ParseObjectCoder.Decode(t.Result.Item2, ParseDecoder.Instance);
                serverState = serverState.MutatedClone(mutableClone => { mutableClone.IsNew = true; });
                return serverState;
            });
        }

        public Task<IObjectState> LogInAsync(string username,
            string password,
            CancellationToken cancellationToken)
        {
            var data = new Dictionary<string, object>
            {
                {"username", username},
                {"password", password}
            };

            var command = new ParseCommand(string.Format("login?{0}", ParseClient.BuildQueryString(data)),
                method: "GET",
                data: null);

            return _commandRunner.RunCommandAsync(command, cancellationToken: cancellationToken)
                .ContinueWith(result =>
                {
                    var serverState = ParseObjectCoder.Decode(result.Result.Item2, ParseDecoder.Instance);
                    serverState = serverState.MutatedClone(mutableClone =>
                    {
                        mutableClone.IsNew = result.Result.Item1 == System.Net.HttpStatusCode.Created;
                    });
                    return serverState;
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        public Task<IObjectState> LogInAsync(string authType,
            IDictionary<string, object> data,
            CancellationToken cancellationToken)
        {
            var authData = new Dictionary<string, object>();
            authData[authType] = data;

            var command = new ParseCommand("users",
                method: "POST",
                data: new Dictionary<string, object>
                {
                    {"authData", authData}
                });

            return _commandRunner.RunCommandAsync(command, cancellationToken: cancellationToken).OnSuccess(t =>
            {
                var serverState = ParseObjectCoder.Decode(t.Result.Item2, ParseDecoder.Instance);
                serverState = serverState.MutatedClone(mutableClone =>
                {
                    mutableClone.IsNew = t.Result.Item1 == System.Net.HttpStatusCode.Created;
                });
                return serverState;
            });
        }

        public Task<IObjectState> GetUserAsync(string sessionToken, CancellationToken cancellationToken)
        {
            var command = new ParseCommand("users/me",
                method: "GET",
                sessionToken: sessionToken,
                data: null);

            return _commandRunner.RunCommandAsync(command, cancellationToken: cancellationToken)
                .OnSuccess(t => ParseObjectCoder.Decode(t.Result.Item2, ParseDecoder.Instance));
        }

        public Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken)
        {
            var command = new ParseCommand("requestPasswordReset",
                method: "POST",
                data: new Dictionary<string, object>
                {
                    {"email", email}
                });

            return _commandRunner.RunCommandAsync(command, cancellationToken: cancellationToken);
        }
    }
}