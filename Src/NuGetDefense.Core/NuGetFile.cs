using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NuGet.Versioning;

namespace NuGetDefense.Core
{
    public class NuGetFile
    {
        public string Path;

        /// <summary>
        ///     Loads NuGet packages in use form packages.config or PackageReferences in the project file
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, NuGetPackage> LoadPackages(string projectFile, bool checkTransitiveDependencies = true)
        {
            var pkgConfig = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(projectFile), "packages.config");
            var legacy = File.Exists(pkgConfig);
            Path = legacy ? pkgConfig : projectFile;
            Dictionary<string, NuGetPackage> pkgs = new Dictionary<string, NuGetPackage>();

                if (System.IO.Path.GetFileName(projectFile) == "packages.config")
                    pkgs = XElement.Load(projectFile, LoadOptions.SetLineInfo).DescendantsAndSelf("package")
                        .Where(x => RemoveInvalidVersions(x))
                        .Select(x => new NuGetPackage
                        {
                            Id = (x.AttributeIgnoreCase("id")).Value, Version = x.AttributeIgnoreCase("version").Value,
                            LineNumber = ((IXmlLineInfo) x).LineNumber, LinePosition = ((IXmlLineInfo) x).LinePosition
                        }).ToDictionary(p => p.Id);
                else
                    pkgs = XElement.Load(projectFile, LoadOptions.SetLineInfo).DescendantsAndSelf("PackageReference")
                        .Where(x => RemoveInvalidVersions(x))
                        .Select(
                            x => new NuGetPackage
                            {
                                Id = x.AttributeIgnoreCase("Include").Value,
                                Version = x.AttributeIgnoreCase("Version").Value,
                                LineNumber = ((IXmlLineInfo) x).LineNumber,
                                LinePosition = ((IXmlLineInfo) x).LinePosition
                            }).ToDictionary(p => p.Id);;
                if(!legacy)
                {
                    var resolvedPackages = dotnetListPackages(projectFile);

                    if (checkTransitiveDependencies)
                    {
                        foreach (var pkg in pkgs.Where(
                            package => resolvedPackages.ContainsKey(package.Key)))
                        {
                            resolvedPackages[pkg.Key].LineNumber = pkg.Value.LineNumber;
                            resolvedPackages[pkg.Key].LinePosition = pkg.Value.LinePosition;
                        }

                        pkgs = resolvedPackages;
                    }
                    else
                    {
                        foreach (var pkg in pkgs)
                        {
                            pkgs[pkg.Key].Version = resolvedPackages[pkg.Key].Version;
                        }
                    }
                }
                else if (checkTransitiveDependencies)
                {
                    Console.WriteLine(
                        $"{Path} : Warning : Transitive dependency checking skipped. 'dotnet list package --include-transitive' only supports SDK style NuGet Package References");
                }
                return pkgs;
        }

        /// <summary>
        ///     Removes any Packages from the list before they are checked for Vulnerabilities
        /// </summary>
        /// <param name="nuGetPackages"> Array of Packages used as the source</param>
        /// <param name="ignoredPackages">Packages to Ignore</param>
        /// <returns>Filtered list of packages</returns>
        private static Dictionary<string, NuGetPackage> IgnorePackages(Dictionary<string, NuGetPackage> nuGetPackages, IEnumerable<NuGetPackage> ignoredPackages)
        {
            return (Dictionary<string, NuGetPackage>) nuGetPackages.Where(nuget => !ignoredPackages
                    .Where(ignoredNupkg => ignoredNupkg.Id == nuget.Value.Id)
                    .Any(ignoredNupkg => !VersionRange.TryParse(ignoredNupkg.Version, out var versionRange) ||
                                         versionRange.Satisfies(new NuGetVersion(nuget.Value.Version))))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        
        private bool RemoveInvalidVersions(XElement x)
        {
            if (NuGetVersion.TryParse(x.AttributeIgnoreCase("Version")?.Value, out var version)) return true;
            Console.WriteLine(
                version != null
                    ? $"{Path}({((IXmlLineInfo) x).LineNumber},{((IXmlLineInfo) x).LinePosition}) : Warning : {version} is not a valid NuGetVersion and is being ignored. See 'https://docs.microsoft.com/en-us/nuget/concepts/package-versioning' for more info on valid versions"
                    : $"{Path}({((IXmlLineInfo) x).LineNumber},{((IXmlLineInfo) x).LinePosition}) : Warning : Unable to find a version for this package. It will be ignored.");
            return false;
        }
        
        /// <summary>
        /// Uses 'dotnet list' to get a list of resolved versions and dependencies
        /// </summary>
        /// <param name="projectFile"></param>
        /// <returns></returns>
        private static Dictionary<string, NuGetPackage> dotnetListPackages(string projectFile)
        {
            Dictionary<string, NuGetPackage> pkgs;
            var startInfo = new ProcessStartInfo("dotnet")
            {
                Arguments = $"list {projectFile} package --include-transitive",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            var dotnet = new Process() {StartInfo = startInfo};
            dotnet.Start();
            dotnet.WaitForExit();
            var output = dotnet.StandardOutput.ReadToEnd();

            var lines = output.Split(Convert.ToChar(Environment.NewLine));
            var topLevelPackageResolvedIndex = lines[2].IndexOf("Resolved") - 8;
            var transitiveHeaderIndex = Array.FindIndex(lines, l => l.Contains("Transitive Package"));
            pkgs = lines.Skip(3).Take(transitiveHeaderIndex - 4).Select(l => new NuGetPackage
            {
                Id = l.Substring(l.IndexOf(">") + 2, topLevelPackageResolvedIndex - l.IndexOf(">") + 3).Trim(),
                Version = l.Substring(topLevelPackageResolvedIndex).Trim()
            }).ToDictionary(p => p.Id);;
                
            var transitiveResolvedColumnStart = output.Split(Convert.ToChar(Environment.NewLine))[transitiveHeaderIndex].IndexOf("Resolved") - 8;

            pkgs.Concat(lines.Skip(transitiveHeaderIndex + 1)
                .SkipLast(2).Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(l => new NuGetPackage
                {
                    Id = l.Substring(l.IndexOf(">") + 2, transitiveResolvedColumnStart - l.IndexOf(">") + 3).Trim(),
                    Version = l.Substring(transitiveResolvedColumnStart).Trim()
                }).ToDictionary(p => p.Id));

            return pkgs;
        }
    }
}