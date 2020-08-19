using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using NuGetDefense.Core;
using Xunit;

namespace CoreTests
{
    public class NuGetFileTests
    {
        [Fact]
        public void dotnetListPackagesTest()
        {
            var dependencies = NuGetFile.ParseListPackages(
                @"Project 'netcoreapp3.1.TestLib' has the following package references
   [netcoreapp3.1]: 
   Top-level Package      Requested   Resolved
   > Bootstrap            3.0.0       3.0.7   
   > NuGetDefense         1.0.8       1.0.6   

   Transitive Package      Resolved
   > jQuery                1.9.0   

");
            Assert.True(dependencies.Count == 3);
            Assert.True(dependencies["Bootstrap"].Version == "3.0.7");
            Assert.True(dependencies["NuGetDefense"].Version == "1.0.6");
            Assert.True(dependencies["jQuery"].Version == "1.9.0");
        }

        [Fact]
        public void LegacyPackagesLoad()
        {
            var nugetFile = new NuGetFile();
            var net461TestProjectDirectory =
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetAssemblyLocation())!, "TestFiles",
                    "net461.TestLib");
            var packages = nugetFile.LoadPackages(Path.Combine(net461TestProjectDirectory, "net461.TestLib.csproj"));
            Assert.Equal(2, packages.Count);
            Assert.Equal(Path.Combine(net461TestProjectDirectory, "packages.config"), nugetFile.Path);
        }

        [Fact]
        public void SdkPackagesLoad()
        {
            var nugetFile = new NuGetFile();
            var netcoreapp31TestProjectDirectory =
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetAssemblyLocation())!, "TestFiles",
                    "netcoreapp3.1.Lib");
            var projectFile = Path.Combine(netcoreapp31TestProjectDirectory, "NuGetDefense.Core.csproj");

            var startInfo = new ProcessStartInfo("dotnet")
            {
                Arguments = $"dotnet restore \"{projectFile}\" ",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var dotnet = new Process {StartInfo = startInfo};
            dotnet.Start();
            dotnet.WaitForExit();

            var packages = nugetFile.LoadPackages(projectFile);
            Assert.Equal(2, packages.Count);
            Assert.Equal(projectFile, nugetFile.Path);
        }
    }
}