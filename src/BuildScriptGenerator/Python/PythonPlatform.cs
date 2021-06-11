// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Oryx.BuildScriptGenerator.Exceptions;
using Microsoft.Oryx.Common.Extensions;
using Microsoft.Oryx.Detector;
using Microsoft.Oryx.Detector.Python;

namespace Microsoft.Oryx.BuildScriptGenerator.Python
{
    /// <summary>
    /// Python Platform.
    /// </summary>
    [BuildProperty(
        VirtualEnvironmentNamePropertyKey,
        "Name of the virtual environment to be created. Defaults to 'pythonenv<Python version>'.")]
    [BuildProperty(
        CompressVirtualEnvPropertyKey,
        "Indicates how and if virtual environment folder should be compressed into a single file in the output " +
        "folder. Options are '" + ZipOption + "', and '" + TarGzOption + "'. Default is to not compress. " +
        "If this option is used, when running the app the virtual environment folder must be extracted from " +
        "this file.")]
    [BuildProperty(
        TargetPackageDirectoryPropertyKey,
        "If provided, packages will be downloaded to the given directory instead of to a virtual environment.")]
    [BuildProperty(
        PythonPackageWheelPropertyKey,
        "If provided, package wheels will be built with universal flag. For example," +
        "folder. Option is '" + UniversalWheel + ". Default is to not Non universal wheel. " +
        "For example, when this property is enabled, wheel build command will be " +
        "'python setup.py bdist_wheel --universal'")]
    internal class PythonPlatform : IProgrammingPlatform
    {
        /// <summary>
        /// The name of virtual environment.
        /// </summary>
        internal const string VirtualEnvironmentNamePropertyKey = "virtualenv_name";

        /// <summary>
        /// The target package directory.
        /// </summary>
        internal const string TargetPackageDirectoryPropertyKey = "packagedir";

        /// <summary>
        /// The compress virtual environment.
        /// </summary>
        internal const string CompressVirtualEnvPropertyKey = "compress_virtualenv";

        /// <summary>
        /// The Package Wheel Property.
        /// </summary>
        internal const string PythonPackageWheelPropertyKey = "packagewheel";

        /// <summary>
        /// The zip option.
        /// </summary>
        internal const string ZipOption = "zip";

        /// <summary>
        /// The tar-gz option.
        /// </summary>
        internal const string TarGzOption = "tar-gz";

        /// <summary>
        /// The Universal Wheel option.
        /// </summary>
        internal const string UniversalWheel = "universal";

        private readonly BuildScriptGeneratorOptions _commonOptions;
        private readonly PythonScriptGeneratorOptions _pythonScriptGeneratorOptions;
        private readonly IPythonVersionProvider _versionProvider;
        private readonly ILogger<PythonPlatform> _logger;
        private readonly IPythonPlatformDetector _detector;
        private readonly PythonPlatformInstaller _platformInstaller;

        /// <summary>
        /// Initializes a new instance of the <see cref="PythonPlatform"/> class.
        /// </summary>
        /// <param name="commonOptions">The <see cref="BuildScriptGeneratorOptions"/>.</param>
        /// <param name="pythonScriptGeneratorOptions">The <see cref="PythonScriptGeneratorOptions"/>.</param>
        /// <param name="versionProvider">The Python version provider.</param>
        /// <param name="logger">The logger of Python platform.</param>
        /// <param name="detector">The detector of Python platform.</param>
        /// <param name="platformInstaller">The <see cref="PythonPlatformInstaller"/>.</param>
        public PythonPlatform(
            IOptions<BuildScriptGeneratorOptions> commonOptions,
            IOptions<PythonScriptGeneratorOptions> pythonScriptGeneratorOptions,
            IPythonVersionProvider versionProvider,
            ILogger<PythonPlatform> logger,
            IPythonPlatformDetector detector,
            PythonPlatformInstaller platformInstaller)
        {
            _commonOptions = commonOptions.Value;
            _pythonScriptGeneratorOptions = pythonScriptGeneratorOptions.Value;
            _versionProvider = versionProvider;
            _logger = logger;
            _detector = detector;
            _platformInstaller = platformInstaller;
        }

        /// <inheritdoc/>
        public string Name => PythonConstants.PlatformName;

        public IEnumerable<string> SupportedVersions
        {
            get
            {
                var versionInfo = _versionProvider.GetVersionInfo();
                return versionInfo.SupportedVersions;
            }
        }

        /// <inheritdoc/>
        public PlatformDetectorResult Detect(RepositoryContext context)
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

