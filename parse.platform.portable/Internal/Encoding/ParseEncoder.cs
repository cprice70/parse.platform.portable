// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Parse.Internal.Operation;
using Parse.Internal.Utilities;
using Parse.ParseCommon.Public.Utilities;
using Parse.Public;

namespace Parse.Internal.Encoding
{
    /// <summary>
    /// A <c>ParseEncoder</c> can be used to transform objects such as <see cref="ParseObject"/> into JSON
    /// data structures.
    /// </summary>
    /// <seealso cref="ParseDecoder"/>
    public abstract class ParseEncoder
    {
        public static bool IsValidType(object value)
        {
            return value == null ||
                   ReflectionHelpers.IsPrimitive(value.GetType()) ||
                   value is string ||
                   value is ParseObject ||
                   value is ParseAcl ||
                   value is ParseFile ||
                   value is ParseGeoPoint ||
                   value is ParseRelationBase ||
                   value is DateTime ||
                   value is byte[] ||
                   Conversion.As<IDictionary<string, object>>(value) != null ||
                   Conversion.As<IList<object>>(value) != null;
        }

        public object Encode(object value)
        {
            // If this object has a special encoding, encode it and return the
            // encoded object. Otherwise, just return the original object.
            switch (value)
            {
                case DateTime _:
                    return new Dictionary<string, object>
                    {
                        {
                            "iso",
                            ((DateTime) value).ToString(ParseClient.DateFormatStrings.First(), CultureInfo.InvariantCulture)
                        },
                        {"__type", "Date"}
                    };
                case byte[] bytes:
                    return new Dictionary<string, object>
                    {
                        {"__type", "Bytes"},
                        {"base64", Convert.ToBase64String(bytes)}
                    };
                case ParseObject obj:
                    return EncodeParseObject(obj);
                case IJsonConvertible jsonConvertible:
                    return jsonConvertible.ToJson();
            }

            var dict = Conversion.As<IDictionary<string, object>>(value);
            if (dict != null)
            {
                var json = new Dictionary<string, object>();
                foreach (var pair in dict)
                {
                    json[pair.Key] = Encode(pair.Value);
                }

                return json;
            }

            var list = Conversion.As<IList<object>>(value);
            if (list != null)
            {
                return EncodeList(list);
            }

            // TODO (hallucinogen): convert IParseFieldOperation to IJsonConvertible
            if (value is IParseFieldOperation operation)
            {
                return operation.Encode();
            }

            return value;
        }

        protected abstract IDictionary<string, object> EncodeParseObject(ParseObject value);

        private object EncodeList(IEnumerable<object> list)
        {
            var newArray = new List<object>();
            // We need to explicitly cast `list` to `List<object>` rather than
            // `IList<object>` because IL2CPP is stricter than the usual Unity AOT compiler pipeline.
            foreach (var item in list)
            {
                if (!IsValidType(item))
                {
                    throw new ArgumentException("Invalid type for value in an array");
                }

                newArray.Add(Encode(item));
            }

            return newArray;
        }
    }
}