﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Microsoft.Oryx.Detector
{
    /// <summary>
    /// The default implementation of <see cref="IDetector"/> which invokes the
    /// <see cref="IDetector.GetAllDetectedPlatforms(DetectorContext)"/> on each of the registered
    /// <see cref="IPlatformDetector"/> and returns back a list of <see cref="PlatformDetectorResult"/>.
    /// </summary>
    public class DefaultPlatformDetector : IDetector
    {
        private readonly IEnumerable<IPlatformDetector> _platformDetectors;
        private readonly ILogger<DefaultPlatformDetector> _logger;

        /// <summary>
        /// Creates an instance of <see cref="DefaultPlatformDetector"/>.
        /// </summary>
        /// <param name="platformDetectors">List of <see cref="IPlatformDetector"/>.</param>
        /// <param name="logger">The <see cref="ILogger{DefaultPlatformDetector}"/></param>
        public DefaultPlatformDetector(
            IEnumerable<IPlatformDetector> platformDetectors,
            ILogger<DefaultPlatformDetector> logger)
        {
            _platformDetectors = platformDetectors;
            _logger = logger;
        }

        /// <inheritdoc />
        public IEnumerable<PlatformDetectorResult> GetAllDetectedPlatforms(DetectorContext context)
        {
            var detectedPlatforms = new List<PlatformDetectorResult>();

            foreach (var platformDetector in _platformDetectors)
            {
                _logger.LogDebug($"Detecting platform using '{platformDetector.GetType()}'...");

                if (IsDetectedPlatform(
                    context,
                    platformDetector,
                    out PlatformDetectorResult platformResult))
                {
                    detectedPlatforms.Add(platformResult);
                }
            }

            return detectedPlatforms;
        }

        private bool IsDetectedPlatform(
            DetectorContext context,
            IPlatformDetector platformDetector,
            out PlatformDetectorResult platformResult)
        {
            platformResult = platformDetector.Detect(context);

            if (platformResult == null)
            {
                _logger.LogInformation("Could not detect any platform in the given repository.");
                return false;
            }

            if (string.IsNullOrEmpty(platformResult.PlatformVersion))
            {
                _logger.LogInformation(
                    $"Platform '{platformResult.Platform}' was detected in the given repository, " +
                    $"but no versions were detected.");
            }

            _logger.LogInformation(
                $"Platform '{platformResult.Platform}' was detected with version '{platformResult.PlatformVersion}'.");
            return true;
        }
    }
}