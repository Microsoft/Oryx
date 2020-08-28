// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.Oryx.Detector.Ruby
{
    internal static class RubyConstants
    {
        internal const string PlatformName = "ruby";
        internal const string GemToolName = "gem";
        internal const string RubyFileNamePattern = "*.rb";
        internal const string GemFileName = "Gemfile";
        internal const string GemFileLockName = "Gemfile.lock";
        internal const string ConfigRubyFileName = "config.ru";
        public static readonly string[] IisStartupFiles = new[]
        {
            "default.htm", "default.html", "default.asp", "index.htm", "index.html", "iisstart.htm", "default.aspx", "index.php"
        };
    }
}
