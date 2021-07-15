// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.IO;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Extensions.Logging;

namespace Microsoft.Oryx.Detector.DotNetCore
{
    /// <summary>
    /// An implementation of <see cref="IPlatformDetector"/> which detects 
    /// ASP.NET Core Web Application projects, .NET Core Azure Functions projects and
    /// ASP.NET Core Blazor Client projects.
    /// </summary>
    public class DotNetCoreDetector : IDotNetCorePlatformDetector
    {
        private readonly DefaultProjectFileProvider _projectFileProvider;
        private readonly ILogger<DotNetCoreDetector> _logger;

        /// <summary>
        /// Creates an instance of <see cref="DotNetCoreDetector"/>.
        /// </summary>
        /// <param name="projectFileProvider">The <see cref="DefaultProjectFileProvider"/>.</param>
        /// <param name="logger">The <see cref="ILogger{DotNetCoreDetector}"/>.</param>
        public DotNetCoreDetector(DefaultProjectFileProvider projectFileProvider, ILogger<DotNetCoreDetector> logger)
        {
            _projectFileProvider = projectFileProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public PlatformDetectorResult Detect(DetectorContext context)
        {
            var projectFile = _projectFileProvider.GetRelativePathToProjectFile(context);
            if (string.IsNullOrEmpty(projectFile))
            {
                return null;
            }

            var sourceRepo = context.SourceRepo;
            var appDirectory = Path.GetDirectoryName(projectFile);
            var installAOTWorkloads = false;
            var projectFileDoc = XDocument.Load(new StringReader(sourceRepo.ReadFile(projectFile)));
            var targetFrameworkElement = projectFileDoc.XPathSelectElement(
                DotNetCoreConstants.TargetFrameworkElementXPathExpression);
            var targetFramework = targetFrameworkElement?.Value;
            if (string.IsNullOrEmpty(targetFramework))
            {
                _logger.LogDebug(
                    $"Could not find 'TargetFramework' element in the project file.");
                return null;
            }

            var version = GetVersion(targetFramework);

            // Any Blazor WebAssembly app on .NET 6 should have the workload installed.
            // https://github.com/microsoft/Oryx/issues/1026
            if (ProjectFileHelpers.IsBlazorWebAssemblyProject(projectFileDoc)
                && !string.IsNullOrEmpty(version)
                && version.StartsWith("6"))
            {
                installAOTWorkloads = true;
            }
            return new DotNetCorePlatformDetectorResult
            {
                Platform = DotNetCoreConstants.PlatformName,
                PlatformVersion = version,
                ProjectFile = projectFile,
                AppDirectory = appDirectory,
                InstallAOTWorkloads = installAOTWorkloads,
            };
        }

        internal string DetermineRuntimeVersion(string targetFramework)
        {
            // Ex: "netcoreapp2.2" => "2.2"
            targetFramework = targetFramework
                .ToLower()
                .Replace("netcoreapp", string.Empty)
                // For handling .NET 5
                .Replace("net", string.Empty);

            // Ex: "2.2" => 2.2
            if (decimal.TryParse(targetFramework, out var val))
            {
                return val.ToString();
            }

            return null;
        }

        private string GetVersion(string targetFramework)
        {
            var version = DetermineRuntimeVersion(targetFramework);
            if (version != null)
            {
                return version;
            }
            _logger.LogDebug(
                   $"Could not determine runtime version from target framework. ");
            return null;
        }
    }
}