﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.Oryx.BuildScriptGenerator.Java
{
    internal static class JavaConstants
    {
        public const string JavaLtsVersion = JavaVersions.JavaVersion;
        public const string PlatformName = "java";
        public const string InstalledJavaVersionsDir = "/opt/java";

        public const string MavenName = "maven";
        public const string MavenVersion = JavaVersions.MavenVersion;
        public const string InstalledMavenVersionsDir = "/opt/maven";
    }
}