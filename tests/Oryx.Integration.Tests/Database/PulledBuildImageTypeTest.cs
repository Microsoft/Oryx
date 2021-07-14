// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.Oryx.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Oryx.Integration.Tests
{
    [Trait("category", "db")]
    public class PulledBuildImageTypeTest
    {
        private readonly DockerCli _dockerCli;
        private readonly ITestOutputHelper _output;

        public PulledBuildImageTypeTest(ITestOutputHelper output)
        {
            _output = output;
            _dockerCli = new DockerCli();
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

        [Theory]
        [InlineData(Settings.WithRootAccessBuildImageName, "full")]
        [InlineData(Settings.WithRootAccessLtsVersionsBuildImageName, "ltsversions")]        
        [InlineData(Settings.GitHubActionsBuildImageName, "githubactions")]
        [InlineData(Settings.CliBuildImageName, "cli")]
        [InlineData(Settings.CliBusterBuildImageName, "cli")]
        [InlineData(Settings.JamStackBuildImageName, "jamstack")]
        [InlineData(Settings.VsoUbuntuBuildImageName, "vso-focal")]
        [InlineData(Settings.VsoSlimBuildImageName, "vso-slim")]
        public void PulledBuildImages_Contains_BUILDIMAGE_TYPE_Info(string buildImageName, string expectedBuildImageType)
        {
            // Arrange and Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = buildImageName,
                CommandToExecuteOnRun = "/bin/bash",
                CommandArguments = new[] { "-c", "cat /opt/oryx/.imagetype" }
            });

            // Assert
            var actualOutput = result.StdOut.ReplaceNewLine();
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains(expectedBuildImageType, actualOutput);
                },
                result.GetDebugInfo());
        }
        
    }
}