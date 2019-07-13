﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.Oryx.BuildScriptGenerator.Node;
using Microsoft.Oryx.Common;
using Microsoft.Oryx.Tests.Common;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Oryx.BuildImage.Tests
{
    public class NodeJSSampleAppsTestWithAppInsightsEnvVariableNotSet : NodeJSSampleAppsTestBase
    {
        public NodeJSSampleAppsTestWithAppInsightsEnvVariableNotSet(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(TestValueGenerator.GetNodeVersions_SupportDebugging),
            MemberType = typeof(TestValueGenerator))]
        public void BuildNodeApp_DoesNotConfigureAppInsights_WithCorrectNodeVersion_AIEnvironmentVariableNotSet(
            string version)
        {
            // Arrange
            var volume = CreateWebFrontEndVolume();
            var appDir = volume.ContainerDir;
            var nestedOutputDir = "/tmp/output";
            var spcifyNodeVersionCommand = "--platform nodejs --platform-version=" + version;
            var script = new ShellScriptBuilder()
                .AddCommand(
                $"oryx build {appDir} -o {nestedOutputDir} {spcifyNodeVersionCommand} --log-file {appDir}/1.log")
                .AddDirectoryExistsCheck($"{nestedOutputDir}/node_modules")
                .AddFileDoesNotExistCheck($"{nestedOutputDir}/oryx-appinsightsloader.js")
                .AddStringDoesNotExistInFileCheck(
                NodeConstants.InjectedAppInsights, $"{nestedOutputDir}/{FilePaths.BuildManifestFileName}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                Volumes = new List<DockerVolume> { volume },
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                },
                result.GetDebugInfo());
        }
    }
}