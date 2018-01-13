// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Parse.Internal.Encoding;
using Parse.Public;

namespace Parse.Internal.Operation
{
    public class ParseRelationOperation : IParseFieldOperation
    {
        private readonly IList<string> _adds;
        private readonly IList<string> _removes;

        private ParseRelationOperation(IEnumerable<string> adds,
            IEnumerable<string> removes,
            string targetClassName)
        {
            TargetClassName = targetClassName;
            _adds = new ReadOnlyCollection<string>(adds.ToList());
            _removes = new ReadOnlyCollection<string>(removes.ToList());
        }

        public ParseRelationOperation(IEnumerable<ParseObject> adds,
            IEnumerable<ParseObject> removes)
        {
            adds = adds ?? new ParseObject[0];
            removes = removes ?? new ParseObject[0];
            var enumerable = removes as ParseObject[] ?? removes.ToArray();
            var objects = adds as ParseObject[] ?? adds.ToArray();
            TargetClassName = objects.Concat(enumerable).Select(o => o.ClassName).FirstOrDefault();
            _adds = new ReadOnlyCollection<string>(IdsFromObjects(objects).ToList());
            _removes = new ReadOnlyCollection<string>(IdsFromObjects(enumerable).ToList());
        }

        public object Encode()
        {
            var adds = _adds
                .Select(id => PointerOrLocalIdEncoder.Instance.Encode(
                    ParseObject.CreateWithoutData(TargetClassName, id)))
                .ToList();
            var removes = _removes
                .Select(id => PointerOrLocalIdEncoder.Instance.Encode(
                    ParseObject.CreateWithoutData(TargetClassName, id)))
                .ToList();
            var addDict = adds.Count == 0
                ? null
                : new Dictionary<string, object>
                {
                    {"__op", "AddRelation"},
                    {"objects", adds}
                };
            var removeDict = removes.Count == 0
                ? null
                : new Dictionary<string, object>
                {
                    {"__op", "RemoveRelation"},
                    {"objects", removes}
                };

            if (addDict != null && removeDict != null)
            {
                return new Dictionary<string, object>
                {
                    {"__op", "Batch"},
                    {"ops", new[] {addDict, removeDict}}
                };
            }

            return addDict ?? removeDict;
        }

        public IParseFieldOperation MergeWithPrevious(IParseFieldOperation previous)
        {
            if (previous == null)
            {
                return this;
            }

            if (previous is ParseDeleteOperation)
            {
                throw new InvalidOperationException("You can't modify a relation after deleting it.");
            }

            var other = previous as ParseRelationOperation;
            if (other != null)
            {
                if (other.TargetClassName != TargetClassName)
                {
                    throw new InvalidOperationException(
                        string.Format("Related object must be of class {0}, but {1} was passed in.",
                            other.TargetClassName,
                            TargetClassName));
                }

                var newAdd = _adds.Union(other._adds.Except(_removes)).ToList();
                var newRemove = _removes.Union(other._removes.Except(_adds)).ToList();
                return new ParseRelationOperation(newAdd, newRemove, TargetClassName);
            }

            throw new InvalidOperationException("Operation is invalid after previous operation.");
        }

        public object Apply(object oldValue, string key)
        {
            if (_adds.Count == 0 && _removes.Count == 0)
            {
                return null;
            }

            if (oldValue == null)
            {
                return ParseRelationBase.CreateRelation(null, key, TargetClassName);
            }

            if (oldValue is ParseRelationBase)
            {
                var oldRelation = (ParseRelationBase) oldValue;
                var oldClassName = oldRelation.TargetClassName;
                if (oldClassName != null && oldClassName != TargetClassName)
                {
                    throw new InvalidOperationException("Related object must be a " + oldClassName
                                                                                    + ", but a " + TargetClassName +
                                                                                    " was passed in.");
                }

                oldRelation.TargetClassName = TargetClassName;
                return oldRelation;
            }

            throw new InvalidOperationException("Operation is invalid after previous operation.");
        }

        public string TargetClassName { get; }

        private IEnumerable<string> IdsFromObjects(IEnumerable<ParseObject> objects)
        {
            var enumerable = objects as ParseObject[] ?? objects.ToArray();
            foreach (var obj in enumerable)
            {
                if (obj.ObjectId == null)
                {
                    throw new ArgumentException(
                        "You can't add an unsaved ParseObject to a relation.");
                }

                if (obj.ClassName != TargetClassName)
                {
                    throw new ArgumentException(string.Format(
                        "Tried to create a ParseRelation with 2 different types: {0} and {1}",
                        TargetClassName,
                        obj.ClassName));
                }
            }

            return enumerable.Select(o => o.ObjectId).Distinct();
        }
    }
}