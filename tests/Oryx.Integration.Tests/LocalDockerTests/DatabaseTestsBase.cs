﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Oryx.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Oryx.Integration.Tests.LocalDockerTests
{
    public class DatabaseTestsBase
    {
        protected const string expectedOutput = "[{\"Name\":\"Car\"},{\"Name\":\"Television\"},{\"Name\":\"Table\"}]";

        [CanBeNull]
        protected readonly Fixtures.DbContainerFixtureBase _dbFixture;

        [NotNull]
        protected readonly ITestOutputHelper _output;

        protected DatabaseTestsBase(ITestOutputHelper outputHelper, [CanBeNull] Fixtures.DbContainerFixtureBase dbFixture, int hostPort)
        {
            _output = outputHelper;
            _dbFixture = dbFixture;
            HostPort = hostPort;
            HostSamplesDir = Path.Combine(Directory.GetCurrentDirectory(), "SampleApps");
            HttpClient = new HttpClient();
        }

        protected ITestOutputHelper OutputHelper { get; }

        protected int HostPort { get; }

        protected string HostSamplesDir { get; }

        protected HttpClient HttpClient { get; }

        protected Task RunTestAsync(
            string language,
            string languageVersion,
            string samplePath)
        {
            return RunTestAsync(language, languageVersion, samplePath, databaseServerContainerName: null);
        }

        protected async Task RunTestAsync(
            string language,
            string languageVersion,
            string samplePath,
            string databaseServerContainerName,
            int containerPort = 8000)
        {
            var volume = DockerVolume.Create(samplePath);
            var appDir = volume.ContainerDir;
            var portMapping = $"{HostPort}:{containerPort}";
            var entrypointScript = "./start.sh";
            var script = new ShellScriptBuilder()
                .AddCommand($"cd {appDir}")
                .AddCommand($"oryx -appPath {appDir} -output {entrypointScript}")
                .AddCommand(entrypointScript)
                .ToString();

            var runtimeImageName = $"oryxdevms/{language}-{languageVersion}";
            if (string.Equals(language, "nodejs", StringComparison.OrdinalIgnoreCase))
            {
                runtimeImageName = $"oryxdevms/node-{languageVersion}";
            }

            // For SqlLite scenarios where there is no database container, there wouldn't be any link
            string link = null;
            if (!string.IsNullOrEmpty(databaseServerContainerName))
            {
                link = $"{databaseServerContainerName}:{Constants.InternalDbLinkName}";
            }

            await EndToEndTestHelper.BuildRunAndAssertAppAsync(
                _output,
                volume,
                "oryx", new[] { "build", appDir, "-l", language, "--language-version", languageVersion },
                runtimeImageName,
                _dbFixture?.GetCredentialsAsEnvVars(),
                portMapping,
                link,
                "/bin/sh", new[] { "-c", script },
                async () =>
                {
                    var data = await HttpClient.GetStringAsync($"http://localhost:{HostPort}/");
                    Assert.Equal(expectedOutput, data, ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);
                });
        }

        protected void RunAsserts(Action action, string message)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                _output.WriteLine(message);
                throw;
            }
        }
    }
}
