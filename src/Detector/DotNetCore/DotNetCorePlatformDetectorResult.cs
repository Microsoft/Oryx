// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.Oryx.Detector.DotNetCore
{
    /// <summary>
    /// Represents a <see cref="PlatformDetectorResult"/> returned by the <see cref="DotNetCoreDetector"/> and
    /// contains additional information related to the detected applications.
    /// </summary>
    public class DotNetCorePlatformDetectorResult : PlatformDetectorResult
    {
        /// <summary>
        /// Gets or sets the relative path to the location of the project file.
        /// </summary>
        /// <example>
        /// "src/ShoppingWeb/ShoppingWeb.csproj"
        /// </example>
        public string ProjectFile { get; set; }

        public string SdkVersion { get; set; }

        public string OutputType { get; set; }
    }
}
