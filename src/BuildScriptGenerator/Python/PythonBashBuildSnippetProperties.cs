// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.Oryx.BuildScriptGenerator.Python
{
    /// <summary>
    /// Build script template for Python in Bash.
    /// </summary>
    public class PythonBashBuildSnippetProperties
    {
        public PythonBashBuildSnippetProperties(
            string virtualEnvironmentName,
            string virtualEnvironmentModule,
            string virtualEnvironmentParameters,
            string packagesDirectory,
            bool enableCollectStatic,
            string compressVirtualEnvCommand,
            string compressedVirtualEnvFileName,
            bool runPythonPackageCommand,
            string pythonVersion,
            string pythonBuildCommandsFileName = null,
            string pythonPackageWheelProperty = null)
        {
            VirtualEnvironmentName = virtualEnvironmentName;
            VirtualEnvironmentModule = virtualEnvironmentModule;
            VirtualEnvironmentParameters = virtualEnvironmentParameters;
            PackagesDirectory = packagesDirectory;
            EnableCollectStatic = enableCollectStatic;
            CompressVirtualEnvCommand = compressVirtualEnvCommand;
            CompressedVirtualEnvFileName = compressedVirtualEnvFileName;
            RunPythonPackageCommand = runPythonPackageCommand;
            PythonPackageWheelProperty = pythonPackageWheelProperty;
            PythonBuildCommandsFileName = pythonBuildCommandsFileName;
            PythonVersion = pythonVersion;
        }

        public string VirtualEnvironmentName { get; set; }

        public string VirtualEnvironmentModule { get; set; }

        public string VirtualEnvironmentParameters { get; set; }

        /// <summary>
        /// Gets or sets the directory where the packages will be downloaded to.
        /// </summary>
        public string PackagesDirectory { get; set; }

        public bool EnableCollectStatic { get; set; }

        public string CompressVirtualEnvCommand { get; set; }

        public string CompressedVirtualEnvFileName { get; set; }

        public bool RunPythonPackageCommand { get; set; }

        public string PythonVersion { get; set; }

        /// <summary>
        /// Gets or sets the name of the python build commands file.
        /// </summary>
        public string PythonBuildCommandsFileName { get; set; }

        public string PythonPackageWheelProperty { get; set; }
    }
}