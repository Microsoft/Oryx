﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.IO;
using System.Threading.Tasks;
using Microsoft.Oryx.BuildScriptGenerator.Common;
using Microsoft.Oryx.BuildScriptGenerator.DotNetCore;
using Microsoft.Oryx.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Oryx.Integration.Tests
{
    [Trait("category", "dotnetcore")]
    public class DotNetCoreRuntimeVersion50Tests : DotNetCoreEndToEndTestsBase
    {
        public DotNetCoreRuntimeVersion50Tests(ITestOutputHelper output, TestTempDirTestFixture testTempDirTestFixture)
            : base(output, testTempDirTestFixture)
        {
        }

        [Fact]
        public async Task CanBuildAndRun_NetCore50MvcApp()
        {
            // Arrange
            var dotnetcoreVersion = DotNetCoreRunTimeVersions.NetCoreApp50;
            var hostDir = Path.Combine(_hostSamplesDir, "DotNetCore", Net5MvcApp);
            var volume = DockerVolume.CreateMirror(hostDir);
            var appDir = volume.ContainerDir;
            var appOutputDir = $"{appDir}/myoutputdir";
            var buildImageScript = new ShellScriptBuilder()
                .SetEnvironmentVariable(
                    SdkStorageConstants.SdkStorageBaseUrlKeyName,
                    SdkStorageConstants.DevSdkStorageBaseUrl)
               .AddCommand(
                $"oryx build {appDir} --platform {DotNetCoreConstants.PlatformName} " +
                $"--platform-version {dotnetcoreVersion} -o {appOutputDir}")
               .ToString();
            var runtimeImageScript = new ShellScriptBuilder()
                .AddCommand(
                $"oryx create-script -appPath {appOutputDir} -bindPort {ContainerPort}")
                .AddCommand(DefaultStartupFilePath)
                .ToString();

            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                Net5MvcApp,
                _output,
                new DockerVolume[] { volume },
                _imageHelper.GetGitHubActionsBuildImage(),
                "/bin/sh",
                new[]
                {
                    "-c",
                    buildImageScript
                },
                _imageHelper.GetRuntimeImage("dotnetcore", "5.0"),
                ContainerPort,
                "/bin/sh",
                new[]
                {
                    "-c",
                    runtimeImageScript
                },
                async (hostPort) =>
                {
                    var data = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                    Assert.Contains("Welcome to ASP.NET Core MVC!", data);
                });
        }
    
        [Fact]
        public async Task CanRun_SelfContainedApp_TargetedForLinux()
        {
            // ****************************************************************
            // A self-contained app is an app which does not depend on whether a .NET Core runtime is present on the
            // target machine or not. It has all the required dependencies (.dlls etc.) in its published output itself,
            // hence it is called self-contained app.
            //
            // To test if our runtime images run self-contained apps correctly (i.e not using the dotnet runtime
            // installed in the runtime image itself), in this test we publish a self-contained 3.0 app and run it in
            // a 1.1 runtime container. This is because 1.1 runtime container does not have 3.0 bits at all and hence
            // if the app fails to run in that container, then we are doing something wrong. If all is well, this 3.0
            // app should run fine.
            // ****************************************************************

            // Arrange
            var hostDir = Path.Combine(_hostSamplesDir, "DotNetCore", Net5MvcApp);
            var volume = DockerVolume.CreateMirror(hostDir);
            var appDir = volume.ContainerDir;
            var appOutputDir = $"{appDir}/myoutputdir";
            var buildImageScript = new ShellScriptBuilder()
               .Source($"benv dotnet={DotNetCoreSdkVersions.DotNet50SdkVersion}")
               .AddCommand($"cd {appDir}")
               .AddCommand($"dotnet publish -c release -r linux-x64 -o {appOutputDir}")
               .ToString();
            var runtimeImageScript = new ShellScriptBuilder()
                .AddCommand(
                $"oryx create-script -appPath {appOutputDir} -bindPort {ContainerPort}")
                .AddCommand(DefaultStartupFilePath)
                .ToString();

            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                Net5MvcApp,
                _output,
                volume,
                "/bin/bash",
                new[]
                {
                    "-c",
                    buildImageScript
                },
                // NOTE: it is 1.1 version on purpose. Read comments at the beginning of this method for more details
                _imageHelper.GetRuntimeImage("dotnetcore", "1.1"),
                ContainerPort,
                "/bin/sh",
                new[]
                {
                    "-c",
                    runtimeImageScript
                },
                async (hostPort) =>
                {
                    var data = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                    Assert.Contains("Welcome to ASP.NET Core MVC!", data);
                });
        }
    }
}