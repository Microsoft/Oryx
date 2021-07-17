// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Oryx.BuildScriptGenerator.Exceptions;
using Microsoft.Oryx.Common.Extensions;
using Microsoft.Oryx.Detector;
using Microsoft.Oryx.Detector.DotNetCore;

namespace Microsoft.Oryx.BuildScriptGenerator.DotNetCore
{
    /// <summary>
    /// .NET Core platform.
    /// </summary>
    [BuildProperty(
        DotNetCoreConstants.ProjectBuildPropertyKey,
        DotNetCoreConstants.ProjectBuildPropertyKeyDocumentation)]
    internal class DotNetCorePlatform : IProgrammingPlatform
    {
        private readonly IDotNetCoreVersionProvider _versionProvider;
        private readonly ILogger<DotNetCorePlatform> _logger;
        private readonly IDotNetCorePlatformDetector _detector;
        private readonly DotNetCoreScriptGeneratorOptions _dotNetCoreScriptGeneratorOptions;
        private readonly BuildScriptGeneratorOptions _commonOptions;
        private readonly DotNetCorePlatformInstaller _platformInstaller;
        private readonly GlobalJsonSdkResolver _globalJsonSdkResolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetCorePlatform"/> class.
        /// </summary>
        /// <param name="versionProvider">The .NET version provider.</param>
        /// <param name="logger">The logger of .NET platform.</param>
        /// <param name="detector">The detector of .NET platform.</param>
        /// <param name="commonOptions">The build options for BuildScriptGenerator.</param>
        /// <param name="dotNetCoreScriptGeneratorOptions">The options if .NET platform.</param>
        /// <param name="platformInstaller">The <see cref="DotNetCorePlatformInstaller"/>.</param>
        /// <param name="globalJsonSdkResolver">The <see cref="GlobalJsonSdkResolver"/>.</param>
        public DotNetCorePlatform(
            IDotNetCoreVersionProvider versionProvider,
            ILogger<DotNetCorePlatform> logger,
            IDotNetCorePlatformDetector detector,
            IOptions<BuildScriptGeneratorOptions> commonOptions,
            IOptions<DotNetCoreScriptGeneratorOptions> dotNetCoreScriptGeneratorOptions,
            DotNetCorePlatformInstaller platformInstaller,
            GlobalJsonSdkResolver globalJsonSdkResolver)
        {
            _versionProvider = versionProvider;
            _logger = logger;
            _detector = detector;
            _dotNetCoreScriptGeneratorOptions = dotNetCoreScriptGeneratorOptions.Value;
            _commonOptions = commonOptions.Value;
            _platformInstaller = platformInstaller;
            _globalJsonSdkResolver = globalJsonSdkResolver;
        }

        /// <inheritdoc/>
        public string Name => DotNetCoreConstants.PlatformName;

        /// <inheritdoc/>
        public IEnumerable<string> SupportedVersions
        {
            get
            {
                var versionMap = _versionProvider.GetSupportedVersions();

                // Map is from runtime version => sdk version
                return versionMap.Keys;
            }
        }

        /// <inheritdoc/>
        public PlatformDetectorResult Detect(RepositoryContext context)
        {
            try
            {
                var detectionResult = _detector.Detect(new DetectorContext
                {
                    SourceRepo = new Detector.LocalSourceRepo(context.SourceRepo.RootPath),
                });
                if (detectionResult == null)
                {
                    return null;
                }

                ResolveVersions(context, detectionResult);
                return detectionResult;
            }
            catch (InvalidProjectFileException e)
            {
                _logger.LogError(e, "Error occurred while trying to detect for .Net Core application(s)");
                throw new InvalidUsageException(e.Message);
            }
        }

