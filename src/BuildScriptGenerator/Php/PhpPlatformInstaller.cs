﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Oryx.BuildScriptGenerator.Php
{
    public class PhpPlatformInstaller : PlatformInstallerBase
    {
        public PhpPlatformInstaller(
            IOptions<BuildScriptGeneratorOptions> commonOptions,
            ILoggerFactory loggerFactory)
            : base(commonOptions, loggerFactory)
        {
        }

        public virtual string GetInstallerScriptSnippet(string version)
        {
            return GetInstallerScriptSnippet(PhpConstants.PlatformName, version);
        }

        public virtual bool IsVersionAlreadyInstalled(string version)
        {
            return IsVersionInstalled(
                version,
                builtInDir: PhpConstants.InstalledPhpVersionsDir,
                dynamicInstallDir: $"{Constants.TemporaryInstallationDirectoryRoot}/php");
        }
    }
}
