// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Parse.Internal.Encoding;
using Parse.ParseCommon.Public.Utilities;

namespace Parse.Internal.Operation
{
    public class ParseAddOperation : IParseFieldOperation
    {
        private readonly ReadOnlyCollection<object> _objects;

        public ParseAddOperation(IEnumerable<object> objects)
        {
            _objects = new ReadOnlyCollection<object>(objects.ToList());
        }

        public object Encode()
        {
            return new Dictionary<string, object>
            {
                {"__op", "Add"},
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
                    return new ParseSetOperation(_objects.ToList());
                case ParseSetOperation _:
                    var setOp = (ParseSetOperation) previous;
                    var oldList = Conversion.To<IList<object>>(setOp.Value);
                    return new ParseSetOperation(oldList.Concat(_objects).ToList());
                case ParseAddOperation _:
                    return new ParseAddOperation(((ParseAddOperation) previous).Objects.Concat(_objects));
            }

            throw new InvalidOperationException("Operation is invalid after previous operation.");
        }

        public object Apply(object oldValue, string key)
        {
            if (oldValue == null)
            {
                return _objects.ToList();
            }

            var oldList = Conversion.To<IList<object>>(oldValue);
            return oldList.Concat(_objects).ToList();
        }

        public IEnumerable<object> Objects => _objects;
    }
}