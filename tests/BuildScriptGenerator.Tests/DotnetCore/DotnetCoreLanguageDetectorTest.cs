﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Oryx.BuildScriptGenerator.DotNetCore;
using Moq;
using Microsoft.Oryx.Tests.Common;
using Xunit;
using Microsoft.Oryx.BuildScriptGenerator.Exceptions;
using System.Collections.Generic;

namespace Microsoft.Oryx.BuildScriptGenerator.Tests.DotNetCore
{
    public class DotNetCoreLanguageDetectorTest
    {
        private const string ProjectFileWithNoTargetFramework = @"
        <Project Sdk=""Microsoft.NET.Sdk.Web"">
          <PropertyGroup>
            <LangVersion>7.3</LangVersion>
          </PropertyGroup>
        </Project>";

        private const string ProjectFileWithMultipleProperties = @"
        <Project Sdk=""Microsoft.NET.Sdk.Web"">
          <PropertyGroup>
            <LangVersion>7.3</LangVersion>
          </PropertyGroup>
          <PropertyGroup>
            <TargetFramework>netcoreapp2.1</TargetFramework>
            <LangVersion>7.3</LangVersion>
          </PropertyGroup>
        </Project>";

        private const string ProjectFileWithTargetFrameworkPlaceHolder = @"
        <Project Sdk=""Microsoft.NET.Sdk.Web"">
          <PropertyGroup>
            <TargetFramework>#TargetFramework#</TargetFramework>
            <LangVersion>7.3</LangVersion>
            <IsPackable>false</IsPackable>
            <AssemblyName>Microsoft.Oryx.BuildScriptGenerator.Tests</AssemblyName>
            <RootNamespace>Microsoft.Oryx.BuildScriptGenerator.Tests</RootNamespace>
          </PropertyGroup>
        </Project>";

        private const string GlobalJsonWithSdkVersionPlaceholder = @"
        {
            ""sdk"": {
                ""version"": ""#version#""
            }
        }";

