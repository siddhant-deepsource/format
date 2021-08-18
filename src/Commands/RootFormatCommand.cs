﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static Microsoft.CodeAnalysis.Tools.FormatCommandCommon;

namespace Microsoft.CodeAnalysis.Tools.Commands
{
    internal static class RootFormatCommand
    {
        private static readonly FormatCommandDefaultHandler s_formatCommandHandler = new();

        public static RootCommand GetCommand()
        {
            var formatCommand = new RootCommand(Resources.Formats_code_to_match_editorconfig_settings)
            {
                FormatWhitespaceCommand.GetCommand(),
                FormatStyleCommand.GetCommand(),
                FormatAnalyzersCommand.GetCommand(),
                DiagnosticsOption,
                SeverityOption,
            };
            formatCommand.AddCommonOptions();
            formatCommand.Handler = s_formatCommandHandler;
            return formatCommand;
        }

        private class FormatCommandDefaultHandler : ICommandHandler
        {
            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var parseResult = context.ParseResult;
                var formatOptions = parseResult.ParseVerbosityOption(FormatOptions.Instance);
                var logger = context.Console.SetupLogging(minimalLogLevel: formatOptions.LogLevel, minimalErrorLevel: LogLevel.Warning);
                formatOptions = parseResult.ParseCommonOptions(formatOptions, logger);
                formatOptions = parseResult.ParseWorkspaceOptions(formatOptions);

                if (parseResult.HasOption(SeverityOption) &&
                    parseResult.ValueForOption(SeverityOption) is string { Length: > 0 } defaultSeverity)
                {
                    formatOptions = formatOptions with { AnalyzerSeverity = GetSeverity(defaultSeverity) };
                    formatOptions = formatOptions with { CodeStyleSeverity = GetSeverity(defaultSeverity) };
                }

                if (parseResult.HasOption(DiagnosticsOption) &&
                    parseResult.ValueForOption(DiagnosticsOption) is string[] { Length: > 0 } diagnostics)
                {
                    formatOptions = formatOptions with { Diagnostics = diagnostics.ToImmutableHashSet() };
                }

                formatOptions = formatOptions with { FixCategory = FixCategory.Whitespace | FixCategory.CodeStyle | FixCategory.Analyzers };

                var formatResult = await CodeFormatter.FormatWorkspaceAsync(
                    formatOptions,
                    logger,
                    context.GetCancellationToken(),
                    binaryLogPath: formatOptions.BinaryLogPath).ConfigureAwait(false);
                return formatResult.GetExitCode(formatOptions.ChangesAreErrors);

                static DiagnosticSeverity GetSeverity(string? severity)
                {
                    return severity?.ToLowerInvariant() switch
                    {
                        "" => DiagnosticSeverity.Error,
                        FixSeverity.Error => DiagnosticSeverity.Error,
                        FixSeverity.Warn => DiagnosticSeverity.Warning,
                        FixSeverity.Info => DiagnosticSeverity.Info,
                        _ => throw new ArgumentOutOfRangeException(nameof(severity)),
                    };
                }
            }
        }
    }
}
