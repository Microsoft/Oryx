// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.Oryx.BuildScriptGenerator.Node
{
    /// <summary>
    /// Build script template for NodeJs in Bash.
    /// </summary>
    public class NodeBashBuildSnippetProperties
    {
        public string PackageRegistryUrl { get; set; }

        public string PackageDirectory { get; set; }

        public string PackageInstallCommand { get; set; }

        public string NpmRunBuildCommand { get; set; }

        public string CustomRunBuildCommand { get; set; }

        public string NpmRunBuildAzureCommand { get; set; }

        public bool HasProdDependencies { get; set; }

        public bool HasDevDependencies { get; set; }

        public string ProductionOnlyPackageInstallCommand { get; set; }

        public string CompressNodeModulesCommand { get; set; }

        public string CompressedNodeModulesFileName { get; set; }

        public bool ConfigureYarnCache { get; set; }

        public bool PruneDevDependencies { get; set; }

        public string AppInsightsInjectCommand { get; set; }

        public string AppInsightsPackageName { get; set; }

        public string AppInsightsLoaderFileName { get; set; }

        public string PackageInstallerVersionCommand { get; set; }

        public bool RunNpmPack { get; set; }

        public string LernaRunBuildCommand { get; set; }

        public string InstallLernaCommand { get; set; }

        public string LernaInitCommand { get; set; }

        public string LernaBootstrapCommand { get; set; }

        public string InstallLageCommand { get; set; }

        public string LageRunBuildCommand { get; set; }

        public string CustomBuildCommand { get; set; }

        /// <summary>
        /// Gets or sets a list of commands for the build.
        /// </summary>
        public IDictionary<string, string> NodeBuildProperties { get; set; }

        /// <summary>
        /// Gets or sets the build command file for nodejs.
        /// </summary>
        public string NodeBuildCommandsFile { get; set; }
    }
}