﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using Microsoft.Oryx.Common;
using Microsoft.Oryx.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Oryx.Integration.Tests
{
    [Trait("category", "python")]
    public class PythonEndToEndTests : PythonEndToEndTestsBase
    {
        public PythonEndToEndTests(ITestOutputHelper output, TestTempDirTestFixture testTempDirTestFixture)
            : base(output, testTempDirTestFixture)
        {
        }

        [Fact]
        public async Task CanBuildAndRunPythonApp_UsingPython36()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateAppVolume(appName);
            var appDir = volume.ContainerDir;
            var buildScript = new ShellScriptBuilder()
               .AddCommand($"oryx build {appDir} --platform python --language-version 3.6")
               .ToString();
            var runScript = new ShellScriptBuilder()
                .AddCommand($"oryx create-script -appPath {appDir} -bindPort {ContainerPort}")
                .AddCommand(DefaultStartupFilePath)
                .ToString();

            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                appName,
                _output,
                volume,
                "/bin/bash",
                new[]
                {
                    "-c",
                    buildScript
                },
                _imageHelper.GetTestRuntimeImage("python", "3.6"),
                ContainerPort,
                "/bin/bash",
                new[]
                {
                    "-c",
                    runScript
                },
                async (hostPort) =>
                {
                    var data = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                    Assert.Contains("Hello World!", data);
                });
        }

        [Fact]
        public async Task CanBuildAndRun_Tweeter3App()
        {
            // Arrange
            var appName = "tweeter3";
            var volume = CreateAppVolume(appName);
            var appDir = volume.ContainerDir;
            var buildScript = new ShellScriptBuilder()
               .AddCommand($"oryx build {appDir} --platform python --platform-version 3.7")
               .ToString();
            var runScript = new ShellScriptBuilder()
                .AddCommand($"oryx create-script -appPath {appDir} -bindPort {ContainerPort}")
                .AddCommand(DefaultStartupFilePath)
                .ToString();

            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                appName,
                _output,
                volume,
                "/bin/bash",
                new[]
                {
                    "-c",
                    buildScript
                },
                _imageHelper.GetTestRuntimeImage("python", "3.7"),
                ContainerPort,
                "/bin/bash",
                new[]
                {
                    "-c",
                    runScript
                },
                async (hostPort) =>
                {
                    var data = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                    Assert.Contains("logged in as: bob", data);
                });
        }

        [Theory]
        [InlineData("3.6")]
        [InlineData("3.7")]
        public async Task BuildWithVirtualEnv_RemovesOryxPackagesDir_FromOlderBuild(string pythonVersion)
        {
            // Arrange
            var appName = "django-app";
            var volume = CreateAppVolume(appName);
            var appDir = volume.ContainerDir;
            const string virtualEnvName = "antenv";

            // Simulate apps that were built using package directory, and then virtual env
            var buildScript = new ShellScriptBuilder()
                .AddBuildCommand($"{appDir} --platform python --language-version {pythonVersion}")
                .AddBuildCommand(
                $"{appDir} -p virtualenv_name={virtualEnvName} " +
                $"--platform python --language-version {pythonVersion}")
                .ToString();

            var runScript = new ShellScriptBuilder()
                .AddDirectoryDoesNotExistCheck("__oryx_packages__")
                .AddCommand(
                $"oryx create-script -appPath {appDir} -bindPort {ContainerPort} -virtualEnvName={virtualEnvName}")
                .AddCommand(DefaultStartupFilePath)
                .ToString();

            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                appName,
                _output,
                volume,
                "/bin/bash",
                new[] { "-c", buildScript },
                _imageHelper.GetTestRuntimeImage("python", pythonVersion),
                ContainerPort,
                "/bin/bash",
                new[] { "-c", runScript },
                async (hostPort) =>
                {
                    var data = await GetResponseDataAsync($"http://localhost:{hostPort}/staticfiles/css/boards.css");
                    Assert.Contains("CSS file from Boards app module", data);

                    data = await GetResponseDataAsync($"http://localhost:{hostPort}/staticfiles/css/uservoice.css");
                    Assert.Contains("CSS file from UserVoice app module", data);

                    data = await GetResponseDataAsync($"http://localhost:{hostPort}/boards/");
                    Assert.Contains("Hello, World! from Boards app", data);

                    data = await GetResponseDataAsync($"http://localhost:{hostPort}/uservoice/");
                    Assert.Contains("Hello, World! from Uservoice app", data);
                });
        }

        [Theory]
        [InlineData("2.7")]
        [InlineData("3.6")]
        [InlineData("3.7")]
        [InlineData("3.8")]
        public async Task BuildWithVirtualEnv_From_File_Setup_Py(string pythonVersion)
        {
            // Arrange
            var appName = "flask-setup-py-app";
            var volume = CreateAppVolume(appName);
            var appDir = volume.ContainerDir;
            
            var buildScript = new ShellScriptBuilder()
                .AddBuildCommand($"{appDir} --platform python --platform-version {pythonVersion}")
                .ToString();

            var runScript = new ShellScriptBuilder()
                .AddCommand(
                $"oryx create-script -appPath {appDir} -bindPort {ContainerPort}")
                .AddCommand(DefaultStartupFilePath)
                .ToString();

            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                appName,
                _output,
                volume,
                "/bin/bash",
                new[] { "-c", buildScript },
                _imageHelper.GetTestRuntimeImage("python", pythonVersion),
                ContainerPort,
                "/bin/bash",
                new[] { "-c", runScript },
                async (hostPort) =>
                {
                    var data = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                    Assert.Contains("Hello World!", data);
                });
        }

        
        [Theory]
        [InlineData("2.7")]
        [InlineData("3.6")]
        [InlineData("3.7")]
        [InlineData("3.8")]
        public async Task BuildWithVirtualEnv_From_File_Requirement_Txt(string pythonVersion)
        {
             // This is to test if we can build and run an app when both the files requirement.txt 
             // and setup.py are provided, we tend to prioritize the root level requirement.txt
            
            // Arrange
            var appName = "flask-setup-py-requirement-txt";
            var volume = CreateAppVolume(appName);
            var appDir = volume.ContainerDir;
            
            var buildScript = new ShellScriptBuilder()
                .AddBuildCommand($"{appDir} --platform python --platform-version {pythonVersion}")
                .ToString();

            var runScript = new ShellScriptBuilder()
                .AddCommand(
                $"oryx create-script -appPath {appDir} -bindPort {ContainerPort}")
                .AddCommand(DefaultStartupFilePath)
                .ToString();

            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                appName,
                _output,
                volume,
                "/bin/bash",
                new[] { "-c", buildScript },
                _imageHelper.GetTestRuntimeImage("python", pythonVersion),
                ContainerPort,
                "/bin/bash",
                new[] { "-c", runScript },
                async (hostPort) =>
                {
                    var data = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                    Assert.Contains("Hello World!", data);
                });
        }

        [Fact]
        public async Task CanBuildAndRunPythonApp_UsingOutputDirectory_NestedUnderSourceDirectory()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateAppVolume(appName);
            var appDir = volume.ContainerDir;
            var buildScript = new ShellScriptBuilder()
               .AddCommand($"oryx build {appDir} -o {appDir}/output --platform python --platform-version 3.8")
               .ToString();
            var runScript = new ShellScriptBuilder()
                .AddCommand($"oryx create-script -appPath {appDir}/output -bindPort {ContainerPort}")
                .AddCommand(DefaultStartupFilePath)
                .ToString();

            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                appName,
                _output,
                volume,
                "/bin/bash",
                new[]
                {
                    "-c",
                    buildScript
                },
                _imageHelper.GetTestRuntimeImage("python", "3.8"),
                ContainerPort,
                "/bin/bash",
                new[]
                {
                    "-c",
                    runScript
                },
                async (hostPort) =>
                {
                    var data = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                    Assert.Contains("Hello World!", data);
                });
        }

        [Fact]
        public async Task CanBuildAndRunPythonApp_UsingIntermediateDir_AndNestedOutputDirectory()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateAppVolume(appName);
            var appDir = volume.ContainerDir;
            var buildScript = new ShellScriptBuilder()
               .AddCommand(
                $"oryx build {appDir} -i /tmp/int -o {appDir}/output --platform python --platform-version 3.8")
               .ToString();
            var runScript = new ShellScriptBuilder()
                .AddCommand($"oryx create-script -appPath {appDir}/output -bindPort {ContainerPort}")
                .AddCommand(DefaultStartupFilePath)
                .ToString();

            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                appName,
                _output,
                volume,
                "/bin/bash",
                new[]
                {
                    "-c",
                    buildScript
                },
                _imageHelper.GetTestRuntimeImage("python", "3.8"),
                ContainerPort,
                "/bin/bash",
                new[]
                {
                    "-c",
                    runScript
                },
                async (hostPort) =>
                {
                    var data = await _httpClient.GetStringAsync($"http://localhost:{hostPort}/");
                    Assert.Contains("Hello World!", data);
                });
        }
    }
}