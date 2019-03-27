﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.Oryx.BuildScriptGenerator.Exceptions
{
    /// <summary>
    /// Supplied version is not supported.
    /// </summary>
    public class UnsupportedVersionException : InvalidUsageException
    {
        public UnsupportedVersionException(string message)
            : base(message)
        {
        }
    }
}