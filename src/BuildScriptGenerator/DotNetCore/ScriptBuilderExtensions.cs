﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Oryx.Common;

namespace Microsoft.Oryx.BuildScriptGenerator.DotNetCore
{
    internal static class ScriptBuilderExtensions
    {
        public static StringBuilder AddScriptToCopyToIntermediateDirectory(
            this StringBuilder scriptBuilder,
            ref string sourceDir,
            string intermediateDir,
            IEnumerable<string> excludeDirs)
        {
            if (string.IsNullOrEmpty(intermediateDir))
            {
                return scriptBuilder;
            }

            if (!Directory.Exists(intermediateDir))
            {
                scriptBuilder
                    .AppendLine()
                    .AppendLine("echo Intermediate directory does not exist, creating it...")
                    .AppendFormatWithLine("mkdir -p \"{0}\"", intermediateDir);
            }

            var excludeDirsSwitch = string.Join(" ", excludeDirs.Select(dir => $"--exclude \"{dir}\""));
            scriptBuilder
                .AppendLine()
                .AppendFormatWithLine("cd \"{0}\"", sourceDir)
                .AppendLine("echo")
                .AppendFormatWithLine(
                    "rsync --delete -rt {0} . \"{1}\"",
                    excludeDirsSwitch,
                    intermediateDir)
                .AppendLine();
            sourceDir = intermediateDir;
            return scriptBuilder;
        }

        public static StringBuilder AddScriptToRunPreBuildCommand(
            this StringBuilder scriptBuilder,
            string sourceDir,
            string preBuildCommand)
        {
            if (string.IsNullOrEmpty(preBuildCommand))
            {
                return scriptBuilder;
            }

            scriptBuilder
                .AppendLine()
                .AppendFormatWithLine("cd \"{0}\"", sourceDir)
                .AppendLine(preBuildCommand)
                .AppendLine();
            return scriptBuilder;
        }

        public static StringBuilder AddScriptToRunPostBuildCommand(
            this StringBuilder scriptBuilder,
            string sourceDir,
            string postBuildCommand)
        {
            if (string.IsNullOrEmpty(postBuildCommand))
            {
                return scriptBuilder;
            }

            scriptBuilder
                .AppendLine()
                .AppendFormatWithLine("cd \"{0}\"", sourceDir)
                .AppendLine(postBuildCommand)
                .AppendLine();
            return scriptBuilder;
        }

        public static StringBuilder AddScriptToSetupSourceAndDestinationDirectories(
            this StringBuilder scriptBuilder,
            string sourceDir,
            string temporaryDestinationDir,
            string destinationDir,
            bool hasUserSuppliedDestinationDir,
            bool zipAllOutput)
        {
            scriptBuilder
                .AppendFormatWithLine("SOURCE_DIR=\"{0}\"", sourceDir)
                .AppendLine("export SOURCE_DIR")
                .AppendLine();

            if (hasUserSuppliedDestinationDir)
            {
                scriptBuilder
                    .AppendLine("echo")
                    .AppendSourceDirectoryInfo(sourceDir)
                    .AppendDestinationDirectoryInfo(destinationDir)
                    .AppendLine("echo")
                    .AppendFormatWithLine("mkdir -p \"{0}\"", destinationDir);

                if (zipAllOutput)
                {
                    destinationDir = temporaryDestinationDir;
                }

                scriptBuilder
                    .AppendFormatWithLine("DESTINATION_DIR=\"{0}\"", destinationDir)
                    .AppendLine("export DESTINATION_DIR");
            }
            else
            {
                scriptBuilder
                    .AppendLine("echo")
                    .AppendSourceDirectoryInfo(sourceDir)
                    .AppendLine("echo");
            }

            return scriptBuilder;
        }

