﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.Oryx.BuildScriptGenerator
{
    public class BaseBashBuildScriptProperties
    {
        /// <summary>
        /// Gets or sets the collection of build script snippets.
        /// </summary>
        public IEnumerable<string> BuildScriptSnippets { get; set; }

        /// <summary>
        /// Gets or sets the path to the pre build script.
        /// </summary>
        public string PreBuildScriptPath { get; set; }

        /// <summary>
        /// Gets or sets the argument to the benv command.
        /// </summary>
        public string BenvArgs { get; set; }

        /// <summary>
        /// Gets or sets the path to the post build script.
        /// </summary>
        public string PostBuildScriptPath { get; set; }
    }
}