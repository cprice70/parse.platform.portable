// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Parse.Internal.Encoding;
using Parse.Internal.Utilities;
using Parse.ParseCommon.Internal.Storage;
using Parse.Public;

namespace Parse.Internal.User.Controller
{
    public class ParseCurrentUserController : IParseCurrentUserController
    {
        private readonly object _mutex = new object();
        private readonly TaskQueue _taskQueue = new TaskQueue();

        private readonly IStorageController _storageController;

        public ParseCurrentUserController(IStorageController storageController)
        {
            _storageController = storageController;
        }

        private ParseUser _currentUser;

        public ParseUser CurrentUser
        {
            get
            {
                lock (_mutex)
                {
                    return _currentUser;
                }
            }
            set
            {
                lock (_mutex)
                {
                    _currentUser = value;
                }
            }
        }

        public Task SetAsync(ParseUser user, CancellationToken cancellationToken)
        {
            return _taskQueue.Enqueue(toAwait =>
            {
                return toAwait.ContinueWith(_ =>
                {
                    Task saveTask;
                    if (user == null)
                    {
                        saveTask = _storageController
                            .LoadAsync()
                            .OnSuccess(t => t.Result.RemoveAsync("CurrentUser"))
                            .Unwrap();
                    }
                    else
                    {
                        // TODO (hallucinogen): we need to use ParseCurrentCoder instead of this janky encoding
                        var data = user.ServerDataToJSONObjectForSerialization();
                        data["objectId"] = user.ObjectId;
                        if (user.CreatedAt != null)
                        {
                            data["createdAt"] = user.CreatedAt.Value.ToString(ParseClient.DateFormatStrings.First(),
                                CultureInfo.InvariantCulture);
                        }

                        if (user.UpdatedAt != null)
                        {
                            data["updatedAt"] = user.UpdatedAt.Value.ToString(ParseClient.DateFormatStrings.First(),
                                CultureInfo.InvariantCulture);
                        }

                        saveTask = _storageController
                            .LoadAsync()
                            .OnSuccess(t => t.Result.AddAsync("CurrentUser", Json.Encode(data)))
                            .Unwrap();
                    }

                    CurrentUser = user;

                    return saveTask;
                }, cancellationToken).Unwrap();
            }, cancellationToken);
        }

        public Task<ParseUser> GetAsync(CancellationToken cancellationToken)
        {
            ParseUser cachedCurrent;

            lock (_mutex)
            {
                cachedCurrent = CurrentUser;
            }

            if (cachedCurrent != null)
            {
                return Task.FromResult(cachedCurrent);
            }

            return _taskQueue.Enqueue(toAwait =>
            {
                return toAwait.ContinueWith(_ =>
                {
                    return _storageController.LoadAsync().OnSuccess(t =>
                    {
                        t.Result.TryGetValue("CurrentUser", out var temp);
                        var userDataString = temp as string;
                        ParseUser user = null;
                        if (userDataString != null)
                        {
                            var userData = Json.Parse(userDataString) as IDictionary<string, object>;
                            var state = ParseObjectCoder.Decode(userData, ParseDecoder.Instance);
                            user = ParseObject.FromState<ParseUser>(state, "_User");
                        }

                        CurrentUser = user;
                        return user;
                    });
                }, cancellationToken).Unwrap();
            }, cancellationToken);
        }

        public Task<bool> ExistsAsync(CancellationToken cancellationToken)
        {
            if (CurrentUser != null)
            {
                return Task.FromResult(true);
            }

            return _taskQueue.Enqueue(toAwait =>
            {
                return toAwait.ContinueWith(_ =>
                    _storageController.LoadAsync().OnSuccess(t => t.Result.ContainsKey("CurrentUser")), cancellationToken).Unwrap();
            }, cancellationToken);
        }

        public bool IsCurrent(ParseUser user)
        {
            lock (_mutex)
            {
                return CurrentUser == user;
            }
        }

        public void ClearFromMemory()
        {
            CurrentUser = null;
        }

        public void ClearFromDisk()
        {
            lock (_mutex)
            {
                ClearFromMemory();

                _taskQueue.Enqueue(
                    toAwait =>
                    {
                        return toAwait.ContinueWith(_ =>
                        {
                            return _storageController.LoadAsync()
                                .OnSuccess(t => t.Result.RemoveAsync("CurrentUser"));
                        }).Unwrap().Unwrap();
                    }, CancellationToken.None);
            }
        }

        public Task<string> GetCurrentSessionTokenAsync(CancellationToken cancellationToken)
        {
            return GetAsync(cancellationToken).OnSuccess(t =>
            {
                var user = t.Result;
                return user?.SessionToken;
            });
        }

        public Task LogOutAsync(CancellationToken cancellationToken)
        {
            return _taskQueue.Enqueue(
                toAwait =>
                {
                    return toAwait.ContinueWith(_ => GetAsync(cancellationToken), cancellationToken).Unwrap()
                        .OnSuccess(t => { ClearFromDisk(); });
                }, cancellationToken);
        }
    }
}