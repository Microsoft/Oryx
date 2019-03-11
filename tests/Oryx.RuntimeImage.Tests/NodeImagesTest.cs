﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.Oryx.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Oryx.RuntimeImage.Tests
{
    public class NodeImagesTest
    {
        private readonly ITestOutputHelper _output;
        private readonly DockerCli _dockerCli;

        public NodeImagesTest(ITestOutputHelper output)
        {
            _output = output;
            _dockerCli = new DockerCli();
        }

        [SkippableTheory]
        [InlineData("4.4")]
        [InlineData("4.5")]
        [InlineData("4.8")]
        [InlineData("6.2")]
        [InlineData("6.6")]
        [InlineData("6.9")]
        [InlineData("6.10")]
        [InlineData("6.11")]
        [InlineData("8.0")]
        [InlineData("8.1")]
        [InlineData("8.2")]
        [InlineData("8.8")]
        [InlineData("8.9")]
        [InlineData("8.11")]
        [InlineData("8.12")]
        [InlineData("9.4")]
        [InlineData("10.1")]
        [InlineData("10.10")]
        [InlineData("10.12")]
        [InlineData("10.14")]
        public void NodeRuntimeImage_Contains_VersionAndCommit_Information(string version)
        {
            var agentOS = Environment.GetEnvironmentVariable("AGENT_OS");
            var gitCommitID = Environment.GetEnvironmentVariable("BUILD_SOURCEVERSION");
            var buildNumber = Environment.GetEnvironmentVariable("BUILD_BUILDNUMBER");
            var expectedOryxVersion = string.Concat(Settings.OryxVersion, buildNumber);

            // we cant always rely on gitcommitid as env variable in case build context is not correctly passed
            // so we should check agent_os environment variable to know if the build is happening in azure devops agent 
            // or locally, locally we need to skip this test
            Skip.If(string.IsNullOrEmpty(agentOS));
            // Act
            var result = _dockerCli.Run(
                "oryxdevms/node-" + version + ":latest",
                commandToExecuteOnRun: "oryx",
                commandArguments: new[] { "--version" });
            // Assert
            RunAsserts(
                () =>
                {
                    Assert.False(result.IsSuccess);
                    Assert.NotNull(result.Error);
                    Assert.DoesNotContain(".unspecified, Commit: unspecified", result.Error);
                    Assert.Contains(gitCommitID, result.Error);
                    Assert.Contains(expectedOryxVersion, result.Error);
                },
                result.GetDebugInfo());
        }

        [Theory]
        [InlineData("4.4", "4.4.7")]
        [InlineData("4.5", "4.5.0")]
        [InlineData("4.8", "4.8.7")]
        [InlineData("6.2", "6.2.2")]
        [InlineData("6.6", "6.6.0")]
        [InlineData("6.9", "6.9.5")]
        [InlineData("6.10", "6.10.3")]
        [InlineData("6.11", "6.11.5")]
        [InlineData("8.0", "8.0.0")]
        [InlineData("8.1", "8.1.4")]
        [InlineData("8.2", "8.2.1")]
        [InlineData("8.8", "8.8.1")]
        [InlineData("8.9", "8.9.4")]
        [InlineData("8.11", "8.11.4")]
        [InlineData("8.12", "8.12.0")]
        [InlineData("9.4", "9.4.0")]
        [InlineData("10.1", "10.1.0")]
        [InlineData("10.10", "10.10.0")]
        [InlineData("10.12", "10.12.0")]
        [InlineData("10.14", "10.14.1")]
        public void NodeVersionMatchesImageName(string nodeTag, string nodeVersion)
        {
            // Arrange & Act
            var expectedNodeVersion = "v" + nodeVersion;
            var result = _dockerCli.Run(
                "oryxdevms/node-" + nodeTag + ":latest",
                commandToExecuteOnRun: "node",
                commandArguments: new[] { "--version" });

            // Assert
            var actualOutput = result.Output.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Equal(expectedNodeVersion, actualOutput);
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
