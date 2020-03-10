﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;

namespace Microsoft.Oryx.BuildScriptGenerator
{
    public static class DirectoryHelper
    {
        /// <summary>
        /// Check if the two directory paths are the same. Is case-sensitive.
        /// </summary>
        /// <param name="dir1"></param>
        /// <param name="dir2"></param>
        /// <returns></returns>
        public static bool AreSameDirectories(string dir1, string dir2)
        {
            var dir1Path = new DirectoryInfo(dir1).FullName.Trim(Path.DirectorySeparatorChar);
            var dir2Path = new DirectoryInfo(dir2).FullName.Trim(Path.DirectorySeparatorChar);

            // We want it to be case-sensitive
            return dir1Path.Equals(dir2Path);
        }

        /// <summary>
        /// Checks if <paramref name="subDir"/> is a sub-directory of <paramref name="parentDir"/>.
        /// </summary>
        /// <param name="subDir"></param>
        /// <param name="parentDir"></param>
        /// <returns></returns>
        public static bool IsSubDirectory(string subDir, string parentDir)
        {
            var parentDirSegments = parentDir.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var subDirSegments = subDir.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

            if (subDirSegments.Length <= parentDirSegments.Length)
            {
                return false;
            }

            // If subDir is really a subset of parentDir, then we should expect all
            // segments of parentDir appearing in subDir and in exact order.
            for (var i = 0; i < parentDirSegments.Length; i++)
            {
                // we want case-sensitive search
                if (!string.Equals(parentDirSegments[i], subDirSegments[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