        /// <inheritdoc/>
        public BuildScriptSnippet GenerateBashBuildScriptSnippet(
            BuildScriptGeneratorContext context,
            PlatformDetectorResult detectorResult)
        {
            var dotNetCorePlatformDetectorResult = detectorResult as DotNetCorePlatformDetectorResult;
            if (dotNetCorePlatformDetectorResult == null)
            {
                throw new ArgumentException(
                    $"Expected '{nameof(detectorResult)}' argument to be of type " +
                    $"'{typeof(DotNetCorePlatformDetectorResult)}' but got '{detectorResult.GetType()}'.");
            }

            var manifestFileProperties = new Dictionary<string, string>();
            manifestFileProperties[ManifestFilePropertyKeys.OperationId] = context.OperationId;
            manifestFileProperties[ManifestFilePropertyKeys.DotNetCoreRuntimeVersion]
                = dotNetCorePlatformDetectorResult.PlatformVersion;
            manifestFileProperties[ManifestFilePropertyKeys.DotNetCoreSdkVersion]
                = dotNetCorePlatformDetectorResult.SdkVersion;
            manifestFileProperties[ManifestFilePropertyKeys.OutputType] = dotNetCorePlatformDetectorResult.OutputType;

            var projectFile = dotNetCorePlatformDetectorResult.ProjectFile;
            if (string.IsNullOrEmpty(projectFile))
            {
                return null;
            }

            var templateProperties = new DotNetCoreBashBuildSnippetProperties
            {
                ProjectFile = projectFile,
                Configuration = GetBuildConfiguration(),
            };

            var script = TemplateHelper.Render(
                TemplateHelper.TemplateResource.DotNetCoreSnippet,
                templateProperties,
                _logger);

            SetStartupFileNameInfoInManifestFile(context, projectFile, manifestFileProperties);

            return new BuildScriptSnippet
            {
                BashBuildScriptSnippet = script,
                BuildProperties = manifestFileProperties,

                // Setting this to false to avoid copying files like '.cs' to the destination
                CopySourceDirectoryContentToDestinationDirectory = false,
            };
        }

        /// <inheritdoc/>
        public bool IsCleanRepo(ISourceRepo repo)
        {
            return true;
        }

        /// <inheritdoc/>
        public string GenerateBashRunTimeInstallationScript(RunTimeInstallationScriptGeneratorOptions options)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public bool IsEnabled(RepositoryContext ctx)
        {
            return _commonOptions.EnableDotNetCoreBuild;
        }

