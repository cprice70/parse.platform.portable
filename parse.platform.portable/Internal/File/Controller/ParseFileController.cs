// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Parse.Internal.Command;
using Parse.Internal.File.State;
using Parse.Internal.Utilities;
using Parse.ParseCommon.Public;

namespace Parse.Internal.File.Controller
{
    public class ParseFileController : IParseFileController
    {
        private readonly IParseCommandRunner _commandRunner;

        public ParseFileController(IParseCommandRunner commandRunner)
        {
            _commandRunner = commandRunner;
        }

        public Task<FileState> SaveAsync(FileState state,
            Stream dataStream,
            string sessionToken,
            IProgress<ParseUploadProgressEventArgs> progress,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (state.Url != null)
            {
                // !isDirty
                return Task.FromResult(state);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                var tcs = new TaskCompletionSource<FileState>();
                tcs.TrySetCanceled();
                return tcs.Task;
            }

            var oldPosition = dataStream.Position;
            var command = new ParseCommand("files/" + state.Name,
                method: "POST",
                sessionToken: sessionToken,
                contentType: state.MimeType,
                stream: dataStream);

            return _commandRunner.RunCommandAsync(command,
                uploadProgress: progress,
                cancellationToken: cancellationToken).OnSuccess(uploadTask =>
            {
                var result = uploadTask.Result;
                var jsonData = result.Item2;
                cancellationToken.ThrowIfCancellationRequested();

                return new FileState
                {
                    Name = jsonData["name"] as string,
                    Url = new Uri(jsonData["url"] as string, UriKind.Absolute),
                    MimeType = state.MimeType
                };
            }).ContinueWith(t =>
            {
                // Rewind the stream on failure or cancellation (if possible)
                if ((t.IsFaulted || t.IsCanceled) && dataStream.CanSeek)
                {
                    dataStream.Seek(oldPosition, SeekOrigin.Begin);
                }

                return t;
            }, cancellationToken).Unwrap();
        }
    }
}