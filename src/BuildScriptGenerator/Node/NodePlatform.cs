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
using Microsoft.Oryx.BuildScriptGenerator.SourceRepo;
using Microsoft.Oryx.Common.Extensions;
using Microsoft.Oryx.Detector;
using Microsoft.Oryx.Detector.Node;
using Newtonsoft.Json.Linq;

namespace Microsoft.Oryx.BuildScriptGenerator.Node
{
    /// <summary>
    /// Node.js Platform.
    /// </summary>
    [BuildProperty(RegistryUrlPropertyKey, "Custom npm registry URL. Will be written to .npmrc during the build.")]
    [BuildProperty(
        CompressNodeModulesPropertyKey,
        "Indicates how and if 'node_modules' folder should be compressed into a single file in the output folder. " +
        "Options are '" + ZipNodeModulesOption + "', and '" + TarGzNodeModulesOption + "'. Default is to not compress. " +
        "If this option is used, when running the app the node_modules folder must be extracted from this file.")]
    [BuildProperty(
        PruneDevDependenciesPropertyKey,
        "When using different intermediate and output folders, only the prod dependencies are copied to the output. " +
        "Options are 'true', blank (same meaning as 'true'), and 'false'. Default is false.")]
    [BuildProperty(
        RequireBuildPropertyKey,
        "Requires either 'npm run build' or 'yarn run build' or custom run build command  to be run. Default is false. " +
        "If value is not provided, it is assumed to be 'true'.")]
    [BuildProperty(
        PackageDirectoryPropertyKey,
        "When multiple packages exists in a repo, indicates within which package directory it will run " +
        "'npm run pack' command'. If value is not provided, it is assumed to be root directory.")]
    internal class NodePlatform : IProgrammingPlatform
    {
        /// <summary>
        /// Property key of Registry URL.
        /// </summary>
        internal const string RegistryUrlPropertyKey = "npm_registry_url";

        /// <summary>
        /// Property key of compress_node_modules.
        /// </summary>
        internal const string CompressNodeModulesPropertyKey = "compress_node_modules";

        /// <summary>
        /// Property key of prune_dev_dependencies.
        /// </summary>
        internal const string PruneDevDependenciesPropertyKey = "prune_dev_dependencies";

        /// <summary>
        /// Property key of package_directory.
        /// </summary>
        internal const string PackageDirectoryPropertyKey = "package_directory";

        /// <summary>
        /// Property key of require_build.
        /// </summary>
        internal const string RequireBuildPropertyKey = "require_build";

        /// <summary>
        /// The zip option for node modules.
        /// </summary>
        internal const string ZipNodeModulesOption = "zip";

        /// <summary>
        /// The tar-gz option for node modules.
        /// </summary>
        internal const string TarGzNodeModulesOption = "tar-gz";
        private readonly BuildScriptGeneratorOptions _commonOptions;
        private readonly NodeScriptGeneratorOptions _nodeScriptGeneratorOptions;
        private readonly INodeVersionProvider _nodeVersionProvider;
        private readonly ILogger<NodePlatform> _logger;
        private readonly INodePlatformDetector _detector;
        private readonly IEnvironment _environment;
        private readonly NodePlatformInstaller _platformInstaller;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodePlatform"/> class.
        /// </summary>
        /// <param name="commonOptions">The <see cref="BuildScriptGeneratorOptions"/>.</param>
        /// <param name="nodeScriptGeneratorOptions">The options for nodeScriptGenerator.</param>
        /// <param name="nodeVersionProvider">The Node.js version provider.</param>
        /// <param name="logger">The logger of Node.js platform.</param>
        /// <param name="detector">The detector of Node.js platform.</param>
        /// <param name="environment">The environment of Node.js platform.</param>
        /// <param name="nodePlatformInstaller">The <see cref="NodePlatformInstaller"/>.</param>
        public NodePlatform(
            IOptions<BuildScriptGeneratorOptions> commonOptions,
            IOptions<NodeScriptGeneratorOptions> nodeScriptGeneratorOptions,
            INodeVersionProvider nodeVersionProvider,
            ILogger<NodePlatform> logger,
            INodePlatformDetector detector,
            IEnvironment environment,
            NodePlatformInstaller nodePlatformInstaller)
        {
            _commonOptions = commonOptions.Value;
            _nodeScriptGeneratorOptions = nodeScriptGeneratorOptions.Value;
            _nodeVersionProvider = nodeVersionProvider;
            _logger = logger;
            _detector = detector;
            _environment = environment;
            _platformInstaller = nodePlatformInstaller;
        }

