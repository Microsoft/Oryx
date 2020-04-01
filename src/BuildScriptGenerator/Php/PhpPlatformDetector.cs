﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Oryx.BuildScriptGenerator.Exceptions;
using Microsoft.Oryx.BuildScriptGenerator.SourceRepo;
using Microsoft.Oryx.Common.Extensions;

namespace Microsoft.Oryx.BuildScriptGenerator.Php
{
    internal class PhpPlatformDetector : IPlatformDetector
    {
        private readonly PhpScriptGeneratorOptions _options;
        private readonly IPhpVersionProvider _versionProvider;
        private readonly ILogger<PhpPlatformDetector> _logger;
        private readonly IStandardOutputWriter _writer;

        public PhpPlatformDetector(
            IOptions<PhpScriptGeneratorOptions> options,
            IPhpVersionProvider versionProvider,
            ILogger<PhpPlatformDetector> logger,
            IStandardOutputWriter writer)
        {
            _options = options.Value;
            _versionProvider = versionProvider;
            _logger = logger;
            _writer = writer;
        }

        public PlatformDetectorResult Detect(RepositoryContext context)
        {
            var sourceRepo = context.SourceRepo;
            if (!sourceRepo.FileExists(PhpConstants.ComposerFileName))
            {
                _logger.LogDebug($"File '{PhpConstants.ComposerFileName}' does not exist in source repo");
                return null;
            }

            var version = GetVersion(context);
            version = GetMaxSatisfyingVersionAndVerify(version);

            return new PlatformDetectorResult
            {
                Platform = PhpConstants.PlatformName,
                PlatformVersion = version,
            };
        }

        private string GetVersion(RepositoryContext context)
        {
            if (context.PhpVersion != null)
            {
                return context.PhpVersion;
            }

            if (_options.PhpVersion != null)
            {
                return _options.PhpVersion;
            }

            var version = GetVersionFromComposerFile(context);
            if (version != null)
            {
                return version;
            }

            return GetDefaultVersionFromProvider();
        }

        private string GetVersionFromComposerFile(RepositoryContext context)
        {
            dynamic composerFile = null;
            try
            {
                composerFile = context.SourceRepo.ReadJsonObjectFromFile(PhpConstants.ComposerFileName);
            }
            catch (Exception ex)
            {
                // We just ignore errors, so we leave malformed composer.json files for Composer to handle,
                // not us. This prevents us from erroring out when Composer itself might be able to tolerate
                // some errors in the composer.json file.
                _logger.LogWarning(
                    ex,
                    $"Exception caught while trying to deserialize {PhpConstants.ComposerFileName.Hash()}");
            }

            return composerFile?.require?.php?.Value as string;
        }

        private string GetDefaultVersionFromProvider()
        {
            var versionInfo = _versionProvider.GetVersionInfo();
            return versionInfo.DefaultVersion;
        }

        private string GetMaxSatisfyingVersionAndVerify(string version)
        {
            var versionInfo = _versionProvider.GetVersionInfo();
            var maxSatisfyingVersion = SemanticVersionResolver.GetMaxSatisfyingVersion(
                version,
                versionInfo.SupportedVersions);

            if (string.IsNullOrEmpty(maxSatisfyingVersion))
            {
                var exc = new UnsupportedVersionException(
                    PhpConstants.PlatformName,
                    version,
                    versionInfo.SupportedVersions);
                _logger.LogError(
                    exc,
                    $"Exception caught, the version '{version}' is not supported for the PHP platform.");
                throw exc;
            }

            return maxSatisfyingVersion;
        }
    }
}
