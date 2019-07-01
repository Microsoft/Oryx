﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Linq;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Oryx.Common;

namespace Microsoft.Oryx.BuildScriptGeneratorCli
{
    [Command("oryx", Description = "Generates and runs build scripts for multiple languages.")]
    [Subcommand(BuildCommand.Name,           typeof(BuildCommand))]
    [Subcommand(PlatformsCommand.Name,       typeof(PlatformsCommand))]
    [Subcommand(BuildScriptCommand.Name,     typeof(BuildScriptCommand))]
    [Subcommand(RunScriptCommand.Name,       typeof(RunScriptCommand))]
    [Subcommand(InstallRunTimeCommand.Name,  typeof(InstallRunTimeCommand))]
    [Subcommand(BuildpackDetectCommand.Name, typeof(BuildpackDetectCommand))]
    [Subcommand(BuildpackBuildCommand.Name,  typeof(BuildpackBuildCommand))]
    internal class Program
    {
        [Option(CommandOptionType.NoValue, Description = "Print version information.")]
        public bool Version { get; set; }

        internal static string GetVersion()
        {
            var informationalVersion = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (informationalVersion != null)
            {
                return informationalVersion.InformationalVersion;
            }

            return null;
        }

        internal static string GetCommit()
        {
            var commitMetadata = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
                    .Where(attr => attr.Key.Equals("GitCommit"))
                    .FirstOrDefault();
            if (commitMetadata != null)
            {
                return commitMetadata.Value;
            }

            return null;
        }

        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            if (Version)
            {
                var version = GetVersion();
                var commit = GetCommit();
                console.WriteLine($"Version: {version}, Commit: {commit}");

                return ProcessConstants.ExitSuccess;
            }

            app.ShowHelp();

            return ProcessConstants.ExitSuccess;
        }

        private static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);
    }
}