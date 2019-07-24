// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.CodingConventions;
using NonBlocking;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    [Export(typeof(ICodingConventionsManager)), Shared]
    internal class CachingCodingConventionsManager : ICodingConventionsManager
    {
        private static readonly ICodingConventionsManager _codingConventionsManager = CodingConventionsManagerFactory.CreateCodingConventionsManager();
        private static readonly ConcurrentDictionary<string, ICodingConventionContext> _contextCache = new ConcurrentDictionary<string, ICodingConventionContext>();

        public async Task<ICodingConventionContext> GetConventionContextAsync(string filePathContext, CancellationToken cancellationToken)
        {
            if (!_contextCache.ContainsKey(filePathContext))
            {
                var context = await _codingConventionsManager.GetConventionContextAsync(filePathContext, cancellationToken);
                _contextCache[filePathContext] = context;
            }

            return _contextCache[filePathContext];
        }

        public async Task<bool> HasConventionContextAsync(string filePathContext, CancellationToken cancellationToken)
        {
            var context = await GetConventionContextAsync(filePathContext, cancellationToken);
            return context?.CurrentConventions is object;
        }
    }
}
