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
using Parse.ParseCommon.Internal.Storage;

namespace Parse.Internal
{
    public interface IParseCorePlugins
    {
        void Reset();

        IHttpClient HttpClient { get; }
        IParseCommandRunner CommandRunner { get; }
        IStorageController StorageController { get; }

        IParseCloudCodeController CloudCodeController { get; }
        IParseConfigController ConfigController { get; }
        IParseFileController FileController { get; }
        IParseObjectController ObjectController { get; }
        IParseQueryController QueryController { get; }
        IParseSessionController SessionController { get; }
        IParseUserController UserController { get; }
        IObjectSubclassingController SubclassingController { get; }
        IParseCurrentUserController CurrentUserController { get; }
        IInstallationIdController InstallationIdController { get; }
    }
}