// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Oryx.BuildScriptGenerator.Common;

namespace Microsoft.Oryx.BuildScriptGenerator
{
    public abstract class PlatformInstallerBase
    {
        public PlatformInstallerBase(
            IOptions<BuildScriptGeneratorOptions> commonOptions,
            ILoggerFactory loggerFactory)
        {
            CommonOptions = commonOptions.Value;
            Logger = loggerFactory.CreateLogger(GetType());
        }

        protected BuildScriptGeneratorOptions CommonOptions { get; }

        protected ILogger Logger { get; }

        protected string GetInstallerScriptSnippet(
            string platformName,
            string version,
            string directoryToInstall = null)
        {
            var sdkStorageBaseUrl = GetPlatformBinariesStorageBaseUrl();

            var versionDirInTemp = directoryToInstall;
            if (string.IsNullOrEmpty(versionDirInTemp))
            {
                versionDirInTemp = Path.Combine(CommonOptions.DynamicInstallRootDir, platformName, version);
            }

            var tarFile = $"{version}.tar.gz";
            var snippet = new StringBuilder();
            snippet
                .AppendLine()
                .AppendLine("PLATFORM_SETUP_START=$SECONDS")
                .AppendLine("echo")
                .AppendLine(
                $"echo \"Downloading and extracting '{platformName}' version '{version}' to '{versionDirInTemp}'...\"")
                .AppendLine($"rm -rf {versionDirInTemp}")
                .AppendLine($"mkdir -p {versionDirInTemp}")
                .AppendLine($"cd {versionDirInTemp}")
                .AppendLine("PLATFORM_BINARY_DOWNLOAD_START=$SECONDS")
                .AppendLine(
                $"curl -D headers.txt -SL \"{sdkStorageBaseUrl}/{platformName}/{platformName}-{version}.tar.gz\" " +
                $"--output {tarFile} >/dev/null 2>&1")
                .AppendLine("PLATFORM_BINARY_DOWNLOAD_ELAPSED_TIME=$(($SECONDS - $PLATFORM_BINARY_DOWNLOAD_START))")
                .AppendLine("echo \"Downloaded in $PLATFORM_BINARY_DOWNLOAD_ELAPSED_TIME sec(s).\"")

                // Search header name ignoring case
                .AppendLine("echo Verifying checksum...")
                .AppendLine("headerName=\"x-ms-meta-checksum\"")
                .AppendLine("checksumHeader=$(cat headers.txt | grep -i $headerName: | tr -d '\\r')")

                // Change header and value to lowercase
                .AppendLine("checksumHeader=$(echo $checksumHeader | tr '[A-Z]' '[a-z]')")
                .AppendLine("checksumValue=${checksumHeader#\"$headerName: \"}")
                .AppendLine("rm -f headers.txt")
                .AppendLine($"echo \"$checksumValue {version}.tar.gz\" | sha512sum -c - >/dev/null 2>&1")
                .AppendLine("echo Extracting contents...")
                .AppendLine($"tar -xzf {tarFile} -C .")
                .AppendLine($"rm -f {tarFile}")
                .AppendLine("PLATFORM_SETUP_ELAPSED_TIME=$(($SECONDS - $PLATFORM_SETUP_START))")
                .AppendLine("echo \"Done in $PLATFORM_SETUP_ELAPSED_TIME sec(s).\"")
                .AppendLine("echo")
                .AppendLine("oryxImageDetectorFile=\"/opt/oryx/.imagetype\"")
                .AppendLine($"platformName=\"{platformName}\"")
                .AppendLine($"if [ -f \"$oryxImageDetectorFile\" ] && [ \"$platformName\" = \"dotnet\" ] && grep -q \"vso-focal\" \"$oryxImageDetectorFile\"; then")
                .AppendLine("echo \"image detector file exists, platform is dotnet..\"")
                .AppendLine($"source /opt/tmp/build/vsoSymlinksDotNetCore.sh")
                .AppendLine("fi")
                .AppendLine($"if [ -f \"$oryxImageDetectorFile\" ] && [ \"$platformName\" = \"nodejs\" ] && grep -q \"vso-focal\" \"$oryxImageDetectorFile\"; then")
                .AppendLine("echo \"image detector file exists, platform is nodejs..\"")
                .AppendLine($"mkdir -p /home/codespace/.nodejs")
                .AppendLine($"ln -sfn /opt/nodejs/{version} /home/codespace/.nodejs/current")
                .AppendLine($"ls -la /home/codespace/.nodejs/current")
                .AppendLine("fi")
                .AppendLine($"if [ -f \"$oryxImageDetectorFile\" ] && [ \"$platformName\" = \"php\" ] && grep -q \"vso-focal\" \"$oryxImageDetectorFile\"; then")
                .AppendLine("echo \"image detector file exists, platform is php..\"")
                .AppendLine($"mkdir -p /home/codespace/.php")
                .AppendLine($"ln -sfn /opt/php/{version} /home/codespace/.php/current")
                .AppendLine($"ls -la /home/codespace/.php/current")
                .AppendLine("fi")
                .AppendLine($"if [ -f \"$oryxImageDetectorFile\" ] && [ \"$platformName\" = \"python\" ] && grep -q \"vso-focal\" \"$oryxImageDetectorFile\"; then")
                .AppendLine("echo \"image detector file exists, platform is python..\"")
                .AppendLine($"mkdir -p /home/codespace/.python")
                .AppendLine($"ln -sfn /opt/python/{version} /home/codespace/.python/current")
                .AppendLine($"ls -la /home/codespace/.python/current")
                .AppendLine("fi")
                .AppendLine($"if [ -f \"$oryxImageDetectorFile\" ] && [ \"$platformName\" = \"java\" ] && grep -q \"vso-focal\" \"$oryxImageDetectorFile\"; then")
                .AppendLine("echo \"image detector file exists, platform is java..\"")
                .AppendLine($"mkdir -p /home/codespace/.java")
                .AppendLine($"ln -sfn /opt/java/{version} /home/codespace/.java/current")
                .AppendLine($"ls -la /home/codespace/.java/current")
                .AppendLine("fi")
                .AppendLine($"if [ -f \"$oryxImageDetectorFile\" ] && [ \"$platformName\" = \"ruby\" ] && grep -q \"vso-focal\" \"$oryxImageDetectorFile\"; then")
                .AppendLine("echo \"image detector file exists, platform is ruby..\"")
                .AppendLine($"mkdir -p /home/codespace/.ruby")
                .AppendLine($"ln -sfn /opt/ruby/{version} /home/codespace/.ruby/current")
                .AppendLine($"ls -la /home/codespace/.ruby/current")
                .AppendLine("fi")

                // Write out a sentinel file to indicate downlaod and extraction was successful
                .AppendLine($"echo > {Path.Combine(versionDirInTemp, SdkStorageConstants.SdkDownloadSentinelFileName)}");
            return snippet.ToString();
        }

