// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Parse.Internal.Object.State;
using Parse.Internal.Operation;

namespace Parse.Internal.Object.Controller
{
    public interface IParseObjectController
    {
        Task<IObjectState> FetchAsync(IObjectState state,
            string sessionToken,
            CancellationToken cancellationToken);

        Task<IObjectState> SaveAsync(IObjectState state,
            IDictionary<string, IParseFieldOperation> operations,
            string sessionToken,
            CancellationToken cancellationToken);

        IEnumerable<Task<IObjectState>> SaveAllAsync(IEnumerable<IObjectState> states,
            IEnumerable<IDictionary<string, IParseFieldOperation>> operationsList,
            string sessionToken,
            CancellationToken cancellationToken);

        Task DeleteAsync(IObjectState state,
            string sessionToken,
            CancellationToken cancellationToken);

        IEnumerable<Task> DeleteAllAsync(IEnumerable<IObjectState> states,
            string sessionToken,
            CancellationToken cancellationToken);
    }
}