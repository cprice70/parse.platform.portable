// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using Parse.Public;

namespace Parse.Internal.Operation
{
    public class ParseObjectIdComparer : IEqualityComparer<object>
    {
        bool IEqualityComparer<object>.Equals(object p1, object p2)
        {
            if (p1 is ParseObject parseObj1 && p2 is ParseObject parseObj2)
            {
                return Equals(parseObj1.ObjectId, parseObj2.ObjectId);
            }

            return Equals(p1, p2);
        }

        public int GetHashCode(object p)
        {
            if (p is ParseObject parseObject)
            {
                return parseObject.ObjectId.GetHashCode();
            }

            return p.GetHashCode();
        }
    }

    static class ParseFieldOperations
    {
        private static ParseObjectIdComparer _comparer;

        public static IParseFieldOperation Decode(IDictionary<string, object> json)
        {
            throw new NotImplementedException();
        }

        public static IEqualityComparer<object> ParseObjectComparer => _comparer ?? (_comparer = new ParseObjectIdComparer());
    }
}