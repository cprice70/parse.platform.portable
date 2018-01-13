// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System.Collections.Generic;
using System.Linq;
using Parse.ParseCommon.Public.Utilities;

namespace Parse.Internal.Utilities
{
    /// <inheritdoc />
    /// <summary>
    /// Provides a Dictionary implementation that can delegate to any other
    /// dictionary, regardless of its value type. Used for coercion of
    /// dictionaries when returning them to users.
    /// </summary>
    /// <typeparam name="TOut">The resulting type of value in the dictionary.</typeparam>
    /// <typeparam name="TIn">The original type of value in the dictionary.</typeparam>
    [Preserve(AllMembers = true, Conditional = false)]
    public class FlexibleDictionaryWrapper<TOut, TIn> : IDictionary<string, TOut>
    {
        private readonly IDictionary<string, TIn> _toWrap;

        public FlexibleDictionaryWrapper(IDictionary<string, TIn> toWrap)
        {
            _toWrap = toWrap;
        }

        public void Add(string key, TOut value)
        {
            _toWrap.Add(key, (TIn) Conversion.ConvertTo<TIn>(value));
        }

        public bool ContainsKey(string key)
        {
            return _toWrap.ContainsKey(key);
        }

        public ICollection<string> Keys => _toWrap.Keys;

        public bool Remove(string key)
        {
            return _toWrap.Remove(key);
        }

        public bool TryGetValue(string key, out TOut value)
        {
            var result = _toWrap.TryGetValue(key, out var outValue);
            value = (TOut) Conversion.ConvertTo<TOut>(outValue);
            return result;
        }

        public ICollection<TOut> Values
        {
            get
            {
                return _toWrap.Values
                    .Select(item => (TOut) Conversion.ConvertTo<TOut>(item)).ToList();
            }
        }

        public TOut this[string key]
        {
            get => (TOut) Conversion.ConvertTo<TOut>(_toWrap[key]);
            set => _toWrap[key] = (TIn) Conversion.ConvertTo<TIn>(value);
        }

        public void Add(KeyValuePair<string, TOut> item)
        {
            _toWrap.Add(new KeyValuePair<string, TIn>(item.Key,
                (TIn) Conversion.ConvertTo<TIn>(item.Value)));
        }

        public void Clear()
        {
            _toWrap.Clear();
        }

        public bool Contains(KeyValuePair<string, TOut> item)
        {
            return _toWrap.Contains(new KeyValuePair<string, TIn>(item.Key,
                (TIn) Conversion.ConvertTo<TIn>(item.Value)));
        }

        public void CopyTo(KeyValuePair<string, TOut>[] array, int arrayIndex)
        {
            var converted = from pair in _toWrap
                select new KeyValuePair<string, TOut>(pair.Key,
                    (TOut) Conversion.ConvertTo<TOut>(pair.Value));
            converted.ToList().CopyTo(array, arrayIndex);
        }

        public int Count => _toWrap.Count;

        public bool IsReadOnly => _toWrap.IsReadOnly;

        public bool Remove(KeyValuePair<string, TOut> item)
        {
            return _toWrap.Remove(new KeyValuePair<string, TIn>(item.Key,
                (TIn) Conversion.ConvertTo<TIn>(item.Value)));
        }

        public IEnumerator<KeyValuePair<string, TOut>> GetEnumerator()
        {
            return _toWrap.Select(pair => new KeyValuePair<string, TOut>(pair.Key,
                (TOut) Conversion.ConvertTo<TOut>(pair.Value))).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}