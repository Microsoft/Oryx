// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.Oryx.Detector.DotNetCore
{
    internal static class DotNetCoreConstants
    {
        public const string PlatformName = "dotnet";
        public const string CSharpProjectFileExtension = "csproj";
        public const string FSharpProjectFileExtension = "fsproj";
        public const string GlobalJsonFileName = "global.json";

        public const string DotNetSdkName = "Microsoft.NET.Sdk";
        public const string DotNetWebSdkName = "Microsoft.NET.Sdk.Web";
        public const string ProjectSdkAttributeValueXPathExpression = "string(/Project/@Sdk)";
        public const string ProjectSdkElementNameAttributeValueXPathExpression = "string(/Project/Sdk/@Name)";
        public const string TargetFrameworkElementXPathExpression = "/Project/PropertyGroup/TargetFramework";
        public const string AssemblyNameXPathExpression = "/Project/PropertyGroup/AssemblyName";
        public const string OutputTypeElementXPathExpression = "/Project/PropertyGroup/OutputType";
        public const string PackageReferenceXPathExpression = "/Project/ItemGroup/PackageReference";

        public const string ProjectBuildPropertyKey = "project";

        public const string AzureFunctionsVersionElementXPathExpression =
            "/Project/PropertyGroup/AzureFunctionsVersion";

        public const string AzureFunctionsPackageReference = "Microsoft.NET.Sdk.Functions";

        public const string AzureBlazorWasmPackageReference = "Microsoft.AspNetCore.Components.WebAssembly";
    }
}