        /// <inheritdoc/>
        public BuildScriptSnippet GenerateBashBuildScriptSnippet(
            BuildScriptGeneratorContext context,
            PlatformDetectorResult detectorResult)
        {
            var pythonPlatformDetectorResult = detectorResult as PythonPlatformDetectorResult;
            if (pythonPlatformDetectorResult == null)
            {
                throw new ArgumentException(
                    $"Expected '{nameof(detectorResult)}' argument to be of type " +
                    $"'{typeof(PythonPlatformDetectorResult)}' but got '{detectorResult.GetType()}'.");
            }

            if (IsCondaEnvironment(pythonPlatformDetectorResult))
            {
                return GetBuildScriptSnippetForConda(context, pythonPlatformDetectorResult);
            }

            var manifestFileProperties = new Dictionary<string, string>();

            // Write the platform name and version to the manifest file
            manifestFileProperties[ManifestFilePropertyKeys.PythonVersion] = pythonPlatformDetectorResult.PlatformVersion;

            var packageDir = GetPackageDirectory(context);
            var virtualEnvName = GetVirtualEnvironmentName(context);
            var isPythonPackageCommandEnabled = _commonOptions.ShouldPackage;
            var pythonPackageWheelType = GetPythonPackageWheelType(context);
            var manifestDirPath = context.ManifestDir;

            if (!isPythonPackageCommandEnabled && !string.IsNullOrWhiteSpace(pythonPackageWheelType))
            {
                throw new InvalidUsageException($"Option '{PythonPackageWheelPropertyKey}' can't exist" +
                    $"without package command being enabled. Please provide --package along with wheel type");
            }

            if (isPythonPackageCommandEnabled &&
                !string.IsNullOrWhiteSpace(pythonPackageWheelType))
            {
                if (!string.Equals(pythonPackageWheelType.ToLower(), "universal"))
                {
                    throw new InvalidUsageException($"Option '{PythonPackageWheelPropertyKey}' can only have 'universal' as value.'");
                }

                manifestFileProperties[PythonManifestFilePropertyKeys.PackageWheel] = pythonPackageWheelType;
            }

            if (!string.IsNullOrWhiteSpace(packageDir) && !string.IsNullOrWhiteSpace(virtualEnvName))
            {
                throw new InvalidUsageException($"Options '{TargetPackageDirectoryPropertyKey}' and " +
                    $"'{VirtualEnvironmentNamePropertyKey}' are mutually exclusive. Please provide " +
                    $"only the target package directory or virtual environment name.");
            }

            if (string.IsNullOrWhiteSpace(packageDir))
            {
                // If the package directory was not provided, we default to virtual envs
                if (string.IsNullOrWhiteSpace(virtualEnvName))
                {
                    virtualEnvName = GetDefaultVirtualEnvName(pythonPlatformDetectorResult);
                }

                manifestFileProperties[PythonManifestFilePropertyKeys.VirtualEnvName] = virtualEnvName;
            }
            else
            {
                manifestFileProperties[PythonManifestFilePropertyKeys.PackageDir] = packageDir;
            }

            var virtualEnvModule = string.Empty;
            var virtualEnvParams = string.Empty;

            var pythonVersion = pythonPlatformDetectorResult.PlatformVersion;
            _logger.LogDebug("Selected Python version: {pyVer}", pythonVersion);

            if (!string.IsNullOrEmpty(pythonVersion) && !string.IsNullOrWhiteSpace(virtualEnvName))
            {
                (virtualEnvModule, virtualEnvParams) = GetVirtualEnvModules(pythonVersion);

                _logger.LogDebug(
                    "Using virtual environment {venv}, module {venvModule}",
                    virtualEnvName,
                    virtualEnvModule);
            }

            GetVirtualEnvPackOptions(
                context,
                virtualEnvName,
                out var compressVirtualEnvCommand,
                out var compressedVirtualEnvFileName);

            if (!string.IsNullOrWhiteSpace(compressedVirtualEnvFileName))
            {
                manifestFileProperties[PythonManifestFilePropertyKeys.CompressedVirtualEnvFile]
                    = compressedVirtualEnvFileName;
            }

            TryLogDependencies(pythonVersion, context.SourceRepo);

            var scriptProps = new PythonBashBuildSnippetProperties(
                virtualEnvironmentName: virtualEnvName,
                virtualEnvironmentModule: virtualEnvModule,
                virtualEnvironmentParameters: virtualEnvParams,
                packagesDirectory: packageDir,
                enableCollectStatic: _pythonScriptGeneratorOptions.EnableCollectStatic,
                compressVirtualEnvCommand: compressVirtualEnvCommand,
                compressedVirtualEnvFileName: compressedVirtualEnvFileName,
                runPythonPackageCommand: isPythonPackageCommandEnabled,
                pythonManifestFileName: Path.Join(manifestDirPath, "oryx-build-commands.txt"),
                pythonVersion: pythonVersion,
                pythonPackageWheelProperty: pythonPackageWheelType);

            string script = TemplateHelper.Render(
                TemplateHelper.TemplateResource.PythonSnippet,
                scriptProps,
                _logger);

            return new BuildScriptSnippet()
            {
                BashBuildScriptSnippet = script,
                BuildProperties = manifestFileProperties,
            };
        }

