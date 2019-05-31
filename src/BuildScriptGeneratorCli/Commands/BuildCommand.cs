﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Oryx.BuildScriptGenerator;
using Microsoft.Oryx.Common;

namespace Microsoft.Oryx.BuildScriptGeneratorCli
{
    [Command("build", Description = "Generate and run build scripts.")]
    internal class BuildCommand : CommandBase
    {
        // Beginning and ending markers for build script output spans that should be time measured
        private readonly TextSpan[] _measurableStdOutSpans =
        {
            new TextSpan(
                "RunPreBuildScript",
                BaseBashBuildScriptProperties.PreBuildCommandPrologue,
                BaseBashBuildScriptProperties.PreBuildCommandEpilogue),
            new TextSpan(
                "RunPostBuildScript",
                BaseBashBuildScriptProperties.PostBuildCommandPrologue,
                BaseBashBuildScriptProperties.PostBuildCommandEpilogue)
        };

        [Argument(0, Description = "The source directory.")]
        public string SourceDir { get; set; }

        [Option(
            "-i|--intermediate-dir <dir>",
            CommandOptionType.SingleValue,
            Description = "The path to a temporary directory to be used by this tool.")]
        public string IntermediateDir { get; set; }

        [Option(
            "-l|--language <name>",
            CommandOptionType.SingleValue,
            Description = "The name of the programming language being used in the provided source directory.")]
        public string Language { get; set; }

        [Option(
            "--language-version <version>",
            CommandOptionType.SingleValue,
            Description = "The version of programming language being used in the provided source directory.")]
        public string LanguageVersion { get; set; }

        [Option(
            "-o|--output <dir>",
            CommandOptionType.SingleValue,
            Description = "The destination directory.")]
        public string DestinationDir { get; set; }

        [Option(
            "-p|--property <key-value>",
            CommandOptionType.MultipleValue,
            Description = "Additional information used by this tool to generate and run build scripts.")]
        public string[] Properties { get; set; }

        public static string BuildOperationName(IEnvironment env)
        {
            LoggingConstants.EnvTypeOperationNamePrefix.TryGetValue(env.Type, out var prefix);
            var opName = env.GetEnvironmentVariable(
                LoggingConstants.OperationNameSourceEnvVars.Single(e => e.Value == env.Type).Key);

            if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(opName))
            {
                return LoggingConstants.DefaultOperationName;
            }

            return $"{prefix}:{opName}";
        }

        internal override int Execute(IServiceProvider serviceProvider, IConsole console)
        {
            return Execute(serviceProvider, console, stdOutHandler: null, stdErrHandler: null);
        }

        // To enable unit testing
        internal int Execute(
            IServiceProvider serviceProvider,
            IConsole console,
            DataReceivedEventHandler stdOutHandler,
            DataReceivedEventHandler stdErrHandler)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<BuildCommand>>();
            var buildOperationId = logger.StartOperation(
                BuildOperationName(serviceProvider.GetRequiredService<IEnvironment>()));

            var options = serviceProvider.GetRequiredService<IOptions<BuildScriptGeneratorOptions>>().Value;

            console.WriteLine("Build orchestrated by Microsoft Oryx, https://github.com/Microsoft/Oryx");
            console.WriteLine("You can report issues at https://github.com/Microsoft/Oryx/issues");
            console.WriteLine();

            var buildInfo = new DefinitionListFormatter();
            buildInfo.AddDefinition("Oryx Version", $"{Program.GetVersion()}, Commit: {Program.GetCommit()}");
            buildInfo.AddDefinition("Build Operation ID", buildOperationId);

            var sourceRepo = serviceProvider.GetRequiredService<ISourceRepoProvider>().GetSourceRepo();
            var commitId = GetSourceRepoCommitId(
                serviceProvider.GetRequiredService<IEnvironment>(),
                sourceRepo,
                logger);
            if (!string.IsNullOrWhiteSpace(commitId))
            {
                buildInfo.AddDefinition("Repository Commit", commitId);
            }

            console.WriteLine(buildInfo.ToString());

