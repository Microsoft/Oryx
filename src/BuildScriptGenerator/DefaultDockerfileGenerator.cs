﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Oryx.BuildScriptGenerator.Exceptions;
using Microsoft.Oryx.BuildScriptGenerator.Resources;

namespace Microsoft.Oryx.BuildScriptGenerator
{
    internal class DefaultDockerfileGenerator : IDockerfileGenerator
    {
        private readonly ICompatiblePlatformDetector _platformDetector;
        private readonly ILogger<DefaultDockerfileGenerator> _logger;

        private readonly IDictionary<string, IList<string>> _slimPlatformVersions =
            new Dictionary<string, IList<string>>()
            {
                { "dotnetcore", new List<string>() { "2.1" } },
                { "node",   new List<string>() { "8.16", "10.16" } },
                { "python", new List<string>() { "3.7" } },
            };

        private readonly string _templateDockerfile =
            @"ARG RUNTIME=RUN_IMAGE:RUN_TAG

FROM mcr.microsoft.com/oryx/build:BUILD_TAG as build
WORKDIR /app
COPY . .
RUN oryx build /app

FROM mcr.microsoft.com/oryx/${RUNTIME}
COPY --from=build /app /app
RUN cd /app && oryx
ENTRYPOINT [""/app/run.sh""]";

        public DefaultDockerfileGenerator(
            ICompatiblePlatformDetector platformDetector,
            ILogger<DefaultDockerfileGenerator> logger)
        {
            _platformDetector = platformDetector;
            _logger = logger;
        }

        public string GenerateDockerfile(DockerfileContext ctx)
        {
            var buildImageTag = "slim";
            var runImage = string.Empty;
            var runImageTag = string.Empty;
            var compatiblePlatforms = GetCompatiblePlatforms(ctx);
            if (!compatiblePlatforms.Any())
            {
                throw new UnsupportedLanguageException(Labels.UnableToDetectLanguageMessage);
            }

            foreach (var platformAndVersion in compatiblePlatforms)
            {
                var platform = platformAndVersion.Key;
                var version = platformAndVersion.Value;
                if (!_slimPlatformVersions.ContainsKey(platform.Name) ||
                    !_slimPlatformVersions[platform.Name].Any(v => version.StartsWith(v)))
                {
                    buildImageTag = "latest";
                }

                runImage = platform.Name == "dotnet" ? "dotnetcore" : platform.Name;
                runImageTag = GenerateRuntimeTag(version);
            }

            return _templateDockerfile.Replace("RUN_IMAGE", runImage)
                                      .Replace("RUN_TAG", runImageTag)
                                      .Replace("BUILD_TAG", buildImageTag);
        }

        private IDictionary<IProgrammingPlatform, string> GetCompatiblePlatforms(DockerfileContext ctx)
        {
            return _platformDetector.GetCompatiblePlatforms(ctx, ctx.Platform, ctx.PlatformVersion);
        }

        private bool IsEnabledForMultiPlatformBuild(IProgrammingPlatform platform, DockerfileContext ctx)
        {
            if (ctx.DisableMultiPlatformBuild)
            {
                return false;
            }

            return platform.IsEnabledForMultiPlatformBuild(ctx);
        }

        private string GenerateRuntimeTag(string version)
        {
            var split = version.Split('.');
            if (split.Length < 3)
            {
                return version;
            }

            if (split[1] == "0")
            {
                return split[0];
            }

            return $"{split[0]}.{split[1]}";
        }
    }
}