        [Fact]
        public void Detect_ReturnsNull_IfRepoDoesNotContain_ProjectFile()
        {
            // Arrange
            var sourceRepo = new Mock<ISourceRepo>();
            var detector = CreateDotnetCoreLanguageDetector(
                supportedVersions: GetAllSupportedRuntimeVersions(),
                projectFile: null);

            // Act
            var result = detector.Detect(sourceRepo.Object);

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
                .Returns(ProjectFileWithNoTargetFramework);
            var detector = CreateDotnetCoreLanguageDetector(
                supportedVersions: GetAllSupportedRuntimeVersions(),
                projectFile);

            // Act
            var result = detector.Detect(sourceRepo.Object);

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("netcoreapp1.0", DotNetCoreRuntimeVersions.NetCoreApp10)]
        [InlineData("netcoreapp1.1", DotNetCoreRuntimeVersions.NetCoreApp11)]
        [InlineData("netcoreapp2.0", DotNetCoreRuntimeVersions.NetCoreApp20)]
        [InlineData("netcoreapp2.1", DotNetCoreRuntimeVersions.NetCoreApp21)]
        [InlineData("netcoreapp2.2", DotNetCoreRuntimeVersions.NetCoreApp22)]
        [InlineData("netcoreapp3.0", DotNetCoreRuntimeVersions.NetCoreApp30)]
        public void Detect_ReturnsExpectedLanguageVersion_ForTargetFrameworkVersions(
            string netCoreAppVersion,
            string expectedSdkVersion)
        {
            // Arrange
            var projectFile = "test.csproj";
            var projectFileContent = ProjectFileWithTargetFrameworkPlaceHolder.Replace(
                "#TargetFramework#",
                netCoreAppVersion);
            var sourceRepo = new Mock<ISourceRepo>();
            sourceRepo
                .Setup(repo => repo.EnumerateFiles(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new[] { projectFile });
            sourceRepo
                .Setup(repo => repo.ReadFile(It.IsAny<string>()))
                .Returns(projectFileContent);
            var detector = CreateDotnetCoreLanguageDetector(
                supportedVersions: GetAllSupportedRuntimeVersions(),
                projectFile);

            // Act
            var result = detector.Detect(sourceRepo.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(DotnetCoreConstants.LanguageName, result.Language);
            Assert.Equal(expectedSdkVersion, result.LanguageVersion);
        }

        [Fact]
        public void Detect_ReturnsExpectedLanguageVersion_WhenProjectFileHasMultiplePropertyGroups()
        {
            // Arrange
            var projectFile = "test.csproj";
            var sourceRepo = new Mock<ISourceRepo>();
            sourceRepo
                .Setup(repo => repo.EnumerateFiles(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new[] { projectFile });
            sourceRepo
                .Setup(repo => repo.ReadFile(It.IsAny<string>()))
                .Returns(ProjectFileWithMultipleProperties);
            var detector = CreateDotnetCoreLanguageDetector(
                supportedVersions: GetAllSupportedRuntimeVersions(),
                projectFile);

            // Act
            var result = detector.Detect(sourceRepo.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(DotnetCoreConstants.LanguageName, result.Language);
            Assert.Equal(DotNetCoreRuntimeVersions.NetCoreApp21, result.LanguageVersion);
        }

        [Fact]
        public void Detect_ReturnsNull_ForUnknownNetCoreAppVersion()
        {
            // Arrange
            var projectFile = "test.csproj";
            var projectFileContent = ProjectFileWithTargetFrameworkPlaceHolder.Replace(
                "#TargetFramework#",
                "netcoreapp0.0");
            var sourceRepo = new Mock<ISourceRepo>();
            sourceRepo
                .Setup(repo => repo.EnumerateFiles(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new[] { projectFile });
            sourceRepo
                .Setup(repo => repo.ReadFile(It.IsAny<string>()))
                .Returns(projectFileContent);
            var detector = CreateDotnetCoreLanguageDetector(
                supportedVersions: GetAllSupportedRuntimeVersions(),
                projectFile);

            // Act
            var result = detector.Detect(sourceRepo.Object);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Detect_ThrowsUnsupportedException_WhenNoVersionFoundReturnsMaximumSatisfyingVersion()
        {
            // Arrange
            var projectFile = "test.csproj";
            var sourceRepo = new Mock<ISourceRepo>();
            sourceRepo
                .Setup(repo => repo.EnumerateFiles(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new[] { projectFile });
            sourceRepo
                .Setup(repo => repo.ReadFile(It.IsAny<string>()))
                .Returns(ProjectFileWithTargetFrameworkPlaceHolder.Replace("#TargetFramework#", "netcoreapp2.1"));
            var detector = CreateDotnetCoreLanguageDetector(
                supportedVersions: new[] { "2.2" },
                projectFile);

            // Act & Assert
            var exception = Assert.Throws<UnsupportedVersionException>(() => detector.Detect(sourceRepo.Object));
            Assert.Equal(
                $"Target .NET Core runtime version '{DotNetCoreRuntimeVersions.NetCoreApp21}' is unsupported. " +
                "Supported versions are: 2.2",
                exception.Message);
        }

        private DotnetCoreLanguageDetector CreateDotnetCoreLanguageDetector(
            string[] supportedVersions,
            string projectFile)
        {
            return CreateDotnetCoreLanguageDetector(
                supportedVersions,
                projectFile,
                new TestEnvironment());
        }

        private DotnetCoreLanguageDetector CreateDotnetCoreLanguageDetector(
            string[] supportedVersions,
            string projectFile,
            IEnvironment environment)
        {
            var optionsSetup = new DotnetCoreScriptGeneratorOptionsSetup(environment);
            var options = new DotnetCoreScriptGeneratorOptions();
            optionsSetup.Configure(options);

            return new DotnetCoreLanguageDetector(
                new TestVersionProvider(supportedVersions),
                Options.Create(options),
                new TestAspNetCoreWebAppProjectFileProvider(projectFile),
                NullLogger<DotnetCoreLanguageDetector>.Instance);
        }

        private string[] GetAllSupportedRuntimeVersions()
        {
            return new[]
            {
                DotNetCoreRuntimeVersions.NetCoreApp10,
                DotNetCoreRuntimeVersions.NetCoreApp11,
                DotNetCoreRuntimeVersions.NetCoreApp20,
                DotNetCoreRuntimeVersions.NetCoreApp21,
                DotNetCoreRuntimeVersions.NetCoreApp22,
                DotNetCoreRuntimeVersions.NetCoreApp30,
            };
        }

        private class TestAspNetCoreWebAppProjectFileProvider : IAspNetCoreWebAppProjectFileProvider
        {
            private readonly string _projectFilePath;

            public TestAspNetCoreWebAppProjectFileProvider(string projectFilePath)
            {
                _projectFilePath = projectFilePath;
            }

            public string GetRelativePathToProjectFile(ISourceRepo sourceRepo)
            {
                return _projectFilePath;
            }
        }
    }
}
