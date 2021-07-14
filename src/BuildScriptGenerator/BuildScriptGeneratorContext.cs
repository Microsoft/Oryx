// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.Oryx.BuildScriptGenerator
{
    /// <summary>
    /// Options for the build script generation process.
    /// </summary>
    public partial class BuildScriptGeneratorContext : RepositoryContext
    {
        /// <summary>
        /// Gets or sets the information which is used to correlate log messages.
        /// </summary>
        public string OperationId { get; set; }

        /// <summary>
        /// Gets or sets a value of a directory where the build manifest file should be put into.
        /// </summary>
        public string ManifestDir { get; set; }

        /// <summary>
        /// Gets or sets a value of the file where build commands will be written during build.
        /// </summary>
        public string BuildCommandsFile { get; set; }
    }
}