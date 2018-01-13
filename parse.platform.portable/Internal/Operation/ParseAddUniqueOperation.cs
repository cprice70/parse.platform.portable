// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Parse.Internal.Encoding;
using Parse.ParseCommon.Public.Utilities;
using Parse.Public;

namespace Parse.Internal.Operation
{
    public class ParseAddUniqueOperation : IParseFieldOperation
    {
        private readonly ReadOnlyCollection<object> _objects;

        public ParseAddUniqueOperation(IEnumerable<object> objects)
        {
            _objects = new ReadOnlyCollection<object>(objects.Distinct().ToList());
        }

        public object Encode()
        {
            return new Dictionary<string, object>
            {
                {"__op", "AddUnique"},
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
                {
                    var setOp = (ParseSetOperation) previous;
                    var oldList = Conversion.To<IList<object>>(setOp.Value);
                    var result = Apply(oldList, null);
                    return new ParseSetOperation(result);
                }
                case ParseAddUniqueOperation _:
                {
                    var oldList = ((ParseAddUniqueOperation) previous).Objects;
                    return new ParseAddUniqueOperation((IList<object>) Apply(oldList, null));
                }
            }

            throw new InvalidOperationException("Operation is invalid after previous operation.");
        }

        public object Apply(object oldValue, string key)
        {
            if (oldValue == null)
            {
                return _objects.ToList();
            }

            var newList = Conversion.To<IList<object>>(oldValue).ToList();
            var comparer = ParseFieldOperations.ParseObjectComparer;
            foreach (var objToAdd in _objects)
            {
                if (objToAdd is ParseObject)
                {
                    var matchedObj = newList.FirstOrDefault(listObj => comparer.Equals(objToAdd, listObj));
                    if (matchedObj == null)
                    {
                        newList.Add(objToAdd);
                    }
                    else
                    {
                        var index = newList.IndexOf(matchedObj);
                        newList[index] = objToAdd;
                    }
                }
                else if (!newList.Contains(objToAdd, comparer))
                {
                    newList.Add(objToAdd);
                }
            }

            return newList;
        }

        public IEnumerable<object> Objects => _objects;
    }
}