// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Parse.Internal.File.State;
using Parse.ParseCommon.Public;

namespace Parse.Internal.File.Controller
{
    public interface IParseFileController
    {
        Task<FileState> SaveAsync(FileState state,
            Stream dataStream,
            string sessionToken,
            IProgress<ParseUploadProgressEventArgs> progress,
            CancellationToken cancellationToken);
    }
}