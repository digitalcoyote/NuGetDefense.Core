using FluentAssertions;
using NuGetDefense;
using NuGetDefense.Core;
using Xunit;

namespace CoreTests
{
    public class MsBuildLoggerTests
    {
        private const string Code = "ND0001";
        private const string Message = "Dependency issue detected.";
        private const int LineNumber = 17;
        private const int LinePosition = 3;

        [Fact]
        public void Log_WithNullFile_UsesFallbackFileName()
        {
            // Arrange
            string? file = null;
            var category = MsBuild.Category.Warning;

            // Act
            var result = MsBuild.Log(file, category, Code, LineNumber, LinePosition, Message);

            // Assert
            result.Should().Be($"NuGetDefense({LineNumber},{LinePosition}) : Warning : {Code} : {Message}");
        }

        [Fact]
        public void Log_WithFile_UsesProvidedFileName()
        {
            // Arrange
            string file = "packages.lock.json";
            var category = MsBuild.Category.Error;

            // Act
            var result = MsBuild.Log(file, category, Code, LineNumber, LinePosition, Message);

            // Assert
            result.Should().Be($"packages.lock.json({LineNumber},{LinePosition}) : Error : {Code} : {Message}");
        }
    }
}