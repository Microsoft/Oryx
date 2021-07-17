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

            var outputTypeElement = projectFileDoc.XPathSelectElement(
                DotNetCoreConstants.OutputTypeElementXPathExpression);
            var outputType = outputTypeElement?.Value;

            if (!string.IsNullOrEmpty(outputType))
            {
                if (outputType.ToLower() == "library")
                {
                    outputType = "in-process";
                }
                else if (outputType.ToLower() == "exe")
                {
                    outputType = "isolated";
                }
            }


            var version = GetVersion(targetFramework);

            return new DotNetCorePlatformDetectorResult
            {
                Platform = DotNetCoreConstants.PlatformName,
                PlatformVersion = version,
                ProjectFile = projectFile,
                AppDirectory = appDirectory,
                OutputType = outputType,
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