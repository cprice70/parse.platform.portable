// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Parse.Internal.Command;
using Parse.Internal.Encoding;
using Parse.Internal.Object.State;
using Parse.Internal.Utilities;
using Parse.Public;

namespace Parse.Internal.Query.Controller
{
    internal class ParseQueryController : IParseQueryController
    {
        private readonly IParseCommandRunner _commandRunner;

        public ParseQueryController(IParseCommandRunner commandRunner)
        {
            _commandRunner = commandRunner;
        }

        public Task<IEnumerable<IObjectState>> FindAsync<T>(ParseQuery<T> query,
            ParseUser user,
            CancellationToken cancellationToken) where T : ParseObject
        {
            string sessionToken = user?.SessionToken;

            return FindAsync(query.ClassName, query.BuildParameters(), sessionToken, cancellationToken).OnSuccess(t =>
            {
                var items = t.Result["results"] as IList<object>;

                return (from item in items
                    select ParseObjectCoder.Decode(item as IDictionary<string, object>, ParseDecoder.Instance)
                );
            });
        }

        public Task<int> CountAsync<T>(ParseQuery<T> query,
            ParseUser user,
            CancellationToken cancellationToken) where T : ParseObject
        {
            var sessionToken = user?.SessionToken;
            var parameters = query.BuildParameters();
            parameters["limit"] = 0;
            parameters["count"] = 1;

            return FindAsync(query.ClassName, parameters, sessionToken, cancellationToken)
                .OnSuccess(t => Convert.ToInt32(t.Result["count"]));
        }

        public Task<IObjectState> FirstAsync<T>(ParseQuery<T> query,
            ParseUser user,
            CancellationToken cancellationToken) where T : ParseObject
        {
            var sessionToken = user?.SessionToken;
            var parameters = query.BuildParameters();
            parameters["limit"] = 1;

            return FindAsync(query.ClassName, parameters, sessionToken, cancellationToken).OnSuccess(t =>
            {
                var items = t.Result["results"] as IList<object>;

                // Not found. Return empty state.
                if (!(items.FirstOrDefault() is IDictionary<string, object> item))
                {
                    return (IObjectState) null;
                }

                return ParseObjectCoder.Decode(item, ParseDecoder.Instance);
            });
        }

        private Task<IDictionary<string, object>> FindAsync(string className,
            IDictionary<string, object> parameters,
            string sessionToken,
            CancellationToken cancellationToken)
        {
            var command = new ParseCommand(string.Format("classes/{0}?{1}",
                    Uri.EscapeDataString(className),
                    ParseClient.BuildQueryString(parameters)),
                method: "GET",
                sessionToken: sessionToken,
                data: null);

            return _commandRunner.RunCommandAsync(command, cancellationToken: cancellationToken)
                .OnSuccess(t => t.Result.Item2);
        }
    }
}