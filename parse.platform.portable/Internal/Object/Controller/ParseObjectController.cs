// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Parse.Internal.Command;
using Parse.Internal.Encoding;
using Parse.Internal.Object.State;
using Parse.Internal.Operation;
using Parse.Internal.Utilities;
using Parse.ParseCommon.Public.Utilities;
using Parse.Public;

namespace Parse.Internal.Object.Controller
{
    public class ParseObjectController : IParseObjectController
    {
        private readonly IParseCommandRunner _commandRunner;

        public ParseObjectController(IParseCommandRunner commandRunner)
        {
            _commandRunner = commandRunner;
        }

        public Task<IObjectState> FetchAsync(IObjectState state,
            string sessionToken,
            CancellationToken cancellationToken)
        {
            var command = new ParseCommand(string.Format("classes/{0}/{1}",
                    Uri.EscapeDataString(state.ClassName),
                    Uri.EscapeDataString(state.ObjectId)),
                method: "GET",
                sessionToken: sessionToken,
                data: null);

            return _commandRunner.RunCommandAsync(command, cancellationToken: cancellationToken)
                .OnSuccess(t => ParseObjectCoder.Decode(t.Result.Item2, ParseDecoder.Instance));
        }

        public Task<IObjectState> SaveAsync(IObjectState state,
            IDictionary<string, IParseFieldOperation> operations,
            string sessionToken,
            CancellationToken cancellationToken)
        {
            var objectJson = ParseObject.ToJsonObjectForSaving(operations);

            var command = new ParseCommand(
                (state.ObjectId == null
                    ? string.Format("classes/{0}", Uri.EscapeDataString(state.ClassName))
                    : string.Format("classes/{0}/{1}", Uri.EscapeDataString(state.ClassName), state.ObjectId)),
                method: (state.ObjectId == null ? "POST" : "PUT"),
                sessionToken: sessionToken,
                data: objectJson);

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

        public IEnumerable<Task<IObjectState>> SaveAllAsync(IEnumerable<IObjectState> states,
            IEnumerable<IDictionary<string, IParseFieldOperation>> operationsList,
            string sessionToken,
            CancellationToken cancellationToken)
        {
            var requests = states
                .Zip(operationsList, (item, ops) => new ParseCommand(
                    item.ObjectId == null
                        ? string.Format("classes/{0}", Uri.EscapeDataString(item.ClassName))
                        : string.Format("classes/{0}/{1}", Uri.EscapeDataString(item.ClassName),
                            Uri.EscapeDataString(item.ObjectId)),
                    method: item.ObjectId == null ? "POST" : "PUT",
                    data: ParseObject.ToJsonObjectForSaving(ops)))
                .ToList();

            var batchTasks = ExecuteBatchRequests(requests, sessionToken, cancellationToken);
            var stateTasks = new List<Task<IObjectState>>();
            foreach (var task in batchTasks)
            {
                stateTasks.Add(task.OnSuccess(t =>
                {
                    return ParseObjectCoder.Decode(t.Result, ParseDecoder.Instance);
                }));
            }

            return stateTasks;
        }

        public Task DeleteAsync(IObjectState state,
            string sessionToken,
            CancellationToken cancellationToken)
        {
            var command = new ParseCommand(string.Format("classes/{0}/{1}",
                    state.ClassName, state.ObjectId),
                method: "DELETE",
                sessionToken: sessionToken,
                data: null);

            return _commandRunner.RunCommandAsync(command, cancellationToken: cancellationToken);
        }

        public IEnumerable<Task> DeleteAllAsync(IEnumerable<IObjectState> states,
            string sessionToken,
            CancellationToken cancellationToken)
        {
            var requests = states
                .Where(item => item.ObjectId != null)
                .Select(item => new ParseCommand(
                    string.Format("classes/{0}/{1}", Uri.EscapeDataString(item.ClassName),
                        Uri.EscapeDataString(item.ObjectId)),
                    method: "DELETE",
                    data: null))
                .ToList();
            return ExecuteBatchRequests(requests, sessionToken, cancellationToken).Cast<Task>().ToList();
        }

        // TODO (hallucinogen): move this out to a class to be used by Analytics
        private const int MaximumBatchSize = 50;

        private IEnumerable<Task<IDictionary<string, object>>> ExecuteBatchRequests(ICollection<ParseCommand> requests,
            string sessionToken,
            CancellationToken cancellationToken)
        {
            var tasks = new List<Task<IDictionary<string, object>>>();
            var batchSize = requests.Count;

            IEnumerable<ParseCommand> remaining = requests;
            var parseCommands = remaining as ParseCommand[] ?? remaining.ToArray();
            while (batchSize > MaximumBatchSize)
            {
                var process = parseCommands.Take(MaximumBatchSize).ToList();
                parseCommands.Skip(MaximumBatchSize);

                tasks.AddRange(ExecuteBatchRequest(process, sessionToken, cancellationToken));

                batchSize = parseCommands.Count();
            }

            tasks.AddRange(ExecuteBatchRequest(parseCommands.ToList(), sessionToken, cancellationToken));

            return tasks;
        }

        private IEnumerable<Task<IDictionary<string, object>>> ExecuteBatchRequest(ICollection<ParseCommand> requests,
            string sessionToken,
            CancellationToken cancellationToken)
        {
            var tasks = new List<Task<IDictionary<string, object>>>();

            try
            {
                var batchSize = requests.Count;
                var tcss = new List<TaskCompletionSource<IDictionary<string, object>>>();
                for (var i = 0; i < batchSize; ++i)
                {
                    var tcs = new TaskCompletionSource<IDictionary<string, object>>();
                    tcss.Add(tcs);
                    tasks.Add(tcs.Task);
                }

                var encodedRequests = requests.Select(r =>
                {
                    var results = new Dictionary<string, object>
                    {
                    {"method", r.Method},
                    {"path", r.Uri.AbsolutePath},
                    };

                    if (r.DataObject != null)
                    {
                        results["body"] = r.DataObject;
                    }

                    return results;
                }).Cast<object>().ToList();

                if (encodedRequests == null)
                    Debug.WriteLine("Break");

                var command = new ParseCommand("batch",
                    method: "POST",
                    sessionToken: sessionToken,
                    data: new Dictionary<string, object> { { "requests", encodedRequests } });

                try
                {
                    _commandRunner.RunCommandAsync(command, cancellationToken: cancellationToken)
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted || t.IsCanceled)
                            {
                                foreach (var tcs in tcss)
                                {
                                    if (t.IsFaulted)
                                    {
                                        tcs.TrySetException(t.Exception);
                                    }
                                    else if (t.IsCanceled)
                                    {
                                        tcs.TrySetCanceled();
                                    }
                                }

                                return;
                            }

                            var resultsArray = Conversion.As<IList<object>>(t.Result.Item2["results"]);
                            var resultLength = resultsArray.Count;
                            if (resultLength != batchSize)
                            {
                                foreach (var tcs in tcss)
                                {
                                    tcs.TrySetException(new InvalidOperationException(
                                        "Batch command result count expected: " + batchSize + " but was: " + resultLength + "."));
                                }

                                return;
                            }

                            for (var i = 0; i < batchSize; ++i)
                            {
                                var result = resultsArray[i] as Dictionary<string, object>;
                                var tcs = tcss[i];

                                if (result != null && result.ContainsKey("success"))
                                {
                                    tcs.TrySetResult(result["success"] as IDictionary<string, object>);
                                }
                                else if (result != null && result.ContainsKey("error"))
                                {
                                    if (!(result["error"] is IDictionary<string, object> error)) continue;
                                    var errorCode = (long)error["code"];
                                    tcs.TrySetException(new ParseException((ParseException.ErrorCode)errorCode,
                                        error["error"] as string));
                                }
                                else
                                {
                                    tcs.TrySetException(new InvalidOperationException(
                                        "Invalid batch command response."));
                                }
                            }
                        }, cancellationToken);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception: {ex}");
            }
            return tasks;
        }
    }
}