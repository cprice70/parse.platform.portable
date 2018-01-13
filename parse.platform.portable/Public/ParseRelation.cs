// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using Parse;
using Parse.Common.Internal;
using Parse.Core.Internal;

namespace parse.platform.portable.Public
{
    /// <inheritdoc />
    /// <summary>
    /// A common base class for ParseRelations.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ParseRelationBase : IJsonConvertible
    {
        private ParseObject _parent;
        private string _key;

        internal ParseRelationBase(ParseObject parent, string key)
        {
            EnsureParentAndKey(parent, key);
        }

        internal ParseRelationBase(ParseObject parent, string key, string targetClassName)
            : this(parent, key)
        {
            TargetClassName = targetClassName;
        }

        private static IObjectSubclassingController SubclassingController => ParseCorePlugins.Instance.SubclassingController;

        internal void EnsureParentAndKey(ParseObject parent, string key)
        {
            _parent = _parent ?? parent;
            _key = _key ?? key;
            Debug.Assert(_parent == parent, "Relation retrieved from two different objects");
            Debug.Assert(_key == key, "Relation retrieved from two different keys");
        }

        internal void Add(ParseObject obj)
        {
            var change = new ParseRelationOperation(new[] {obj}, null);
            _parent.PerformOperation(_key, change);
            TargetClassName = change.TargetClassName;
        }

        internal void Remove(ParseObject obj)
        {
            var change = new ParseRelationOperation(null, new[] {obj});
            _parent.PerformOperation(_key, change);
            TargetClassName = change.TargetClassName;
        }

        IDictionary<string, object> IJsonConvertible.ToJSON()
        {
            return new Dictionary<string, object>
            {
                {"__type", "Relation"},
                {"className", TargetClassName}
            };
        }

        internal ParseQuery<T> GetQuery<T>() where T : ParseObject
        {
            if (TargetClassName != null)
            {
                return new ParseQuery<T>(TargetClassName)
                    .WhereRelatedTo(_parent, _key);
            }

            return new ParseQuery<T>(_parent.ClassName)
                .RedirectClassName(_key)
                .WhereRelatedTo(_parent, _key);
        }

        internal string TargetClassName { get; set; }

        /// <summary>
        /// Produces the proper ParseRelation&lt;T&gt; instance for the given classname.
        /// </summary>
        internal static ParseRelationBase CreateRelation(ParseObject parent,
            string key,
            string targetClassName)
        {
            var targetType = SubclassingController.GetType(targetClassName) ?? typeof(ParseObject);

            Expression<Func<ParseRelation<ParseObject>>> createRelationExpr =
                () => CreateRelation<ParseObject>(parent, key, targetClassName);
            var createRelationMethod =
                ((MethodCallExpression) createRelationExpr.Body)
                .Method
                .GetGenericMethodDefinition()
                .MakeGenericMethod(targetType);
            return (ParseRelationBase) createRelationMethod.Invoke(null, new object[] {parent, key, targetClassName});
        }

        private static ParseRelation<T> CreateRelation<T>(ParseObject parent, string key, string targetClassName)
            where T : ParseObject
        {
            return new ParseRelation<T>(parent, key, targetClassName);
        }
    }

    /// <summary>
    /// Provides access to all of the children of a many-to-many relationship. Each instance of
    /// ParseRelation is associated with a particular parent and key.
    /// </summary>
    /// <typeparam name="T">The type of the child objects.</typeparam>
    public sealed class ParseRelation<T> : ParseRelationBase where T : ParseObject
    {
        internal ParseRelation(ParseObject parent, string key) : base(parent, key)
        {
        }

        internal ParseRelation(ParseObject parent, string key, string targetClassName)
            : base(parent, key, targetClassName)
        {
        }

        /// <summary>
        /// Adds an object to this relation. The object must already have been saved.
        /// </summary>
        /// <param name="obj">The object to add.</param>
        public void Add(T obj)
        {
            base.Add(obj);
        }

        /// <summary>
        /// Removes an object from this relation. The object must already have been saved.
        /// </summary>
        /// <param name="obj">The object to remove.</param>
        public void Remove(T obj)
        {
            base.Remove(obj);
        }

        /// <summary>
        /// Gets a query that can be used to query the objects in this relation.
        /// </summary>
        public ParseQuery<T> Query
        {
            get { return GetQuery<T>(); }
        }
    }
}