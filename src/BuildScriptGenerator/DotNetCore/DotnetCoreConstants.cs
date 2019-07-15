﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.Oryx.BuildScriptGenerator.DotNetCore
{
    public static class DotNetCoreConstants
    {
        public const string LanguageName = "dotnet";
        public const string CSharpProjectFileExtension = "csproj";
        public const string FSharpProjectFileExtension = "fsproj";
        public const string GlobalJsonFileName = "global.json";

        public const string NetCoreApp10 = "netcoreapp1.0";
        public const string NetCoreApp11 = "netcoreapp1.1";
        public const string NetCoreApp20 = "netcoreapp2.0";
        public const string NetCoreApp21 = "netcoreapp2.1";
        public const string NetCoreApp22 = "netcoreapp2.2";
        public const string NetCoreApp30 = "netcoreapp3.0";

        public const string OryxOutputPublishDirectory = "oryx_publish_output";

        public const string AspNetCorePackageReference = "Microsoft.AspNetCore";
        public const string AspNetCoreAllPackageReference = "Microsoft.AspNetCore.All";
        public const string AspNetCoreAppPackageReference = "Microsoft.AspNetCore.App";

        public const string ProjectFileLanguageDetectorProperty = "ProjectFile";

        public const string DotNetSdkName = "Microsoft.NET.Sdk";
        public const string DotNetWebSdkName = "Microsoft.NET.Sdk.Web";
        public const string ProjectSdkAttributeValueXPathExpression = "string(/Project/@Sdk)";
        public const string ProjectSdkElementNameAttributeValueXPathExpression = "string(/Project/Sdk/@Name)";
        public const string TargetFrameworkElementXPathExpression = "/Project/PropertyGroup/TargetFramework";
        public const string AssemblyNameXPathExpression = "/Project/PropertyGroup/AssemblyName";
        public const string PackageReferenceXPathExpression = "/Project/ItemGroup/PackageReference";

        public const string DefaultMSBuildConfiguration = "Release";

        public const string ProjectBuildPropertyKey = "project";
        public const string ProjectBuildPropertyKeyDocumentation = "Relative path to the project file to build.";

        public const string AzureFunctionsVersionElementXPathExpression =
            "/Project/PropertyGroup/AzureFunctionsVersion";
        public const string AzureFunctionsPackageReference = "Microsoft.NET.Sdk.Functions";
    }
}