        /// <inheritdoc/>
        public bool IsCleanRepo(ISourceRepo repo)
        {
            // TODO: support venvs
            return !repo.DirExists(PythonConstants.DefaultTargetPackageDirectory);
        }

        /// <inheritdoc/>
        public string GenerateBashRunTimeInstallationScript(RunTimeInstallationScriptGeneratorOptions options)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public bool IsEnabled(RepositoryContext ctx)
        {
            return _commonOptions.EnablePythonBuild;
        }

        /// <inheritdoc/>
        public bool IsEnabledForMultiPlatformBuild(RepositoryContext ctx)
        {
            return true;
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetDirectoriesToExcludeFromCopyToBuildOutputDir(
            BuildScriptGeneratorContext context)
        {
            var dirs = new List<string>();
            var virtualEnvName = GetVirtualEnvironmentName(context);
            if (GetVirtualEnvPackOptions(
                context,
                virtualEnvName,
                out _,
                out string compressedFileName))
            {
                dirs.Add(virtualEnvName);
            }
            else if (!string.IsNullOrWhiteSpace(compressedFileName))
            {
                dirs.Add(compressedFileName);
            }

            return dirs;
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetDirectoriesToExcludeFromCopyToIntermediateDir(
            BuildScriptGeneratorContext context)
        {
            var excludeDirs = new List<string>();

            excludeDirs.Add(PythonConstants.DefaultTargetPackageDirectory);

            var virtualEnvName = GetVirtualEnvironmentName(context);
            if (!string.IsNullOrEmpty(virtualEnvName))
            {
                excludeDirs.Add(virtualEnvName);
                excludeDirs.Add(string.Format(PythonConstants.ZipVirtualEnvFileNameFormat, virtualEnvName));
                excludeDirs.Add(string.Format(PythonConstants.TarGzVirtualEnvFileNameFormat, virtualEnvName));
            }

            return excludeDirs;
        }

        /// <inheritdoc/>
        public string GetInstallerScriptSnippet(
            BuildScriptGeneratorContext context,
            PlatformDetectorResult detectorResult)
        {
            var pythonPlatformDetectorResult = detectorResult as PythonPlatformDetectorResult;
            if (pythonPlatformDetectorResult != null && IsCondaEnvironment(pythonPlatformDetectorResult))
            {
                _logger.LogDebug(
                    "Application in the source directory is a Conda based app, " +
                    "so skipping dynamic installation of Python SDK.");
                return null;
            }

            string installationScriptSnippet = null;
            if (_commonOptions.EnableDynamicInstall)
            {
                _logger.LogDebug("Dynamic install is enabled.");

                if (_platformInstaller.IsVersionAlreadyInstalled(detectorResult.PlatformVersion))
                {
                    _logger.LogDebug(
                       "Python version {version} is already installed. So skipping installing it again.",
                       detectorResult.PlatformVersion);
                }
                else
                {
                    _logger.LogDebug(
                        "Python version {version} is not installed. " +
                        "So generating an installation script snippet for it.",
                        detectorResult.PlatformVersion);

                    installationScriptSnippet = _platformInstaller.GetInstallerScriptSnippet(
                        detectorResult.PlatformVersion);
                }
            }
            else
            {
                _logger.LogDebug("Dynamic install not enabled.");
            }

            return installationScriptSnippet;
        }

        /// <inheritdoc/>
        public void ResolveVersions(RepositoryContext context, PlatformDetectorResult detectorResult)
        {
            var resolvedVersion = GetVersionUsingHierarchicalRules(detectorResult.PlatformVersion);
            resolvedVersion = GetMaxSatisfyingVersionAndVerify(resolvedVersion);
            detectorResult.PlatformVersion = resolvedVersion;
        }

        public IDictionary<string, string> GetToolsToBeSetInPath(
            RepositoryContext context,
            PlatformDetectorResult detectorResult)
        {
            var pythonPlatformDetectorResult = detectorResult as PythonPlatformDetectorResult;
            if (pythonPlatformDetectorResult == null)
            {
                throw new ArgumentException(
                    $"Expected '{nameof(detectorResult)}' argument to be of type " +
                    $"'{typeof(PythonPlatformDetectorResult)}' but got '{detectorResult.GetType()}'.");
            }

            // Since conda is already in the path we do not need to set it explicitly in the path
            if (IsCondaEnvironment(pythonPlatformDetectorResult))
            {
                return null;
            }

            var tools = new Dictionary<string, string>();
            tools[PythonConstants.PlatformName] = pythonPlatformDetectorResult.PlatformVersion;
            return tools;
        }

        private static string GetPackageDirectory(BuildScriptGeneratorContext context)
        {
            string packageDir = null;
            if (context.Properties != null)
            {
                context.Properties.TryGetValue(TargetPackageDirectoryPropertyKey, out packageDir);
            }

            return packageDir;
        }

        private BuildScriptSnippet GetBuildScriptSnippetForConda(
            BuildScriptGeneratorContext context,
            PythonPlatformDetectorResult detectorResult)
        {
            var scriptProperties = new JupyterNotebookBashBuildSnippetProperties();
            scriptProperties.HasRequirementsTxtFile = detectorResult.HasRequirementsTxtFile;
            var manifestDirPath = context.ManifestDir;

            if (detectorResult.HasCondaEnvironmentYmlFile)
            {
                scriptProperties.EnvironmentYmlFile = CondaConstants.CondaEnvironmentYmlFileName;
            }
            else
            {
                string pythonVersion;
                string templateName;
                var version = new SemVer.Version(detectorResult.PlatformVersion);
                if (version.Major.Equals(2))
                {
                    templateName = CondaConstants.DefaultPython2CondaEnvironmentYmlFileTemplateName;

                    // Conda seems to have a problem with post 2.7.15 version,
                    // so we by default restrict it to this version
                    pythonVersion = CondaConstants.DefaultPython2Version;
                }
                else
                {
                    templateName = CondaConstants.DefaultCondaEnvironmentYmlFileTemplateName;
                    pythonVersion = detectorResult.PlatformVersion;
                }

                scriptProperties.EnvironmentTemplateFileName = templateName;
                scriptProperties.EnvironmentTemplatePythonVersion = pythonVersion;
                scriptProperties.NoteBookManifestFileName = Path.Join(manifestDirPath, "oryx-build-commands.txt");
            }

            var script = TemplateHelper.Render(
                TemplateHelper.TemplateResource.PythonJupyterNotebookSnippet,
                scriptProperties,
                _logger);

            return new BuildScriptSnippet
            {
                BashBuildScriptSnippet = script,
            };
        }

        private string GetDefaultVirtualEnvName(PlatformDetectorResult detectorResult)
        {
            string pythonVersion = detectorResult.PlatformVersion;
            if (!string.IsNullOrWhiteSpace(pythonVersion))
            {
                var versionSplit = pythonVersion.Split('.');
                if (versionSplit.Length > 1)
                {
                    pythonVersion = $"{versionSplit[0]}.{versionSplit[1]}";
                }
            }

            return $"pythonenv{pythonVersion}";
        }

        private bool GetVirtualEnvPackOptions(
            BuildScriptGeneratorContext context,
            string virtualEnvName,
            out string compressVirtualEnvCommand,
            out string compressedVirtualEnvFileName)
        {
            var isVirtualEnvPackaged = false;
            compressVirtualEnvCommand = null;
            compressedVirtualEnvFileName = null;
            if (context.Properties != null &&
                context.Properties.TryGetValue(CompressVirtualEnvPropertyKey, out string compressVirtualEnvOption))
            {
                // default to tar.gz if the property was provided with no value.
                if (string.IsNullOrEmpty(compressVirtualEnvOption) ||
                    compressVirtualEnvOption.EqualsIgnoreCase(TarGzOption))
                {
                    compressedVirtualEnvFileName = string.Format(
                        PythonConstants.TarGzVirtualEnvFileNameFormat,
                        virtualEnvName);
                    compressVirtualEnvCommand = $"tar -zcf";
                    isVirtualEnvPackaged = true;
                }
                else if (compressVirtualEnvOption.EqualsIgnoreCase(ZipOption))
                {
                    compressedVirtualEnvFileName = string.Format(
                        PythonConstants.ZipVirtualEnvFileNameFormat,
                        virtualEnvName);
                    compressVirtualEnvCommand = $"zip -y -q -r";
                    isVirtualEnvPackaged = true;
                }
            }

            return isVirtualEnvPackaged;
        }

        private (string virtualEnvModule, string virtualEnvParams) GetVirtualEnvModules(string pythonVersion)
        {
            string virtualEnvModule;
            string virtualEnvParams = string.Empty;
            switch (pythonVersion.Split('.')[0])
            {
                case "2":
                    virtualEnvModule = "virtualenv";
                    break;

                case "3":
                    virtualEnvModule = "venv";
                    virtualEnvParams = "--copies";
                    break;

                default:
                    string errorMessage = "Python version '" + pythonVersion + "' is not supported";
                    _logger.LogError(errorMessage);
                    throw new NotSupportedException(errorMessage);
            }

            return (virtualEnvModule, virtualEnvParams);
        }

        private void TryLogDependencies(string pythonVersion, ISourceRepo repo)
        {
            if (!repo.FileExists(PythonConstants.RequirementsFileName))
            {
                return;
            }

            try
            {
                var deps = repo.ReadAllLines(PythonConstants.RequirementsFileName)
                    .Where(line => !line.TrimStart().StartsWith("#"));
                _logger.LogDependencies(PythonConstants.PlatformName, pythonVersion, deps);
            }
            catch (Exception exc)
            {
                _logger.LogWarning(exc, "Exception caught while logging dependencies");
            }
        }

        private string GetVirtualEnvironmentName(BuildScriptGeneratorContext context)
        {
            if (context.Properties == null ||
                !context.Properties.TryGetValue(VirtualEnvironmentNamePropertyKey, out var virtualEnvName))
            {
                virtualEnvName = string.Empty;
            }

            return virtualEnvName;
        }

        private string GetPythonPackageWheelType(BuildScriptGeneratorContext context)
        {
            if (context.Properties == null ||
                !context.Properties.TryGetValue(PythonPackageWheelPropertyKey, out var packageWheelProperty))
            {
                packageWheelProperty = string.Empty;
            }

            return packageWheelProperty;
        }

        private string GetMaxSatisfyingVersionAndVerify(string version)
        {
            var supportedVersions = SupportedVersions;

            // Since our semantic versioning library does not work with Python preview version format, here
            // we do some trivial way of finding the latest version which matches a given runtime version.
            // Preview version of sdks have alphabet letter in the version name. Such as '3.8.0b3', '3.9.0b1',etc.
            var nonPreviewRuntimeVersions = supportedVersions.Where(v => !v.Any(c => char.IsLetter(c)));
            var maxSatisfyingVersion = SemanticVersionResolver.GetMaxSatisfyingVersion(
                version,
                nonPreviewRuntimeVersions);

            // Check if a preview version is available
            if (string.IsNullOrEmpty(maxSatisfyingVersion))
            {
                // Preview versions: '3.8.0b3', '3.9.0b1', etc
                var previewRuntimeVersions = supportedVersions
                    .Where(v => v.Any(c => char.IsLetter(c)))
                    .Where(v => v.StartsWith(version))
                    .OrderByDescending(v => v);
                if (previewRuntimeVersions.Any())
                {
                    maxSatisfyingVersion = previewRuntimeVersions.First();
                }
            }

            if (string.IsNullOrEmpty(maxSatisfyingVersion))
            {
                var exc = new UnsupportedVersionException(
                    PythonConstants.PlatformName,
                    version,
                    supportedVersions);
                _logger.LogError(
                    exc,
                    $"Exception caught, the version '{version}' is not supported for the Python platform.");
                throw exc;
            }

            return maxSatisfyingVersion;
        }

        private string GetVersionUsingHierarchicalRules(string detectedVersion)
        {
            // Explicitly specified version by user wins over detected version
            if (!string.IsNullOrEmpty(_pythonScriptGeneratorOptions.PythonVersion))
            {
                return _pythonScriptGeneratorOptions.PythonVersion;
            }

            // If a version was detected, then use it.
            if (detectedVersion != null)
            {
                return detectedVersion;
            }

            // Fallback to default version
            var versionInfo = _versionProvider.GetVersionInfo();
            return versionInfo.DefaultVersion;
        }

        private bool IsCondaEnvironment(PythonPlatformDetectorResult pythonPlatformDetectorResult)
        {
            if (pythonPlatformDetectorResult.HasCondaEnvironmentYmlFile 
                && IsCondaInstalledInImage())
            {
                return true;
            }

            return false;
        }

        private bool IsCondaInstalledInImage()
        {
            return File.Exists(PythonConstants.CondaExecutablePath);
        }
    }
}