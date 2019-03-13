﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.Oryx.Common;
using Microsoft.Oryx.Tests.Common;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Oryx.RuntimeImage.Tests
{
    public class PhpImageTest : TestBase
    {
        public PhpImageTest(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [InlineData("7.3", PhpVersions.Php73Version)]
        [InlineData("7.2", PhpVersions.Php72Version)]
        [InlineData("7.0", PhpVersions.Php70Version)]
        [InlineData("5.6", PhpVersions.Php56Version)]
        public void VersionMatchesImageName(string imageTag, string expectedPhpVersion)
        {
            // Arrange & Act
            var result = _dockerCli.Run("oryxdevms/php-" + imageTag + ":latest", "php", new[] { "--version" });

            // Assert
            RunAsserts(() =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains("PHP " + expectedPhpVersion, result.StdOut);
                },
                result.GetDebugInfo());
        }
    }
}