        protected bool IsVersionInstalled(string lookupVersion, string builtInDir, string dynamicInstallDir)
        {
            var versionsFromDisk = VersionProviderHelper.GetVersionsFromDirectory(builtInDir);
            if (HasVersion(versionsFromDisk))
            {
                Logger.LogDebug(
                    "Version {version} is already installed at directory {installationDir}",
                    lookupVersion,
                    builtInDir);

                return true;
            }

            versionsFromDisk = VersionProviderHelper.GetVersionsFromDirectory(dynamicInstallDir);
            if (HasVersion(versionsFromDisk))
            {
                // Only if there is a sentinel file we want to indicate that a version exists.
                // This is because a user could kill a build midway which might leave the download of an SDK
                // in a corrupt state.
                var sentinelFile = Path.Combine(
                    dynamicInstallDir,
                    lookupVersion,
                    SdkStorageConstants.SdkDownloadSentinelFileName);

                if (File.Exists(sentinelFile))
                {
                    Logger.LogDebug(
                        "Version {version} is already installed at directory {installationDir}",
                        lookupVersion,
                        dynamicInstallDir);

                    return true;
                }

                Logger.LogDebug(
                    "Directory for version {version} was already found at directory {installationDir}, " +
                    "but sentinel file {sentinelFile} was not found.",
                    lookupVersion,
                    dynamicInstallDir,
                    SdkStorageConstants.SdkDownloadSentinelFileName);
            }

            Logger.LogDebug(
                "Version {version} was not found to be installed at {builtInDir} or {dynamicInstallDir}",
                lookupVersion,
                builtInDir,
                dynamicInstallDir);

            return false;

            bool HasVersion(IEnumerable<string> versionsOnDisk)
            {
                return versionsOnDisk.Any(onDiskVersion
                    => string.Equals(lookupVersion, onDiskVersion, StringComparison.OrdinalIgnoreCase));
            }
        }

        private string GetPlatformBinariesStorageBaseUrl()
        {
            var platformBinariesStorageBaseUrl = CommonOptions.OryxSdkStorageBaseUrl;
            if (string.IsNullOrEmpty(platformBinariesStorageBaseUrl))
            {
                throw new InvalidOperationException(
                    $"Environment variable '{SdkStorageConstants.SdkStorageBaseUrlKeyName}' is required.");
            }

            platformBinariesStorageBaseUrl = platformBinariesStorageBaseUrl.TrimEnd('/');
            return platformBinariesStorageBaseUrl;
        }
    }
}
