﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static Microsoft.CodeAnalysis.Tools.FormatCommandCommon;

namespace Microsoft.CodeAnalysis.Tools.Commands
{
    internal static class FormatWhitespaceCommand
    {
        // This delegate should be kept in Sync with the FormatCommand options and argument names
        // so that values bind correctly.
        internal delegate Task<int> Handler(
            bool folder,
            string? workspace,
            bool noRestore,
            bool check,
            string[] include,
            string[] exclude,
            bool includeGenerated,
            string? verbosity,
            string? binarylog,
            string? report,
            IConsole console);

        private static readonly FormatWhitespaceHandler s_formattingHandler = new();

        internal static Symbol GetCommand()
        {
            var command = new Command("whitespace", Resources.Run_whitespace_formatting)
            {
                FolderOption
            };
            command.AddCommonOptions();
            command.AddValidator(EnsureFolderNotSpecifiedWithNoRestore);
            command.AddValidator(EnsureFolderNotSpecifiedWhenLoggingBinlog);
            command.Handler = s_formattingHandler;
            return command;
        }

        internal static string? EnsureFolderNotSpecifiedWithNoRestore(CommandResult symbolResult)
        {
            var folder = symbolResult.GetValueForOption<bool>("--folder");
            var noRestore = symbolResult.GetOptionResult("--no-restore");
            return folder && noRestore != null
                ? Resources.Cannot_specify_the_folder_option_with_no_restore
                : null;
        }

        internal static string? EnsureFolderNotSpecifiedWhenLoggingBinlog(CommandResult symbolResult)
        {
            var folder = symbolResult.GetValueForOption<bool>("--folder");
            var binarylog = symbolResult.GetOptionResult("--binarylog");
            return folder && binarylog is not null && !binarylog.IsImplicit
                ? Resources.Cannot_specify_the_folder_option_when_writing_a_binary_log
                : null;
        }

        private class FormatWhitespaceHandler : ICommandHandler
        {
            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var parseResult = context.ParseResult;
                var formatOptions = parseResult.ParseVerbosityOption(FormatOptions.Instance);
                var logger = context.Console.SetupLogging(minimalLogLevel: formatOptions.LogLevel, minimalErrorLevel: LogLevel.Warning);
                formatOptions = parseResult.ParseCommonOptions(formatOptions, logger);

                try
                {
                    formatOptions = parseResult.ParseWorkspaceOptions(formatOptions);
                }
                catch (FileNotFoundException fex)
                {
                    logger.LogError(fex.Message);
                    return UnhandledExceptionExitCode;
                }

                formatOptions = formatOptions with { FixCategory = FixCategory.Whitespace };

                return await FormatAsync(formatOptions, logger, context.GetCancellationToken()).ConfigureAwait(false);
            }
        }
    }
}
