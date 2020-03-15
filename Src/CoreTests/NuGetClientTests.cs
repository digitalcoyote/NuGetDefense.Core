using System.Collections.Generic;
using FluentAssertions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGetDefense;
using Xunit;

namespace CoreTests
{
    public class NuGetClientTests
    {
        [Fact]
        public void GetPackageDependenciesTest()
        {
            var pkgs = NuGetClient.GetAllPackageDependencies(new List<NuGetPackage>()
                    {
                        new NuGetPackage()
                        {
                            Id = "MessagePack",
                            Version = "2.1.90"
                        }
                    },
                    "netstandard2.0")
                .Result;

            var ExpectedDependencies = new List<NuGetPackage>()
            {
                new NuGetPackage() {Id = "MessagePack", Version = "2.1.90"},
                new NuGetPackage() {Id = "MessagePack.Annotations", Version = "2.1.90"},
                new NuGetPackage() {Id = "Microsoft.Bcl.AsyncInterfaces", Version = "1.0.0"},
                new NuGetPackage() {Id = "System.Threading.Tasks.Extensions", Version = "4.5.3"},
                new NuGetPackage() {Id = "System.Runtime.CompilerServices.Unsafe", Version = "4.5.2"},
                new NuGetPackage() {Id = "System.Buffers", Version = "4.4.0"},
                new NuGetPackage() {Id = "System.Numerics.Vectors", Version = "4.4.0"},
                new NuGetPackage() {Id = "System.Reflection.Emit.Lightweight", Version = "4.6.0"},
                new NuGetPackage() {Id = "System.Reflection.Emit.ILGeneration", Version = "4.6.0"},
                new NuGetPackage() {Id = "System.Reflection.Emit", Version = "4.6.0"}
            };

            foreach (var expectedDependency in ExpectedDependencies) pkgs.Should().Contain(expectedDependency);
        }
    }
}