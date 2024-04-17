using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using NuGetDefense.Core;
using Xunit;

namespace CoreTests
{
    /// <summary>
    /// LANG=ru_RU.utf8 can be used to generate other language outputs for testing
    /// </summary>
    public class NuGetFileTests
    {
        [Theory, InlineData(@"Project 'netcoreapp3.1.TestLib' has the following package references
   [netcoreapp3.1]: 
   Top-level Package      Requested   Resolved
   > Bootstrap            3.0.0       3.0.7   
   > NuGetDefense         1.0.8       1.0.6   
   > IdentityServer4      3.1.3       3.1.3          

   Transitive Package      Resolved
   > jQuery                1.9.0   
"), InlineData(@"Das Projekt ""ConsoleApp1"" enthält die folgenden Paketverweise.
   [netcoreapp3.1]: 
   Paket oberster Ebene Angefordert Aufgelöst
   > Bootstrap            3.0.0       3.0.7   
   > IdentityServer4      3.1.3       3.1.3
   > NuGetDefense         1.0.8       1.0.6 
 
   Transitive Package      Resolved
   > jQuery                1.9.0   
")]
        public void DotNetListPackagesTest(string dotnetOutput)
        {
            var dependencies = NuGetFile.ParseListPackages(dotnetOutput);
            Assert.True(dependencies.Count == 4);
            Assert.True(dependencies["Bootstrap"].Version == "3.0.7");
            Assert.True(dependencies["NuGetDefense"].Version == "1.0.6");
            Assert.True(dependencies["jQuery"].Version == "1.9.0");
            Assert.True(dependencies["IdentityServer4"].Version == "3.1.3");
        }

        [Theory, InlineData(@"Das Projekt ""ConsoleApp1"" enthält die folgenden Paketverweise.
   [net5.0]: 
   Top-level Package                                    Requested         Resolved       
   > IdentityServer4                                    3.1.3             3.1.3    
   > NuGetDefense                                       1.0.8             1.0.6   
    

   Transitive Package                                                                   Resolved
   > jQuery                                                                             1.9.0   





项目'ClassLibrary1'具有以下包引用
   [net5.0]: 
   顶级包                    已请求      已解决  
   > Bootstrap            3.0.0       3.0.7   


   Transitive Package                                       Resolved
   > jQuery                                                 1.9.0   

 ")]
        public void DotNetListSlnPackagesTest(string dotnetOutput)
        {
            var projects = NuGetFile.ParseListSlnPackages(dotnetOutput);
            Assert.True(projects.Count == 2);
            Assert.True(projects["ConsoleApp1"].Count == 3);
            Assert.True(projects["ClassLibrary1"].Count == 2);
            Assert.True(projects["ConsoleApp1"]["IdentityServer4"].Version == "3.1.3");
            Assert.True(projects["ClassLibrary1"]["Bootstrap"].Version == "3.0.7");
            Assert.True(projects["ConsoleApp1"]["NuGetDefense"].Version == "1.0.6");
            Assert.True(projects["ConsoleApp1"]["jQuery"].Version == "1.9.0");
            Assert.True(projects["ClassLibrary1"]["jQuery"].Version == "1.9.0");
        }

        [Fact]
        public void LegacyPackagesLoad()
        {
            var net461TestProjectDirectory =
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetAssemblyLocation())!, "TestFiles",
                    "net461.TestLib");
            var nugetFile = new NuGetFile(Path.Combine(net461TestProjectDirectory, "net461.TestLib.csproj"));

            var packages = nugetFile.LoadPackages();
            Assert.Equal(2, packages.Count);
            Assert.Equal(Path.Combine(net461TestProjectDirectory, "packages.config"), nugetFile.Path);
        }

        [Fact(Skip="Broken pending rewrite. Needs a lock file created for testing")]
        public void SdkPackagesLoadNoTransitiveDependencies()
        {
            var netcoreapp31TestProjectDirectory =
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetAssemblyLocation())!, "TestFiles",
                    "net5.0.Lib");

            var projectFile = Path.Combine(netcoreapp31TestProjectDirectory, "NuGetDefense.Core.csproj");
            var nugetFile = new NuGetFile(projectFile);

            RestorePackage(projectFile);

            var packages = nugetFile.LoadPackages("netstandard2.1", false);
            Assert.Equal(4, packages.Count);
            Assert.Equal(projectFile, nugetFile.Path);
        }

        [Fact(Skip="Broken pending rewrite. Needs a lock file created for testing")]
        public void SdkPackagesLoadWithTransitiveDependencies()
        {
            var netcoreapp31TestProjectDirectory =
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetAssemblyLocation())!, "TestFiles",
                    "net5.0.Lib");

            var projectFile = Path.Combine(netcoreapp31TestProjectDirectory, "NuGetDefense.Core.csproj");
            var nugetFile = new NuGetFile(projectFile);

            RestorePackage(projectFile);

            var packages = nugetFile.LoadPackages("netstandard2.1");
            Assert.Equal(13, packages.Count);
            Assert.Equal(projectFile, nugetFile.Path);
        }

        private static void RestorePackage(string projectFile)
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                Arguments = $"restore \"{projectFile}\"",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var dotnet = new Process {StartInfo = startInfo};
            dotnet.Start();
            dotnet.WaitForExit();
        }
        
        [Fact]
        public void ParseLockFileAllFrameworks()
        {
            var net8TestProjectDirectory =
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetAssemblyLocation())!, "TestFiles", "net8.0.cli");

            var projectFile = Path.Combine(net8TestProjectDirectory, "TestCli.csproj");
            var nugetFile = new NuGetFile(projectFile);
            var packages = nugetFile.LoadPackages();
            Assert.Equal(20, packages.Count);
            Assert.Equal(projectFile, nugetFile.Path);
        }        
        
        [Fact]
        public void ParseLockFileNet8()
        {
            var net8TestProjectDirectory =
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetAssemblyLocation())!, "TestFiles", "net8.0.cli");

            var projectFile = Path.Combine(net8TestProjectDirectory, "TestCli.csproj");
            var nugetFile = new NuGetFile(projectFile);
            var packages = nugetFile.LoadPackages("net8.0");
            Assert.Equal(19, packages.Count);
            Assert.Equal(projectFile, nugetFile.Path);
        }
    }
}