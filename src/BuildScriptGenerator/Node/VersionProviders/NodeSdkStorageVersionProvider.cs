﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Net.Http;

namespace Microsoft.Oryx.BuildScriptGenerator.Node
{
    internal class NodeSdkStorageVersionProvider : SdkStorageVersionProviderBase, INodeVersionProvider
    {
        private PlatformVersionInfo _platformVersionInfo;

        public NodeSdkStorageVersionProvider(IEnvironment environment, IHttpClientFactory httpClientFactory)
            : base(environment, httpClientFactory)
        {
        }

        // To enable unit testing
        public virtual PlatformVersionInfo GetVersionInfo()
        {
            if (_platformVersionInfo == null)
            {
                _platformVersionInfo = GetAvailableVersionsFromStorage(
                    platformName: "nodejs",
                    versionMetadataElementName: "Version");
            }

            return _platformVersionInfo;
        }
    }
}