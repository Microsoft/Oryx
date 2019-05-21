﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Oryx.BuildScriptGenerator.Exceptions;
using Microsoft.Oryx.Tests.Common;
using Xunit;

namespace Microsoft.Oryx.BuildScriptGenerator.Tests
{
    public class DefaultBuildScriptGeneratorTest : IClassFixture<TestTempDirTestFixture>
    {
        private const string TestPlatformName = "test";

        private readonly string _tempDirRoot;

        public DefaultBuildScriptGeneratorTest(TestTempDirTestFixture testFixure)
        {
            _tempDirRoot = testFixure.RootDirPath;
        }

        [Fact]
        public void TryGenerateScript_ReturnsTrue_IfNoLanguageIsProvided_AndCanDetectLanguage()
        {
            // Arrange
            var detector = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: "test",
                detectedLanguageVersion: "1.0.0");
            var platform = new TestProgrammingPlatform(
                "test",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "script-content",
                detector: detector);
            var generator = CreateDefaultScriptGenerator(platform);
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: null,
                suppliedLanguageVersion: null);

            // Act
            generator.GenerateBashScript(context, out var generatedScript);

            // Assert
            Assert.Contains("script-content", generatedScript);
            Assert.True(detector.DetectInvoked);
        }

        [Fact]
        public void TryGenerateScript_OnlyProcessProvidedPlatform_IfMultiPlatformIsDisabled()
        {
            // Arrange
            var detector1 = new TestLanguageDetectorSimpleMatch(shouldMatch: true);
            var platform1 = new TestProgrammingPlatform(
                "main",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "script-content",
                detector: detector1);
            var detector2 = new TestLanguageDetectorSimpleMatch(shouldMatch: true);
            var platform2 = new TestProgrammingPlatform(
                "anotherPlatform",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "some code",
                detector: detector2);
            var generator = CreateDefaultScriptGenerator(new[] { platform1, platform2 });
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: "main",
                suppliedLanguageVersion: "1.0.0");
            context.DisableMultiPlatformBuild = true;

            // Act
            generator.GenerateBashScript(context, out var generatedScript);

            // Assert
            Assert.Contains("script-content", generatedScript);
            Assert.DoesNotContain("some code", generatedScript);
        }

        [Fact]
        public void TryGenerateScript_ReturnsTrue_IfLanguageIsProvidedButNoVersion_AndCanDetectVersion()
        {
            // Arrange
            var detector = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: "test",
                detectedLanguageVersion: "1.0.0");
            var platform = new TestProgrammingPlatform(
                "test",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "script-content",
                detector);
            var generator = CreateDefaultScriptGenerator(platform);
            var context = CreateScriptGeneratorContext();
            context.Language = "test";
            context.LanguageVersion = null; // version not provided by user

            // Act
            generator.GenerateBashScript(context, out var generatedScript);

            // Assert
            Assert.Contains("script-content", generatedScript);
            Assert.True(detector.DetectInvoked);
        }

        [Fact]
        public void TryGenerateScript_Throws_IfNoLanguageIsProvided_AndCannotDetectLanguage()
        {
            // Arrange
            var detector = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: null,
                detectedLanguageVersion: null);
            var platform = new TestProgrammingPlatform("test", new[] { "1.0.0" }, detector: detector);
            var generator = CreateDefaultScriptGenerator(platform);
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: null,
                suppliedLanguageVersion: null,
                enableMultiPlatformBuild: true);

            // Act & Assert
            var exception = Assert.Throws<UnsupportedLanguageException>(
                () => generator.GenerateBashScript(context, out var generatedScript));
            Assert.Equal("Could not detect the language from repo.", exception.Message);
            Assert.True(detector.DetectInvoked);
        }

        [Fact]
        public void TryGenerateScript_Throws_IfLanguageIsProvidedButNoVersion_AndCannotDetectVersion()
        {
            // Arrange
            var detector = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: "test",
                detectedLanguageVersion: null);
            var platform = new TestProgrammingPlatform("test", new[] { "1.0.0" }, detector: detector);
            var generator = CreateDefaultScriptGenerator(platform);
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: "test",
                suppliedLanguageVersion: null);

            // Act & Assert
            var exception = Assert.Throws<UnsupportedVersionException>(
                () => generator.GenerateBashScript(context, out var generatedScript));
            Assert.Equal("Couldn't detect a version for the platform 'test' in the repo.", exception.Message);
            Assert.True(detector.DetectInvoked);
        }

        [Fact]
        public void TryGenerateScript_Throws_IfLanguageIsProvided_AndCannotDetectLanguage()
        {
            // Arrange
            var detector = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: null,
                detectedLanguageVersion: null);
            var platform = new TestProgrammingPlatform("test1", new[] { "1.0.0" }, detector: detector);
            var generator = CreateDefaultScriptGenerator(platform);
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: "test2",
                suppliedLanguageVersion: null);

            // Act & Assert
            var exception = Assert.Throws<UnsupportedLanguageException>(
                () => generator.GenerateBashScript(context, out var generatedScript));
            Assert.Equal("'test2' platform is not supported. Supported platforms are: test1", exception.Message);
        }

        [Fact]
        public void TryGenerateScript_Throws_IfLanguageIsProvidedButDisabled()
        {
            // Arrange
            var detector = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: "test",
                detectedLanguageVersion: "1.0.0");
            var platform = new TestProgrammingPlatform("test", new[] { "1.0.0" }, detector: detector, enabled: false);
            var generator = CreateDefaultScriptGenerator(platform);
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: "test",
                suppliedLanguageVersion: "1.0.0");

            // Act & Assert
            var exception = Assert.Throws<UnsupportedLanguageException>(
                () => generator.GenerateBashScript(context, out var generatedScript));
        }

        [Fact]
        public void TryGenerateScript_Throws_IfCanDetectLanguageVersion_AndLanguageVersionIsUnsupported()
        {
            // Arrange
            var detector = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: "test",
                detectedLanguageVersion: "2.0.0"); // Unsupported version
            var platform = new TestProgrammingPlatform(
                "test",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "script-content",
                detector);
            var generator = CreateDefaultScriptGenerator(platform);
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: null,
                suppliedLanguageVersion: null);

            // Act & Assert
            var exception = Assert.Throws<UnsupportedVersionException>(
                () => generator.GenerateBashScript(context, out var generatedScript));
            Assert.Equal(
                "The 'test' version '2.0.0' is not supported. Supported versions are: 1.0.0",
                exception.Message);
            Assert.True(detector.DetectInvoked);
        }

        [Fact]
        public void TryGenerateScript_Throws_IfSuppliedLanguageIsUnsupported()
        {
            // Arrange
            var detector = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: "test",
                detectedLanguageVersion: "1.0.0");
            var platform = new TestProgrammingPlatform(
                "test",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "script-content",
                detector);
            var generator = CreateDefaultScriptGenerator(platform);
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: "unsupported",
                suppliedLanguageVersion: "1.0.0");

            // Act & Assert
            var exception = Assert.Throws<UnsupportedLanguageException>(
                () => generator.GenerateBashScript(context, out var generatedScript));
            Assert.Equal(
                "'unsupported' platform is not supported. Supported platforms are: test",
                exception.Message);
            Assert.False(detector.DetectInvoked);
        }

        [Fact]
        public void TryGenerateScript_Throws_IfSuppliedLanguageVersionIsUnsupported()
        {
            // Arrange
            var detector = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: "test",
                detectedLanguageVersion: "1.0.0");
            var platform = new TestProgrammingPlatform(
                "test",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "script-content",
                detector);
            var generator = CreateDefaultScriptGenerator(platform);
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: "test",
                suppliedLanguageVersion: "2.0.0"); //unsupported version

            // Act & Assert
            var exception = Assert.Throws<UnsupportedVersionException>(
                () => generator.GenerateBashScript(context, out var generatedScript));
            Assert.Equal(
                "The 'test' version '2.0.0' is not supported. Supported versions are: 1.0.0",
                exception.Message);
            Assert.False(detector.DetectInvoked);
        }

        [Fact]
        public void TryGenerateScript_ReturnsFalse_IfGeneratorTryGenerateScript_IsFalse()
        {
            // Arrange
            var detector = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: "test",
                detectedLanguageVersion: "1.0.0");
            var platform = new TestProgrammingPlatform(
                "test",
                new[] { "1.0.0" },
                canGenerateScript: false,
                scriptContent: null,
                detector);
            var generator = CreateDefaultScriptGenerator(platform);
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: null,
                suppliedLanguageVersion: null);

            // Act & Assert
            var exception = Assert.Throws<UnsupportedLanguageException>(
                () => generator.GenerateBashScript(context, out var generatedScript));
            Assert.Equal("Could not detect the language from repo.", exception.Message);
            Assert.True(detector.DetectInvoked);
        }

        [Fact]
        public void TryGenerateScript_CallsDetector_IfMultiPlatformIsOff_AndNoLangProvided()
        {
            // Arrange
            var detector = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: "test",
                detectedLanguageVersion: "1.0.0");
            var platform = new TestProgrammingPlatform(
                "test",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "script-content",
                detector);
            var generator = CreateDefaultScriptGenerator(platform);
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: null,
                suppliedLanguageVersion: null,
                enableMultiPlatformBuild: false);

            // Act & Assert
            generator.GenerateBashScript(context, out var generatedScript);
            Assert.True(detector.DetectInvoked);
        }

        [Fact]
        public void TryGenerateScript_DoesntCallDetector_IfMultiPlatformIsOff_AndLangProvided()
        {
            // Arrange
            var detector = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: "test",
                detectedLanguageVersion: "1.0.0");
            var platform = new TestProgrammingPlatform(
                "test",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "script-content",
                detector);

            var detector2 = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: "test2",
                detectedLanguageVersion: "1.0.0");
            var platform2 = new TestProgrammingPlatform(
                "test2",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "script-content",
                detector2);

            var generator = CreateDefaultScriptGenerator(new[] { platform, platform2 });
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: "test",
                suppliedLanguageVersion: "1.0.0",
                enableMultiPlatformBuild: false);

            // Act & Assert
            generator.GenerateBashScript(context, out var generatedScript);
            Assert.False(detector.DetectInvoked);
            Assert.False(detector2.DetectInvoked);
        }

        [Fact]
        public void TryGenerateScript_CallsDetector_IfMultiPlatformIsOn_AndLangProvided()
        {
            // Arrange
            var detector = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: "test",
                detectedLanguageVersion: "1.0.0");
            var platform = new TestProgrammingPlatform(
                "test",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "script-content",
                detector);

            var detector2 = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: "test2",
                detectedLanguageVersion: "1.0.0");
            var platform2 = new TestProgrammingPlatform(
                "test2",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "script-content",
                detector2);

            var generator = CreateDefaultScriptGenerator(new[] { platform, platform2 });
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: "test",
                suppliedLanguageVersion: "1.0.0",
                enableMultiPlatformBuild: true);

            // Act & Assert
            generator.GenerateBashScript(context, out var generatedScript);
            Assert.False(detector.DetectInvoked);
            Assert.True(detector2.DetectInvoked);
        }

        [Fact]
        public void GeneratesScript_UsingTheFirstplatform_WhichCanGenerateScript()
        {
            // Arrange
            var detector = new TestLanguageDetectorUsingLangName(
                detectedLanguageName: null,
                detectedLanguageVersion: null);
            var platform1 = new TestProgrammingPlatform(
                "lang1",
                new[] { "1.0.0" },
                canGenerateScript: false,
                scriptContent: null,
                detector);
            var platform2 = new TestProgrammingPlatform(
                "lang2",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "script-content",
                detector);
            var generator = CreateDefaultScriptGenerator(
                new[] { platform1, platform2 });
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: "lang2",
                suppliedLanguageVersion: "1.0.0");

            // Act
            generator.GenerateBashScript(context, out var generatedScript);

            // Assert
            Assert.Contains("script-content", generatedScript);
            Assert.False(detector.DetectInvoked);
        }

        [Fact]
        public void GeneratesScript_AddsSnippetsForMultiplePlatforms()
        {
            // Arrange
            var platform1 = new TestProgrammingPlatform(
                languageName: "lang1",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "ABCDEFG",
                detector: new TestLanguageDetectorSimpleMatch(
                    shouldMatch: true,
                    language: "lang1",
                    languageVersion: "1.0.0"));
            var platform2 = new TestProgrammingPlatform(
                languageName: "lang2",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "123456",
                detector: new TestLanguageDetectorSimpleMatch(
                    shouldMatch: true,
                    language: "lang2",
                    languageVersion: "1.0.0"));
            var generator = CreateDefaultScriptGenerator(new[] { platform1, platform2 });
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: "lang1",
                suppliedLanguageVersion: "1.0.0",
                enableMultiPlatformBuild: true);

            // Act
            generator.GenerateBashScript(context, out var generatedScript);

            // Assert
            Assert.Contains("ABCDEFG", generatedScript);
            Assert.Contains("123456", generatedScript);
        }

        [Fact]
        public void GeneratesScript_AddsSnippetsForOnePlatform_OtherIsDisabled()
        {
            // Arrange
            var platform1 = new TestProgrammingPlatform(
                "test",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "ABCDEFG",
                detector: new TestLanguageDetectorSimpleMatch(shouldMatch: true));
            var platform2 = new TestProgrammingPlatform(
                "test",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "123456",
                detector: new TestLanguageDetectorSimpleMatch(shouldMatch: true),
                enabled: false);
            var generator = CreateDefaultScriptGenerator(
                new[] { platform1, platform2 });
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: "test",
                suppliedLanguageVersion: "1.0.0");

            // Act
            generator.GenerateBashScript(context, out var generatedScript);

            // Assert
            Assert.Contains("ABCDEFG", generatedScript);
            Assert.DoesNotContain("123456", generatedScript);
        }

        [Fact]
        public void GetCompatiblePlatforms_ReturnsOnlyPlatforms_ParticipatingIn_MultiPlatformBuilds()
        {
            // Arrange
            var platform1 = new TestProgrammingPlatform(
                languageName: "lang1",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "ABCDEFG",
                detector: new TestLanguageDetectorSimpleMatch(
                    shouldMatch: true,
                    language: "lang1",
                    languageVersion: "1.0.0"));
            var platform2 = new TestProgrammingPlatform(
                languageName: "lang2",
                new[] { "1.0.0" },
                canGenerateScript: true,
                scriptContent: "123456",
                detector: new TestLanguageDetectorSimpleMatch(
                    shouldMatch: true,
                    language: "lang2",
                    languageVersion: "1.0.0"),
                platformIsEnabledForMultiPlatformBuild: false); // This platform explicitly opts out
            var generator = CreateDefaultScriptGenerator(new[] { platform1, platform2 });
            var context = CreateScriptGeneratorContext(
                suppliedLanguageName: "lang1",
                suppliedLanguageVersion: "1.0.0",
                enableMultiPlatformBuild: true);

            // Act
            var compatiblePlatforms = generator.GetCompatiblePlatforms(context);

            // Assert
            Assert.NotNull(compatiblePlatforms);
            Assert.Equal(2, compatiblePlatforms.Count);
        }

        [Fact]
        public void Checkers_AreAppliedCorrectly()
        {
            // Arrange
            var repoWarning = new CheckerMessage("some repo warning");
            IChecker[] checkers = { new TestChecker(() => new[] { repoWarning }) };

            var platformVersion = "1.0.0";
            var detector = new TestLanguageDetectorSimpleMatch(true, TestPlatformName, platformVersion);
            var platform = new TestProgrammingPlatform(
                TestPlatformName, new[] { platformVersion }, true, "script-content", detector);

            var generator = CreateDefaultScriptGenerator(new[] { platform }, checkers);
            var context = CreateScriptGeneratorContext(TestPlatformName, platformVersion);

            var messages = new List<ICheckerMessage>();

            // Act
            // Return value of TryGenerateBashScript is irrelevant - messages should be added even if build fails
            generator.GenerateBashScript(context, out var generatedScript, messages);

            // Assert
            Assert.Single(messages);
            Assert.Equal(repoWarning, messages.First());
        }

        [Fact]
        public void Checkers_DontFailTheBuild_WhenTheyThrow()
        {
            // Arrange
            bool checkerRan = false;
            IChecker[] checkers = { new TestChecker(() =>
            {
                checkerRan = true;
                throw new Exception("checker failed");
            }) };

            var platformVersion = "1.0.0";
            var detector = new TestLanguageDetectorSimpleMatch(true, TestPlatformName, platformVersion);
            var scriptContent = "script-content";
            var platform = new TestProgrammingPlatform(
                TestPlatformName, new[] { platformVersion }, true, scriptContent, detector);

            var generator = CreateDefaultScriptGenerator(new[] { platform }, checkers);
            var context = CreateScriptGeneratorContext(TestPlatformName, platformVersion);

            var messages = new List<ICheckerMessage>();

            // Act
            generator.GenerateBashScript(context, out var generatedScript, messages);

            // Assert
            Assert.True(checkerRan);
        }

        private string CreateNewDir()
        {
            return Directory.CreateDirectory(Path.Combine(_tempDirRoot, Guid.NewGuid().ToString("N"))).FullName;
        }

        private DefaultBuildScriptGenerator CreateDefaultScriptGenerator(IProgrammingPlatform platform)
        {
            return CreateDefaultScriptGenerator(new[] { platform });
        }

        private DefaultBuildScriptGenerator CreateDefaultScriptGenerator(
            IProgrammingPlatform[] platforms,
            IEnumerable<IChecker> checkers = null)
        {
            return new DefaultBuildScriptGenerator(
                platforms,
                new TestEnvironmentSettingsProvider(),
                checkers,
                NullLogger<DefaultBuildScriptGenerator>.Instance);
        }

        private static BuildScriptGeneratorContext CreateScriptGeneratorContext(
            string suppliedLanguageName = null,
            string suppliedLanguageVersion = null,
            bool enableMultiPlatformBuild = false)
        {
            return new BuildScriptGeneratorContext
            {
                Language = suppliedLanguageName,
                LanguageVersion = suppliedLanguageVersion,
                SourceRepo = new TestSourceRepo(),
                DisableMultiPlatformBuild = !enableMultiPlatformBuild
            };
        }

        private class TestLanguageDetectorUsingLangName : ILanguageDetector
        {
            private readonly string _languageName;
            private readonly string _languageVersion;

            public TestLanguageDetectorUsingLangName(string detectedLanguageName, string detectedLanguageVersion)
            {
                _languageName = detectedLanguageName;
                _languageVersion = detectedLanguageVersion;
            }

            public bool DetectInvoked { get; private set; }

            public LanguageDetectorResult Detect(ISourceRepo sourceRepo)
            {
                DetectInvoked = true;

                if (!string.IsNullOrEmpty(_languageName))
                {
                    return new LanguageDetectorResult
                    {
                        Language = _languageName,
                        LanguageVersion = _languageVersion,
                    };
                }
                return null;
            }
        }

        private class TestLanguageDetectorSimpleMatch : ILanguageDetector
        {
            private readonly string _languageVersion;
            private bool _shouldMatch;
            private readonly string _language;

            public TestLanguageDetectorSimpleMatch(
                bool shouldMatch,
                string language = "universe",
                string languageVersion = "42")
            {
                _shouldMatch = shouldMatch;
                _language = language;
                _languageVersion = languageVersion;
            }

            public bool DetectInvoked { get; private set; }

            public LanguageDetectorResult Detect(ISourceRepo sourceRepo)
            {
                DetectInvoked = true;

                if (_shouldMatch)
                {
                    return new LanguageDetectorResult
                    {
                        Language = _language,
                        LanguageVersion = _languageVersion
                    };
                }
                else
                {
                    return null;
                }
            }
        }

        private class TestProgrammingPlatform : IProgrammingPlatform
        {
            private readonly bool? _canGenerateScript;
            private readonly string _scriptContent;
            private readonly ILanguageDetector _detector;
            private bool _enabled;
            private bool _platformIsEnabledForMultiPlatformBuild;

            public TestProgrammingPlatform(
                string languageName,
                string[] languageVersions,
                bool? canGenerateScript = null,
                string scriptContent = null,
                ILanguageDetector detector = null,
                bool enabled = true,
                bool platformIsEnabledForMultiPlatformBuild = true)
            {
                Name = languageName;
                SupportedLanguageVersions = languageVersions;
                _canGenerateScript = canGenerateScript;
                _scriptContent = scriptContent;
                _detector = detector;
                _enabled = enabled;
                _platformIsEnabledForMultiPlatformBuild = platformIsEnabledForMultiPlatformBuild;
            }

            public string Name { get; }

            public IEnumerable<string> SupportedLanguageVersions { get; }

            public LanguageDetectorResult Detect(ISourceRepo sourceRepo)
            {
                return _detector.Detect(sourceRepo);
            }

            public BuildScriptSnippet GenerateBashBuildScriptSnippet(
                BuildScriptGeneratorContext scriptGeneratorContext)
            {
                if (_canGenerateScript == true)
                {
                    return new BuildScriptSnippet { BashBuildScriptSnippet = _scriptContent };
                }

                return null;
            }

            public string GenerateBashRunScript(RunScriptGeneratorOptions runScriptGeneratorOptions)
            {
                throw new NotImplementedException();
            }

            public string GenerateBashRunTimeInstallationScript(RunTimeInstallationScriptGeneratorOptions options)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<string> GetDirectoriesToExcludeFromCopyToIntermediateDir(
                BuildScriptGeneratorContext scriptGeneratorContext)
            {
                return Array.Empty<string>();
            }

            public IEnumerable<string> GetDirectoriesToExcludeFromCopyToBuildOutputDir(
                BuildScriptGeneratorContext scriptGeneratorContext)
            {
                return Array.Empty<string>();
            }

            public bool IsCleanRepo(ISourceRepo repo)
            {
                return true;
            }

            public bool IsEnabled(BuildScriptGeneratorContext scriptGeneratorContext)
            {
                return _enabled;
            }

            public void SetRequiredTools(
                ISourceRepo sourceRepo,
                string targetPlatformVersion,
                IDictionary<string, string> toolsToVersion)
            {
                toolsToVersion.Add(Name, SupportedLanguageVersions.First());
            }

            public void SetVersion(BuildScriptGeneratorContext context, string version)
            {
            }

            public bool IsEnabledForMultiPlatformBuild(BuildScriptGeneratorContext scriptGeneratorContext)
            {
                return _platformIsEnabledForMultiPlatformBuild;
            }
        }

        [Checker(TestPlatformName)]
        private class TestChecker : IChecker
        {
            private readonly Func<IEnumerable<ICheckerMessage>> _sourceRepoMessageProvider;
            private readonly Func<IEnumerable<ICheckerMessage>> _toolVersionMessageProvider;

            public TestChecker(
                Func<IEnumerable<ICheckerMessage>> repoMessageProvider = null,
                Func<IEnumerable<ICheckerMessage>> toolMessageProvider = null)
            {
                _sourceRepoMessageProvider  = repoMessageProvider ?? (() => Enumerable.Empty<ICheckerMessage>());
                _toolVersionMessageProvider = toolMessageProvider ?? (() => Enumerable.Empty<ICheckerMessage>());
            }

            public IEnumerable<ICheckerMessage> CheckSourceRepo(ISourceRepo repo) =>
                _sourceRepoMessageProvider();

            public IEnumerable<ICheckerMessage> CheckToolVersions(IDictionary<string, string> tools) =>
                _toolVersionMessageProvider();
        }

        private class TestSourceRepo : ISourceRepo
        {
            public string RootPath => string.Empty;

            public bool FileExists(params string[] paths)
            {
                throw new NotImplementedException();
            }

            public bool DirExists(params string[] paths)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<string> EnumerateFiles(string searchPattern, bool searchSubDirectories)
            {
                throw new NotImplementedException();
            }

            public string ReadFile(params string[] paths)
            {
                throw new NotImplementedException();
            }

            public string[] ReadAllLines(params string[] paths)
            {
                throw new NotImplementedException();
            }

            public string GetGitCommitId() => null;
        }
    }
}