﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Oryx.BuildScriptGenerator.Common;
using Microsoft.Oryx.Detector;
using Newtonsoft.Json;

namespace Microsoft.Oryx.BuildScriptGeneratorCli
{
    [Command(Name, Description = "Detect all platforms and versions in the given app source directory.")]
    internal class DetectCommand : CommandBase
    {
        public const string Name = "detect";

        [Argument(0, Description = "The source directory. If no value is provided, the current directory is used.")]
        [DirectoryExists]
        public string SourceDir { get; set; }

        [Option(
            "-o|--output",
            CommandOptionType.SingleValue,
            Description = "Output the detected platform data in chosen format. " +
            "Example: json, table. " +
            "If not set, by default output will print out as a table. ")]
        public string OutputFormat { get; set; }

        internal override int Execute(IServiceProvider serviceProvider, IConsole console)
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<DetectCommand>();
            var sourceRepo = new LocalSourceRepo(SourceDir, loggerFactory);
            var ctx = new DetectorContext
            {
                SourceRepo = sourceRepo,
            };

            var detector = serviceProvider.GetRequiredService<IDetector>();
            var detectedPlatformResults = detector.GetAllDetectedPlatforms(ctx);
            using (var timedEvent = logger.LogTimedEvent("DetectCommand"))
            {
                if (detectedPlatformResults == null || !detectedPlatformResults.Any())
                {
                    logger?.LogError($"No platforms and versions detected from source directory: '{SourceDir}'");
                    console.WriteErrorLine($"No platforms and versions detected from source directory: '{SourceDir}'");
                }

                if (!string.IsNullOrEmpty(OutputFormat) 
                    && string.Equals(OutputFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    PrintJsonResult(detectedPlatformResults, console);
                }
                else
                {
                    PrintTableResult(detectedPlatformResults, console);
                }

                return ProcessConstants.ExitSuccess;
            }
        }

        internal override bool IsValidInput(IServiceProvider serviceProvider, IConsole console)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<DetectCommand>>();
            SourceDir = string.IsNullOrEmpty(SourceDir) ? Directory.GetCurrentDirectory() : Path.GetFullPath(SourceDir);

            if (!Directory.Exists(SourceDir))
            {
                logger?.LogError("Could not find the source directory.");
                console.WriteErrorLine($"Could not find the source directory: '{SourceDir}'");

                return false;
            }

            if (!string.IsNullOrEmpty(OutputFormat) 
                && !string.Equals(OutputFormat, "json", StringComparison.OrdinalIgnoreCase) 
                && !string.Equals(OutputFormat, "table", StringComparison.OrdinalIgnoreCase))
            {
                logger?.LogError("Unsupported output format. Supported output formats are: json, table.");
                console.WriteErrorLine($"Unsupported output format: '{OutputFormat}'. " + 
                    "Supported output formats are: json, table.");

                return false;
            }

            return true;
        }

        private void PrintTableResult(IEnumerable<PlatformDetectorResult> detectedPlatformResults, IConsole console)
        {
            var defs = new DefinitionListFormatter();
            if (detectedPlatformResults == null || !detectedPlatformResults.Any())
            {
                defs.AddDefinition("Platform", "Not Detected");
                defs.AddDefinition("PlatformVersion", "Not Detected");
                console.WriteLine(defs.ToString());
                return;
            }

            foreach (var detectedPlatformResult in detectedPlatformResults)
            {
                // This is to filter out the indexed properties from properties variable.
                var propertyInfos = detectedPlatformResult
                    .GetType()
                    .GetProperties()
                    .Where(p => p.GetIndexParameters().Length == 0);

                // Get all properties from a detected platform result and add them to DefinitionListFormatter.
                foreach (var propertyInfo in propertyInfos)
                {
                    var propertyValue = propertyInfo.GetValue(detectedPlatformResult, null);
                    defs.AddDefinition(propertyInfo.Name,
                        propertyValue == null ? "Not Detected" : propertyValue.ToString());
                }
            }

            console.WriteLine(defs.ToString());
        }

        private void PrintJsonResult(IEnumerable<PlatformDetectorResult> detectedPlatformResults, IConsole console)
        {
            if (detectedPlatformResults == null || !detectedPlatformResults.Any())
            {
                console.WriteLine("{}");
                return;
            }

            foreach (var detectedPlatformResult in detectedPlatformResults)
            {
                detectedPlatformResult.PlatformVersion = detectedPlatformResult.PlatformVersion ?? string.Empty;
            }

            console.WriteLine(JsonConvert.SerializeObject(detectedPlatformResults, Formatting.Indented));
        }
    }
}
