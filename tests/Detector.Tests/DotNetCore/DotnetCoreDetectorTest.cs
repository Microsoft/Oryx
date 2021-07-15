// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Oryx.Detector.DotNetCore;
using Moq;
using Xunit;

namespace Microsoft.Oryx.Detector.Tests.DotNetCore
{
    public class DotNetCoreDetectorTest
    {
        [Fact]
        public void Detect_ReturnsNull_IfRepoDoesNotContain_ProjectFile()
        {
            // Arrange
            var sourceRepo = new Mock<ISourceRepo>();
            var context = CreateContext(sourceRepo.Object);
            var detector = CreateDetector(projectFile: null);

            // Act
            var result = detector.Detect(context);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Detect_ReturnsNull_IfProjectFile_DoesNotHaveTargetFrameworkSpecified()
        {
            // Arrange
            var projectFile = "test.csproj";
            var sourceRepo = new Mock<ISourceRepo>();
            sourceRepo
                .Setup(repo => repo.EnumerateFiles(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new[] { projectFile });
            sourceRepo
                .Setup(repo => repo.ReadFile(It.IsAny<string>()))
                .Returns(SampleProjectFileContents.ProjectFileWithNoTargetFramework);
            var context = CreateContext(sourceRepo.Object);
            var detector = CreateDetector(projectFile);

            // Act
            var result = detector.Detect(context);

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("net6.0", true)]
        [InlineData("net5.0", false)]
        public void Detect_ReturnsExpected_BlazorWebAssemblyApp_ProjectFileHaveTargetFrameworkSpecified(
            string targetFrameworkName,
            bool installAOTWorkloads)
        {
            // Arrange
            var projectFile = "test.csproj";
            var sourceRepo = new Mock<ISourceRepo>();
            sourceRepo
                .Setup(repo => repo.EnumerateFiles(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new[] { projectFile });
            sourceRepo
                .Setup(repo => repo.ReadFile(It.IsAny<string>()))
                .Returns(SampleProjectFileContents.ProjectFileAzureBlazorWasmClientWithTargetFrameworkPlaceHolder
                .Replace(
                    "#TargetFramework#",
                    targetFrameworkName));
            var context = CreateContext(sourceRepo.Object);
            var detector = CreateDetector(projectFile);

            // Act
            var result = (DotNetCorePlatformDetectorResult) detector.Detect(context);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(installAOTWorkloads, result.InstallAOTWorkloads);
        }

        [Theory]
        [InlineData("netcoreapp2.1", "2.1")]
        [InlineData("net5.0", "5.0")]
        public void Detect_ReturnsVersionPartOfTargetFramework(
            string targetFrameworkName,
            string expectedRuntimeVersion)
        {
            // Arrange
            var projectFile = "test.csproj";
            var sourceRepo = new Mock<ISourceRepo>();
            sourceRepo
                .Setup(repo => repo.EnumerateFiles(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new[] { projectFile });
            sourceRepo
                .Setup(repo => repo.ReadFile(It.IsAny<string>()))
                .Returns(SampleProjectFileContents.ProjectFileWithTargetFrameworkPlaceHolder.Replace(
                    "#TargetFramework#",
                    targetFrameworkName));
            var context = CreateContext(sourceRepo.Object);
            var detector = CreateDetector(projectFile);

            // Act
            var result = detector.Detect(context);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedRuntimeVersion, result.PlatformVersion);
        }

        private DetectorContext CreateContext(ISourceRepo sourceRepo)
        {
            return new DetectorContext
            {
                SourceRepo = sourceRepo,
            };
        }

        private DotNetCoreDetector CreateDetector(string projectFile)
        {
            return new DotNetCoreDetector(
                new TestProjectFileProvider(projectFile),
                NullLogger<DotNetCoreDetector>.Instance);
        }
    }
}
