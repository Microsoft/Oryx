﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.IO;
using Microsoft.Oryx.BuildScriptGenerator.DotNetCore;
using Microsoft.Oryx.BuildScriptGenerator.Exceptions;
using Microsoft.Oryx.Tests.Common;
using Xunit;

namespace Microsoft.Oryx.BuildScriptGenerator.Tests.DotNetCore
{
    public class ProbeAndFindProjectFileProviderTest : ProjectFileProviderTestBase
    {
        public ProbeAndFindProjectFileProviderTest(TestTempDirTestFixture testFixture) : base(testFixture)
        {
        }

        [Fact]
        public void GetRelativePathToProjectFile_ReturnsNull_IfNoProjectFileFound()
        {
            // Arrange
            var sourceRepoDir = CreateSourceRepoDir();
            var sourceRepo = CreateSourceRepo(sourceRepoDir);
            var context = GetContext(sourceRepo);
            var providers = GetProjectFileProviders();

            // Act
            var actual = ProjectFileProviderHelper.GetRelativePathToProjectFile(providers, context);

            // Assert
            Assert.Null(actual);
        }

        [Theory]
        [InlineData(DotNetCoreConstants.CSharpProjectFileExtension)]
        [InlineData(DotNetCoreConstants.FSharpProjectFileExtension)]
        public void GetRelativePathToProjectFile_ReturnsNull_IfNoWebSdkProjectFound_AllAcrossRepo(
            string projectFileExtension)
        {
            // Arrange
            var sourceRepoDir = CreateSourceRepoDir();
            var srcDir = CreateDir(sourceRepoDir, "src");
            var webApp1Dir = CreateDir(srcDir, "WebApp1");
            File.WriteAllText(Path.Combine(webApp1Dir, $"WebApp1.{projectFileExtension}"), NonWebSdkProjectFile);
            var webApp2Dir = CreateDir(srcDir, "WebApp2");
            File.WriteAllText(Path.Combine(webApp2Dir, $"WebApp2.{projectFileExtension}"), NonWebSdkProjectFile);
            var sourceRepo = CreateSourceRepo(sourceRepoDir);
            var context = GetContext(sourceRepo);
            var providers = GetProjectFileProviders();

            // Act
            var actual = ProjectFileProviderHelper.GetRelativePathToProjectFile(providers, context);

            // Assert
            Assert.Null(actual);
        }

        [Theory]
        [InlineData(DotNetCoreConstants.CSharpProjectFileExtension)]
        [InlineData(DotNetCoreConstants.FSharpProjectFileExtension)]
        public void GetRelativePathToProjectFile_Throws_IfSourceRepo_HasMultipleWebSdkProjects(
            string projectFileExtension)
        {
            // Arrange
            var sourceRepoDir = CreateSourceRepoDir();
            var srcDir = CreateDir(sourceRepoDir, "src");
            var webApp1Dir = CreateDir(srcDir, "WebApp1");
            File.WriteAllText(Path.Combine(webApp1Dir, $"WebApp1.{projectFileExtension}"), WebSdkProjectFile);
            var webApp2Dir = CreateDir(srcDir, "WebApp2");
            File.WriteAllText(Path.Combine(webApp2Dir, $"WebApp2.{projectFileExtension}"), WebSdkProjectFile);
            var sourceRepo = CreateSourceRepo(sourceRepoDir);
            var context = GetContext(sourceRepo);
            var providers = GetProjectFileProviders();

            // Act & Assert
            var exception = Assert.Throws<InvalidUsageException>(
                () => ProjectFileProviderHelper.GetRelativePathToProjectFile(providers, context));
            Assert.StartsWith(
                "Ambiguity in selecting a project to build. Found multiple projects:",
                exception.Message);
        }

        [Theory]
        [InlineData(DotNetCoreConstants.CSharpProjectFileExtension)]
        [InlineData(DotNetCoreConstants.FSharpProjectFileExtension)]
        public void GetRelativePathToProjectFile_Throws_IfSourceRepo_HasMultipleWebAppProjects_AtDifferentDirLevels(
            string projectFileExtension)
        {
            // Arrange
            var sourceRepoDir = CreateSourceRepoDir();
            var srcDir = CreateDir(sourceRepoDir, "src");
            var webApp1Dir = CreateDir(srcDir, "WebApp1");
            File.WriteAllText(Path.Combine(webApp1Dir, $"WebApp1.{projectFileExtension}"), WebSdkProjectFile);
            var fooDir = CreateDir(srcDir, "foo");
            var webApp2Dir = CreateDir(fooDir, "WebApp2");
            File.WriteAllText(Path.Combine(webApp2Dir, $"WebApp2.{projectFileExtension}"), WebSdkProjectFile);
            var sourceRepo = CreateSourceRepo(sourceRepoDir);
            var context = GetContext(sourceRepo);
            var providers = GetProjectFileProviders();

            // Act & Assert
            var exception = Assert.Throws<InvalidUsageException>(
                () => ProjectFileProviderHelper.GetRelativePathToProjectFile(providers, context));
            Assert.StartsWith(
                "Ambiguity in selecting a project to build. Found multiple projects:",
                exception.Message);
        }

        [Theory]
        [InlineData(DotNetCoreConstants.CSharpProjectFileExtension)]
        [InlineData(DotNetCoreConstants.FSharpProjectFileExtension)]
        public void GetRelativePathToProjectFile_ReturnsProjectFile_ByProbingAllAcrossRepo(string projectFileExtension)
        {
            // Arrange
            var sourceRepoDir = CreateSourceRepoDir();
            var srcDir = CreateDir(sourceRepoDir, "src");
            var webApp1Dir = CreateDir(srcDir, "WebApp1");
            File.WriteAllText(Path.Combine(webApp1Dir, $"WebApp1.{projectFileExtension}"), NonWebSdkProjectFile);
            var webApp2Dir = CreateDir(srcDir, "WebApp2");
            var projectFile = Path.Combine(webApp2Dir, $"WebApp2.{projectFileExtension}");
            File.WriteAllText(projectFile, WebSdkProjectFile);
            var expectedRelativePath = Path.Combine("src", "WebApp2", $"WebApp2.{projectFileExtension}");
            var sourceRepo = CreateSourceRepo(sourceRepoDir);
            var context = GetContext(sourceRepo);
            var providers = GetProjectFileProviders();

            // Act
            var actualPath = ProjectFileProviderHelper.GetRelativePathToProjectFile(providers, context);

            // Assert
            Assert.Equal(expectedRelativePath, actualPath);
        }
    }
}
