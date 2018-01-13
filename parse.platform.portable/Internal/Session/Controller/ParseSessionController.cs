// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Parse.Internal.Command;
using Parse.Internal.Encoding;
using Parse.Internal.Object.State;
using Parse.Internal.Utilities;

namespace Parse.Internal.Session.Controller
{
    public class ParseSessionController : IParseSessionController
    {
        private readonly IParseCommandRunner _commandRunner;

        public ParseSessionController(IParseCommandRunner commandRunner)
        {
            _commandRunner = commandRunner;
        }

        public Task<IObjectState> GetSessionAsync(string sessionToken, CancellationToken cancellationToken)
        {
            var command = new ParseCommand("sessions/me",
                method: "GET",
                sessionToken: sessionToken,
                data: null);

            return _commandRunner.RunCommandAsync(command, cancellationToken: cancellationToken)
                .OnSuccess(t => ParseObjectCoder.Decode(t.Result.Item2, ParseDecoder.Instance));
        }

        public Task RevokeAsync(string sessionToken, CancellationToken cancellationToken)
        {
            var command = new ParseCommand("logout",
                method: "POST",
                sessionToken: sessionToken,
                data: new Dictionary<string, object>());

            return _commandRunner.RunCommandAsync(command, cancellationToken: cancellationToken);
        }

        public Task<IObjectState> UpgradeToRevocableSessionAsync(string sessionToken,
            CancellationToken cancellationToken)
        {
            var command = new ParseCommand("upgradeToRevocableSession",
                method: "POST",
                sessionToken: sessionToken,
                data: new Dictionary<string, object>());

            return _commandRunner.RunCommandAsync(command, cancellationToken: cancellationToken)
                .OnSuccess(t => ParseObjectCoder.Decode(t.Result.Item2, ParseDecoder.Instance));
        }

        public bool IsRevocableSessionToken(string sessionToken)
        {
            return sessionToken.Contains("r:");
        }
    }
}