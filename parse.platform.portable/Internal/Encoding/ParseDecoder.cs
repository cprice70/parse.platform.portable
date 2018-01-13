// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Parse.Internal.Operation;
using Parse.ParseCommon.Public.Utilities;
using Parse.Public;

namespace Parse.Internal.Encoding
{
    public sealed class ParseDecoder
    {
        // This class isn't really a Singleton, but since it has no state, it's more efficient to get
        // the default instance.

        public static ParseDecoder Instance { get; } = new ParseDecoder();

        // Prevent default constructor.
        private ParseDecoder()
        {
        }

        public object Decode(object data)
        {
            if (data == null)
            {
                return null;
            }

            if (data is IDictionary<string, object> dict)
            {
                if (dict.ContainsKey("__op"))
                {
                    return ParseFieldOperations.Decode(dict);
                }

                dict.TryGetValue("__type", out var type);

                if (!(type is string typeString))
                {
                    var newDict = new Dictionary<string, object>();
                    foreach (var pair in dict)
                    {
                        newDict[pair.Key] = Decode(pair.Value);
                    }

                    return newDict;
                }

                switch (typeString)
                {
                    case "Date":
                        return ParseDate(dict["iso"] as string);
                    case "Bytes":
                        return Convert.FromBase64String(dict["base64"] as string);
                    case "Pointer":
                        return DecodePointer(dict["className"] as string, dict["objectId"] as string);
                    case "File":
                        return new ParseFile(dict["name"] as string, new Uri(dict["url"] as string));
                    case "GeoPoint":
                        return new ParseGeoPoint(Conversion.To<double>(dict["latitude"]),
                            Conversion.To<double>(dict["longitude"]));
                    case "Object":
                        var state = ParseObjectCoder.Decode(dict, this);
                        return ParseObject.FromState<ParseObject>(state, dict["className"] as string);
                    case "Relation":
                        return ParseRelationBase.CreateRelation(null, null, dict["className"] as string);
                }

                var converted = new Dictionary<string, object>();
                foreach (var pair in dict)
                {
                    converted[pair.Key] = Decode(pair.Value);
                }

                return converted;
            }

            if (data is IList<object> list)
            {
                return (from item in list
                    select Decode(item)).ToList();
            }

            return data;
        }

        private static object DecodePointer(string className, string objectId)
        {
            return ParseObject.CreateWithoutData(className, objectId);
        }

        public static DateTime ParseDate(string input)
        {
            // TODO(hallucinogen): Figure out if we should be more flexible with the date formats
            // we accept.
            return DateTime.ParseExact(input,
                ParseClient.DateFormatStrings,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None);
        }
    }
}