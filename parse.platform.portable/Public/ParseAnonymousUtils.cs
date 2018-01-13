using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Parse.Public
{
    /// <summary>
    /// Parse anonymous utils.
    /// </summary>
    public static class ParseAnonymousUtils
    {
        /// <summary>
        /// The type of the auth.
        /// </summary>
        private const string AuthType = "anonymous";

        /// <summary>
        /// Whether the user is logged in anonymously
        /// 
        /// </summary>
        public static bool IsLinked(this ParseUser user)
        {
            return user.IsLinked(AuthType);
        }

        /// <summary>
        /// Logs the in async.
        /// </summary>
        /// <returns>The in async.</returns>
        /// <param name="token">Token.</param>
        public static Task<ParseUser> LogInAsync(CancellationToken token)
        {
            return ParseUser.LogInAsync(AuthType, GetAuthData(), token);
        }

        /// <summary>
        /// Gets the auth data.
        /// </summary>
        /// <returns>The auth data.</returns>
        private static Dictionary<string, object> GetAuthData()
        {
            return new Dictionary<string, object>
            {
                {"id", Guid.NewGuid().ToString()}
            };
        }
    }
}