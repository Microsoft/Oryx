﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using Microsoft.Oryx.BuildScriptGenerator.DotNetCore;
using Microsoft.Oryx.BuildScriptGeneratorCli;
using Microsoft.Oryx.Common;
using Microsoft.Oryx.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Oryx.BuildImage.Tests
{
    [Trait("platform", "dotnet")]
    public class DotNetCoreDynamicInstallTest : SampleAppsTestBase
    {
        private DockerVolume CreateSampleAppVolume(string sampleAppName) =>
            DockerVolume.CreateMirror(Path.Combine(_hostSamplesDir, "DotNetCore", sampleAppName));

        private readonly string SdkVersionMessageFormat = "Using .NET Core SDK Version: {0}";

        public DotNetCoreDynamicInstallTest(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [InlineData("NetCoreApp21WebApp", "2.1")]
        [InlineData("NetCoreApp31.MvcApp", "3.1")]
        public void BuildsApplication_InIntermediateDirectory_WhenIntermediateDirectorySwitchIsUsed(
            string appName,
            string runtimeVersion)
        {
            // Arrange
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/output";
            var script = new ShellScriptBuilder()
                .SetEnvironmentVariable(
                    SdkStorageConstants.SdkStorageBaseUrlKeyName,
                    SdkStorageConstants.DevSdkStorageBaseUrl)
                .AddBuildCommand(
                $"{appDir} -i /tmp/int -o {appOutputDir} " +
                $"--platform {DotNetCoreConstants.PlatformName} --platform-version {runtimeVersion}")
                .AddFileExistsCheck($"{appOutputDir}/{appName}.dll")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = _imageHelper.GetGitHubActionsBuildImage(),
                EnvironmentVariables = new List<EnvironmentVariable> { CreateAppNameEnvVar(appName) },
                Volumes = new List<DockerVolume> { volume },
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains(string.Format(SdkVersionMessageFormat, runtimeVersion), result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void DynamicInstall_ReInstallsSdk_IfSentinelFileIsNotPresent()
        {
            // Arrange
            var appName = "NetCoreApp31.MvcApp";
            var runtimeVersion = "3.1.2"; //NOTE: use the full version so that we know the install directory path
            var installationDir = $"{BuildScriptGenerator.Constants.TemporaryInstallationDirectoryRoot}/dotnet/runtimes/{runtimeVersion}";
            var sentinelFile = $"{installationDir}/{SdkStorageConstants.SdkDownloadSentinelFileName}";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/output";
            var buildCmd = $"{appDir} -i /tmp/int -o {appOutputDir} " +
                $"--platform {DotNetCoreConstants.PlatformName} --platform-version {runtimeVersion}";
            var script = new ShellScriptBuilder()
                .SetEnvironmentVariable(
                    SdkStorageConstants.SdkStorageBaseUrlKeyName,
                    SdkStorageConstants.DevSdkStorageBaseUrl)
                .AddBuildCommand(buildCmd)
                .AddFileExistsCheck($"{appOutputDir}/{appName}.dll")
                .AddFileExistsCheck(sentinelFile)
                .AddCommand($"rm -f {sentinelFile}")
                .AddBuildCommand(buildCmd)
                .AddFileExistsCheck(sentinelFile)
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = _imageHelper.GetGitHubActionsBuildImage(),
                EnvironmentVariables = new List<EnvironmentVariable> { CreateAppNameEnvVar(appName) },
                Volumes = new List<DockerVolume> { volume },
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains(string.Format(SdkVersionMessageFormat, runtimeVersion), result.StdOut);
                },
                result.GetDebugInfo());
        }
    }
}
