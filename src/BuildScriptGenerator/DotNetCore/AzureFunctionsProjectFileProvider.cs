﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Extensions.Logging;
using Microsoft.Oryx.BuildScriptGenerator.Exceptions;
using Microsoft.Oryx.Common.Extensions;

namespace Microsoft.Oryx.BuildScriptGenerator.DotNetCore
{
    internal class AzureFunctionsProjectFileProvider : IProjectFileProvider
    {
        private readonly ILogger<AzureFunctionsProjectFileProvider> _logger;

        // Since this service is registered as a singleton, we can cache the lookup of project file.
        private bool _probedForProjectFile;
        private string _projectFileRelativePath;

        public AzureFunctionsProjectFileProvider(ILogger<AzureFunctionsProjectFileProvider> logger)
        {
            _logger = logger;
        }

        public string GetRelativePathToProjectFile(BuildScriptGeneratorContext context)
        {
            if (_probedForProjectFile)
            {
                return _projectFileRelativePath;
            }

            var sourceRepo = context.SourceRepo;
            string projectFile = null;

            // Check if any of the sub-directories has a .csproj or .fsproj file and if that file has references
            // websdk

            // search for .csproj files
            var projectFiles = GetAllProjectFilesInRepo(
                    sourceRepo,
                    DotNetCoreConstants.CSharpProjectFileExtension);

            if (!projectFiles.Any())
            {
                _logger.LogDebug(
                    "Could not find any files with extension " +
                    $"'{DotNetCoreConstants.CSharpProjectFileExtension}' in repo.");

                // search for .fsproj files
                projectFiles = GetAllProjectFilesInRepo(
                    sourceRepo,
                    DotNetCoreConstants.FSharpProjectFileExtension);

                if (!projectFiles.Any())
                {
                    _logger.LogDebug(
                        "Could not find any files with extension " +
                        $"'{DotNetCoreConstants.FSharpProjectFileExtension}' in repo.");
                    return null;
                }
            }

            var webAppProjects = new List<string>();
            foreach (var file in projectFiles)
            {
                if (IsAspNetCoreWebApplicationProject(sourceRepo, file))
                {
                    webAppProjects.Add(file);
                }
            }

            if (webAppProjects.Count == 0)
            {
                _logger.LogDebug(
                    "Could not find any ASP.NET Core web application projects. " +
                    $"Found the following project files: '{string.Join(" ", projectFiles)}'");
                return null;
            }

            if (webAppProjects.Count > 1)
            {
                var projects = string.Join(", ", webAppProjects);
                throw new InvalidUsageException(
                    "Ambiguity in selecting an ASP.NET Core web application to build. " +
                    $"Found multiple applications: '{projects}'. Use the environment variable " +
                    $"'{EnvironmentSettingsKeys.Project}' to specify the relative path to the project " +
                    "to be deployed.");
            }

            projectFile = webAppProjects[0];

            // Cache the results
            _probedForProjectFile = true;
            _projectFileRelativePath = GetRelativePathToRoot(projectFile, sourceRepo.RootPath);
            return _projectFileRelativePath;
        }

        // To enable unit testing
        internal static bool IsAspNetCoreWebApplicationProject(XDocument projectFileDoc)
        {
            // For reference
            // https://docs.microsoft.com/en-us/visualstudio/msbuild/project-element-msbuild?view=vs-2019

            // Look for the attribute value on Project element first as that is more common
            // Example: <Project Sdk="Microsoft.NET.Sdk.Web/1.0.0">
            var expectedWebSdkName = DotNetCoreConstants.WebSdkName.ToLowerInvariant();
            var sdkAttributeValue = projectFileDoc.XPathEvaluate(
                DotNetCoreConstants.ProjectSdkAttributeValueXPathExpression);
            var sdkName = sdkAttributeValue as string;
            if (!string.IsNullOrEmpty(sdkName) &&
                sdkName.StartsWith(expectedWebSdkName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Example:
            // <Project>
            //    <Sdk Name="Microsoft.NET.Sdk.Web" Version="1.0.0" />
            var sdkNameAttributeValue = projectFileDoc.XPathEvaluate(
                DotNetCoreConstants.ProjectSdkElementNameAttributeValueXPathExpression);
            sdkName = sdkNameAttributeValue as string;

            return sdkName.EqualsIgnoreCase(expectedWebSdkName);
        }

        // To enable unit testing
        internal static string GetRelativePathToRoot(string projectFilePath, string repoRoot)
        {
            var repoRootDir = new DirectoryInfo(repoRoot);
            var projectFileInfo = new FileInfo(projectFilePath);
            var currDir = projectFileInfo.Directory;
            var parts = new List<string>();
            parts.Add(projectFileInfo.Name);

            // Since directory names are case sensitive on non-Windows OSes, try not to use ignore case
            while (!string.Equals(currDir.FullName, repoRootDir.FullName, StringComparison.Ordinal))
            {
                parts.Insert(0, currDir.Name);
                currDir = currDir.Parent;
            }

            return Path.Combine(parts.ToArray());
        }

        private static IEnumerable<string> GetAllProjectFilesInRepo(
            ISourceRepo sourceRepo,
            string projectFileExtension)
        {
            return sourceRepo.EnumerateFiles($"*.{projectFileExtension}", searchSubDirectories: true);
        }

        private static string GetProjectFileAtRoot(ISourceRepo sourceRepo, string projectFileExtension)
        {
            return sourceRepo
                .EnumerateFiles($"*.{projectFileExtension}", searchSubDirectories: false)
                .FirstOrDefault();
        }

        private static bool IsAspNetCore30App(XDocument projectFileDoc)
        {
            var targetFrameworkElement = projectFileDoc.XPathSelectElement(
                DotNetCoreConstants.TargetFrameworkElementXPathExpression);
            if (string.Equals(targetFrameworkElement.Value, DotNetCoreConstants.NetCoreApp30))
            {
                var projectElement = projectFileDoc.XPathSelectElement(
                    DotNetCoreConstants.ProjectSdkAttributeValueXPathExpression);
                return projectElement != null;
            }

            return false;
        }

        private bool IsAspNetCoreWebApplicationProject(ISourceRepo sourceRepo, string projectFile)
        {
            var projFileDoc = XDocument.Load(new StringReader(sourceRepo.ReadFile(projectFile)));
            return IsAspNetCoreWebApplicationProject(projFileDoc);
        }
    }
}
