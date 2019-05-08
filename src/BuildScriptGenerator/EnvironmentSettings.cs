﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.Oryx.BuildScriptGenerator
{
    public class EnvironmentSettings
    {
        // Note: The following two properties exist so that we do not break
        // existing users who might still be using them
        public string PreBuildScriptPath { get; set; }

        public string PostBuildScriptPath { get; set; }

        public string PreBuildCommand { get; set; }

        public string PostBuildCommand { get; set; }
    }
}
