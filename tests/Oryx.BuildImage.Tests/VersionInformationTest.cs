// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.Oryx.BuildScriptGenerator.Common;
using Microsoft.Oryx.BuildScriptGenerator.DotNetCore;
using Microsoft.Oryx.BuildScriptGenerator.Node;
using Microsoft.Oryx.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Oryx.BuildImage.Tests
{
    public class VersionInformationTest
    {
        private const string Python27VersionInfo = "Python " + PythonVersions.Python27Version;
        private const string Python36VersionInfo = "Python " + PythonVersions.Python36Version;
        private const string Python37VersionInfo = "Python " + PythonVersions.Python37Version;
        private const string Python38VersionInfo = "Python " + PythonVersions.Python38Version;

        private readonly ITestOutputHelper _output;
        private readonly DockerCli _dockerCli;

        public VersionInformationTest(ITestOutputHelper output)
        {
            _output = output;
            _dockerCli = new DockerCli();
        }

        public static TheoryData<string> ImageNameData
        {
            get
            {
                var data = new TheoryData<string>();
                data.Add(Settings.BuildImageName);
                data.Add(Settings.LtsVersionsBuildImageName);
                var imageTestHelper = new ImageTestHelper();
                data.Add(imageTestHelper.GetAzureFunctionsJamStackBuildImage());
                data.Add(imageTestHelper.GetGitHubActionsBuildImage());
                data.Add(imageTestHelper.GetVsoBuildImage("vso-focal"));
                data.Add(imageTestHelper.GetVsoBuildImage("vso-slim"));
                return data;
            }
        }

        [SkippableTheory]
        [MemberData(nameof(ImageNameData))]
        public void OryxBuildImage_Contains_VersionAndCommit_Information(string buildImageName)
        {
            // we cant always rely on gitcommitid as env variable in case build context is not correctly passed
            // so we should check agent_os environment variable to know if the test is happening in azure devops agent 
            // or locally, locally we need to skip this test
            var agentOS = Environment.GetEnvironmentVariable("AGENT_OS");
            Skip.If(string.IsNullOrEmpty(agentOS));

            // Arrange
            var gitCommitID = GitHelper.GetCommitID();
            var buildNumber = Environment.GetEnvironmentVariable("BUILD_BUILDNUMBER");
            var expectedOryxVersion = string.Concat(Settings.OryxVersion, buildNumber);

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = buildImageName,
                CommandToExecuteOnRun = "oryx",
                CommandArguments = new[] { "--version" }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.DoesNotContain(".unspecified, Commit: unspecified", actualOutput);
                    Assert.Contains(gitCommitID, actualOutput);
                    Assert.Contains(expectedOryxVersion, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData("vso-focal")]
        [InlineData("vso-slim")]
        public void OryxVsoBuildImage_Contains_PHP_Xdebug(string imageVersion)
        {
            var imageTestHelper = new ImageTestHelper();
            string buildImage = imageTestHelper.GetVsoBuildImage(imageVersion);

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = buildImage,
                CommandToExecuteOnRun = "php",
                CommandArguments = new[] { "--version" }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains("with Xdebug", actualOutput);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData("bundler", "vso-focal")]
        [InlineData("rake", "vso-focal")]
        [InlineData("ruby-debug-ide", "vso-focal")]
        [InlineData("debase", "vso-focal")]
        [InlineData("bundler", "vso-slim")]
        [InlineData("rake", "vso-slim")]
        [InlineData("ruby-debug-ide", "vso-slim")]
        [InlineData("debase", "vso-slim")]
        public void OryxVsoBuildImage_Contains_Required_Ruby_Gems(string gemName, string imageVersion)
        {
            var imageTestHelper = new ImageTestHelper();
            string buildImage = imageTestHelper.GetVsoBuildImage(imageVersion);

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = buildImage,
                CommandToExecuteOnRun = "gem",
                CommandArguments = new[] { "list", gemName }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains(gemName, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData(Settings.BuildImageName)]
        [InlineData(Settings.LtsVersionsBuildImageName)]
        public void DotNetAlias_UsesLtsVersion_ByDefault(string buildImageName)
        {
            // Arrange
            var expectedOutput = DotNetCoreSdkVersions.DotNetCore31SdkVersion;

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = buildImageName,
                CommandToExecuteOnRun = "dotnet",
                CommandArguments = new[] { "--version" }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData(DotNetCoreSdkVersions.DotNetCore21SdkVersion)]
        [InlineData(DotNetCoreSdkVersions.DotNetCore22SdkVersion)]
        [InlineData(DotNetCoreSdkVersions.DotNetCore30SdkVersion)]
        [InlineData(DotNetCoreSdkVersions.DotNetCore31SdkVersion)]
        public void DotNetAlias_UsesVersion_SetOnBenv(string expectedSdkVersion)
        {
            // Arrange
            var script = new ShellScriptBuilder()
                .Source($"benv dotnet={expectedSdkVersion}")
                .AddCommand("dotnet --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedSdkVersion, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData(Settings.BuildImageName)]
        [InlineData(Settings.LtsVersionsBuildImageName)]
        public void Node_UsesLTSVersion_ByDefault_WhenNoExplicitVersionIsProvided(string buildImageName)
        {
            // Arrange
            var expectedOutput = "v" + NodeConstants.NodeLtsVersion;

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = buildImageName,
                CommandToExecuteOnRun = "node",
                CommandArguments = new[] { "--version" }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData(Settings.BuildImageName)]
        [InlineData(Settings.LtsVersionsBuildImageName)]
        public void Python3Alias_UsesPythonLatestVersion_ByDefault_WhenNoExplicitVersionIsProvided(
            string buildImageName)
        {
            // Arrange
            var expectedOutput = $"Python {PythonVersions.Python38Version}";

            // Arrange & Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = buildImageName,
                CommandToExecuteOnRun = "python3",
                CommandArguments = new[] { "--version" }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Trait("platform", "node")]
        [Theory]
        [InlineData("8.1.4", "v8.1.4")]
        [InlineData("8.11", "v8.11.4")]
        [InlineData("8.11.4", "v8.11.4")]
        [InlineData("8.12.0", "v8.12.0")]
        [InlineData("8.15", "v8.15.1")]
        [InlineData("9", "v9.4.0")]
        [InlineData("9.4", "v9.4.0")]
        [InlineData("9.4.0", "v9.4.0")]
        [InlineData("10.1", "v10.1.0")]
        [InlineData("10.1.0", "v10.1.0")]
        [InlineData("10.10.0", "v10.10.0")]
        [InlineData("10.14.2", "v10.14.2")]
        [InlineData("8", "v" + NodeVersions.Node8Version)]
        [InlineData("10", "v" + NodeVersions.Node10Version)]
        [InlineData("12", "v" + NodeVersions.Node12Version)]
        [InlineData(NodeVersions.Node8Version, "v" + NodeVersions.Node8Version)]
        [InlineData(NodeVersions.Node10Version, "v" + NodeVersions.Node10Version)]
        [InlineData(NodeVersions.Node12Version, "v" + NodeVersions.Node12Version)]
        [InlineData(NodeVersions.Node14Version, "v" + NodeVersions.Node14Version)]
        [InlineData("lts", "v" + NodeConstants.NodeLtsVersion)]
        public void NodeAlias_UsesVersion_SetOnBenv(string specifiedVersion, string expectedOutput)
        {
            // Arrange
            var script = new ShellScriptBuilder()
                .Source($"benv node={specifiedVersion}")
                .AddCommand("node --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Trait("platform", "node")]
        [Theory]
        // Only version 6 of npm is upgraded, so the following should remain unchanged.
        [InlineData("10.1", "5.6.0")]
        // Make sure the we get the upgraded version of npm in the following cases
        [InlineData("10.10.0", "6.9.0")]
        [InlineData("10.14.2", "6.9.0")]
        public void UsesExpectedNpmVersion(string nodeVersion, string expectedOutput)
        {
            // Arrange
            var script = new ShellScriptBuilder()
                .Source($"benv node={nodeVersion}")
                .AddCommand("npm --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void NpmVersion_IsNotUpgraded_To_6_9_0()
        {
            // Arrange
            var nodeVersion = NodeVersions.Node12Version;
            var script = new ShellScriptBuilder()
                .Source($"benv node={nodeVersion}")
                .AddCommand("npm --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.DoesNotContain("6.9.0", actualOutput);
                },
                result.GetDebugInfo());
        }

        [Trait("platform", "python")]
        [Theory]
        [InlineData("2", Python27VersionInfo)]
        [InlineData("2.7", Python27VersionInfo)]
        [InlineData(PythonVersions.Python27Version, Python27VersionInfo)]
        public void PythonAlias_UsesVersion_SetOnBenv(string specifiedVersion, string expectedOutput)
        {
            // Arrange
            var script = new ShellScriptBuilder()
                .Source($"benv python={specifiedVersion}")
                .AddCommand("python --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            // NOTE: Python2 version writes out information to StdErr unlike Python3 versions
            var actualOutput = result.StdErr.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Trait("platform", "python")]
        [Theory]
        [InlineData("2", Python27VersionInfo)]
        [InlineData("2.7", Python27VersionInfo)]
        [InlineData(PythonVersions.Python27Version, Python27VersionInfo)]
        public void Python2Alias_UsesVersion_SetOnBenv(string specifiedVersion, string expectedOutput)
        {
            // Arrange
            var script = new ShellScriptBuilder()
                .Source($"benv python={specifiedVersion}")
                .AddCommand("python2 --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            var actualOutput = result.StdErr.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Trait("platform", "python")]
        [Theory]
        [InlineData("latest", Python38VersionInfo)]
        [InlineData("stable", Python38VersionInfo)]
        [InlineData("3", Python38VersionInfo)]
        [InlineData("3.6", Python36VersionInfo)]
        [InlineData(PythonVersions.Python36Version, Python36VersionInfo)]
        [InlineData("3.7", Python37VersionInfo)]
        [InlineData(PythonVersions.Python37Version, Python37VersionInfo)]
        [InlineData("3.8", Python38VersionInfo)]
        [InlineData(PythonVersions.Python38Version, Python38VersionInfo)]
        public void Python3_UsesVersion_SetOnBenv(string specifiedVersion, string expectedOutput)
        {
            // Arrange
            var script = new ShellScriptBuilder()
                .Source($"benv python={specifiedVersion}")
                .AddCommand("python --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Trait("platform", "dotnet")]
        [Theory]
        [InlineData("DotNet", "dotnet")]
        [InlineData("dotnet", "dotNet")]
        [InlineData("DOTNET_VERSION", "DOTNET_VERSION")]
        [InlineData("dotnet_version", "dotnet_version")]
        public void DotNetAlias_UsesVersionSetOnBenvArgument_OverVersionSetInEnvironmentVariable(
            string environmentVariableName,
            string argumentName)
        {
            // Arrange
            var expectedOutput = DotNetCoreSdkVersions.DotNetCore21SdkVersion;
            var script = new ShellScriptBuilder()
                //.SetEnvironmentVariable("ENABLE_DYNAMIC_INSTALL", "true")
                .SetEnvironmentVariable(environmentVariableName, DotNetCoreSdkVersions.DotNetCore31SdkVersion)
                .Source($"benv {argumentName}={DotNetCoreSdkVersions.DotNetCore21SdkVersion}")
                .AddCommand("dotnet --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Trait("platform", "dotnet")]
        [Fact]
        public void RunningBenvMultipleTimes_HonorsLastRunArguments()
        {
            // Arrange
            var expectedOutput = DotNetCoreSdkVersions.DotNetCore21SdkVersion;
            var script = new ShellScriptBuilder()
                .Source($"benv dotnet={DotNetCoreSdkVersions.DotNetCore31SdkVersion}")
                .Source($"benv dotnet_version={DotNetCoreSdkVersions.DotNetCore21SdkVersion}")
                // benv should update the PATH environment in such a way that we should version 1
                .AddCommand("dotnet --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        public static TheoryData<string> PhpVersionImageNameData
        {
            get
            {
                var data = new TheoryData<string>();
                data.Add(Settings.BuildImageName);
                var imageTestHelper = new ImageTestHelper();
                data.Add(imageTestHelper.GetVsoBuildImage("vso-slim"));
                data.Add(imageTestHelper.GetVsoBuildImage("vso-focal"));
                return data;
            }
        }

        [Theory]
        [MemberData(nameof(PhpVersionImageNameData))]
        public void PhpAlias_UsesPhpLatestVersion_ByDefault_WhenNoExplicitVersionIsProvided(string buildImageName)
        {
            // Arrange
            var expectedOutput = $"PHP {PhpVersions.Php73Version} (cli) ";

            // Arrange & Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = buildImageName,
                CommandToExecuteOnRun = "php",
                CommandArguments = new[] { "--version" }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData(PhpVersions.Php73Version, PhpVersions.Php73Version)]
        [InlineData("7.3", PhpVersions.Php73Version)]
        [InlineData("7", PhpVersions.Php73Version)]
        public void Php_UsesVersion_SetOnBenv(string specifiedVersion, string expectedVersion)
        {
            // Arrange
            var expectedOutput = $"PHP {expectedVersion} (cli) ";
            var script = new ShellScriptBuilder()
                .Source($"benv php={specifiedVersion}")
                .AddCommand("php --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains(expectedOutput, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void BenvShouldSetUpEnviroment_WhenMultiplePlatforms_AreSuppliedAsArguments()
        {
            // Arrange
            var expectedDotNetVersion = DotNetCoreSdkVersions.DotNetCore21SdkVersion;
            var expectedPythonVersion = Python36VersionInfo;
            var script = new ShellScriptBuilder()
                .Source($"benv dotnet={DotNetCoreSdkVersions.DotNetCore21SdkVersion} python=3.6")
                .AddCommand("dotnet --version")
                .AddCommand("python --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains(expectedDotNetVersion, actualOutput);
                    Assert.Contains(expectedPythonVersion, actualOutput);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void BenvShouldSetUpEnviroment_UsingExactNames()
        {
            // Arrange
            var expectedDotNetVersion = DotNetCoreSdkVersions.DotNetCore31SdkVersion;
            var script = new ShellScriptBuilder()
                .Source("benv dotnet_foo=1")
                .AddCommand("dotnet --version")
                .ToString();

            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", script }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains(expectedDotNetVersion, actualOutput);
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