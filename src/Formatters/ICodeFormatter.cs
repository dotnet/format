﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Options;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    internal interface ICodeFormatter
    {
        /// <summary>
        /// Applies formatting and returns a formatted <see cref="Solution"/>
        /// </summary>
        Task<Solution> FormatAsync(
            Solution solution,
            ImmutableArray<(Document, OptionSet)> formattableDocuments,
            ILogger logger,
            CancellationToken cancellationToken);
    }
}