        /// <inheritdoc/>
        public string Name => NodeConstants.PlatformName;

        /// <inheritdoc/>
        public IEnumerable<string> SupportedVersions
        {
            get
            {
                var versionInfo = _nodeVersionProvider.GetVersionInfo();
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
            BuildScriptGeneratorContext ctx,
            PlatformDetectorResult detectorResult)
        {
            var nodePlatformDetectorResult = detectorResult as NodePlatformDetectorResult;
            if (nodePlatformDetectorResult == null)
            {
                throw new ArgumentException(
                    $"Expected '{nameof(detectorResult)}' argument to be of type " +
                    $"'{typeof(NodePlatformDetectorResult)}' but got '{detectorResult.GetType()}'.");
            }

            var manifestFileProperties = new Dictionary<string, string>();
            var nodeCommandManifestFileProperties = new Dictionary<string, string>();

            // Write the platform name and version to the manifest file
            manifestFileProperties[ManifestFilePropertyKeys.NodeVersion] = nodePlatformDetectorResult.PlatformVersion;
            nodeCommandManifestFileProperties["PlatformWithVersion"] = "nodejs " + nodePlatformDetectorResult.PlatformVersion;
            var packageJson = GetPackageJsonObject(ctx.SourceRepo, _logger);
            string runBuildCommand = null;
            string runBuildAzureCommand = null;
            string runBuildLernaCommand = null;
            string runBuildLageCommand = null;
            string installLernaCommand = null;
            bool configureYarnCache = false;
            string packageManagerCmd = null;
            string packageInstallCommand = null;
            string packageInstallerVersionCommand = null;
            var manifestDirPath = ctx.ManifestDir;

            if (_nodeScriptGeneratorOptions.EnableNodeMonorepoBuild &&
                nodePlatformDetectorResult.HasLernaJsonFile &&
                nodePlatformDetectorResult.HasLageConfigJSFile)
            {
                _logger.LogError(
                "Could not build monorepo with multiple package management tools. Both 'lerna.json' and 'lage.config.js' files are found.");
                throw new InvalidUsageException("Multiple monorepo package management tools are found, please choose to use either Lerna or Lage.");
            }

            if (ctx.SourceRepo.FileExists(NodeConstants.YarnLockFileName))
            {
                packageManagerCmd = NodeConstants.YarnCommand;
                configureYarnCache = false;
                packageInstallerVersionCommand = NodeConstants.YarnVersionCommand;

                // In Yarn 2+ and .yarnrc.yml file replaces .yarnrc in Yarn 2+.
                // Applying yarn 2 cache folder name and package install command.
                if (nodePlatformDetectorResult.HasYarnrcYmlFile)
                {
                    packageInstallCommand = NodeConstants.Yarn2PackageInstallCommand;
                }
                else
                {
                    packageInstallCommand = NodeConstants.YarnPackageInstallCommand;
                }
            }
            else
            {
                packageManagerCmd = NodeConstants.NpmCommand;
                packageInstallCommand = NodeConstants.NpmPackageInstallCommand;
                packageInstallerVersionCommand = NodeConstants.NpmVersionCommand;
            }

            if (_nodeScriptGeneratorOptions.EnableNodeMonorepoBuild)
            {
                // If a 'lerna.json' file exists, override the npm client that lerna chosen to build monorepo.
                if (nodePlatformDetectorResult.HasLernaJsonFile)
                {
                    packageManagerCmd = nodePlatformDetectorResult.LernaNpmClient;
                    runBuildLernaCommand = string.Format(
                            NodeConstants.PkgMgrRunBuildCommandTemplate,
                            NodeConstants.LernaCommand);
                    if (!string.IsNullOrEmpty(nodePlatformDetectorResult.LernaNpmClient)
                        && nodePlatformDetectorResult.LernaNpmClient.Equals(
                        NodeConstants.YarnCommand, StringComparison.OrdinalIgnoreCase))
                    {
                        packageInstallCommand = NodeConstants.YarnPackageInstallCommand;
                        configureYarnCache = false;
                        packageInstallerVersionCommand = NodeConstants.YarnVersionCommand;
                        installLernaCommand = NodeConstants.InstallLernaCommandYarn;
                    }
                    else
                    {
                        packageInstallCommand = NodeConstants.NpmPackageInstallCommand;
                        packageInstallerVersionCommand = NodeConstants.NpmVersionCommand;
                        installLernaCommand = NodeConstants.InstallLernaCommandNpm;
                    }
                }

                // If a 'lage.config.js' file exits, run build using lage specifc commands.
                if (nodePlatformDetectorResult.HasLageConfigJSFile)
                {
                    runBuildLageCommand = ctx.SourceRepo.FileExists(NodeConstants.YarnLockFileName) ?
                            NodeConstants.YarnRunLageBuildCommand : NodeConstants.NpmRunLageBuildCommand;
                }
            }

            _logger.LogInformation("Using {packageManager}", packageManagerCmd);

            var hasProdDependencies = false;
            if (packageJson?.dependencies != null)
            {
                hasProdDependencies = true;
            }

            var hasDevDependencies = false;
            if (packageJson?.devDependencies != null)
            {
                // If development time dependencies are present we want to avoid copying them to improve performance
                hasDevDependencies = true;
            }

            var productionOnlyPackageInstallCommand = string.Format(
                NodeConstants.ProductionOnlyPackageInstallCommandTemplate, packageInstallCommand);

            if (string.IsNullOrEmpty(_nodeScriptGeneratorOptions.CustomBuildCommand)
                && string.IsNullOrEmpty(_nodeScriptGeneratorOptions.CustomRunBuildCommand)
                && string.IsNullOrEmpty(runBuildLernaCommand)
                && string.IsNullOrEmpty(runBuildLageCommand))
            {
                var scriptsNode = packageJson?.scripts;
                if (scriptsNode != null)
                {
                    if (scriptsNode.build != null)
                    {
                        runBuildCommand = string.Format(NodeConstants.PkgMgrRunBuildCommandTemplate, packageManagerCmd);
                    }

                    if (scriptsNode["build:azure"] != null && !_commonOptions.ShouldPackage)
                    {
                        runBuildAzureCommand = string.Format(
                            NodeConstants.PkgMgrRunBuildAzureCommandTemplate,
                            packageManagerCmd);
                    }
                }
            }

            if (IsBuildRequired(ctx)
                && string.IsNullOrEmpty(_nodeScriptGeneratorOptions.CustomBuildCommand)
                && string.IsNullOrEmpty(_nodeScriptGeneratorOptions.CustomRunBuildCommand)
                && string.IsNullOrEmpty(runBuildCommand)
                && string.IsNullOrEmpty(runBuildAzureCommand)
                && string.IsNullOrEmpty(runBuildLernaCommand)
                && string.IsNullOrEmpty(runBuildLageCommand))
            {
                throw new NoBuildStepException(
                    "Could not find either 'build' or 'build:azure' node under 'scripts' in package.json. " +
                    "Could not find value for custom run build command using the environment variable " +
                    "key 'RUN_BUILD_COMMAND'." +
                    "Could not find tools for building monorepos, no 'lerna.json' or 'lage.config.js' files found.");
            }

            if (packageJson?.dependencies != null)
            {
                var depSpecs = ((JObject)packageJson.dependencies).ToObject<IDictionary<string, string>>();
                _logger.LogDependencies(
                    _commonOptions.PlatformName,
                    nodePlatformDetectorResult.PlatformVersion,
                    depSpecs.Select(d => d.Key + d.Value));
            }

            if (packageJson?.devDependencies != null)
            {
                var depSpecs = ((JObject)packageJson.devDependencies).ToObject<IDictionary<string, string>>();
                _logger.LogDependencies(
                    _commonOptions.PlatformName,
                    nodePlatformDetectorResult.PlatformVersion,
                    depSpecs.Select(d => d.Key + d.Value), true);
            }

            string compressNodeModulesCommand = null;
            string compressedNodeModulesFileName = null;
            GetNodeModulesPackOptions(ctx, out compressNodeModulesCommand, out compressedNodeModulesFileName);

            if (!string.IsNullOrWhiteSpace(compressedNodeModulesFileName))
            {
                manifestFileProperties[NodeConstants.NodeModulesFileBuildProperty] = compressedNodeModulesFileName;
            }

            bool pruneDevDependencies = ShouldPruneDevDependencies(ctx);
            string appInsightsInjectCommand = string.Empty;

            GetAppOutputDirPath(packageJson, manifestFileProperties);

            string customRegistryUrl = null;
            if (ctx.Properties != null)
            {
                ctx.Properties.TryGetValue(RegistryUrlPropertyKey, out customRegistryUrl);
                if (!string.IsNullOrWhiteSpace(customRegistryUrl))
                {
                    // Write the custom registry to the build manifest
                    manifestFileProperties[$"{NodeConstants.PlatformName}_{RegistryUrlPropertyKey}"] = customRegistryUrl;
                }
            }

            string packageDir = null;
            if (ctx.Properties != null)
            {
                ctx.Properties.TryGetValue(PackageDirectoryPropertyKey, out packageDir);
                if (!string.IsNullOrWhiteSpace(packageDir))
                {
                    // Write the package directory to the build manifest
                    manifestFileProperties[$"{PackageDirectoryPropertyKey}"] = packageDir;
                }
            }

            var scriptProps = new NodeBashBuildSnippetProperties
            {
                PackageRegistryUrl = customRegistryUrl,
                PackageDirectory = packageDir,
                PackageInstallCommand = packageInstallCommand,
                NpmRunBuildCommand = runBuildCommand,
                NpmRunBuildAzureCommand = runBuildAzureCommand,
                HasProdDependencies = hasProdDependencies,
                HasDevDependencies = hasDevDependencies,
                ProductionOnlyPackageInstallCommand = productionOnlyPackageInstallCommand,
                CompressNodeModulesCommand = compressNodeModulesCommand,
                CompressedNodeModulesFileName = compressedNodeModulesFileName,
                ConfigureYarnCache = configureYarnCache,
                PruneDevDependencies = pruneDevDependencies,
                AppInsightsInjectCommand = appInsightsInjectCommand,
                AppInsightsPackageName = NodeConstants.NodeAppInsightsPackageName,
                AppInsightsLoaderFileName = NodeAppInsightsLoader.NodeAppInsightsLoaderFileName,
                PackageInstallerVersionCommand = packageInstallerVersionCommand,
                RunNpmPack = _commonOptions.ShouldPackage,
                CustomBuildCommand = _nodeScriptGeneratorOptions.CustomBuildCommand,
                CustomRunBuildCommand = _nodeScriptGeneratorOptions.CustomRunBuildCommand,
                LernaRunBuildCommand = runBuildLernaCommand,
                InstallLernaCommand = installLernaCommand,
                LernaInitCommand = NodeConstants.LernaInitCommand,
                LernaBootstrapCommand = NodeConstants.LernaBootstrapCommand,
                InstallLageCommand = NodeConstants.InstallLageCommand,
                LageRunBuildCommand = runBuildLageCommand,
                NodeBuildProperties = nodeCommandManifestFileProperties,
                NodeManifestFileName = Path.Join(manifestDirPath, "oryx-build-commands.txt"),
            };
            string script = TemplateHelper.Render(
                TemplateHelper.TemplateResource.NodeBuildSnippet,
                scriptProps,
                _logger);

            return new BuildScriptSnippet
            {
                BashBuildScriptSnippet = script,
                BuildProperties = manifestFileProperties,
            };
        }

        /// <inheritdoc/>
        public bool IsCleanRepo(ISourceRepo repo)
        {
            return !repo.DirExists(NodeConstants.NodeModulesDirName);
        }

        /// <inheritdoc/>
        public bool IsEnabled(RepositoryContext ctx)
        {
            return _commonOptions.EnableNodeJSBuild;
        }

        /// <inheritdoc/>
        public bool IsEnabledForMultiPlatformBuild(RepositoryContext ctx)
        {
            return true;
        }

        /// <inheritdoc/>
        public string GenerateBashRunTimeInstallationScript(RunTimeInstallationScriptGeneratorOptions options)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetDirectoriesToExcludeFromCopyToBuildOutputDir(BuildScriptGeneratorContext ctx)
        {
            var dirs = new List<string>
            {
                NodeConstants.AllNodeModulesDirName,
                NodeConstants.ProdNodeModulesDirName,
            };

            // If the node modules folder is being packaged in a file, we don't copy it to the output
            if (GetNodeModulesPackOptions(ctx, out _, out string compressedFileName))
            {
                // we need to make sure we are not copying the root's node_modules folder
                // if there are any other node_modules folder we will copy them to destination
                dirs.Add(string.Concat("/", NodeConstants.NodeModulesDirName));
            }
            else if (!string.IsNullOrWhiteSpace(compressedFileName))
            {
                dirs.Add(compressedFileName);
            }

            return dirs;
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetDirectoriesToExcludeFromCopyToIntermediateDir(BuildScriptGeneratorContext ctx)
        {
            return new[]
            {
                NodeConstants.AllNodeModulesDirName,
                NodeConstants.ProdNodeModulesDirName,

                // we need to make sure we are not copying the root's node_modules folder
                // if there are any other node_modules folder we will copy them to destination
                string.Concat("/", NodeConstants.NodeModulesDirName),
                NodeConstants.NodeModulesToBeDeletedName,
                NodeConstants.NodeModulesZippedFileName,
                NodeConstants.NodeModulesTarGzFileName,
            };
        }

        /// <inheritdoc/>
        public string GetInstallerScriptSnippet(
            BuildScriptGeneratorContext context,
            PlatformDetectorResult detectorResult)
        {
            string installationScriptSnippet = null;
            if (_commonOptions.EnableDynamicInstall)
            {
                _logger.LogDebug("Dynamic install is enabled.");

                if (_platformInstaller.IsVersionAlreadyInstalled(detectorResult.PlatformVersion))
                {
                    _logger.LogDebug(
                        "Node version {version} is already installed. So skipping installing it again.",
                        detectorResult.PlatformVersion);
                }
                else
                {
                    _logger.LogDebug(
                        "Node version {version} is not installed. " +
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

        /// <inheritdoc/>
        public IDictionary<string, string> GetToolsToBeSetInPath(
            RepositoryContext context,
            PlatformDetectorResult detectorResult)
        {
            var tools = new Dictionary<string, string>();
            tools[NodeConstants.PlatformName] = detectorResult.PlatformVersion;
            return tools;
        }

        /// <summary>
        /// Gets the package json object.
        /// </summary>
        /// <param name="sourceRepo">The source repository.</param>
        /// <param name="logger">The logger of Node.js platform.</param>
        /// <returns>Package json Object.</returns>
        internal static dynamic GetPackageJsonObject(ISourceRepo sourceRepo, ILogger logger)
        {
            if (!sourceRepo.FileExists(NodeConstants.PackageJsonFileName))
            {
                return null;
            }

            dynamic packageJson = null;
            try
            {
                packageJson = sourceRepo.ReadJsonObjectFromFile(NodeConstants.PackageJsonFileName);
            }
            catch (Exception exc)
            {
                // Leave malformed package.json files for Node.js to handle.
                // This prevents Oryx from erroring out when Node.js itself might be able to tolerate the file.
                logger.LogWarning(
                    exc,
                    $"Exception caught while trying to deserialize {NodeConstants.PackageJsonFileName.Hash()}");
            }

            return packageJson;
        }

        private static bool ShouldPruneDevDependencies(BuildScriptGeneratorContext context)
        {
            return BuildPropertiesHelper.IsTrue(PruneDevDependenciesPropertyKey, context, valueIsRequired: false);
        }

        private static bool IsBuildRequired(BuildScriptGeneratorContext context)
        {
            return BuildPropertiesHelper.IsTrue(RequireBuildPropertyKey, context, valueIsRequired: false);
        }

        private static bool DoesPackageDependencyExist(dynamic packageJson, string packageName)
        {
            if (packageJson?.dependencies != null)
            {
                JObject deps = packageJson.dependencies;
                var pkgDependencies = deps.ToObject<IDictionary<string, string>>();
                if (pkgDependencies.ContainsKey(packageName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool GetNodeModulesPackOptions(
            BuildScriptGeneratorContext context,
            out string compressNodeModulesCommand,
            out string compressedNodeModulesFileName)
        {
            var isNodeModulesPackaged = false;
            compressNodeModulesCommand = null;
            compressedNodeModulesFileName = null;
            if (context.Properties != null &&
                context.Properties.TryGetValue(CompressNodeModulesPropertyKey, out string compressNodeModulesOption))
            {
                // default to tar.gz if the property was provided with no value.
                if (string.IsNullOrEmpty(compressNodeModulesOption) ||
                    compressNodeModulesOption.EqualsIgnoreCase(TarGzNodeModulesOption))
                {
                    compressedNodeModulesFileName = NodeConstants.NodeModulesTarGzFileName;
                    compressNodeModulesCommand = $"tar -zcf";
                    isNodeModulesPackaged = true;
                }
                else if (compressNodeModulesOption.EqualsIgnoreCase(ZipNodeModulesOption))
                {
                    compressedNodeModulesFileName = NodeConstants.NodeModulesZippedFileName;
                    compressNodeModulesCommand = $"zip -y -q -r";
                    isNodeModulesPackaged = true;
                }
            }

            return isNodeModulesPackaged;
        }

        private void GetAppOutputDirPath(dynamic packageJson, Dictionary<string, string> buildProperties)
        {
            if (packageJson == null || packageJson.scripts == null || packageJson.scripts["build"] == null)
            {
                return;
            }

            var buildNode = packageJson.scripts["build"] as JValue;
            var buildCommand = buildNode.Value as string;

            if (string.IsNullOrEmpty(buildCommand))
            {
                return;
            }

            string outputDirPath = null;
            if (buildCommand.Contains("ng build", StringComparison.OrdinalIgnoreCase))
            {
                outputDirPath = "dist";
            }
            else if (buildCommand.Contains("gatsby build", StringComparison.OrdinalIgnoreCase))
            {
                outputDirPath = "public";
            }
            else if (buildCommand.Contains("react-scripts build", StringComparison.OrdinalIgnoreCase))
            {
                outputDirPath = "build";
            }
            else if (buildCommand.Contains("next build", StringComparison.OrdinalIgnoreCase))
            {
                outputDirPath = ".next";
            }
            else if (buildCommand.Contains("nuxt build", StringComparison.OrdinalIgnoreCase))
            {
                outputDirPath = ".nuxt";
            }
            else if (buildCommand.Contains("vue-cli-service build", StringComparison.OrdinalIgnoreCase))
            {
                outputDirPath = "dist";
            }
            else if (buildCommand.Contains("hexo generate", StringComparison.OrdinalIgnoreCase))
            {
                outputDirPath = "public";
            }

            if (!string.IsNullOrEmpty(outputDirPath))
            {
                buildProperties[NodeManifestFilePropertyKeys.OutputDirPath] = outputDirPath;
            }
        }

        private string GetMaxSatisfyingVersionAndVerify(string version)
        {
            var versionInfo = _nodeVersionProvider.GetVersionInfo();
            var maxSatisfyingVersion = SemanticVersionResolver.GetMaxSatisfyingVersion(
                version,
                versionInfo.SupportedVersions);

            if (string.IsNullOrEmpty(maxSatisfyingVersion))
            {
                var exception = new UnsupportedVersionException(
                    NodeConstants.PlatformName,
                    version,
                    versionInfo.SupportedVersions);
                _logger.LogError(
                    exception,
                    $"Exception caught, the version '{version}' is not supported for the Node platform.");
                throw exception;
            }

            return maxSatisfyingVersion;
        }

        private string GetVersionUsingHierarchicalRules(string detectedVersion)
        {
            // Explicitly specified version by user wins over detected version
            if (!string.IsNullOrEmpty(_nodeScriptGeneratorOptions.NodeVersion))
            {
                return _nodeScriptGeneratorOptions.NodeVersion;
            }

            // If a version was detected, then use it.
            if (detectedVersion != null)
            {
                return detectedVersion;
            }

            // Fallback to default version
            var versionInfo = _nodeVersionProvider.GetVersionInfo();
            return versionInfo.DefaultVersion;
        }
    }
}