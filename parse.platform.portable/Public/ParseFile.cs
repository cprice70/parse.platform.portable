// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Parse.Internal;
using Parse.Internal.File.Controller;
using Parse.Internal.File.State;
using Parse.Internal.Utilities;
using Parse.ParseCommon.Public;

namespace Parse.Public
{
    /// <inheritdoc />
    ///  <summary>
    ///  ParseFile is a local representation of a file that is saved to the Parse cloud.
    ///  </summary>
    ///  <example>
    ///  The workflow is to construct a <see cref="T:Parse.Public.ParseFile" /> with data and a filename,
    ///  then save it and set it as a field on a ParseObject:
    ///  <code>
    ///  var file = new ParseFile("hello.txt",
    ///      new MemoryStream(Encoding.UTF8.GetBytes("hello")));
    ///  await file.SaveAsync();
    ///  var obj = new ParseObject("TestObject");
    ///  obj["file"] = file;
    ///  await obj.SaveAsync();
    ///  </code>
    ///  </example>
    public class ParseFile : IJsonConvertible
    {
        private FileState _state;
        private readonly Stream _dataStream;
        private readonly TaskQueue _taskQueue = new TaskQueue();

        #region Constructor

        internal ParseFile(string name, Uri uri, string mimeType = null)
        {
            _state = new FileState
            {
                Name = name,
                Url = uri,
                MimeType = mimeType
            };
        }

        /// <summary>
        /// Creates a new file from a byte array and a name.
        /// </summary>
        /// <param name="name">The file's name, ideally with an extension. The file name
        /// must begin with an alphanumeric character, and consist of alphanumeric
        /// characters, periods, spaces, underscores, or dashes.</param>
        /// <param name="data">The file's data.</param>
        /// <param name="mimeType">To specify the content-type used when uploading the
        /// file, provide this parameter.</param>
        public ParseFile(string name, byte[] data, string mimeType = null)
            : this(name, new MemoryStream(data), mimeType)
        {
        }

        /// <summary>
        /// Creates a new file from a stream and a name.
        /// </summary>
        /// <param name="name">The file's name, ideally with an extension. The file name
        /// must begin with an alphanumeric character, and consist of alphanumeric
        /// characters, periods, spaces, underscores, or dashes.</param>
        /// <param name="data">The file's data.</param>
        /// <param name="mimeType">To specify the content-type used when uploading the
        /// file, provide this parameter.</param>
        private ParseFile(string name, Stream data, string mimeType = null)
        {
            _state = new FileState
            {
                Name = name,
                MimeType = mimeType
            };
            _dataStream = data;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets whether the file still needs to be saved.
        /// </summary>
        public bool IsDirty => _state.Url == null;

        /// <summary>
        /// Gets the name of the file. Before save is called, this is the filename given by
        /// the user. After save is called, that name gets prefixed with a unique identifier.
        /// </summary>
        [ParseFieldName("name")]
        private string Name => _state.Name;

        /// <summary>
        /// Gets the MIME type of the file. This is either passed in to the constructor or
        /// inferred from the file extension. "unknown/unknown" will be used if neither is
        /// available.
        /// </summary>
        public string MimeType => _state.MimeType;

        /// <summary>
        /// Gets the url of the file. It is only available after you save the file or after
        /// you get the file from a <see cref="ParseObject"/>.
        /// </summary>
        [ParseFieldName("url")]
        private Uri Url => _state.SecureUrl;

        private static IParseFileController FileController => ParseCorePlugins.Instance.FileController;

        #endregion

        IDictionary<string, object> IJsonConvertible.ToJson()
        {
            if (IsDirty)
            {
                throw new InvalidOperationException(
                    "ParseFile must be saved before it can be serialized.");
            }

            return new Dictionary<string, object>
            {
                {"__type", "File"},
                {"name", Name},
                {"url", Url.AbsoluteUri}
            };
        }

        #region Save

        /// <summary>
        /// Saves the file to the Parse cloud.
        /// </summary>
        public Task SaveAsync()
        {
            return SaveAsync(null, CancellationToken.None);
        }

        /// <summary>
        /// Saves the file to the Parse cloud.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public Task SaveAsync(CancellationToken cancellationToken)
        {
            return SaveAsync(null, cancellationToken);
        }

        /// <summary>
        /// Saves the file to the Parse cloud.
        /// </summary>
        /// <param name="progress">The progress callback.</param>
        public Task SaveAsync(IProgress<ParseUploadProgressEventArgs> progress)
        {
            return SaveAsync(progress, CancellationToken.None);
        }

        /// <summary>
        /// Saves the file to the Parse cloud.
        /// </summary>
        /// <param name="progress">The progress callback.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private Task SaveAsync(IProgress<ParseUploadProgressEventArgs> progress,
            CancellationToken cancellationToken)
        {
            return _taskQueue.Enqueue(
                    toAwait => FileController.SaveAsync(_state, _dataStream, ParseUser.CurrentSessionToken, progress,
                        cancellationToken), cancellationToken)
                .OnSuccess(t => { _state = t.Result; });
        }

        #endregion
    }
}