﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Net.Http;
using McMaster.Extensions.CommandLineUtils;

namespace AutoUpdater
{
    [Command(
        "digest",
        Description = "Compares docker image digest between Oryx master branch and GitHub runners Readme file and " +
        "creates a pull requests if they differ.")]
    public class GitHubRunnersCachedImagesCheckCommand
    {
        private readonly HttpClient httpClient = new HttpClient();

        [Argument(0, Description = "The source directory.")]
        [DirectoryExists]
        public string SourceDir { get; set; }

        public int OnExecute(CommandLineApplication app, IConsole console)
        {
            string sourceDir = null;
            if (string.IsNullOrEmpty(SourceDir))
            {
                sourceDir = Directory.GetCurrentDirectory();
            }
            else
            {
                sourceDir = Path.GetFullPath(SourceDir);
            }

            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"Could not find directory '{sourceDir}'.");
            }

            var gitHubRunnersReadMeDigest = GetDigestFromGitHubRunnersReadMe();
            var oryxDockerfileDigest = GetDigestFromOryxGitHubRunnersDockerfile();

            if (string.IsNullOrEmpty(gitHubRunnersReadMeDigest))
            {
                console.Error.WriteLine("GitHub runners digest is empty.");
                return 1;
            }

            if (string.Equals(gitHubRunnersReadMeDigest, oryxDockerfileDigest, StringComparison.OrdinalIgnoreCase))
            {
                console.WriteLine("Digests match. Skipping further action...");
                return 0;
            }

            console.WriteLine("Digests do not match. Creating a pull requesting in upstream branch...");

            var newContentInDockerfile = $"FROM buildpack-deps:stretch@sha256:{gitHubRunnersReadMeDigest}";
            var dockerFileLocation = Path.Combine(
                sourceDir,
                "images",
                "build",
                "Dockerfiles",
                "gitHubRunners.BuildPackDepsStretch.Dockerfile");

            var buildNumber = Environment.GetEnvironmentVariable("BUILD_BUILDNUMBER");
            var newBranchName = $"autoupdater/update.githubrunners.digest.{buildNumber}";
            var forkAccountName = Environment.GetEnvironmentVariable("FORK_ACCOUNT_NAME");
            var accessToken = Environment.GetEnvironmentVariable("AUTOUPDATE_PAT");

            var prDescription = PullRequestHelper.GetDescriptionForCreatingPullRequest(newBranchName);
            var scriptBuilder = new ShellScriptBuilder(cmdSeparator: Environment.NewLine)
                .AddShebang("/bin/bash")
                .AddCommand("set -e")
                .AddCommand($"cd {sourceDir}")
                .AddCommand($"git checkout -b {newBranchName}")
                .AddCommand($"echo '# Auto-generated by Oryx-AutoUpdate build pipeline' > {dockerFileLocation}")
                .AddCommand($"echo '{newContentInDockerfile}' >> {dockerFileLocation}")
                .AddCommand($"git add {dockerFileLocation}")
                .AddCommand($"git commit -m 'Updated GitHub runners digest sha'")
                .AddCommand($"git push -u origin {newBranchName}")
                .AddCommand($"curl -u {forkAccountName}:{accessToken} -X POST " +
                $"-H 'Accept: application/vnd.github.v3+json' " +
                $"https://api.github.com/repos/microsoft/oryx/pulls -d " +
                @$"'{{""title"":""Updated GitHub runners digest"",""head"":""{forkAccountName}:{newBranchName}"",""base"":""master"",""body"":""{prDescription}""}}'");
            var script = scriptBuilder.ToString();

            var scriptPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sh");
            console.WriteLine($"Script generated at {scriptPath}");
            File.WriteAllText(scriptPath, script);

            console.WriteLine("Setting executable permission...");
            var exitCode = ProcessHelper.TrySetExecutableMode(scriptPath);
            if (exitCode != 0)
            {
                console.Error.WriteLine("Error setting executable permission on the script");
                return exitCode;
            }

            console.WriteLine("Running the script...");
            exitCode = ProcessHelper.RunProcess(
                fileName: scriptPath,
                arguments: new string[] { },
                workingDirectory: Path.GetTempPath(),
                // Preserve the output structure and use AppendLine as these handlers
                // are called for each line that is written to the output.
                standardOutputHandler: (sender, args) =>
                {
                    console.WriteLine(args.Data);
                },
                standardErrorHandler: (sender, args) =>
                {
                    console.Error.WriteLine(args.Data);
                },
                waitTimeForExit: null);

            console.WriteLine("Done.");
            return exitCode;
        }

        private string GetDigestFromGitHubRunnersReadMe()
        {
            var url = "https://raw.githubusercontent.com/actions/virtual-environments/main/images/linux/Ubuntu2004-README.md";
            return GetImageDigest(url);
        }

        private string GetDigestFromOryxGitHubRunnersDockerfile()
        {
            var url = "https://raw.githubusercontent.com/microsoft/Oryx/master/images/build/Dockerfiles/gitHubRunners.BuildPackDepsStretch.Dockerfile";
            return GetImageDigest(url);
        }

        private string GetImageDigest(string url)
        {
            var content = httpClient.GetStringAsync(url).Result;
            var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains("buildpack-deps:stretch", StringComparison.OrdinalIgnoreCase))
                {
                    var prefix = "sha256:";
                    var startIndex = line.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                    // sha256 is 64 characters long
                    return line.Substring(startIndex + prefix.Length, 64);
                }
            }

            return null;
        }
    }
}
