﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.Oryx.Detector.Exceptions
{
    /// <summary>
    /// Exception to identify user errors, as opposed to system ones.
    /// Its message must be user friendly, and can be displayed directly to the user.
    /// </summary>
    public class InvalidProjectFileException : DetectorException
    {
        public InvalidProjectFileException(string message)
            : base(message)
        {
        }
    }
}