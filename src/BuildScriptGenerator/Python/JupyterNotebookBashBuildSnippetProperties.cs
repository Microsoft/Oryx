// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.Oryx.BuildScriptGenerator.Python
{
    public class JupyterNotebookBashBuildSnippetProperties
    {
        public string EnvironmentYmlFile { get; set; }

        public string EnvironmentTemplateFileName { get; set; }

        public string EnvironmentTemplatePythonVersion { get; set; }

        public bool HasRequirementsTxtFile { get; set; }

        /// <summary>
        /// Gets or sets the name of the manifest file.
        /// </summary>
        public string NoteBookManifestFileName { get; set; }
    }
}
