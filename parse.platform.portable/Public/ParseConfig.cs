// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Parse.Internal;
using Parse.Internal.Config.Controller;
using Parse.Internal.Encoding;
using Parse.Internal.Utilities;
using Parse.ParseCommon.Public.Utilities;

namespace Parse.Public
{
    /// <inheritdoc />
    /// <summary>
    /// The ParseConfig is a representation of the remote configuration object,
    /// that enables you to add things like feature gating, a/b testing or simple "Message of the day".
    /// </summary>
    public sealed class ParseConfig : IJsonConvertible
    {
        private readonly IDictionary<string, object> properties = new Dictionary<string, object>();

        /// <summary>
        /// Gets the latest fetched ParseConfig.
        /// </summary>
        /// <returns>ParseConfig object</returns>
        public static ParseConfig CurrentConfig
        {
            get
            {
                var task = ConfigController.CurrentConfigController.GetCurrentConfigAsync();
                task.Wait();
                return task.Result;
            }
        }

        internal static void ClearCurrentConfig()
        {
            ConfigController.CurrentConfigController.ClearCurrentConfigAsync().Wait();
        }

        internal static void ClearCurrentConfigInMemory()
        {
            ConfigController.CurrentConfigController.ClearCurrentConfigInMemoryAsync().Wait();
        }

        private static IParseConfigController ConfigController => ParseCorePlugins.Instance.ConfigController;

        internal ParseConfig()
        {
        }

        internal ParseConfig(IDictionary<string, object> fetchedConfig)
        {
            var props = ParseDecoder.Instance.Decode(fetchedConfig["params"]) as IDictionary<string, object>;
            properties = props;
        }

        /// <summary>
        /// Retrieves the ParseConfig asynchronously from the server.
        /// </summary>
        /// <returns>ParseConfig object that was fetched</returns>
        public static Task<ParseConfig> GetAsync()
        {
            return GetAsync(CancellationToken.None);
        }

        /// <summary>
        /// Retrieves the ParseConfig asynchronously from the server.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>ParseConfig object that was fetched</returns>
        public static Task<ParseConfig> GetAsync(CancellationToken cancellationToken)
        {
            return ConfigController.FetchConfigAsync(ParseUser.CurrentSessionToken, cancellationToken);
        }

        /// <summary>
        /// Gets a value for the key of a particular type.
        /// </summary>
        /// <typeparam name="T">The type to convert the value to. Supported types are
        /// ParseObject and its descendents, Parse types such as ParseRelation and ParseGeopoint,
        /// primitive types,IList&lt;T&gt;, IDictionary&lt;string, T&gt; and strings.</typeparam>
        /// <param name="key">The key of the element to get.</param>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">The property is retrieved
        /// and <paramref name="key"/> is not found.</exception>
        /// <exception cref="System.FormatException">The property under this <paramref name="key"/>
        /// key was found, but of a different type.</exception>
        public T Get<T>(string key)
        {
            return Conversion.To<T>(properties[key]);
        }

        /// <summary>
        /// Populates result with the value for the key, if possible.
        /// </summary>
        /// <typeparam name="T">The desired type for the value.</typeparam>
        /// <param name="key">The key to retrieve a value for.</param>
        /// <param name="result">The value for the given key, converted to the
        /// requested type, or null if unsuccessful.</param>
        /// <returns>true if the lookup and conversion succeeded, otherwise false.</returns>
        public bool TryGetValue<T>(string key, out T result)
        {
            if (properties.ContainsKey(key))
            {
                try
                {
                    var temp = Conversion.To<T>(properties[key]);
                    result = temp;
                    return true;
                }
                catch (Exception ex)
                {
                    // Could not convert, do nothing
                }
            }

            result = default(T);
            return false;
        }

        IDictionary<string, object> IJsonConvertible.ToJson()
        {
            return new Dictionary<string, object>
            {
                {"params", NoObjectsEncoder.Instance.Encode(properties)}
            };
        }
    }
}