﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.Oryx.Common;
using Microsoft.Oryx.Tests.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Oryx.Integration.Tests
{
    [Trait("category", "node")]
    public class NodeAngularAppTest : NodeEndToEndTestsBase
    {
        public NodeAngularAppTest(ITestOutputHelper output, TestTempDirTestFixture fixture)
            : base(output, fixture)
        {
        }

        // Official Node.js version that is supported by Angular CLI 6.0+ is 8.9 or greater
        [Theory]
        [InlineData("8")]
        [InlineData("9.4")]
        [InlineData("10")]
        [InlineData("12")]
        public async Task CanBuildAndRun_Angular6NodeApp_WithoutZippingNodeModules(string nodeVersion)
        {
            // Arrange
            var appName = "angular6app";
            var volume = CreateAppVolume(appName);
            var appDir = volume.ContainerDir;
            var buildScript = new ShellScriptBuilder()
               .AddCommand($"oryx build {appDir} --platform nodejs --platform-version {nodeVersion}")
               .ToString();
            var runScript = new ShellScriptBuilder()
                .AddCommand($"export PORT=4200")
                .AddCommand($"oryx -appPath {appDir}")
                .AddCommand(DefaultStartupFilePath)
                .ToString();

            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                appName,
                _output,
                volume,
                "/bin/sh",
                new[]
                {
                    "-c",
                    buildScript
                },
                _imageHelper.GetTestRuntimeImage("node", nodeVersion),
                4200,
                "/bin/sh",
                new[]
                {
                    "-c",
                    runScript
                },
                async (hostPort) =>
                {
                    var data = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                    Assert.Contains("Angular6app", data);
                });
        }

        [Theory]
        [InlineData("8")]
        [InlineData("9.4")]
        [InlineData("10")]
        [InlineData("12")]
        public async Task CanBuildAndRunAngular6App_WithDevAndProdDependencies_UsingZippedNodeModules(string nodeVersion)
        {
            // Arrange
            string compressFormat = "tar-gz";
            var appOutputDirPath = Directory.CreateDirectory(Path.Combine(_tempRootDir, Guid.NewGuid().ToString("N")))
                .FullName;
            var appOutputDirVolume = DockerVolume.CreateMirror(appOutputDirPath);
            var appOutputDir = appOutputDirVolume.ContainerDir;
            var appName = "angular6app";
            var volume = CreateAppVolume(appName);
            var appDir = volume.ContainerDir;
            var runAppScript = new ShellScriptBuilder()
                .AddCommand($"export PORT=4200")
                .AddCommand($"oryx -appPath {appOutputDir}")
                .AddCommand(DefaultStartupFilePath)
                .ToString();
            var buildScript = new ShellScriptBuilder()
                .AddCommand(
                $"oryx build {appDir} -i /tmp/int -o {appOutputDir} --platform nodejs " +
                $"--platform-version {nodeVersion} -p compress_node_modules={compressFormat}")
                .ToString();

            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                appName,
                _output,
                new List<DockerVolume> { appOutputDirVolume, volume },
                "/bin/sh",
                new[]
                {
                    "-c",
                    buildScript
                },
                _imageHelper.GetTestRuntimeImage("node", nodeVersion),
                4200,
                "/bin/sh",
                new[]
                {
                    "-c",
                    runAppScript
                },
                async (hostPort) =>
                {
                    var data = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                    Assert.Contains("Angular6app", data);
                });

            // Re-run the runtime container multiple times against the same output to catch any issues.
            var dockerCli = new DockerCli();
            for (var i = 0; i < 5; i++)
            {
                await EndToEndTestHelper.RunAndAssertAppAsync(
                    imageName: _imageHelper.GetTestRuntimeImage("node", nodeVersion),
                    output: _output,
                    volumes: new List<DockerVolume> { appOutputDirVolume, volume },
                    environmentVariables: new List<EnvironmentVariable>(),
                    port: 4200,
                    link: null,
                    runCmd: "/bin/sh",
                    runArgs: new[]
                    {
                    "-c",
                    runAppScript
                    },
                    assertAction: async (hostPort) =>
                    {
                        var data = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                        Assert.Contains("Angular6app", data);
                    },
                    dockerCli);
            }
        }

        // Official Node.js version that is supported by Angular CLI 8.0+ is 10.9 or greater
        [Theory]
        [InlineData("10")]
        [InlineData("12")]
        public async Task CanBuildAndRun_Angular8NodeApp_WithoutZippingNodeModules(string nodeVersion)
        {
            // Arrange
            var appName = "angular8app";
            var volume = CreateAppVolume(appName);
            var appDir = volume.ContainerDir;
            var buildScript = new ShellScriptBuilder()
               .AddCommand($"oryx build {appDir} --platform nodejs --platform-version {nodeVersion}")
               .ToString();
            var runScript = new ShellScriptBuilder()
                .AddCommand($"export PORT=4200")
                .AddCommand($"oryx -appPath {appDir}")
                .AddCommand(DefaultStartupFilePath)
                .ToString();

            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                appName,
                _output,
                volume,
                "/bin/sh",
                new[]
                {
                    "-c",
                    buildScript
                },
                _imageHelper.GetTestRuntimeImage("node", nodeVersion),
                4200,
                "/bin/sh",
                new[]
                {
                    "-c",
                    runScript
                },
                async (hostPort) =>
                {
                    var data = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                    Assert.Contains("Angular8app", data);
                });
        }

        [Theory]
        [InlineData("10")]
        [InlineData("12")]
        public async Task CanBuildAndRunAngular8App_WithDevAndProdDependencies_UsingZippedNodeModules(string nodeVersion)
        {
            // Arrange
            string compressFormat = "tar-gz";
            var appOutputDirPath = Directory.CreateDirectory(Path.Combine(_tempRootDir, Guid.NewGuid().ToString("N")))
                .FullName;
            var appOutputDirVolume = DockerVolume.CreateMirror(appOutputDirPath);
            var appOutputDir = appOutputDirVolume.ContainerDir;
            var appName = "angular8app";
            var volume = CreateAppVolume(appName);
            var appDir = volume.ContainerDir;
            var runAppScript = new ShellScriptBuilder()
                .AddCommand($"export PORT=4200")
                .AddCommand($"oryx -appPath {appOutputDir}")
                .AddCommand(DefaultStartupFilePath)
                .ToString();
            var buildScript = new ShellScriptBuilder()
                .AddCommand(
                $"oryx build {appDir} -i /tmp/int -o {appOutputDir} --platform nodejs " +
                $"--platform-version {nodeVersion} -p compress_node_modules={compressFormat}")
                .ToString();

            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                appName,
                _output,
                new List<DockerVolume> { appOutputDirVolume, volume },
                "/bin/sh",
                new[]
                {
                    "-c",
                    buildScript
                },
                _imageHelper.GetTestRuntimeImage("node", nodeVersion),
                4200,
                "/bin/sh",
                new[]
                {
                    "-c",
                    runAppScript
                },
                async (hostPort) =>
                {
                    var data = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                    Assert.Contains("Angular8app", data);
                });
        }
    }
}