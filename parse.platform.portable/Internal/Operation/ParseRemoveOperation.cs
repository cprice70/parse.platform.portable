// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Parse.Internal.Encoding;
using Parse.ParseCommon.Public.Utilities;

namespace Parse.Internal.Operation
{
    public class ParseRemoveOperation : IParseFieldOperation
    {
        private readonly ReadOnlyCollection<object> _objects;

        public ParseRemoveOperation(IEnumerable<object> objects)
        {
            _objects = new ReadOnlyCollection<object>(objects.Distinct().ToList());
        }

        public object Encode()
        {
            return new Dictionary<string, object>
            {
                {"__op", "Remove"},
                {"objects", PointerOrLocalIdEncoder.Instance.Encode(_objects)}
            };
        }

        public IParseFieldOperation MergeWithPrevious(IParseFieldOperation previous)
        {
            switch (previous)
            {
                case null:
                    return this;
                case ParseDeleteOperation _:
                    return previous;
                case ParseSetOperation _:
                    var setOp = (ParseSetOperation) previous;
                    var oldList = Conversion.As<IList<object>>(setOp.Value);
                    return new ParseSetOperation(Apply(oldList, null));
                case ParseRemoveOperation _:
                    var oldOp = (ParseRemoveOperation) previous;
                    return new ParseRemoveOperation(oldOp.Objects.Concat(_objects));
            }

            throw new InvalidOperationException("Operation is invalid after previous operation.");
        }

        public object Apply(object oldValue, string key)
        {
            if (oldValue == null)
            {
                return new List<object>();
            }

            var oldList = Conversion.As<IList<object>>(oldValue);
            return oldList.Except(_objects, ParseFieldOperations.ParseObjectComparer).ToList();
        }

        public IEnumerable<object> Objects => _objects;
    }
}