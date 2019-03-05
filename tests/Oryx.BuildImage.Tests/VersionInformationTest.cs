﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.Oryx.BuildScriptGenerator.DotnetCore;
using Microsoft.Oryx.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Oryx.BuildImage.Tests
{
    public class VersionInformationTest
    {
        private const string Python27VersionInfo = "Python " + Settings.Python27Version;
        private const string Python35VersionInfo = "Python " + Settings.Python35Version;
        private const string Python36VersionInfo = "Python " + Settings.Python36Version;
        private const string Python37VersionInfo = "Python " + Common.PythonVersions.Python37Version;

        private readonly ITestOutputHelper _output;
        private readonly DockerCli _dockerCli;

        public VersionInformationTest(ITestOutputHelper output)
        {
            _output = output;
            _dockerCli = new DockerCli();
        }

        [Fact]
        public void DotnetAlias_UsesLtsVersion_ByDefault()
        {
            // Arrange
            var expectedOutput = DotnetCoreConstants.DotnetCoreSdkVersion21;

            // Act
            var result = _dockerCli.Run(
                Settings.BuildImageName,
                commandToExecuteOnRun: "dotnet",
                commandArguments: new[] { "--version" });

            // Assert
            var actualOutput = result.Output.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData("1", DotnetCoreConstants.DotnetCoreSdkVersion11)]
        [InlineData("1.1", DotnetCoreConstants.DotnetCoreSdkVersion11)]
        [InlineData("1.1.12", DotnetCoreConstants.DotnetCoreSdkVersion11)]
        [InlineData("2", DotnetCoreConstants.DotnetCoreSdkVersion21)]
        [InlineData("2.1", DotnetCoreConstants.DotnetCoreSdkVersion21)]
        [InlineData("lts", DotnetCoreConstants.DotnetCoreSdkVersion21)]
        [InlineData(DotnetCoreConstants.DotnetCoreSdkVersion21, DotnetCoreConstants.DotnetCoreSdkVersion21)]
        [InlineData("2.2", DotnetCoreConstants.DotnetCoreSdkVersion22)]
        [InlineData(DotnetCoreConstants.DotnetCoreSdkVersion22, DotnetCoreConstants.DotnetCoreSdkVersion22)]
        public void DotnetAlias_UsesVersion_SpecifiedAtDockerRun(
            string versionSentToDockerRun,
            string expectedOutput)
        {
            // Arrange
            var script = new ShellScriptBuilder()
                .AddCommand($"source /usr/local/bin/benv dotnet={versionSentToDockerRun}")
                .AddCommand("dotnet --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(
                Settings.BuildImageName,
                commandToExecuteOnRun: "/bin/bash",
                commandArguments:
                new[]
                {
                    "-c",
                    script
                });

            // Assert
            var actualOutput = result.Output.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void Node_UsesLTSVersion_ByDefault_WhenNoExplicitVersionIsProvided()
        {
            // Arrange
            var expectedOutput = "v10.15.2";

            // Arrange & Act
            var result = _dockerCli.Run(
                Settings.BuildImageName,
                commandToExecuteOnRun: "node",
                commandArguments: new[] { "--version" });

            // Assert
            var actualOutput = result.Output.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void PythonAlias_UsesPython2_ByDefault_WhenNoExplicitVersionIsProvided()
        {
            // Arrange
            var expectedOutput = Python27VersionInfo;

            // Arrange & Act
            var result = _dockerCli.Run(
                Settings.BuildImageName,
                commandToExecuteOnRun: "python",
                commandArguments: new[] { "--version" });

            // Assert
            var actualOutput = result.Error.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void Python3Alias_UsesPythonLatestVersion_ByDefault_WhenNoExplicitVersionIsProvided()
        {
            // Arrange
            var expectedOutput = $"Python {Common.PythonVersions.Python37Version}";

            // Arrange & Act
            var result = _dockerCli.Run(
                Settings.BuildImageName,
                commandToExecuteOnRun: "python3",
                commandArguments: new[] { "--version" });

            // Assert
            var actualOutput = result.Output.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData("4", "v4.5.0")]
        [InlineData("4.5", "v4.5.0")]
        [InlineData("4.5.0", "v4.5.0")]
        [InlineData("6", "v6.11.0")]
        [InlineData("6.11", "v6.11.0")]
        [InlineData("6.11.0", "v6.11.0")]
        [InlineData("lts", "v10.15.2")]
        [InlineData("8", "v8.15.1")]
        [InlineData("8.1.4", "v8.1.4")]
        [InlineData("8.11", "v8.11.2")]
        [InlineData("8.11.2", "v8.11.2")]
        [InlineData("8.12.0", "v8.12.0")]
        [InlineData("8.15", "v8.15.1")]
        [InlineData("9", "v9.4.0")]
        [InlineData("9.4", "v9.4.0")]
        [InlineData("9.4.0", "v9.4.0")]
        [InlineData("10", "v10.15.2")]
        [InlineData("10.1", "v10.1.0")]
        [InlineData("10.1.0", "v10.1.0")]
        [InlineData("10.10.0", "v10.10.0")]
        [InlineData("10.14.1", "v10.14.1")]
        [InlineData("10.15", "v10.15.2")]
        public void NodeAlias_UsesVersion_SpecifiedAtDockerRun(
            string versionSentToDockerRun,
            string expectedOutput)
        {
            // Arrange
            var script = new ShellScriptBuilder()
                .AddCommand($"source /usr/local/bin/benv node={versionSentToDockerRun}")
                .AddCommand("node --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(
                Settings.BuildImageName,
                commandToExecuteOnRun: "/bin/bash",
                commandArguments:
                new[]
                {
                    "-c",
                    script
                });

            // Assert
            var actualOutput = result.Output.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData("2", Python27VersionInfo)]
        [InlineData("2.7", Python27VersionInfo)]
        [InlineData(Settings.Python27Version, Python27VersionInfo)]
        public void PythonAlias_UsesVersion_SpecifiedAtDockerRun(
            string versionSentToDockerRun,
            string expectedOutput)
        {
            // Arrange
            var script = new ShellScriptBuilder()
                .AddCommand($"source /usr/local/bin/benv python={versionSentToDockerRun}")
                .AddCommand("python --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(
                Settings.BuildImageName,
                commandToExecuteOnRun: "/bin/bash",
                commandArguments:
                new[]
                {
                    "-c",
                    script
                });

            // Assert
            // NOTE: Python2 version writes out information to StdErr unlike Python3 versions
            var actualOutput = result.Error.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData("2", Python27VersionInfo)]
        [InlineData("2.7", Python27VersionInfo)]
        [InlineData(Settings.Python27Version, Python27VersionInfo)]
        public void Python2Alias_UsesVersion_SpecifiedAtDockerRun(
            string versionSentToDockerRun,
            string expectedOutput)
        {
            // Arrange
            var script = new ShellScriptBuilder()
                .AddCommand($"source /usr/local/bin/benv python={versionSentToDockerRun}")
                .AddCommand("python2 --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(
                Settings.BuildImageName,
                commandToExecuteOnRun: "/bin/bash",
                commandArguments:
                new[]
                {
                    "-c",
                    script
                });

            // Assert
            var actualOutput = result.Error.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData("latest", Python37VersionInfo)]
        [InlineData("3", Python37VersionInfo)]
        [InlineData("3.5", Python35VersionInfo)]
        [InlineData(Settings.Python35Version, Python35VersionInfo)]
        [InlineData("3.6", Python36VersionInfo)]
        [InlineData(Settings.Python36Version, Python36VersionInfo)]
        [InlineData("3.7", Python37VersionInfo)]
        [InlineData(Common.PythonVersions.Python37Version, Python37VersionInfo)]
        public void Python3_UsesVersion_SpecifiedAtDockerRun(
            string versionSentToDockerRun,
            string expectedOutput)
        {
            // Arrange
            var script = new ShellScriptBuilder()
                .AddCommand($"source /usr/local/bin/benv python={versionSentToDockerRun}")
                .AddCommand("python --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(
                Settings.BuildImageName,
                commandToExecuteOnRun: "/bin/bash",
                commandArguments:
                new[]
                {
                    "-c",
                    script
                });

            // Assert
            var actualOutput = result.Output.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        private void RunAsserts(Action action, string message)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                _output.WriteLine(message);
                throw;
            }
        }
    }
}