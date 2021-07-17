// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.Oryx.BuildScriptGenerator
{
    public static class ManifestFilePropertyKeys
    {
        public const string OperationId = nameof(OperationId);

        public const string PhpVersion = nameof(PhpVersion);

        public const string NodeVersion = nameof(NodeVersion);

        public const string DotNetCoreRuntimeVersion = nameof(DotNetCoreRuntimeVersion);

        public const string DotNetCoreSdkVersion = nameof(DotNetCoreSdkVersion);

        public const string OutputType = nameof(OutputType);

        internal const string PythonVersion = nameof(PythonVersion);

        internal const string HugoVersion = nameof(HugoVersion);

        internal const string RubyVersion = nameof(RubyVersion);

        internal const string PlatformName = nameof(PlatformName);

        internal const string SourceDirectoryInBuildContainer = nameof(SourceDirectoryInBuildContainer);

        internal const string CompressDestinationDir = nameof(CompressDestinationDir);
    }
}