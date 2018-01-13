// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Parse.Internal.Command;
using Parse.Internal.Encoding;
using Parse.ParseCommon.Public.Utilities;

namespace Parse.Internal.Cloud.Controller
{
    public class ParseCloudCodeController : IParseCloudCodeController
    {
        private readonly IParseCommandRunner _commandRunner;

        public ParseCloudCodeController(IParseCommandRunner commandRunner)
        {
            _commandRunner = commandRunner;
        }

        public Task<T> CallFunctionAsync<T>(string name,
            IDictionary<string, object> parameters,
            string sessionToken,
            CancellationToken cancellationToken)
        {
            var command = new ParseCommand(string.Format("functions/{0}", Uri.EscapeUriString(name)),
                method: "POST",
                sessionToken: sessionToken,
                data: NoObjectsEncoder.Instance.Encode(parameters) as IDictionary<string, object>);

            return _commandRunner.RunCommandAsync(command, cancellationToken: cancellationToken).ContinueWith(t =>
            {
                var decoded = ParseDecoder.Instance.Decode(t.Result.Item2) as IDictionary<string, object>;
                return decoded != null && !decoded.ContainsKey("result")
                    ? default(T)
                    : Conversion.To<T>(decoded["result"]);
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }
}