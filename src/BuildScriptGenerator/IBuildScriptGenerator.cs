﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------
namespace Microsoft.Oryx.BuildScriptGenerator
{
    public interface IBuildScriptGenerator
    {
        /// <summary>
        /// Tries generating a bash script based on the application in source directory.
        /// </summary>
        /// <param name="scriptGeneratorContext">The <see cref="BuildScriptGeneratorContext"/>.</param>
        /// <param name="script">The generated script if the operation was successful.</param>
        /// <returns><c>true</c> if the operation was successful, <c>false</c> otherwise.</returns>
        bool TryGenerateBashScript(BuildScriptGeneratorContext scriptGeneratorContext, out string script);
    }
}