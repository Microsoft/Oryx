// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Oryx.BuildScriptGenerator.DotNetCore;
using Microsoft.Oryx.Detector.DotNetCore;
using Xunit;
using System.Collections.Generic;

namespace Microsoft.Oryx.BuildScriptGenerator.Tests.DotNetCore
{
    public class DotNetCoreBuildScriptGenerationTest
    {
        
        private const string ProjectFileAzureBlazorWasmClientWithTargetFramework6 = @"
        <Project Sdk=""Microsoft.NET.Sdk.BlazorWebAssembly"">
          <PropertyGroup>
            <TargetFramework>net6.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include=""Microsoft.AspNetCore.Components.WebAssembly"" Version=""6.0.0-preview.3.21201.13"" />
            <PackageReference Include=""Microsoft.AspNetCore.Components.WebAssembly.DevServer"" Version=""6.0.0-preview.3.21201.13"" PrivateAssets=""all"" />
          </ItemGroup>
        </Project>
        ";

        [Fact]
        public void GeneratedBuildSnippet_AOTCompilationInstallCommandWillExecute_WhenBlazorWasmDotNet6App()
        {
            // Arrange
            var installationScript = "test-script";
            var dotNetCorePlatform = CreateDotNetCorePlatform(
                isDotNetCoreVersionAlreadyInstalled: true,
                dotNetCoreInstallationScript: installationScript);
            var repo = new MemorySourceRepo();
            repo.AddFile(ProjectFileAzureBlazorWasmClientWithTargetFramework6, "test.csproj");
            var context = CreateContext(repo);
            var detectedResult = new DotNetCorePlatformDetectorResult
            {
                Platform = DotNetCoreConstants.PlatformName,
                PlatformVersion = "6.0",
                InstallAOTWorkloads = true,
                ProjectFile = "test.csproj",
            };

            // Act
            var buildScriptSnippet = dotNetCorePlatform.GenerateBashBuildScriptSnippet(context, detectedResult);

            // Assert
            Assert.NotNull(buildScriptSnippet);
            Assert.Contains(DotNetCoreConstants.InstallBlazorWebAssemblyAOTWorkloadCommand,
                            buildScriptSnippet.BashBuildScriptSnippet);
        }

        private BuildScriptGeneratorContext CreateContext(ISourceRepo sourceRepo = null)
        {
            sourceRepo = sourceRepo ?? new MemorySourceRepo();

            return new BuildScriptGeneratorContext
            {
                SourceRepo = sourceRepo,
            };
        }

        private DotNetCorePlatform CreateDotNetCorePlatform(
            Dictionary<string, string> supportedDotNetCoreVersions = null,
            string defaultVersion = null,
            string detectedVersion = null,
            BuildScriptGeneratorOptions commonOptions = null,
            DotNetCoreScriptGeneratorOptions DotNetCoreScriptGeneratorOptions = null,
            bool? isDotNetCoreVersionAlreadyInstalled = null,
            string DotNetCoreInstallationScript = null,
            string dotNetCoreInstallationScript = null)
        {
            commonOptions = commonOptions ?? new BuildScriptGeneratorOptions();
            DotNetCoreScriptGeneratorOptions = DotNetCoreScriptGeneratorOptions ?? new DotNetCoreScriptGeneratorOptions();
            isDotNetCoreVersionAlreadyInstalled = isDotNetCoreVersionAlreadyInstalled ?? true;
            DotNetCoreInstallationScript = DotNetCoreInstallationScript ?? "default-DotNetCore-installation-script";
            var versionProvider = new TestDotNetCoreVersionProvider(supportedDotNetCoreVersions, defaultVersion);
            var detector = new TestDotNetCorePlatformDetector(detectedVersion: detectedVersion);
            var DotNetCoreInstaller = new TestDotNetCorePlatformInstaller(
                Options.Create(commonOptions),
                isDotNetCoreVersionAlreadyInstalled.Value,
                DotNetCoreInstallationScript);
            var globalJsonSdkResolver = new GlobalJsonSdkResolver(NullLogger<GlobalJsonSdkResolver>.Instance);
            return new TestDotNetCorePlatform(
                Options.Create(DotNetCoreScriptGeneratorOptions),
                Options.Create(commonOptions),
                versionProvider,
                NullLogger<TestDotNetCorePlatform>.Instance,
                detector,
                DotNetCoreInstaller,
                globalJsonSdkResolver);
        }

        private class TestDotNetCorePlatform : DotNetCorePlatform
        {
            public TestDotNetCorePlatform(
                IOptions<DotNetCoreScriptGeneratorOptions> DotNetCoreScriptGeneratorOptions,
                IOptions<BuildScriptGeneratorOptions> commonOptions,
                IDotNetCoreVersionProvider DotNetCoreVersionProvider,
                ILogger<DotNetCorePlatform> logger,
                IDotNetCorePlatformDetector detector,
                DotNetCorePlatformInstaller DotNetCoreInstaller,
                GlobalJsonSdkResolver globalJsonSdkResolver)
                : base(
                      DotNetCoreVersionProvider,
                      logger,
                      detector,
                      commonOptions,
                      DotNetCoreScriptGeneratorOptions,
                      DotNetCoreInstaller,
                      globalJsonSdkResolver)
            {
            }
        }

        private class TestDotNetCorePlatformInstaller : DotNetCorePlatformInstaller
        {
            private readonly bool _isVersionAlreadyInstalled;
            private readonly string _installerScript;

            public TestDotNetCorePlatformInstaller(
                IOptions<BuildScriptGeneratorOptions> commonOptions,
                bool isVersionAlreadyInstalled,
                string installerScript)
                : base(commonOptions, NullLoggerFactory.Instance)
            {
                _isVersionAlreadyInstalled = isVersionAlreadyInstalled;
                _installerScript = installerScript;
            }

            public override bool IsVersionAlreadyInstalled(string version)
            {
                return _isVersionAlreadyInstalled;
            }

            public override string GetInstallerScriptSnippet(string version)
            {
                return _installerScript;
            }
        }
    }
}