// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

namespace Microsoft.Oryx.BuildScriptGenerator.Php
{
    internal class PhpScriptGeneratorOptionsSetup : IConfigureOptions<PhpScriptGeneratorOptions>
    {
        private readonly EnvironmentSettings _settings;

        public PhpScriptGeneratorOptionsSetup(IEnvironmentSettingsProvider envSettingsProvider)
        {
            envSettingsProvider.TryGetAndLoadSettings(out _settings);
        }

        public void Configure(PhpScriptGeneratorOptions options)
        {
            var defaultVersion = _environment.GetEnvironmentVariable(PhpConstants.DefaultVersion);
            if (string.IsNullOrEmpty(defaultVersion))
            {
                defaultVersion = PythonConstants.PythonLtsVersion;
            }

            options.PythonDefaultVersion = defaultVersion;
            options.InstalledPythonVersionsDir = InstalledPythonVersionsDir;
            options.SupportedPythonVersions = _environment.GetEnvironmentVariableAsList(PythonSupportedVersionsEnvVariable);
        }
    }
}