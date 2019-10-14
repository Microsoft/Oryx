﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Oryx.BuildScriptGenerator;
using Microsoft.Oryx.Common;

namespace Microsoft.Oryx.BuildScriptGeneratorCli
{
    [Command(Name, Description = "Generate startup script for an app.",
        ThrowOnUnexpectedArgument = false, AllowArgumentSeparator = true)]
    internal class RunScriptCommand : CommandBase
    {
        public const string Name = "run-script";

        [Argument(0, Description = "The application directory.")]
        [DirectoryExists]
        public string AppDir { get; set; } = ".";

        [Option(
            OptionTemplates.Platform,
            CommandOptionType.SingleValue,
            Description = "The name of the programming platform, e.g. 'nodejs'.")]
        public string PlatformName { get; set; }

        [Option(
            OptionTemplates.PlatformVersion,
            CommandOptionType.SingleValue,
            Description = "The version of the platform to run the application on, e.g. '10' for nodejs.")]
        public string PlatformVersion { get; set; }

        [Option(
            "--output",
            CommandOptionType.SingleValue,
            Description = "[Optional] Path to which the script will be written. If not specified, will default to STDOUT.")]
        public string OutputPath { get; set; }

        public string[] RemainingArgs { get; }

        internal override int Execute(IServiceProvider serviceProvider, IConsole console)
        {
            ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var sourceRepo = new LocalSourceRepo(AppDir, loggerFactory);

            var ctx = new RunScriptGeneratorContext
            {
                SourceRepo = sourceRepo,
                Platform = PlatformName,
                PlatformVersion = PlatformVersion,
                PassThruArguments = RemainingArgs,
            };

            var runScriptGenerator = serviceProvider.GetRequiredService<IRunScriptGenerator>();
            var script = runScriptGenerator.GenerateBashScript(ctx);
            if (string.IsNullOrEmpty(script))
            {
                console.WriteErrorLine("Couldn't generate startup script.");
                return ProcessConstants.ExitFailure;
            }

            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                console.WriteLine(script);
            }
            else
            {
                File.WriteAllText(OutputPath, script);
                console.WriteLine($"Script written to '{OutputPath}'");

                // Try making the script executable
                ProcessHelper.TrySetExecutableMode(OutputPath);
            }

            return ProcessConstants.ExitSuccess;
        }

        internal override bool IsValidInput(IServiceProvider serviceProvider, IConsole console)
        {
            AppDir = string.IsNullOrEmpty(AppDir) ? Directory.GetCurrentDirectory() : Path.GetFullPath(AppDir);
            if (!Directory.Exists(AppDir))
            {
                console.WriteErrorLine($"Could not find the source directory '{AppDir}'.");
                return false;
            }

            return true;
        }
    }
}