        /// <inheritdoc/>
        public bool IsEnabledForMultiPlatformBuild(RepositoryContext ctx)
        {
            return true;
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetDirectoriesToExcludeFromCopyToBuildOutputDir(
            BuildScriptGeneratorContext scriptGeneratorContext)
        {
            var dirs = new List<string>();
            dirs.Add("obj");
            dirs.Add("bin");
            return dirs;
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetDirectoriesToExcludeFromCopyToIntermediateDir(
            BuildScriptGeneratorContext scriptGeneratorContext)
        {
            var dirs = new List<string>();
            dirs.Add(".git");
            dirs.Add("obj");
            dirs.Add("bin");
            return dirs;
        }

        /// <inheritdoc/>
        public string GetInstallerScriptSnippet(
            BuildScriptGeneratorContext context,
            PlatformDetectorResult detectorResult)
        {
            var dotNetCorePlatformDetectorResult = detectorResult as DotNetCorePlatformDetectorResult;
            if (dotNetCorePlatformDetectorResult == null)
            {
                throw new ArgumentException(
                    $"Expected '{nameof(detectorResult)}' argument to be of type " +
                    $"'{typeof(DotNetCorePlatformDetectorResult)}' but got '{detectorResult.GetType()}'.");
            }

            string installationScriptSnippet = null;
            if (_commonOptions.EnableDynamicInstall)
            {
                _logger.LogDebug("Dynamic install is enabled.");

                if (_platformInstaller.IsVersionAlreadyInstalled(dotNetCorePlatformDetectorResult.SdkVersion))
                {
                    _logger.LogDebug(
                        "DotNetCore SDK version {globalJsonSdkVersion} is already installed. " +
                        "So skipping installing it again.",
                        dotNetCorePlatformDetectorResult.SdkVersion);
                }
                else
                {
                    _logger.LogDebug(
                        "DotNetCore SDK version {globalJsonSdkVersion} is not installed. " +
                        "So generating an installation script snippet for it.",
                        dotNetCorePlatformDetectorResult.SdkVersion);

                    installationScriptSnippet = _platformInstaller.GetInstallerScriptSnippet(
                        dotNetCorePlatformDetectorResult.SdkVersion);
                }
            }
            else
            {
                _logger.LogDebug("Dynamic install is not enabled.");
            }

            return installationScriptSnippet;
        }

        /// <inheritdoc/>
        public void ResolveVersions(RepositoryContext context, PlatformDetectorResult detectorResult)
        {
            var dotNetCorePlatformDetectorResult = detectorResult as DotNetCorePlatformDetectorResult;
            if (dotNetCorePlatformDetectorResult == null)
            {
                throw new ArgumentException(
                    $"Expected '{nameof(detectorResult)}' argument to be of type " +
                    $"'{typeof(DotNetCorePlatformDetectorResult)}' but got '{detectorResult.GetType()}'.");
            }

            // Get runtime version
            var resolvedRuntimeVersion = GetRuntimeVersionUsingHierarchicalRules(
                dotNetCorePlatformDetectorResult.PlatformVersion);
            resolvedRuntimeVersion = GetMaxSatisfyingRuntimeVersionAndVerify(resolvedRuntimeVersion);
            dotNetCorePlatformDetectorResult.PlatformVersion = resolvedRuntimeVersion;

            var versionMap = _versionProvider.GetSupportedVersions();
            var sdkVersion = GetSdkVersion(context, dotNetCorePlatformDetectorResult.PlatformVersion, versionMap);
            dotNetCorePlatformDetectorResult.SdkVersion = sdkVersion;
        }

        /// <inheritdoc/>
        public IDictionary<string, string> GetToolsToBeSetInPath(
            RepositoryContext context,
            PlatformDetectorResult detectorResult)
        {
            var dotNetCorePlatformDetectorResult = detectorResult as DotNetCorePlatformDetectorResult;
            if (dotNetCorePlatformDetectorResult == null)
            {
                throw new ArgumentException(
                    $"Expected '{nameof(detectorResult)}' argument to be of type " +
                    $"'{typeof(DotNetCorePlatformDetectorResult)}' but got '{detectorResult.GetType()}'.");
            }

            var tools = new Dictionary<string, string>();
            tools[DotNetCoreConstants.PlatformName] = dotNetCorePlatformDetectorResult.SdkVersion;
            return tools;
        }

        private string GetSdkVersion(
            RepositoryContext context,
            string runtimeVersion,
            Dictionary<string, string> versionMap)
        {
            if (_commonOptions.EnableDynamicInstall
                && context.SourceRepo.FileExists(DotNetCoreConstants.GlobalJsonFileName))
            {
                var availableSdks = versionMap.Values;
                var globalJsonSdkVersion = _globalJsonSdkResolver.GetSatisfyingSdkVersion(
                    context.SourceRepo,
                    runtimeVersion,
                    availableSdks);
                return globalJsonSdkVersion;
            }

            return versionMap[runtimeVersion];
        }

        private string GetBuildConfiguration()
        {
            var configuration = _dotNetCoreScriptGeneratorOptions.MSBuildConfiguration;
            if (string.IsNullOrEmpty(configuration))
            {
                configuration = DotNetCoreConstants.DefaultMSBuildConfiguration;
            }

            return configuration;
        }

        /// <summary>
        /// Even though the runtime container has the logic of finding out the startup file based on
        /// 'runtimeconfig.json' prefix, we still set the name in the manifest file because of the following
        /// scenario: let's say output directory currently has 'foo.dll' and user made a change to the project
        /// name or assembly name property to 'bar' which causes 'bar.dll' to be published. If the output
        /// directory was NOT cleaned, then we would now be having both 'foo.runtimeconfig.json' and
        /// 'bar.runtimeconfig.json' which causes a problem for runtime container as it cannot figure out the
        /// right startup DLL. So, to help that scenario we always set the start-up file name in manifest file.
        /// The runtime container will first look into manifest file to find the startup filename, if the
        /// file name is not present or if a manifest file is not present at all(ex: in case of VS Publish where
        /// the build does not happen with Oryx), then the runtime container's logic will fallback to looking at
        /// runtimeconfig.json prefixes.
        /// </summary>
        private void SetStartupFileNameInfoInManifestFile(
            BuildScriptGeneratorContext context,
            string projectFile,
            IDictionary<string, string> buildProperties)
        {
            string startupDllFileName;
            var projectFileContent = context.SourceRepo.ReadFile(projectFile);
            var projFileDoc = XDocument.Load(new StringReader(projectFileContent));
            var assemblyNameElement = projFileDoc.XPathSelectElement(DotNetCoreConstants.AssemblyNameXPathExpression);
            if (assemblyNameElement == null)
            {
                var name = Path.GetFileNameWithoutExtension(projectFile);
                startupDllFileName = $"{name}.dll";
            }
            else
            {
                startupDllFileName = $"{assemblyNameElement.Value}.dll";
            }

            buildProperties[DotNetCoreManifestFilePropertyKeys.StartupDllFileName] = startupDllFileName;
        }

        private string GetMaxSatisfyingRuntimeVersionAndVerify(string runtimeVersion)
        {
            var versionMap = _versionProvider.GetSupportedVersions();

            // Since our semantic versioning library does not work with .NET Core preview version format, here
            // we do some trivial way of finding the latest version which matches a given runtime version
            // Runtime versions are usually like: 1.0, 2.1, 3.1, 5.0 etc.
            // (these are constructed from netcoreapp21, netcoreapp31 etc.)
            // Preview version of sdks also have preview versions of runtime versions and hence they
            // have '-' in their names.
            var nonPreviewRuntimeVersions = versionMap.Keys.Where(version => version.IndexOf("-") < 0);
            var maxSatisfyingVersion = SemanticVersionResolver.GetMaxSatisfyingVersion(
                runtimeVersion,
                nonPreviewRuntimeVersions);

            // Check if a preview version is available
            if (string.IsNullOrEmpty(maxSatisfyingVersion))
            {
                // NOTE:
                // Preview versions: 5.0.0-preview.3.20214.6, 5.0.0-preview.2.20160.6, 5.0.0-preview.1.20120.5
                var previewRuntimeVersions = versionMap.Keys
                    .Where(version => version.IndexOf("-") >= 0)
                    .Where(version => version.StartsWith(runtimeVersion))
                    .OrderByDescending(version => version);
                if (previewRuntimeVersions.Any())
                {
                    maxSatisfyingVersion = previewRuntimeVersions.First();
                }
            }

            if (string.IsNullOrEmpty(maxSatisfyingVersion))
            {
                var exception = new UnsupportedVersionException(
                    DotNetCoreConstants.PlatformName,
                    runtimeVersion,
                    versionMap.Keys);
                _logger.LogError(
                    exception,
                    $"Exception caught, the version '{runtimeVersion}' is not supported for the .NET Core platform.");
                throw exception;
            }

            return maxSatisfyingVersion;
        }

        private string GetRuntimeVersionUsingHierarchicalRules(string detectedVersion)
        {
            // Explicitly specified version by user wins over detected version
            if (!string.IsNullOrEmpty(_dotNetCoreScriptGeneratorOptions.DotNetCoreRuntimeVersion))
            {
                return _dotNetCoreScriptGeneratorOptions.DotNetCoreRuntimeVersion;
            }

            // If a version was detected, then use it.
            if (!string.IsNullOrEmpty(detectedVersion))
            {
                return detectedVersion;
            }

            // Fallback to default version
            var defaultVersion = _versionProvider.GetDefaultRuntimeVersion();
            return defaultVersion;
        }

        private bool TryGetExplicitVersion(out string explicitVersion)
        {
            explicitVersion = null;

            var platformName = _commonOptions.PlatformName;
            if (platformName.EqualsIgnoreCase(DotNetCoreConstants.PlatformName))
            {
                if (string.IsNullOrWhiteSpace(_dotNetCoreScriptGeneratorOptions.DotNetCoreRuntimeVersion))
                {
                    return false;
                }

                explicitVersion = _dotNetCoreScriptGeneratorOptions.DotNetCoreRuntimeVersion;
                return true;
            }

            return false;
        }
    }
}