// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using Parse.Internal.Cloud.Controller;
using Parse.Internal.Command;
using Parse.Internal.Config.Controller;
using Parse.Internal.File.Controller;
using Parse.Internal.InstallationId.Controller;
using Parse.Internal.Object.Controller;
using Parse.Internal.Object.Subclassing;
using Parse.Internal.Query.Controller;
using Parse.Internal.Session.Controller;
using Parse.Internal.User.Controller;
using Parse.ParseCommon.Internal.HttpClient;
using Parse.ParseCommon.Internal.HttpClient.Portable;
using Parse.ParseCommon.Internal.Storage;
using Parse.ParseCommon.Internal.Storage.Portable;
using Parse.Public;

namespace Parse.Internal
{
    public class ParseCorePlugins : IParseCorePlugins
    {
        private static readonly object InstanceMutex = new object();
        private static IParseCorePlugins _instance;

        public static IParseCorePlugins Instance
        {
            get
            {
                lock (InstanceMutex)
                {
                    _instance = _instance ?? new ParseCorePlugins();
                    return _instance;
                }
            }
            set
            {
                lock (InstanceMutex)
                {
                    _instance = value;
                }
            }
        }

        private readonly object _mutex = new object();

        #region Server Controllers

        private IHttpClient _httpClient;
        private IParseCommandRunner _commandRunner;
        private IStorageController _storageController;

        private IParseCloudCodeController _cloudCodeController;
        private IParseConfigController _configController;
        private IParseFileController _fileController;
        private IParseObjectController _objectController;
        private IParseQueryController _queryController;
        private IParseSessionController _sessionController;
        private IParseUserController _userController;
        private IObjectSubclassingController _subclassingController;

        #endregion

        #region Current Instance Controller

        private IParseCurrentUserController _currentUserController;
        private IInstallationIdController _installationIdController;

        #endregion

        public void Reset()
        {
            lock (_mutex)
            {
                HttpClient = null;
                CommandRunner = null;
                StorageController = null;

                CloudCodeController = null;
                FileController = null;
                ObjectController = null;
                SessionController = null;
                UserController = null;
                SubclassingController = null;

                CurrentUserController = null;
                InstallationIdController = null;
            }
        }

        public IHttpClient HttpClient
        {
            get
            {
                lock (_mutex)
                {
                    _httpClient = _httpClient ?? new HttpClient();
                    return _httpClient;
                }
            }
            set
            {
                lock (_mutex)
                {
                    _httpClient = value;
                }
            }
        }

        public IParseCommandRunner CommandRunner
        {
            get
            {
                lock (_mutex)
                {
                    _commandRunner = _commandRunner ?? new ParseCommandRunner(HttpClient, InstallationIdController);
                    return _commandRunner;
                }
            }
            set
            {
                lock (_mutex)
                {
                    _commandRunner = value;
                }
            }
        }

        public IStorageController StorageController
        {
            get
            {
                lock (_mutex)
                {
                    _storageController = _storageController ?? new StorageController();
                    return _storageController;
                }
            }
            set
            {
                lock (_mutex)
                {
                    _storageController = value;
                }
            }
        }

        public IParseCloudCodeController CloudCodeController
        {
            get
            {
                lock (_mutex)
                {
                    _cloudCodeController = _cloudCodeController ?? new ParseCloudCodeController(CommandRunner);
                    return _cloudCodeController;
                }
            }
            set
            {
                lock (_mutex)
                {
                    _cloudCodeController = value;
                }
            }
        }

        public IParseFileController FileController
        {
            get
            {
                lock (_mutex)
                {
                    _fileController = _fileController ?? new ParseFileController(CommandRunner);
                    return _fileController;
                }
            }
            set
            {
                lock (_mutex)
                {
                    _fileController = value;
                }
            }
        }

        public IParseConfigController ConfigController
        {
            get
            {
                lock (_mutex)
                {
                    return _configController ??
                           (_configController = new ParseConfigController(CommandRunner, StorageController));
                }
            }
            set
            {
                lock (_mutex)
                {
                    _configController = value;
                }
            }
        }

        public IParseObjectController ObjectController
        {
            get
            {
                lock (_mutex)
                {
                    _objectController = _objectController ?? new ParseObjectController(CommandRunner);
                    return _objectController;
                }
            }
            set
            {
                lock (_mutex)
                {
                    _objectController = value;
                }
            }
        }

        public IParseQueryController QueryController
        {
            get
            {
                lock (_mutex)
                {
                    return _queryController ?? (_queryController = new ParseQueryController(CommandRunner));
                }
            }
            set
            {
                lock (_mutex)
                {
                    _queryController = value;
                }
            }
        }

        public IParseSessionController SessionController
        {
            get
            {
                lock (_mutex)
                {
                    _sessionController = _sessionController ?? new ParseSessionController(CommandRunner);
                    return _sessionController;
                }
            }
            set
            {
                lock (_mutex)
                {
                    _sessionController = value;
                }
            }
        }

        public IParseUserController UserController
        {
            get
            {
                lock (_mutex)
                {
                    _userController = _userController ?? new ParseUserController(CommandRunner);
                    return _userController;
                }
            }
            set
            {
                lock (_mutex)
                {
                    _userController = value;
                }
            }
        }

        public IParseCurrentUserController CurrentUserController
        {
            get
            {
                lock (_mutex)
                {
                    _currentUserController =
                        _currentUserController ?? new ParseCurrentUserController(StorageController);
                    return _currentUserController;
                }
            }
            set
            {
                lock (_mutex)
                {
                    _currentUserController = value;
                }
            }
        }

        public IObjectSubclassingController SubclassingController
        {
            get
            {
                lock (_mutex)
                {
                    if (_subclassingController != null) return _subclassingController;
                    _subclassingController = new ObjectSubclassingController();
                    _subclassingController.AddRegisterHook(typeof(ParseUser),
                        () => CurrentUserController.ClearFromMemory());

                    return _subclassingController;
                }
            }
            set
            {
                lock (_mutex)
                {
                    _subclassingController = value;
                }
            }
        }

        public IInstallationIdController InstallationIdController
        {
            get
            {
                lock (_mutex)
                {
                    _installationIdController =
                        _installationIdController ?? new InstallationIdController(StorageController);
                    return _installationIdController;
                }
            }
            set
            {
                lock (_mutex)
                {
                    _installationIdController = value;
                }
            }
        }
    }
}