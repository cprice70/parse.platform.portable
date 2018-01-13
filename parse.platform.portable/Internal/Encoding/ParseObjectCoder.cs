// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using Parse.Internal.Object.State;
using Parse.Public;

namespace Parse.Internal.Encoding
{
    // TODO: (richardross) refactor entire parse coder interfaces.
    public class ParseObjectCoder
    {
        public static ParseObjectCoder Instance { get; } = new ParseObjectCoder();

        // Prevent default constructor.
        private ParseObjectCoder()
        {
        }

        public static IObjectState Decode(IDictionary<string, object> data,
            ParseDecoder decoder)
        {
            IDictionary<string, object> serverData = new Dictionary<string, object>();
            var mutableData = new Dictionary<string, object>(data);
            var objectId =
                ExtractFromDictionary(mutableData, "objectId", (obj) => obj as string);
            var createdAt = ExtractFromDictionary<DateTime?>(mutableData, "createdAt",
                (obj) => ParseDecoder.ParseDate(obj as string));
            var updatedAt = ExtractFromDictionary<DateTime?>(mutableData, "updatedAt",
                (obj) => ParseDecoder.ParseDate(obj as string));

            if (mutableData.ContainsKey("ACL"))
            {
                serverData["ACL"] = ExtractFromDictionary(mutableData, "ACL",
                    (obj) => new ParseAcl(obj as IDictionary<string, object>));
            }

            if (createdAt != null && updatedAt == null)
            {
                updatedAt = createdAt;
            }

            // Bring in the new server data.
            foreach (var pair in mutableData)
            {
                if (pair.Key == "__type" || pair.Key == "className")
                {
                    continue;
                }

                var value = pair.Value;
                serverData[pair.Key] = decoder.Decode(value);
            }

            return new MutableObjectState
            {
                ObjectId = objectId,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
                ServerData = serverData
            };
        }

        private static T ExtractFromDictionary<T>(IDictionary<string, object> data, string key, Func<object, T> action)
        {
            var result = default(T);
            if (!data.ContainsKey(key)) return result;
            result = action(data[key]);
            data.Remove(key);

            return result;
        }
    }
}