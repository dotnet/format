// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal static class FormatCommand
    {
        internal static string[] VerbosityLevels => new[] { "q", "quiet", "m", "minimal", "n", "normal", "d", "detailed", "diag", "diagnostic" };
        internal static string[] SeverityLevels => new[] { FixSeverity.Info, FixSeverity.Warn, FixSeverity.Error };

        internal static RootCommand CreateCommandLineOptions()
        {
            var rootCommand = new RootCommand
            {
                new Argument<string?>("workspace")
                {
                    Arity = ArgumentArity.ZeroOrOne,
                    Description = Resources.A_path_to_a_solution_file_a_project_file_or_a_folder_containing_a_solution_or_project_file_If_a_path_is_not_specified_then_the_current_directory_is_used
                }.LegalFilePathsOnly(),
                new Option<bool>(new[] { "--folder", "-f" }, Resources.Whether_to_treat_the_workspace_argument_as_a_simple_folder_of_files),
                new Option<DiagnosticSeverity?>(new[] { "--fix-style" }, Resources.Run_code_style_analyzer_and_apply_fixes)
                {
                    Argument = new Argument<DiagnosticSeverity?>(parse: ParseDiagnosticSeverity) { Arity = ArgumentArity.ZeroOrOne }.FromAmong(SeverityLevels)
                },
                new Option<DiagnosticSeverity?>(new[] { "--fix-analyzers" }, Resources.Run_code_style_analyzer_and_apply_fixes)
                {
                    Argument = new Argument<DiagnosticSeverity?>(parse: ParseDiagnosticSeverity) { Arity = ArgumentArity.ZeroOrOne }.FromAmong(SeverityLevels)
                },
                new Option<string[]>(new[] { "--include" }, getDefaultValue: () => Array.Empty<string>(), description: Resources.A_list_of_relative_file_or_folder_paths_to_include_in_formatting_All_files_are_formatted_if_empty),
                new Option<string[]>(new[] { "--exclude" }, getDefaultValue: () => Array.Empty<string>(), description: Resources.A_list_of_relative_file_or_folder_paths_to_exclude_from_formatting),
                new Option<bool>(new[] { "--check" }, Resources.Formats_files_without_saving_changes_to_disk_Terminates_with_a_non_zero_exit_code_if_any_files_were_formatted),
                new Option<string?>(new[] { "--report" }, getDefaultValue: () => null, description: Resources.Accepts_a_file_path_which_if_provided_will_produce_a_format_report_json_file_in_the_given_directory),
                new Option<LogLevel?>(new[] { "--verbosity", "-v" }, Resources.Set_the_verbosity_level_Allowed_values_are_quiet_minimal_normal_detailed_and_diagnostic)
                {
                    Argument = new Argument<LogLevel?>(parse: ParseLogLevel) { Arity = ArgumentArity.ExactlyOne }.FromAmong(VerbosityLevels)
                },
                new Option<bool>(new[] { "--include-generated" }, Resources.Include_generated_code_files_in_formatting_operations)
                {
                    IsHidden = true
                },
            };

            rootCommand.Description = "dotnet-format";

            return rootCommand;
        }

        private static LogLevel? ParseLogLevel(ArgumentResult result)
        {
            var verbosity = result.Tokens.Single();
            return verbosity.Value.ToLowerInvariant() switch
            {
                "q" => LogLevel.Error,
                "quiet" => LogLevel.Error,
                "m" => LogLevel.Warning,
                "minimal" => LogLevel.Warning,
                "n" => LogLevel.Information,
                "normal" => LogLevel.Information,
                "d" => LogLevel.Debug,
                "diag" => LogLevel.Debug,
                "diagnostic" => LogLevel.Trace,
                _ => LogLevel.Information
            };
        }

        private static DiagnosticSeverity? ParseDiagnosticSeverity(ArgumentResult result)
        {
            if (result.Tokens.Count == 0)
            {
                // Use Defaults
                return DiagnosticSeverity.Error;
            }

            var severityValue = result.Tokens.Single().Value;
            if (!HasMatch(severityValue, out var severity))
            {
                // Assume that this means our arity is zero and the parser should re-parse accordingly
                result.ErrorMessage = $"Cannot specify severity more than once for {result.Argument.Aliases.FirstOrDefault()}";
                return default;
            }

            return severity;

            static bool HasMatch(string? severityValue, out DiagnosticSeverity severity)
            {
                try
                {
                    severity = ParseSeverity(severityValue);
                    return true;
                }
                catch (Exception)
                {
                    severity = default;
                    return false;
                }
            }

            static DiagnosticSeverity ParseSeverity(string? severityValue)
            {
                return severityValue?.ToLowerInvariant() switch
                {
                    FixSeverity.Error => DiagnosticSeverity.Error,
                    FixSeverity.Warn => DiagnosticSeverity.Warning,
                    FixSeverity.Info => DiagnosticSeverity.Info,
                    _ => throw new ArgumentException(nameof(severityValue)),
                };
            }
        }
    }
}
