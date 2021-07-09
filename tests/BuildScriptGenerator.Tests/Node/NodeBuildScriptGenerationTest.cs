// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Oryx.BuildScriptGenerator.Common;
using Microsoft.Oryx.BuildScriptGenerator.Node;
using Microsoft.Oryx.Detector;
using Microsoft.Oryx.Detector.Node;
using Microsoft.Oryx.Tests.Common;
using Xunit;

namespace Microsoft.Oryx.BuildScriptGenerator.Tests.Node
{
    /// <summary>
    /// Component tests for NodeJs support.
    /// </summary>
    public class NodeBuildScriptGenerationTest
    {
        private const string PackageJsonWithBuildScript = @"{
          ""name"": ""mynodeapp"",
          ""version"": ""1.0.0"",
          ""description"": ""test app"",
          ""main"": ""server.js"",
          ""scripts"": {
            ""build"": ""tsc"",
            ""build:azure"": ""tsc"",
          },
          ""author"": ""Dev"",
          ""license"": ""ISC"",
          ""dependencies"": {
            ""@types/node"": ""7.0.22"",
            ""express"": ""4.15.3"",
          },
          ""devDependencies"": {
            ""@types/mocha"": ""2.2.41"",
            ""@types/node"": ""7.0.22"",
            ""tsc"": ""1.20150623.0"",
            ""typescript"": ""2.3.3"",
            ""typescript-eslint-parser"": ""3.0.0""
          }
        }";

