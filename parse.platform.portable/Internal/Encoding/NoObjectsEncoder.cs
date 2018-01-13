// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using Parse.Public;

namespace Parse.Internal.Encoding
{
    /// <inheritdoc />
    /// <summary>
    /// A <see cref="T:Parse.Core.Internal.ParseEncoder" /> that throws an exception if it attempts to encode
    /// a <see cref="T:Parse.ParseObject" />
    /// </summary>
    public class NoObjectsEncoder : ParseEncoder
    {
        // This class isn't really a Singleton, but since it has no state, it's more efficient to get
        // the default instance.

        public static NoObjectsEncoder Instance { get; } = new NoObjectsEncoder();

        protected override IDictionary<string, object> EncodeParseObject(ParseObject value)
        {
            throw new ArgumentException("ParseObjects not allowed here.");
        }
    }
}