        public static StringBuilder AddScriptToZipAllOutput(
            this StringBuilder scriptBuilder,
            string projectFile,
            string buildConfiguration,
            string sourceDir,
            string temporaryDestinationDir,
            string finalDestinationDir,
            string postBuildCommand,
            IDictionary<string, string> buildProperties)
        {
            var zipFileName = FilePaths.CompressedOutputFileName;

            scriptBuilder
                .AppendLine()
                .AppendLine("echo")
                .AppendFormatWithLine("echo \"Publishing output to '{0}'\"", temporaryDestinationDir)
                .AppendFormatWithLine(
                    "dotnet publish \"{0}\" -c {1} -o \"{2}\"",
                    projectFile,
                    buildConfiguration,
                    temporaryDestinationDir)
                .AddScriptToRunPostBuildCommand(sourceDir, postBuildCommand)
                .AppendLine()
                .AppendLine("echo Compressing the contents of the output directory...")
                .AppendFormatWithLine("cd \"{0}\"", temporaryDestinationDir)
                .AppendFormatWithLine("tar -zcf ../{0} .", zipFileName)
                .AppendLine("cd ..")
                .AppendFormatWithLine(
                    "cp -f \"{0}\" \"{1}/{2}\"",
                    zipFileName,
                    finalDestinationDir,
                    zipFileName);

            buildProperties[ManifestFilePropertyKeys.ZipAllOutput] = "true";
            return scriptBuilder;
        }

        public static StringBuilder AddScriptToCreateManifestFile(
            this StringBuilder scriptBuilder,
            IDictionary<string, string> buildProperties,
            string manifestDir,
            string finalDestinationDir)
        {
            if (!buildProperties.Any())
            {
                return scriptBuilder;
            }

            var manifestFileDir = manifestDir;
            if (string.IsNullOrEmpty(manifestFileDir))
            {
                manifestFileDir = finalDestinationDir;
            }

            if (string.IsNullOrEmpty(manifestFileDir))
            {
                return scriptBuilder;
            }

            scriptBuilder
                .AppendLine()
                .AppendFormatWithLine("mkdir -p \"{0}\"", manifestFileDir)
                .AppendLine("echo")
                .AppendLine("echo Removing any existing manifest file...")
                .AppendFormatWithLine(
                    "rm -f \"{0}/{1}\"",
                    manifestFileDir,
                    FilePaths.BuildManifestFileName)
                .AppendLine("echo Creating a manifest file...");

            foreach (var property in buildProperties)
            {
                scriptBuilder.AppendFormatWithLine(
                    "echo '{0}=\"{1}\"' >> \"{2}/{3}\"",
                    property.Key,
                    property.Value,
                    manifestFileDir,
                    FilePaths.BuildManifestFileName);
            }

            scriptBuilder.AppendLine("echo Manifest file created.");
            return scriptBuilder;
        }

        public static StringBuilder AddScriptToRestorePackages(this StringBuilder scriptBuilder, string projectFile)
        {
            scriptBuilder
                .AppendLine("echo")
                .AppendLine("echo Restoring packages...")
                .AppendFormatWithLine("dotnet restore \"{0}\"", projectFile);
            return scriptBuilder;
        }

        public static StringBuilder AddScriptToBuildProject(this StringBuilder scriptBuilder, string projectFile)
        {
            scriptBuilder
                .AppendLine()
                .AppendLine("echo")
                .AppendFormatWithLine("echo \"Building project '{0}'\"", projectFile)
                // Use the default build configuration 'Debug' here.
                .AppendFormatWithLine("dotnet build \"{0}\"", projectFile)
                .AppendLine();
            return scriptBuilder;
        }

        public static StringBuilder AddScriptToPublishOutput(
            this StringBuilder scriptBuilder,
            string projectFile,
            string buildConfiguration,
            string finalDestinationDir)
        {
            scriptBuilder
                .AppendLine()
                .AppendFormatWithLine(
                    "echo \"Publishing output to '{0}'\"",
                    finalDestinationDir)
                .AppendFormatWithLine(
                    "dotnet publish \"{0}\" -c {1} -o \"{2}\"",
                    projectFile,
                    buildConfiguration,
                    finalDestinationDir);
            return scriptBuilder;
        }
    }
}
