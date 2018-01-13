// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Threading.Tasks;
using Parse.Internal.Utilities;
using Parse.ParseCommon.Internal.Storage;

namespace Parse.Internal.InstallationId.Controller
{
    public class InstallationIdController : IInstallationIdController
    {
        private const string InstallationIdKey = "InstallationId";
        private readonly object _mutex = new object();
        private Guid? _installationId;

        private readonly IStorageController _storageController;

        public InstallationIdController(IStorageController storageController)
        {
            _storageController = storageController;
        }

        public Task SetAsync(Guid? installationId)
        {
            lock (_mutex)
            {
                Task saveTask;

                if (installationId == null)
                {
                    saveTask = _storageController
                        .LoadAsync()
                        .OnSuccess(storage => storage.Result.RemoveAsync(InstallationIdKey))
                        .Unwrap();
                }
                else
                {
                    saveTask = _storageController
                        .LoadAsync()
                        .OnSuccess(storage => storage.Result.AddAsync(InstallationIdKey, installationId.ToString()))
                        .Unwrap();
                }

                _installationId = installationId;
                return saveTask;
            }
        }

        public Task<Guid?> GetAsync()
        {
            lock (_mutex)
            {
                if (_installationId != null)
                {
                    return Task.FromResult(_installationId);
                }
            }

            return _storageController
                .LoadAsync()
                .OnSuccess(s =>
                {
                    object id;
                    s.Result.TryGetValue(InstallationIdKey, out id);
                    try
                    {
                        lock (_mutex)
                        {
                            _installationId = new Guid((string) id);
                            return Task.FromResult(_installationId);
                        }
                    }
                    catch (Exception)
                    {
                        var newInstallationId = Guid.NewGuid();
                        return SetAsync(newInstallationId).OnSuccess<Guid?>(_ => newInstallationId);
                    }
                })
                .Unwrap();
        }

        public Task ClearAsync()
        {
            return SetAsync(null);
        }
    }
}