        private const string PackageJsonWithNoNpmVersion = @"{
          ""name"": ""mynodeapp"",
          ""version"": ""1.0.0"",
          ""description"": ""test app"",
          ""main"": ""server.js"",
          ""scripts"": {
            ""test"": ""echo \""Error: no test specified\"" && exit 1"",
            ""start"": ""node server.js""
          },
          ""author"": ""Dev"",
          ""license"": ""ISC"",
          ""engines"" : { ""node"" : ""6.11.0"" }
        }";

        private const string PackageJsonWithNpmVersion = @"{
          ""name"": ""mynodeapp"",
          ""version"": ""1.0.0"",
          ""description"": ""test app"",
          ""main"": ""server.js"",
          ""scripts"": {
            ""test"": ""echo \""Error: no test specified\"" && exit 1"",
            ""start"": ""node server.js""
          },
          ""author"": ""Dev"",
          ""license"": ""ISC"",
          ""engines"" : { ""npm"" : ""5.4.2"" },
          ""dependencies"": { ""foo"" : ""1.0.0 - 2.9999.9999"", ""bar"" : "">=1.0.2 <2.1.2"" }
        }";
        private const string MalformedPackageJson = @"{
          ""name"": ""mynodeapp"",
          ""version"": ""1.0.0"",
          ""description"": ""test app"",
          ""main"": ""server.js"",
          ""scripts"": {
            ""test"": ""echo ,
            ""start"": ""node server.js""
          },
          ""author"": ""Dev"",
          ""license"": ""ISC""
        }";

        private const string NpmInstallCommand = NodeConstants.NpmPackageInstallCommand;
        private const string YarnInstallCommand = "yarn install --prefer-offline";

        [Fact]
        public void GeneratedScript_HasNpmVersion_SpecifiedInPackageJson()
        {
            // Arrange
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                new BuildScriptGeneratorOptions { PlatformVersion = "8.2.1" },
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithNpmVersion, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
            };

            var expected = new NodeBashBuildSnippetProperties
            {
                PackageInstallCommand = NpmInstallCommand,
                PackageInstallerVersionCommand = NodeConstants.NpmVersionCommand,
                NpmRunBuildCommand = null,
                NpmRunBuildAzureCommand = null,
                HasProdDependencies = true,
                HasDevDependencies = false,
                ProductionOnlyPackageInstallCommand = string.Format(
                    NodeConstants.ProductionOnlyPackageInstallCommandTemplate,
                    NpmInstallCommand),
                CompressedNodeModulesFileName = null,
                CompressNodeModulesCommand = null,
                NodeBuildProperties = new Dictionary<string, string>
                {
                    {"PlatformWithVersion", "nodejs 10.10.10" },
                },
                NodeManifestFileName = FilePaths.BuildCommandsFileName,
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Equal(
                TemplateHelper.Render(TemplateHelper.TemplateResource.NodeBuildSnippet, expected),
                snippet.BashBuildScriptSnippet);
            Assert.True(scriptGenerator.IsCleanRepo(repo));
        }

        [Fact]
        public void GeneratedScript_HasDefaultNpmVersion_IfPackageJsonDoesNotHaveOne()
        {
            // Arrange
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                new BuildScriptGeneratorOptions { PlatformVersion = "8.2.1" },
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithNoNpmVersion, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
            };
            var expected = new NodeBashBuildSnippetProperties
            {
                PackageInstallCommand = NpmInstallCommand,
                PackageInstallerVersionCommand = NodeConstants.NpmVersionCommand,
                NpmRunBuildCommand = null,
                NpmRunBuildAzureCommand = null,
                HasProdDependencies = false,
                ProductionOnlyPackageInstallCommand = string.Format(
                    NodeConstants.ProductionOnlyPackageInstallCommandTemplate,
                    NpmInstallCommand),
                CompressedNodeModulesFileName = null,
                CompressNodeModulesCommand = null,
                NodeBuildProperties = new Dictionary<string, string>
                {
                    {"PlatformWithVersion", "nodejs 10.10.10" },
                },
                NodeManifestFileName = FilePaths.BuildCommandsFileName,
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);
            var renderedTemplate = TemplateHelper.Render(TemplateHelper.TemplateResource.NodeBuildSnippet, expected);

            // Assert
            Assert.NotNull(snippet);
            Assert.Equal(renderedTemplate, snippet.BashBuildScriptSnippet);
            Assert.True(scriptGenerator.IsCleanRepo(repo));
        }

        [Fact]
        public void GeneratesScript_WithDefaultNpmVersion_ForMalformedPackageJson()
        {
            // Arrange
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                new BuildScriptGeneratorOptions { PlatformVersion = "8.2.1" },
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(MalformedPackageJson, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
            };
            var expected = new NodeBashBuildSnippetProperties
            {
                PackageInstallCommand = NpmInstallCommand,
                PackageInstallerVersionCommand = NodeConstants.NpmVersionCommand,
                NpmRunBuildCommand = null,
                NpmRunBuildAzureCommand = null,
                HasProdDependencies = false,
                ProductionOnlyPackageInstallCommand = string.Format(
                    NodeConstants.ProductionOnlyPackageInstallCommandTemplate,
                    NpmInstallCommand),
                CompressedNodeModulesFileName = null,
                CompressNodeModulesCommand = null,
                NodeBuildProperties = new Dictionary<string, string>
                {
                    {"PlatformWithVersion", "nodejs 10.10.10" },
                },
                NodeManifestFileName = FilePaths.BuildCommandsFileName,
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Equal(
                TemplateHelper.Render(TemplateHelper.TemplateResource.NodeBuildSnippet, expected),
                snippet.BashBuildScriptSnippet);
            Assert.True(scriptGenerator.IsCleanRepo(repo));
        }

        [Fact]
        public void GeneratedScript_UsesYarnInstall_IfYarnLockFileIsPresent()
        {
            // Arrange
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                new BuildScriptGeneratorOptions { PlatformVersion = "8.2.1" },
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithNoNpmVersion, NodeConstants.PackageJsonFileName);
            repo.AddFile("Yarn lock file content here", NodeConstants.YarnLockFileName);
            var context = CreateScriptGeneratorContext(repo);
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
            };
            var expected = new NodeBashBuildSnippetProperties
            {
                PackageInstallCommand = YarnInstallCommand,
                PackageInstallerVersionCommand = NodeConstants.YarnVersionCommand,
                NpmRunBuildCommand = null,
                NpmRunBuildAzureCommand = null,
                HasProdDependencies = false,
                ProductionOnlyPackageInstallCommand = string.Format(
                    NodeConstants.ProductionOnlyPackageInstallCommandTemplate,
                    YarnInstallCommand),
                CompressedNodeModulesFileName = null,
                CompressNodeModulesCommand = null,
                ConfigureYarnCache = false,
                NodeBuildProperties = new Dictionary<string, string>
                {
                    {"PlatformWithVersion", "nodejs 10.10.10" },
                },
                NodeManifestFileName = FilePaths.BuildCommandsFileName,
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Equal(
                TemplateHelper.Render(TemplateHelper.TemplateResource.NodeBuildSnippet, expected),
                snippet.BashBuildScriptSnippet);
            Assert.True(scriptGenerator.IsCleanRepo(repo));
        }

        [Fact]
        public void GeneratedScript_UsesYarnInstallAndRunsNpmBuild_IfYarnLockIsPresent_AndHasBuildNodeUnderScripts()
        {
            // Arrange
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                new BuildScriptGeneratorOptions { PlatformVersion = "8.2.1" },
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithBuildScript, NodeConstants.PackageJsonFileName);
            repo.AddFile("Yarn lock file content here", NodeConstants.YarnLockFileName);
            var context = CreateScriptGeneratorContext(repo);
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
            };
            var expected = new NodeBashBuildSnippetProperties
            {
                PackageInstallCommand = YarnInstallCommand,
                PackageInstallerVersionCommand = NodeConstants.YarnVersionCommand,
                NpmRunBuildCommand = "yarn run build",
                NpmRunBuildAzureCommand = "yarn run build:azure",
                HasProdDependencies = true,
                HasDevDependencies = true,
                ProductionOnlyPackageInstallCommand = string.Format(
                    NodeConstants.ProductionOnlyPackageInstallCommandTemplate,
                    YarnInstallCommand),
                CompressedNodeModulesFileName = null,
                CompressNodeModulesCommand = null,
                ConfigureYarnCache = false,
                NodeBuildProperties = new Dictionary<string, string>
                {
                    {"PlatformWithVersion", "nodejs 10.10.10" },
                },
                NodeManifestFileName = FilePaths.BuildCommandsFileName,
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Equal(
                TemplateHelper.Render(TemplateHelper.TemplateResource.NodeBuildSnippet, expected),
                snippet.BashBuildScriptSnippet);
            Assert.True(scriptGenerator.IsCleanRepo(repo));
        }

        [Fact]
        public void GeneratedScript_UsesYarn2InstallAndRunsNpmBuild_IfYarnRCIsPresent()
        {
            // Arrange
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                new BuildScriptGeneratorOptions { PlatformVersion = "8.2.1" },
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithBuildScript, NodeConstants.PackageJsonFileName);
            repo.AddFile("", NodeConstants.YarnLockFileName);
            repo.AddFile("", ".yarnrc.yml");
            var context = CreateScriptGeneratorContext(repo);
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
                HasYarnrcYmlFile = true,
                IsYarnLockFileValidYamlFormat = true,

            };
            var expected = new NodeBashBuildSnippetProperties
            {
                PackageInstallCommand = NodeConstants.Yarn2PackageInstallCommand,
                PackageInstallerVersionCommand = NodeConstants.YarnVersionCommand,
                NpmRunBuildCommand = "yarn run build",
                NpmRunBuildAzureCommand = "yarn run build:azure",
                HasProdDependencies = true,
                HasDevDependencies = true,
                ProductionOnlyPackageInstallCommand = string.Format(
                    NodeConstants.ProductionOnlyPackageInstallCommandTemplate,
                    NodeConstants.Yarn2PackageInstallCommand),
                CompressedNodeModulesFileName = null,
                CompressNodeModulesCommand = null,
                ConfigureYarnCache = false,
                NodeBuildProperties = new Dictionary<string, string>
                {
                    {"PlatformWithVersion", "nodejs 10.10.10" },
                },
                NodeManifestFileName = FilePaths.BuildCommandsFileName,
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Equal(
                TemplateHelper.Render(TemplateHelper.TemplateResource.NodeBuildSnippet, expected),
                snippet.BashBuildScriptSnippet);
            Assert.True(scriptGenerator.IsCleanRepo(repo));
        }

        [Fact]
        public void GeneratedScript_UsesYarn1InstallAndRunsNpmBuild_IfYarnRCFileIsNotPresent()
        {
            // Arrange
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                new BuildScriptGeneratorOptions { PlatformVersion = "8.2.1" },
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithBuildScript, NodeConstants.PackageJsonFileName);
            repo.AddFile("", NodeConstants.YarnLockFileName);
            var context = CreateScriptGeneratorContext(repo);
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
                HasYarnrcYmlFile = false,
                IsYarnLockFileValidYamlFormat = true,
            };
            var expected = new NodeBashBuildSnippetProperties
            {
                PackageInstallCommand = NodeConstants.YarnPackageInstallCommand,
                PackageInstallerVersionCommand = NodeConstants.YarnVersionCommand,
                NpmRunBuildCommand = "yarn run build",
                NpmRunBuildAzureCommand = "yarn run build:azure",
                HasProdDependencies = true,
                HasDevDependencies = true,
                ProductionOnlyPackageInstallCommand = string.Format(
                    NodeConstants.ProductionOnlyPackageInstallCommandTemplate,
                    NodeConstants.YarnPackageInstallCommand),
                CompressedNodeModulesFileName = null,
                CompressNodeModulesCommand = null,
                ConfigureYarnCache = false,
                NodeBuildProperties = new Dictionary<string, string>
                {
                    {"PlatformWithVersion", "nodejs 10.10.10" },
                },
                NodeManifestFileName = FilePaths.BuildCommandsFileName,
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Equal(
                TemplateHelper.Render(TemplateHelper.TemplateResource.NodeBuildSnippet, expected),
                snippet.BashBuildScriptSnippet);
            Assert.True(scriptGenerator.IsCleanRepo(repo));
        }

        [Fact]
        public void GeneratedScript_UsesNpmInstall_IfPackageLockJsonFileIsPresent()
        {
            // Arrange
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                new BuildScriptGeneratorOptions { PlatformVersion = "8.2.1" },
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithNoNpmVersion, NodeConstants.PackageJsonFileName);
            repo.AddFile("Package lock json file content here", NodeConstants.PackageLockJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
            };
            var expected = new NodeBashBuildSnippetProperties
            {
                PackageInstallCommand = NpmInstallCommand,
                PackageInstallerVersionCommand = NodeConstants.NpmVersionCommand,
                NpmRunBuildCommand = null,
                NpmRunBuildAzureCommand = null,
                HasProdDependencies = false,
                ProductionOnlyPackageInstallCommand = string.Format(
                    NodeConstants.ProductionOnlyPackageInstallCommandTemplate,
                    NpmInstallCommand),
                CompressedNodeModulesFileName = null,
                CompressNodeModulesCommand = null,
                NodeBuildProperties = new Dictionary<string, string>
                {
                    {"PlatformWithVersion", "nodejs 10.10.10" },
                },
                NodeManifestFileName = FilePaths.BuildCommandsFileName,
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Equal(
                TemplateHelper.Render(TemplateHelper.TemplateResource.NodeBuildSnippet, expected),
                snippet.BashBuildScriptSnippet);
            Assert.True(scriptGenerator.IsCleanRepo(repo));
        }

        [Fact]
        public void GeneratedScript_UsesNpmRunBuild_IfBuildNodeIsPresentUnderScripts()
        {
            // Arrange
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                new BuildScriptGeneratorOptions { PlatformVersion = "8.2.1" },
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithBuildScript, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
            };
            var expected = new NodeBashBuildSnippetProperties
            {
                PackageInstallCommand = NpmInstallCommand,
                PackageInstallerVersionCommand = NodeConstants.NpmVersionCommand,
                NpmRunBuildCommand = "npm run build",
                NpmRunBuildAzureCommand = "npm run build:azure",
                HasProdDependencies = true,
                HasDevDependencies = true,
                ProductionOnlyPackageInstallCommand = string.Format(
                    NodeConstants.ProductionOnlyPackageInstallCommandTemplate,
                    NpmInstallCommand),
                CompressedNodeModulesFileName = null,
                CompressNodeModulesCommand = null,
                NodeBuildProperties = new Dictionary<string, string>
                {
                    {"PlatformWithVersion", "nodejs 10.10.10" },
                },
                NodeManifestFileName = FilePaths.BuildCommandsFileName,
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Equal(
                TemplateHelper.Render(TemplateHelper.TemplateResource.NodeBuildSnippet, expected),
                snippet.BashBuildScriptSnippet);
            Assert.True(scriptGenerator.IsCleanRepo(repo));
        }

        [Fact]
        public void GeneratedScript_ZipsNodeModules_IfZipNodeProperty_IsTarGz()
        {
            // Arrange
            var commonOptions = new BuildScriptGeneratorOptions();
            commonOptions.PlatformVersion = "8.2.1";
            commonOptions.Properties = new Dictionary<string, string>();
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                commonOptions,
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithBuildScript, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            context.Properties[NodePlatform.CompressNodeModulesPropertyKey] = "tar-gz";
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
            };
            var expected = new NodeBashBuildSnippetProperties
            {
                PackageInstallCommand = NpmInstallCommand,
                PackageInstallerVersionCommand = NodeConstants.NpmVersionCommand,
                NpmRunBuildCommand = "npm run build",
                NpmRunBuildAzureCommand = "npm run build:azure",
                HasProdDependencies = true,
                HasDevDependencies = true,
                ProductionOnlyPackageInstallCommand = string.Format(
                    NodeConstants.ProductionOnlyPackageInstallCommandTemplate,
                    NpmInstallCommand),
                CompressedNodeModulesFileName = "node_modules.tar.gz",
                CompressNodeModulesCommand = "tar -zcf",
                NodeBuildProperties = new Dictionary<string, string>
                {
                    {"PlatformWithVersion", "nodejs 10.10.10" },
                },
                NodeManifestFileName = FilePaths.BuildCommandsFileName,
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Equal(
                TemplateHelper.Render(TemplateHelper.TemplateResource.NodeBuildSnippet, expected),
                snippet.BashBuildScriptSnippet);
            Assert.Contains("echo Zipping existing 'node_modules' folder", snippet.BashBuildScriptSnippet);
            Assert.True(scriptGenerator.IsCleanRepo(repo));
        }

        [Fact]
        public void GeneratedScript_ZipsNodeModules_IfZipNodeProperty_IsNull()
        {
            // Arrange
            var commonOptions = new BuildScriptGeneratorOptions();
            commonOptions.PlatformVersion = "8.2.1";
            commonOptions.Properties = new Dictionary<string, string>();
            commonOptions.Properties[NodePlatform.CompressNodeModulesPropertyKey] = null;
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                commonOptions,
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithBuildScript, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            context.Properties[NodePlatform.CompressNodeModulesPropertyKey] = null;
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
            };
            var expected = new NodeBashBuildSnippetProperties
            {
                PackageInstallCommand = NpmInstallCommand,
                PackageInstallerVersionCommand = NodeConstants.NpmVersionCommand,
                NpmRunBuildCommand = "npm run build",
                NpmRunBuildAzureCommand = "npm run build:azure",
                HasProdDependencies = true,
                HasDevDependencies = true,
                ProductionOnlyPackageInstallCommand = string.Format(
                    NodeConstants.ProductionOnlyPackageInstallCommandTemplate,
                    NpmInstallCommand),
                CompressedNodeModulesFileName = "node_modules.tar.gz",
                CompressNodeModulesCommand = "tar -zcf",
                NodeBuildProperties = new Dictionary<string, string>
                {
                    {"PlatformWithVersion", "nodejs 10.10.10" },
                },
                NodeManifestFileName = FilePaths.BuildCommandsFileName,
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Contains("echo Zipping existing 'node_modules' folder", snippet.BashBuildScriptSnippet);
            Assert.Equal(
                TemplateHelper.Render(TemplateHelper.TemplateResource.NodeBuildSnippet, expected),
                snippet.BashBuildScriptSnippet);
            Assert.True(scriptGenerator.IsCleanRepo(repo));
        }

        [Fact]
        public void GeneratedScript_ZipsNodeModules_IfZipNodeProperty_IsZip()
        {
            // Arrange
            var commonOptions = new BuildScriptGeneratorOptions();
            commonOptions.PlatformVersion = "8.2.1";
            commonOptions.Properties = new Dictionary<string, string>();
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                commonOptions,
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithBuildScript, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            context.Properties[NodePlatform.CompressNodeModulesPropertyKey] = "zip";
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
            };
            var expected = new NodeBashBuildSnippetProperties
            {
                PackageInstallCommand = NpmInstallCommand,
                PackageInstallerVersionCommand = NodeConstants.NpmVersionCommand,
                NpmRunBuildCommand = "npm run build",
                NpmRunBuildAzureCommand = "npm run build:azure",
                HasProdDependencies = true,
                HasDevDependencies = true,
                ProductionOnlyPackageInstallCommand = string.Format(
                    NodeConstants.ProductionOnlyPackageInstallCommandTemplate,
                    NpmInstallCommand),
                CompressedNodeModulesFileName = "node_modules.zip",
                CompressNodeModulesCommand = "zip -y -q -r",
                NodeBuildProperties = new Dictionary<string, string>
                {
                    {"PlatformWithVersion", "nodejs 10.10.10" },
                },
                NodeManifestFileName = FilePaths.BuildCommandsFileName,
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Contains("echo Zipping existing 'node_modules' folder", snippet.BashBuildScriptSnippet);
            Assert.Equal(
                TemplateHelper.Render(TemplateHelper.TemplateResource.NodeBuildSnippet, expected),
                snippet.BashBuildScriptSnippet);
            Assert.True(scriptGenerator.IsCleanRepo(repo));
        }

        [Fact]
        public void GeneratedScript_DoesNotZipNodeModules_IfZipNodeModulesEnvironmentVariable_False()
        {
            // Arrange
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                new BuildScriptGeneratorOptions { PlatformVersion = "8.2.1" },
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithBuildScript, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
            };
            
            //var commandManifestProperties = new Dictionary<string, string>
            //{
            //    {"PlatformWithVersion", "10.10.10" },
            // };
            var expected = new NodeBashBuildSnippetProperties
            {
                PackageInstallCommand = NpmInstallCommand,
                PackageInstallerVersionCommand = NodeConstants.NpmVersionCommand,
                NpmRunBuildCommand = "npm run build",
                NpmRunBuildAzureCommand = "npm run build:azure",
                HasProdDependencies = true,
                HasDevDependencies = true,
                ProductionOnlyPackageInstallCommand = string.Format(
                    NodeConstants.ProductionOnlyPackageInstallCommandTemplate,
                    NpmInstallCommand),
                CompressedNodeModulesFileName = null,
                CompressNodeModulesCommand = null,
                NodeBuildProperties = new Dictionary<string, string>
                {
                    {"PlatformWithVersion", "nodejs 10.10.10" },
                },
                NodeManifestFileName = FilePaths.BuildCommandsFileName,
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Equal(
                TemplateHelper.Render(TemplateHelper.TemplateResource.NodeBuildSnippet, expected),
                snippet.BashBuildScriptSnippet);
            Assert.True(scriptGenerator.IsCleanRepo(repo));
        }

        [Fact]
        public void GeneratedScript_WritesNpmRc_WithCustomRegistry()
        {
            // Arrange
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                new BuildScriptGeneratorOptions { PlatformVersion = "8.2.1" },
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithNpmVersion, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            context.Properties[NodePlatform.RegistryUrlPropertyKey] = "https://example.com/registry/";
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Contains(
                $"echo \"registry={context.Properties[NodePlatform.RegistryUrlPropertyKey]}\" >> ~/.npmrc",
                snippet.BashBuildScriptSnippet);
        }

        [Fact]
        public void GeneratedScript_RunsNpmPackCommand_WithinCustomPackageDirectory()
        {
            // Arrange
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                new BuildScriptGeneratorOptions { PlatformVersion = "8.2.1", ShouldPackage = true },
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithNpmVersion, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            context.Properties[NodePlatform.PackageDirectoryPropertyKey] = "packages/app1";
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Contains("Switching to package directory provided: " +
                $"'{context.Properties[NodePlatform.PackageDirectoryPropertyKey]}'...",
                snippet.BashBuildScriptSnippet);
            Assert.Contains("cd \"$SOURCE_DIR/$packageDirName\"",
                snippet.BashBuildScriptSnippet);
        }

        [Fact]
        public void GeneratedScript_RunsNpmPackCommand_ExitWhenPackageDirectoryDoesNotExist()
        {
            // Arrange
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                new BuildScriptGeneratorOptions { PlatformVersion = "8.2.1", ShouldPackage = true },
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithNpmVersion, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            context.Properties[NodePlatform.PackageDirectoryPropertyKey] = "packages/random";
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Contains("Switching to package directory provided: " +
                $"'{context.Properties[NodePlatform.PackageDirectoryPropertyKey]}'...",
                snippet.BashBuildScriptSnippet);
            Assert.Contains($"Package directory '$SOURCE_DIR/$packageDirName' does not exist.",
                snippet.BashBuildScriptSnippet);
        }


        [Fact]
        public void GeneratedScript_Contains_BuildCommands_CommandManifestFile()
        {
            // Arrange
            var scriptGenerator = GetNodePlatform(
                defaultNodeVersion: NodeVersions.Node12Version,
                new BuildScriptGeneratorOptions { PlatformVersion = "12.19.1" },
                new NodeScriptGeneratorOptions());
            var repo = new MemorySourceRepo();
            repo.AddFile(PackageJsonWithBuildScript, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            var detectorResult = new NodePlatformDetectorResult
            {
                Platform = NodeConstants.PlatformName,
                PlatformVersion = "10.10.10",
            };
            
            var commandManifestProperties = new Dictionary<string, string>();
            commandManifestProperties["PlatformWithVersion"] = "nodejs 10.10.10";

            var expected = new NodeBashBuildSnippetProperties
            {
                PackageInstallCommand = NpmInstallCommand,
                PackageInstallerVersionCommand = NodeConstants.NpmVersionCommand,
                NpmRunBuildCommand = "npm run build",
                NpmRunBuildAzureCommand = "npm run build:azure",
                HasProdDependencies = true,
                HasDevDependencies = true,
                ProductionOnlyPackageInstallCommand = string.Format(
                    NodeConstants.ProductionOnlyPackageInstallCommandTemplate,
                    NpmInstallCommand),
                CompressedNodeModulesFileName = null,
                CompressNodeModulesCommand = null,
                NodeBuildProperties = commandManifestProperties,
                NodeManifestFileName = FilePaths.BuildCommandsFileName,
            };

            // Act
            var snippet = scriptGenerator.GenerateBashBuildScriptSnippet(context, detectorResult);

            // Assert
            Assert.NotNull(snippet);
            Assert.Equal(
                TemplateHelper.Render(TemplateHelper.TemplateResource.NodeBuildSnippet, expected),
                snippet.BashBuildScriptSnippet);
            Assert.Contains("Node Command Manifest file created.", snippet.BashBuildScriptSnippet);
            Assert.True(scriptGenerator.IsCleanRepo(repo));
        }

        private static IProgrammingPlatform GetNodePlatform(
            string defaultNodeVersion,
            BuildScriptGeneratorOptions commonOptions,
            NodeScriptGeneratorOptions nodeScriptGeneratorOptions)
        {
            var nodeVersionProvider = new TestNodeVersionProvider(
                new[] { "6.11.0", NodeVersions.Node8Version, NodeVersions.Node10Version, NodeVersions.Node12Version },
                defaultVersion: defaultNodeVersion);

            nodeScriptGeneratorOptions = nodeScriptGeneratorOptions ?? new NodeScriptGeneratorOptions();
            commonOptions = commonOptions ?? new BuildScriptGeneratorOptions();
            return new NodePlatform(
                Options.Create(commonOptions),
                Options.Create(nodeScriptGeneratorOptions),
                nodeVersionProvider,
                NullLogger<NodePlatform>.Instance,
                detector: null,
                new TestEnvironment(),
                new NodePlatformInstaller(Options.Create(commonOptions), NullLoggerFactory.Instance));
        }

        private static BuildScriptGeneratorContext CreateScriptGeneratorContext(ISourceRepo sourceRepo)
        {
            return new BuildScriptGeneratorContext
            {
                SourceRepo = sourceRepo,
                Properties = new Dictionary<string, string>()
            };
        }

        private class TestNodeVersionProvider : INodeVersionProvider
        {
            private readonly string[] _supportedNodeVersions;
            private readonly string _defaultVersion;

            public TestNodeVersionProvider(string[] supportedNodeVersions, string defaultVersion)
            {
                _supportedNodeVersions = supportedNodeVersions;
                _defaultVersion = defaultVersion;
            }

            public PlatformVersionInfo GetVersionInfo()
            {
                return PlatformVersionInfo.CreateOnDiskVersionInfo(_supportedNodeVersions, _defaultVersion);
            }
        }
    }
}