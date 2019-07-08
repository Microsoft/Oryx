﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using McMaster.Extensions.CommandLineUtils;
using Microsoft.Oryx.Tests.Common;
using Xunit;
using Microsoft.Oryx.Common;

namespace Microsoft.Oryx.BuildScriptGeneratorCli.Tests
{
    public class ExecCommandTest : IClassFixture<TestTempDirTestFixture>
    {
        private readonly TestTempDirTestFixture _testDir;

        public ExecCommandTest(TestTempDirTestFixture testFixture)
        {
            _testDir = testFixture;
        }

        [Fact]
        public void OnExecute_ExitsSuccessfully_WhenCommandIsEmpty()
        {
            // Arrange
            var cmd = new ExecCommand { DebugMode = true, SourceDir = _testDir.RootDirPath };
            var console = new TestConsole();

            // Act
            var exitCode = cmd.OnExecute(new CommandLineApplication(console), console);

            // Assert
            Assert.Equal(ProcessConstants.ExitSuccess, exitCode);
        }
    }
}
