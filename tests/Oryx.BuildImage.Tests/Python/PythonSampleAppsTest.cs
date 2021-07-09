// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Oryx.BuildScriptGenerator;
using Microsoft.Oryx.BuildScriptGenerator.Common;
using Microsoft.Oryx.BuildScriptGenerator.Python;
using Microsoft.Oryx.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Oryx.BuildImage.Tests
{
    public class PythonSampleAppsTest : PythonSampleAppsTestBase, IClassFixture<TestTempDirTestFixture>
    {
        protected readonly string _tempDirRootPath;

        public PythonSampleAppsTest(ITestOutputHelper output, TestTempDirTestFixture testFixture)
            : base(output)
        {
            _tempDirRootPath = testFixture.RootDirPath;
        }

        [Theory]
        [InlineData(Settings.BuildImageName)]
        [InlineData(Settings.LtsVersionsBuildImageName)]
        public void GeneratesScript_AndBuilds(string buildImageName)
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddBuildCommand($"{appDir} -o {appOutputDir}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = buildImageName,
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
                    Assert.Contains(
                        $"Python Version: /opt/python/{PythonConstants.PythonLtsVersion}/bin/python3",
                        result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void GeneratesScript_AndBuilds_WithPackageDir()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddBuildCommand($"{appDir} -o {appOutputDir} -p packagedir={PackagesDirectory}")
                .AddDirectoryExistsCheck($"{appOutputDir}/{PackagesDirectory}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                    Assert.Contains(
                        $"Python Version: /opt/python/{PythonConstants.PythonLtsVersion}/bin/python3",
                        result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData("github-actions")]
        [InlineData("github-actions-buster")]
        [InlineData("lts-versions")]
        [InlineData("latest")]
        public void DoesNotGenerateCondaBuildScript_IfImageDoesNotHaveCondaInstalledInIt(string imageTag)
        {
            // Arrange
            var appName = "requirements";
            var volume = DockerVolume.CreateMirror(Path.Combine(_hostSamplesDir, "python", "conda-samples", appName));
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddBuildCommand($"{appDir} -o {appOutputDir}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = _imageHelper.GetBuildImage(imageTag),
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
                    Assert.DoesNotContain("Setting up Conda virtual environment", result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void Builds_AndCopiesContentToOutputDirectory_Recursively()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var subDir = Guid.NewGuid();
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                // Add a test sub-directory with a file
                .CreateDirectory($"{appDir}/{subDir}")
                .CreateFile($"{appDir}/{subDir}/file1.txt", "file1.txt")
                // Execute command
                .AddBuildCommand($"{appDir} -o {appOutputDir}")
                // Check the output directory for the sub directory
                .AddFileExistsCheck($"{appOutputDir}/{subDir}/file1.txt")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void Build_CopiesOutput_ToOutputDirectory_NestedUnderSourceDirectory()
        {
            // Arrange
            var virtualEnvName = GetDefaultVirtualEnvName(PythonConstants.PythonLtsVersion);
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddBuildCommand(
                $"{appDir} -o {appDir}/output --platform {PythonConstants.PlatformName} " +
                $"--platform-version 3.7")
                .AddDirectoryExistsCheck($"{appDir}/output/pythonenv3.7")
                .AddDirectoryDoesNotExistCheck($"{appDir}/output/output")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.LtsVersionsBuildImageName,
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
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void SubsequentBuilds_CopyOutput_ToOutputDirectory_NestedUnderSourceDirectory()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            // NOTE: we want to make sure that even after subsequent builds(like in case of AppService),
            // the output structure is like what we expect.
            var buildCmd = $"oryx build {appDir} -o {appDir}/output --platform {PythonConstants.PlatformName} " +
                $"--platform-version 3.7";
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddCommand(buildCmd)
                .AddCommand(buildCmd)
                .AddCommand(buildCmd)
                .AddDirectoryExistsCheck($"{appDir}/output/pythonenv3.7")
                .AddDirectoryDoesNotExistCheck($"{appDir}/output/output")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.LtsVersionsBuildImageName,
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
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void GeneratesScriptAndBuilds_WhenSourceAndDestinationFolders_AreSame()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var virtualEnvName = GetDefaultVirtualEnvName(PythonConstants.PythonLtsVersion);
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddBuildCommand($"{appDir}")
                .AddDirectoryExistsCheck($"{appDir}/{virtualEnvName}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void GeneratesScriptAndBuilds_WhenDestination_IsSubDirectoryOfSource()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = $"{appDir}/output";
            var virtualEnvName = GetDefaultVirtualEnvName(PythonConstants.PythonLtsVersion);
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddBuildCommand($"{appDir} -o {appOutputDir}")
                .AddDirectoryExistsCheck($"{appOutputDir}/{virtualEnvName}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void Build_DoestNotCleanDestinationDir()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var virtualEnvName = GetDefaultVirtualEnvName(PythonConstants.PythonLtsVersion);
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                // Pre-populate the output directory with content
                .CreateDirectory(appOutputDir)
                .CreateFile($"{appOutputDir}/hi.txt", "hi")
                .CreateDirectory($"{appOutputDir}/blah")
                .CreateFile($"{appOutputDir}/blah/hi.txt", "hi")
                .AddBuildCommand($"{appDir} -o {appOutputDir}")
                .AddDirectoryExistsCheck($"{appOutputDir}/{virtualEnvName}")
                .AddFileExistsCheck($"{appOutputDir}/hi.txt")
                .AddFileExistsCheck($"{appOutputDir}/blah/hi.txt")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void ErrorDuringBuild_ResultsIn_NonSuccessfulExitCode()
        {
            // Try building a Python 2.7 app with 3.7 version. This should fail as there are major
            // API changes between these versions

            // Arrange
            var langVersion = PythonVersions.Python37Version;
            var appName = "python2-flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var generatedScript = "/tmp/build.sh";
            var appOutputDir = "/tmp/app-output";
            var tempDir = "/tmp/" + Guid.NewGuid();
            var script = new ShellScriptBuilder()
                .AddScriptCommand(
                $"{appDir} --platform {PythonConstants.PlatformName} " +
                $"--platform-version {langVersion} > {generatedScript}")
                .SetExecutePermissionOnFile(generatedScript)
                .CreateDirectory(tempDir)
                .AddCommand($"{generatedScript} {appDir} {appOutputDir} {tempDir}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                EnvironmentVariables = new List<EnvironmentVariable> { CreateAppNameEnvVar(appName) },
                Volumes = new List<DockerVolume> { volume },
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.False(result.IsSuccess);
                    Assert.Contains("Missing parentheses in call to 'print'", result.StdErr);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void GeneratesScript_AndBuilds_WhenExplicitPlatformAndVersion_AreProvided()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = $"{appDir}/output";
            var manifestFile = $"{appOutputDir}/{FilePaths.BuildManifestFileName}";
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddBuildCommand(
                $"{appDir} -o {appOutputDir} --platform {PythonConstants.PlatformName} " +
                $"--platform-version {PythonVersions.Python36Version}")
                .AddCommand($"cat {manifestFile}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                    Assert.Contains(
                        $"Python Version: /opt/python/{PythonVersions.Python36Version}/bin/python3",
                        result.StdOut);
                    Assert.Contains(
                       $"{ManifestFilePropertyKeys.PythonVersion}=\"{PythonVersions.Python36Version}\"",
                       result.StdOut);
                    Assert.Contains(
                       $"{ManifestFilePropertyKeys.SourceDirectoryInBuildContainer}=\"{appDir}\"",
                       result.StdOut);
                },
                result.GetDebugInfo());
        }

        // This is to test if we can build an app when there is no requirement.txt
        // but setup.py is provided at root level
        [Fact]
        public void GeneratesScript_AndBuilds_WhenSetupDotPy_File_isProvided()
        {
            // Arrange
            var appName = "flask-setup-py-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = $"{appDir}/output";
            var manifestFile = $"{appOutputDir}/{FilePaths.BuildManifestFileName}";
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddBuildCommand(
                $"{appDir} -o {appOutputDir} --platform {PythonConstants.PlatformName} " +
                $"--platform-version {PythonVersions.Python36Version}")
                .AddCommand($"cat {manifestFile}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                    Assert.Contains(
                        $"Python Version: /opt/python/{PythonVersions.Python36Version}/bin/python3",
                        result.StdOut);
                    Assert.Contains(
                       $"{ManifestFilePropertyKeys.PythonVersion}=\"{PythonVersions.Python36Version}\"",
                       result.StdOut);
                },
                result.GetDebugInfo());
        }

        // This is to test if we can build an app when both the files requirement.txt
        // and setup.py are provided, we tend to prioritize the root level requirement.txt
        [Fact]
        public void GeneratesScript_AndBuilds_With_Both_Files_areProvided()
        {
            // Arrange
            var appName = "flask-setup-py-requirement-txt";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = $"{appDir}/output";
            var manifestFile = $"{appOutputDir}/{FilePaths.BuildManifestFileName}";
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddBuildCommand(
                $"{appDir} -o {appOutputDir} --platform {PythonConstants.PlatformName} " +
                $"--platform-version {PythonVersions.Python36Version}")
                .AddCommand($"cat {manifestFile}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                    Assert.Contains(
                        $"Python Version: /opt/python/{PythonVersions.Python36Version}/bin/python3",
                        result.StdOut);
                    Assert.Contains(
                       $"{ManifestFilePropertyKeys.PythonVersion}=\"{PythonVersions.Python36Version}\"",
                       result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact(Skip = "Issue# 1094264")]
        public void CanBuild_UsingScriptGeneratedBy_ScriptOnlyOption()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var generatedScript = "/tmp/build.sh";
            var appOutputDir = "/tmp/app-output";
            var tempDir = "/tmp/" + Guid.NewGuid();
            var script = new ShellScriptBuilder()
                .AddScriptCommand(
                $"{appDir} --platform {PythonConstants.PlatformName} " +
                $"--platform-version {PythonVersions.Python36Version} > {generatedScript}")
                .SetExecutePermissionOnFile(generatedScript)
                .CreateDirectory(tempDir)
                .AddCommand($"{generatedScript} {appDir} {appOutputDir} {tempDir}")
                .AddDirectoryExistsCheck($"{appOutputDir}/pythonenv3.6/lib/python3.6/site-packages/flask/")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                },
                result.GetDebugInfo());
        }

        [Fact(Skip = "Issue# 1094264")]
        public void ThrowsException_ForInvalidPythonVersion()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var generatedScript = "/tmp/build.sh";
            var appOutputDir = "/tmp/app-output";
            var tempDir = "/tmp/" + Guid.NewGuid();
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddScriptCommand(
                $"{appDir} --platform {PythonConstants.PlatformName} --platform-version 4.0.1 > {generatedScript}")
                .SetExecutePermissionOnFile(generatedScript)
                .CreateDirectory(tempDir)
                .AddCommand($"{generatedScript} {appDir} {appOutputDir} {tempDir}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                EnvironmentVariables = new List<EnvironmentVariable> { CreateAppNameEnvVar(appName) },
                Volumes = new List<DockerVolume> { volume },
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            RunAsserts(
                () =>
                {
                    string errorMessage = "Platform 'python' version '4.0.1' is unsupported. Supported versions: " +
                        $"{PythonVersions.Python27Version}, {PythonVersions.Python36Version}, {PythonVersions.Python37Version}";
                    Assert.False(result.IsSuccess);
                    Assert.Contains(errorMessage, result.StdErr);
                },
                result.GetDebugInfo());
        }

        [Fact(Skip = "Issue# 1094264")]
        public void CanBuild_Python2_WithScriptOnlyOption()
        {
            // Arrange
            var langVersion = PythonVersions.Python27Version;
            var appName = "python2-flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var generatedScript = "/tmp/build.sh";
            var appOutputDir = "/tmp/app-output";
            var tempDir = "/tmp/" + Guid.NewGuid();
            var script = new ShellScriptBuilder()
                .AddScriptCommand(
                $"{appDir} --platform {PythonConstants.PlatformName} " +
                $"--platform-version {langVersion} > {generatedScript}")
                .SetExecutePermissionOnFile(generatedScript)
                .CreateDirectory(tempDir)
                .AddCommand($"{generatedScript} {appDir} {appOutputDir} {tempDir}")
                .AddDirectoryExistsCheck($"{appOutputDir}/pythonenv2.7/lib/python2.7/site-packages/flask/")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void GeneratesScript_AndBuilds_UsingSuppliedIntermediateDir()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appIntermediateDir = "/tmp/app-intermediate";
            var appOutputDir = "/tmp/app-output";
            var virtualEnvName = GetDefaultVirtualEnvName(PythonConstants.PythonLtsVersion);
            var manifestFile = $"{appOutputDir}/{FilePaths.BuildManifestFileName}";
            var script = new ShellScriptBuilder()
                .AddBuildCommand($"{appDir} -o {appOutputDir} -i {appIntermediateDir}")
                .AddDirectoryExistsCheck($"{appOutputDir}/{virtualEnvName}")
                .AddCommand($"cat {manifestFile}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                    Assert.Contains(
                       $"{ManifestFilePropertyKeys.SourceDirectoryInBuildContainer}=\"{appIntermediateDir}\"",
                       result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData("flask-app")]
        [InlineData("django-realworld-example-app")]
        public void BuildPythonApps_Prints_BuildCommands_In_File(string appName)
        {
            // Arrange
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app1-output";
            var commandListFile = $"{appOutputDir}/FilePaths.BuildCommandsFileName";
            var script = new ShellScriptBuilder()
                .SetEnvironmentVariable(
                    SdkStorageConstants.SdkStorageBaseUrlKeyName,
                    SdkStorageConstants.DevSdkStorageBaseUrl)
                .AddBuildCommand($"{appDir} -o {appOutputDir} --manifest-dir {appOutputDir}")
                .AddFileExistsCheck($"{commandListFile}")
                .AddStringExistsInFileCheck("PlatformWithVersion=", $"{commandListFile}")
                .AddStringExistsInFileCheck("BuildCommands=", $"{commandListFile}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = _imageHelper.GetVsoBuildImage("vso-focal"),
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
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void Build_VirtualEnv_Unzipped_ByDefault()
        {
            // Arrange
            var virtualEnvironmentName = GetDefaultVirtualEnvName(PythonConstants.PythonLtsVersion);
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddBuildCommand($"{appDir} -i /tmp/int -o {appOutputDir}")
                .AddDirectoryExistsCheck($"{appOutputDir}/{virtualEnvironmentName}")
                .AddFileDoesNotExistCheck($"{appOutputDir}/{virtualEnvironmentName}.tar.gz")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                    Assert.Contains(
                        $"Python Version: /opt/python/{PythonConstants.PythonLtsVersion}/bin/python3",
                        result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("tar-gz")]
        public void Build_CompressesVirtualEnv_InTargGzFormat(string compressionFormat)
        {
            // Arrange
            var virtualEnvironmentName = "myenv";
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddBuildCommand(
                $"{appDir} -i /tmp/int -o {appOutputDir} " +
                $"-p virtualenv_name={virtualEnvironmentName} -p compress_virtualenv={compressionFormat}")
                .AddDirectoryDoesNotExistCheck($"{appOutputDir}/{virtualEnvironmentName}")
                .AddFileExistsCheck($"{appOutputDir}/{virtualEnvironmentName}.tar.gz")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                    Assert.Contains(
                        $"Python Version: /opt/python/{PythonConstants.PythonLtsVersion}/bin/python3",
                        result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void Build_CompressesVirtualEnv_InZipFormat()
        {
            // Arrange
            var virtualEnvironmentName = "myenv";
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddBuildCommand(
                $"{appDir} -i /tmp/int -o {appOutputDir} " +
                $"-p virtualenv_name={virtualEnvironmentName} -p compress_virtualenv=zip")
                .AddDirectoryDoesNotExistCheck($"{appOutputDir}/{virtualEnvironmentName}")
                .AddFileExistsCheck($"{appOutputDir}/{virtualEnvironmentName}.zip")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                    Assert.Contains(
                        $"Python Version: /opt/python/{PythonConstants.PythonLtsVersion}/bin/python3",
                        result.StdOut);
                },
                result.GetDebugInfo());
        }


        [Fact]
        public void Build_InstallsVirtualEnvironment_AndPackagesInIt()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddBuildCommand(
                $"{appDir} -o {appOutputDir} --platform {PythonConstants.PlatformName} " +
                $"--platform-version {PythonVersions.Python37Version}")
                .AddDirectoryExistsCheck($"{appOutputDir}/pythonenv3.7/lib/python3.7/site-packages/flask")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                    Assert.Contains(
                        $"Python Version: /opt/python/{PythonVersions.Python37Version}/bin/python3",
                        result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void Build_InstallsVirtualEnvironment_AndPackagesInIt_From_File_Setup_Py()
        {
            // Arrange
            var appName = "flask-setup-py-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddBuildCommand(
                $"{appDir} -o {appOutputDir} --platform {PythonConstants.PlatformName} " +
                $"--platform-version {PythonVersions.Python37Version}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                    Assert.Contains(
                        $"Python Version: /opt/python/{PythonVersions.Python37Version}/bin/python3",
                        result.StdOut);
                },
                result.GetDebugInfo());
        }


        [Fact]
        public void Build_ExecutesPreAndPostBuildScripts_UsingBuildEnvironmentFile()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            using (var sw = File.AppendText(
                Path.Combine(volume.MountedHostDir, BuildScriptGeneratorCli.Constants.BuildEnvironmentFileName)))
            {
                sw.NewLine = "\n";
                sw.WriteLine("PRE_BUILD_SCRIPT_PATH=scripts/prebuild.sh");
                sw.WriteLine("POST_BUILD_SCRIPT_PATH=scripts/postbuild.sh");
            }
            var scriptsDir = Directory.CreateDirectory(Path.Combine(volume.MountedHostDir, "scripts"));
            using (var sw = File.AppendText(Path.Combine(scriptsDir.FullName, "prebuild.sh")))
            {
                sw.NewLine = "\n";
                sw.WriteLine("#!/bin/bash");
                sw.WriteLine("echo Executing the pre-build script from a standalone script!");
            }
            using (var sw = File.AppendText(Path.Combine(scriptsDir.FullName, "postbuild.sh")))
            {
                sw.NewLine = "\n";
                sw.WriteLine("#!/bin/bash");
                sw.WriteLine("echo Executing the post-build script from a standalone script!");
            }
            if (RuntimeInformation.IsOSPlatform(Settings.LinuxOS))
            {
                ProcessHelper.RunProcess(
                    "chmod",
                    new[] { "-R", "777", scriptsDir.FullName },
                    workingDirectory: null,
                    waitTimeForExit: null);
            }
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var script = new ShellScriptBuilder()
                .AddBuildCommand($"{appDir} -o {appOutputDir}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
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
                    Assert.Contains(
                        "Executing the pre-build script from a standalone script!",
                        result.StdOut);
                    Assert.Contains(
                        "Executing the post-build script from a standalone script!",
                        result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void Build_ExecutesPreAndPostBuildScripts_UsingEnvironmentVariables()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var scriptsDir = Directory.CreateDirectory(Path.Combine(volume.MountedHostDir, "scripts"));
            using (var sw = File.AppendText(Path.Combine(scriptsDir.FullName, "prebuild.sh")))
            {
                sw.NewLine = "\n";
                sw.WriteLine("#!/bin/bash");
                sw.WriteLine("echo Executing the pre-build script from a standalone script!");
            }
            using (var sw = File.AppendText(Path.Combine(scriptsDir.FullName, "postbuild.sh")))
            {
                sw.NewLine = "\n";
                sw.WriteLine("#!/bin/bash");
                sw.WriteLine("echo Executing the post-build script from a standalone script!");
            }
            if (RuntimeInformation.IsOSPlatform(Settings.LinuxOS))
            {
                ProcessHelper.RunProcess(
                    "chmod",
                    new[] { "-R", "777", scriptsDir.FullName },
                    workingDirectory: null,
                    waitTimeForExit: null);
            }
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var script = new ShellScriptBuilder()
                .AddBuildCommand($"{appDir} -o {appOutputDir}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                EnvironmentVariables = new List<EnvironmentVariable>
                {
                    CreateAppNameEnvVar(appName),
                    new EnvironmentVariable("PRE_BUILD_SCRIPT_PATH", "scripts/prebuild.sh"),
                    new EnvironmentVariable("POST_BUILD_SCRIPT_PATH", "scripts/postbuild.sh")
                },
                Volumes = new List<DockerVolume> { volume },
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains(
                        "Executing the pre-build script from a standalone script!",
                        result.StdOut);
                    Assert.Contains(
                        "Executing the post-build script from a standalone script!",
                        result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void PreAndPostBuildScripts_HaveAccessToSourceAndDestinationDirectoryVariables()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var scriptsDir = Directory.CreateDirectory(Path.Combine(volume.MountedHostDir, "scripts"));
            using (var sw = File.AppendText(Path.Combine(scriptsDir.FullName, "prebuild.sh")))
            {
                sw.NewLine = "\n";
                sw.WriteLine("#!/bin/bash");
                sw.WriteLine("echo \"pre-build: $SOURCE_DIR, $DESTINATION_DIR\"");
            }
            using (var sw = File.AppendText(Path.Combine(scriptsDir.FullName, "postbuild.sh")))
            {
                sw.NewLine = "\n";
                sw.WriteLine("#!/bin/bash");
                sw.WriteLine("echo \"post-build: $SOURCE_DIR, $DESTINATION_DIR\"");
            }
            if (RuntimeInformation.IsOSPlatform(Settings.LinuxOS))
            {
                ProcessHelper.RunProcess(
                    "chmod",
                    new[] { "-R", "777", scriptsDir.FullName },
                    workingDirectory: null,
                    waitTimeForExit: null);
            }
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var script = new ShellScriptBuilder()
                .AddBuildCommand($"{appDir} -o {appOutputDir}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                EnvironmentVariables = new List<EnvironmentVariable>
                {
                    CreateAppNameEnvVar(appName),
                    new EnvironmentVariable("PRE_BUILD_SCRIPT_PATH", "scripts/prebuild.sh"),
                    new EnvironmentVariable("POST_BUILD_SCRIPT_PATH", "scripts/postbuild.sh")
                },
                Volumes = new List<DockerVolume> { volume },
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains($"pre-build: {appDir}, {appOutputDir}", result.StdOut);
                    Assert.Contains($"post-build: {appDir}, {appOutputDir}", result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void Build_Executes_InlinePreAndPostBuildCommands()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            using (var sw = File.AppendText(
                Path.Combine(volume.MountedHostDir, BuildScriptGeneratorCli.Constants.BuildEnvironmentFileName)))
            {
                sw.NewLine = "\n";
                sw.WriteLine("PRE_BUILD_COMMAND=\"echo from pre-build command\"");
                sw.WriteLine("POST_BUILD_COMMAND=\"echo from post-build command\"");
            }

            var appDir = volume.ContainerDir;
            var script = new ShellScriptBuilder()
                .AddBuildCommand($"{appDir} -o /tmp/output")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                EnvironmentVariables = new List<EnvironmentVariable>
                {
                    CreateAppNameEnvVar(appName),
                },
                Volumes = new List<DockerVolume> { volume },
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains("from pre-build command", result.StdOut);
                    Assert.Contains("from post-build command", result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void Django_CollectStaticFailure_DoesNotFailBuild()
        {
            // Arrange
            var appName = "django-realworld-example-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var script = new ShellScriptBuilder()
                .AddBuildCommand(
                $"{appDir} -o {appOutputDir} --platform {PythonConstants.PlatformName} " +
                $"--platform-version {PythonVersions.Python37Version}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                EnvironmentVariables = new List<EnvironmentVariable>
                {
                    CreateAppNameEnvVar(appName),
                },
                Volumes = new List<DockerVolume> { volume },
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains("'collectstatic' exited with exit code 1.", result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData(PythonVersions.Python38Version)]
        [InlineData(PythonVersions.Python27Version)]
        public void Build_ExecutesPreAndPostBuildScripts_WithinBenvContext(string version)
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            using (var sw = File.AppendText(
                Path.Combine(volume.MountedHostDir, BuildScriptGeneratorCli.Constants.BuildEnvironmentFileName)))
            {
                sw.NewLine = "\n";
                sw.WriteLine("PRE_BUILD_SCRIPT_PATH=scripts/prebuild.sh");
                sw.WriteLine("POST_BUILD_SCRIPT_PATH=scripts/postbuild.sh");
            }
            var scriptsDir = Directory.CreateDirectory(Path.Combine(volume.MountedHostDir, "scripts"));
            using (var sw = File.AppendText(Path.Combine(scriptsDir.FullName, "prebuild.sh")))
            {
                sw.NewLine = "\n";
                sw.WriteLine("#!/bin/bash");
                sw.WriteLine("echo \"Pre-build script: $python\"");
                sw.WriteLine("echo \"Pre-build script: $pip\"");
            }
            using (var sw = File.AppendText(Path.Combine(scriptsDir.FullName, "postbuild.sh")))
            {
                sw.NewLine = "\n";
                sw.WriteLine("#!/bin/bash");
                sw.WriteLine("echo \"Post-build script: $python\"");
                sw.WriteLine("echo \"Post-build script: $pip\"");
            }
            if (RuntimeInformation.IsOSPlatform(Settings.LinuxOS))
            {
                ProcessHelper.RunProcess(
                    "chmod",
                    new[] { "-R", "777", scriptsDir.FullName },
                    workingDirectory: null,
                    waitTimeForExit: null);
            }
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddBuildCommand($"{appDir} -o {appOutputDir} --platform {PythonConstants.PlatformName} " +
                $"--platform-version {version}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                EnvironmentVariables = new List<EnvironmentVariable>
                {
                    CreateAppNameEnvVar(appName),
                },
                Volumes = new List<DockerVolume> { volume },
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    var semVer = new SemVer.Version(version);
                    var virtualEnvSuffix = $"{semVer.Major}.{semVer.Minor}";
                    Assert.Matches($"Pre-build script: /opt/python/{version}/bin/python{virtualEnvSuffix}", result.StdOut);
                    Assert.Matches($"Pre-build script: /opt/python/{version}/bin/pip", result.StdOut);
                    Assert.Matches($"Post-build script: /opt/python/{version}/bin/python{virtualEnvSuffix}", result.StdOut);
                    Assert.Matches($"Post-build script: /opt/python/{version}/bin/pip", result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void BuildsAppSuccessfully_EvenIfRequirementsTxtOrSetupPyFileDoNotExist()
        {
            // Arrange
            var appName = "flask-app";
            var hostDir = Directory.CreateDirectory(
                Path.Combine(_tempDirRootPath, Guid.NewGuid().ToString("N"))).FullName;
            var volume = DockerVolume.CreateMirror(hostDir);
            var appDir = volume.ContainerDir;
            var appOutputDir = "/tmp/app-output";
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddCommand($"mkdir -p {appDir}/foo")
                .AddCommand($"echo > {appDir}/foo/test.py")
                .AddBuildCommand($"{appDir} -o {appOutputDir}")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = _imageHelper.GetLtsVersionsBuildImage(),
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
                    Assert.Contains(
                        $"Python Version: /opt/python/{PythonConstants.PythonLtsVersion}/bin/python3",
                        result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void BuildsAppAndCompressesOutputDirectory()
        {
            // Arrange
            var appName = "flask-app";
            var volume = CreateSampleAppVolume(appName);
            var appDir = volume.ContainerDir;
            var buildDir = "/tmp/int";
            var outputDir = "/tmp/output";
            var virtualEnvName = "antenv";
            var manifestFile = $"{outputDir}/{FilePaths.BuildManifestFileName}";
            var script = new ShellScriptBuilder()
                .AddDefaultTestEnvironmentVariables()
                .AddCommand(
                $"oryx build {appDir} -i {buildDir} -o {outputDir} " +
                $"-p virtualenv_name={virtualEnvName} --compress-destination-dir")
                .AddFileExistsCheck($"{outputDir}/output.tar.gz")
                .AddFileExistsCheck($"{outputDir}/oryx-manifest.toml")
                .AddFileDoesNotExistCheck($"{outputDir}/requirements.txt")
                .AddDirectoryDoesNotExistCheck($"{outputDir}/{virtualEnvName}")
                .AddCommand($"cat {manifestFile}")
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
                    Assert.Contains(
                       $"{ManifestFilePropertyKeys.CompressDestinationDir}=\"true\"",
                       result.StdOut);
                    Assert.Contains(
                       $"{ManifestFilePropertyKeys.SourceDirectoryInBuildContainer}=\"{buildDir}\"",
                       result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData("lts-versions", "3")]
        [InlineData("vso-focal", "3")]
        [InlineData("latest", "2")]
        [InlineData("latest", "3")]
        public void JamSpell_CanBe_Installed_In_The_BuildImage(string tagName, string pythonVersion)
        {
            // Arrange
            var expectedPackage = "jamspell";

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = _imageHelper.GetBuildImage(tagName),
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", $"wget -O - https://pypi.org/simple/ | grep -i {expectedPackage}" },
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains(expectedPackage, actualOutput);
                },
                result.GetDebugInfo());
        }

        private string GetDefaultVirtualEnvName(string version)
        {
            var ver = new SemVer.Version(version);
            return $"pythonenv{ver.Major}.{ver.Minor}";
        }
    }
}