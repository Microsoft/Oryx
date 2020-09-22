﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.Oryx.Common;
using System.Collections.Generic;

namespace Microsoft.Oryx.Detector.DotNetCore
{
    public class DefaultProjectFileProvider : IProjectFileProvider
    {
        private readonly IEnumerable<IProjectFileProvider> _projectFileProviders;

        public DefaultProjectFileProvider(IEnumerable<IProjectFileProvider> projectFileProviders)
        {
            _projectFileProviders = projectFileProviders;
        }

        public virtual string GetRelativePathToProjectFile(DetectorContext context)
        {
            foreach (var projectFileProvider in _projectFileProviders)
            {
                var projectFile = projectFileProvider.GetRelativePathToProjectFile(context);
                if (!string.IsNullOrEmpty(projectFile))
                {
                    return projectFile;
                }
            }

            return null;
        }
    }
}