            var environmentSettingsProvider = serviceProvider.GetRequiredService<IEnvironmentSettingsProvider>();
            if (!environmentSettingsProvider.TryGetAndLoadSettings(out var environmentSettings))
            {
                return ProcessConstants.ExitFailure;
            }

            // Generate build script
            string scriptContent;
            using (var stopwatch = logger.LogTimedEvent("GenerateBuildScript"))
            {
                var checkerMessages = new List<ICheckerMessage>();
                var scriptGenerator = new BuildScriptGenerator(
                    serviceProvider, console, checkerMessages, buildOperationId);

                var generated = scriptGenerator.TryGenerateScript(out scriptContent);
                stopwatch.AddProperty("generateSucceeded", generated.ToString());

                if (checkerMessages.Count > 0)
                {
                    var messageFormatter = new DefinitionListFormatter();
                    checkerMessages.ForEach(msg => messageFormatter.AddDefinition(msg.Level.ToString(), msg.Content));
                    console.WriteLine(messageFormatter.ToString());
                }
                else
                {
                    logger.LogDebug("No checker messages emitted");
                }

                if (!generated)
                {
                    return ProcessConstants.ExitFailure;
                }
            }

            // Get the path where the generated script should be written into.
            var tempDirectoryProvider = serviceProvider.GetRequiredService<ITempDirectoryProvider>();
            var buildScriptPath = Path.Combine(tempDirectoryProvider.GetTempDirectory(), "build.sh");

            // Write build script to selected path
            File.WriteAllText(buildScriptPath, scriptContent);
            logger.LogTrace("Build script written to file");

            var buildEventProps = new Dictionary<string, string>()
            {
                { "oryxVersion", Program.GetVersion() },
                { "oryxCommitId", Program.GetCommit() },
                {
                    "oryxCommandLine",
                    string.Join(
                        ' ',
                        serviceProvider.GetRequiredService<IEnvironment>().GetCommandLineArgs())
                },
                { nameof(commitId), commitId },
                { "scriptPath", buildScriptPath },
                { "envVars", string.Join(",", GetEnvVarNames(serviceProvider.GetRequiredService<IEnvironment>())) },
            };

            var buildScriptOutput = new StringBuilder();
            var stdOutEventLogger = new TextSpanEventLogger(logger, _measurableStdOutSpans);

            DataReceivedEventHandler stdOutBaseHandler = (sender, args) =>
            {
                string line = args.Data;
                if (line == null)
                {
                    return;
                }

                console.WriteLine(line);
                buildScriptOutput.AppendLine(line);
                stdOutEventLogger.CheckString(line);
            };

            DataReceivedEventHandler stdErrBaseHandler = (sender, args) =>
            {
                string line = args.Data;
                if (line == null)
                {
                    return;
                }

                console.Error.WriteLine(args.Data);
                buildScriptOutput.AppendLine(args.Data);
            };

            // Try make the pre-build & post-build scripts executable
            ProcessHelper.TrySetExecutableMode(environmentSettings.PreBuildScriptPath);
            ProcessHelper.TrySetExecutableMode(environmentSettings.PostBuildScriptPath);

            // Run the generated script
            int exitCode;
            using (var timedEvent = logger.LogTimedEvent("RunBuildScript", buildEventProps))
            {
                exitCode = serviceProvider.GetRequiredService<IScriptExecutor>().ExecuteScript(
                    buildScriptPath,
                    new[]
                    {
                        sourceRepo.RootPath,
                        options.DestinationDir ?? string.Empty,
                        options.IntermediateDir ?? string.Empty
                    },
                    workingDirectory: sourceRepo.RootPath,
                    stdOutHandler == null ? stdOutBaseHandler : stdOutBaseHandler + stdOutHandler,
                    stdErrHandler == null ? stdErrBaseHandler : stdErrBaseHandler + stdErrHandler);

                timedEvent.AddProperty("exitCode", exitCode.ToString());
            }

            logger.LogDebug("Build script content:\n" + scriptContent);
            logger.LogLongMessage(LogLevel.Debug, "Build script output", buildScriptOutput.ToString());

