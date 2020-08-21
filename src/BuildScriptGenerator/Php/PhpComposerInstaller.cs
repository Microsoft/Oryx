﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Oryx.BuildScriptGenerator.Php
{
    /// <summary>
    /// Generates an installation script snippet to install PHP Composer.
    /// </summary>
    internal class PhpComposerInstaller : PlatformInstallerBase
    {
        public PhpComposerInstaller(
            IOptions<BuildScriptGeneratorOptions> commonOptions,
            ILoggerFactory loggerFactory)
            : base(commonOptions, loggerFactory)
        {
        }

        public virtual string GetInstallerScriptSnippet(string version)
        {
            return GetInstallerScriptSnippet(platformName: "php-composer", version);
        }

        public virtual bool IsVersionAlreadyInstalled(string version)
        {
            return IsVersionInstalled(
                version,
                builtInDir: PhpConstants.InstalledPhpComposerVersionDir,
                dynamicInstallDir: Path.Combine(CommonOptions.DynamicInstallRootDir, "php-composer"));
        }
    }
}
