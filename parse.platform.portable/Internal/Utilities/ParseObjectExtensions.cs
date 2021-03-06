// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System.Collections.Generic;
using Parse.Internal.Object.State;
using Parse.Internal.Operation;
using Parse.Public;

namespace Parse.Internal.Utilities
{
    /// <summary>
    /// So here's the deal. We have a lot of internal APIs for ParseObject, ParseUser, etc.
    ///
    /// These cannot be 'internal' anymore if we are fully modularizing things out, because
    /// they are no longer a part of the same library, especially as we create things like
    /// Installation inside push library.
    ///
    /// So this class contains a bunch of extension methods that can live inside another
    /// namespace, which 'wrap' the intenral APIs that already exist.
    /// </summary>
    public static class ParseObjectExtensions
    {
        public static T FromState<T>(IObjectState state, string defaultClassName) where T : ParseObject
        {
            return ParseObject.FromState<T>(state, defaultClassName);
        }

        public static IObjectState GetState(this ParseObject obj)
        {
            return obj.State;
        }

        public static void HandleFetchResult(this ParseObject obj, IObjectState serverState)
        {
            obj.HandleFetchResult(serverState);
        }

        public static IDictionary<string, IParseFieldOperation> GetCurrentOperations(this ParseObject obj)
        {
            return obj.CurrentOperations;
        }

        public static IEnumerable<object> DeepTraversal(object root, bool traverseParseObjects = false,
            bool yieldRoot = false)
        {
            return ParseObject.DeepTraversal(root, traverseParseObjects, yieldRoot);
        }

        public static void SetIfDifferent<T>(this ParseObject obj, string key, T value)
        {
            obj.SetIfDifferent(key, value);
        }

        public static IDictionary<string, object> ServerDataToJSONObjectForSerialization(this ParseObject obj)
        {
            return obj.ServerDataToJsonObjectForSerialization();
        }

        public static void Set(this ParseObject obj, string key, object value)
        {
            obj.Set(key, value);
        }
    }
}