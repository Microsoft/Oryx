﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Oryx.BuildScriptGenerator.Exceptions;

namespace Microsoft.Oryx.BuildScriptGenerator
{
    public class RunTimeInstallationScriptGenerator : IRunTimeInstallationScriptGenerator
    {
        private readonly IEnumerable<IProgrammingPlatform> _programmingPlatforms;

        public RunTimeInstallationScriptGenerator(IEnumerable<IProgrammingPlatform> programmingPlatforms)
        {
            _programmingPlatforms = programmingPlatforms;
        }

        public string GenerateBashScript(string targetPlatformName, RunTimeInstallationScriptGeneratorOptions opts)
        {
            var targetPlatform = _programmingPlatforms
                .Where(p => p.Name.EqualsIgnoreCase(targetPlatformName))
                .FirstOrDefault();

            if (targetPlatform == null)
            {
                throw new UnsupportedLanguageException($"Platform '{targetPlatformName}' is not supported.");
            }

            var runScript = targetPlatform.GenerateBashRunTimeInstallationScript(opts);
            return runScript;
        }
    }
}