            if (exitCode != ProcessConstants.ExitSuccess)
            {
                logger.LogError("Build script exited with {exitCode}", exitCode);
                return exitCode;
            }

            return ProcessConstants.ExitSuccess;
        }

        internal override bool IsValidInput(IServiceProvider serviceProvider, IConsole console)
        {
            var options = serviceProvider.GetRequiredService<IOptions<BuildScriptGeneratorOptions>>().Value;
            var logger = serviceProvider.GetRequiredService<ILogger<BuildCommand>>();

            if (!Directory.Exists(options.SourceDir))
            {
                logger.LogError("Could not find the source directory {srcDir}", options.SourceDir);
                console.Error.WriteLine($"Error: Could not find the source directory '{options.SourceDir}'.");
                return false;
            }

            // Invalid to specify language version without language name
            if (string.IsNullOrEmpty(options.Language) && !string.IsNullOrEmpty(options.LanguageVersion))
            {
                logger.LogError("Cannot use lang version without lang name");
                console.Error.WriteLine("Cannot use language version without specifying language name also.");
                return false;
            }

            if (!string.IsNullOrEmpty(options.IntermediateDir))
            {
                // Intermediate directory cannot be a sub-directory of the source directory
                if (IsSubDirectory(options.IntermediateDir, options.SourceDir))
                {
                    logger.LogError(
                        "Intermediate directory {intermediateDir} cannot be a child of {srcDir}",
                        options.IntermediateDir,
                        options.SourceDir);
                    console.Error.WriteLine(
                        $"Intermediate directory '{options.IntermediateDir}' cannot be a " +
                        $"sub-directory of source directory '{options.SourceDir}'.");
                    return false;
                }
            }

            return true;
        }

        internal override void ConfigureBuildScriptGeneratorOptions(BuildScriptGeneratorOptions options)
        {
            BuildScriptGeneratorOptionsHelper.ConfigureBuildScriptGeneratorOptions(
                options,
                SourceDir,
                DestinationDir,
                IntermediateDir,
                Language,
                LanguageVersion,
                scriptOnly: false,
                Properties);
        }

        /// <summary>
        /// Checks if <paramref name="dir1"/> is a sub-directory of <paramref name="dir2"/>.
        /// </summary>
        /// <param name="dir1">The directory to be checked as subdirectory.</param>
        /// <param name="dir2">The directory to be tested as the parent.</param>
        /// <returns>true if <c>dir1</c> is a sub-directory of <c>dir2</c>, false otherwise.</returns>
        internal bool IsSubDirectory(string dir1, string dir2)
        {
            var dir1Segments = dir1.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var dir2Segments = dir2.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

            if (dir1Segments.Length < dir2Segments.Length)
            {
                return false;
            }

            // If dir1 is really a subset of dir2, then we should expect all
            // segments of dir2 appearing in dir1 and in exact order.
            for (var i = 0; i < dir2Segments.Length; i++)
            {
                // we want case-sensitive search
                if (dir1Segments[i] != dir2Segments[i])
                {
                    return false;
                }
            }

            return true;
        }

        private string GetSourceRepoCommitId(IEnvironment env, ISourceRepo repo, ILogger<BuildCommand> logger)
        {
            string commitId = env.GetEnvironmentVariable(ExtVarNames.ScmCommitIdEnvVarName);

            if (string.IsNullOrEmpty(commitId))
            {
                using (var timedEvent = logger.LogTimedEvent("GetGitCommitId"))
                {
                    commitId = repo.GetGitCommitId();
                    timedEvent.AddProperty(nameof(commitId), commitId);
                }
            }

            return commitId;
        }

        private string[] GetEnvVarNames([CanBeNull] IEnvironment env)
        {
            var envVarKeyCollection = env?.GetEnvironmentVariables()?.Keys;
            if (envVarKeyCollection == null)
            {
                return new string[] { };
            }

            string[] envVarNames = new string[envVarKeyCollection.Count];
            envVarKeyCollection.CopyTo(envVarNames, 0);
            return envVarNames;
        }
    }
}