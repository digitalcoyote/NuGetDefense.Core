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
        /// <summary>
        ///     Boolean indicating the Path is for a packages.config and not a project file.
        /// </summary>
        public bool PackagesConfig;

        public string Path;

        public NuGetFile(string path)
        {
            var pkgConfig = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), "packages.config");
            PackagesConfig = File.Exists(pkgConfig);
            Path = PackagesConfig ? pkgConfig : path;
        }

        /// <summary>
        ///     Loads NuGet packages in use form packages.config or PackageReferences in the project file
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, NuGetPackage> LoadPackages(string targetFramework = "",
            bool checkTransitiveDependencies = true)
        {
            var pkgs = new Dictionary<string, NuGetPackage>();

            if (System.IO.Path.GetFileName(Path) == "packages.config")
                pkgs = XElement.Load(Path, LoadOptions.SetLineInfo).DescendantsAndSelf("package")
                    .Where(x => RemoveInvalidVersions(x))
                    .Select(x => new NuGetPackage
                    {
                        Id = x.AttributeIgnoreCase("id").Value, Version = x.AttributeIgnoreCase("version").Value,
                        LineNumber = ((IXmlLineInfo) x).LineNumber, LinePosition = ((IXmlLineInfo) x).LinePosition
                    }).ToDictionary(p => p.Id);
            else
                pkgs = XElement.Load(Path, LoadOptions.SetLineInfo).DescendantsAndSelf("PackageReference")
                    .Where(x => RemoveInvalidVersions(x))
                    .Select(
                        x => new NuGetPackage
                        {
                            Id = x.AttributeIgnoreCase("Include").Value,
                            Version = x.AttributeIgnoreCase("Version").Value,
                            LineNumber = ((IXmlLineInfo) x).LineNumber,
                            LinePosition = ((IXmlLineInfo) x).LinePosition
                        }).ToDictionary(p => p.Id);
            if (!PackagesConfig)
            {
                var resolvedPackages = dotnetListPackages(Path, targetFramework);

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
                    foreach (var pkg in pkgs) pkgs[pkg.Key].Version = resolvedPackages[pkg.Key].Version;
                }
            }
            else if (checkTransitiveDependencies)
            {
                Console.WriteLine(MsBuild.Log(Path, MsBuild.Category.Warning,
                    "Transitive dependency checking skipped. 'dotnet list package --include-transitive' only supports SDK style NuGet Package References"));
            }

            return pkgs;
        }

        /// <summary>
        ///     Removes any Packages from the list before they are checked for Vulnerabilities
        /// </summary>
        /// <param name="nuGetPackages"> Array of Packages used as the source</param>
        /// <param name="ignoredPackages">Packages to Ignore</param>
        /// <returns>Filtered list of packages</returns>
        private static Dictionary<string, NuGetPackage> IgnorePackages(Dictionary<string, NuGetPackage> nuGetPackages,
            IEnumerable<NuGetPackage> ignoredPackages)
        {
            return nuGetPackages.Where(nuget => !ignoredPackages
                    .Where(ignoredNupkg => ignoredNupkg.Id == nuget.Value.Id)
                    .Any(ignoredNupkg => !VersionRange.TryParse(ignoredNupkg.Version, out var versionRange) ||
                                         versionRange.Satisfies(new NuGetVersion(nuget.Value.Version))))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        private bool RemoveInvalidVersions(XElement x)
        {
            if (NuGetVersion.TryParse(x.AttributeIgnoreCase("Version")?.Value, out var version)) return true;
            Console.WriteLine(MsBuild.Log(Path, MsBuild.Category.Warning, ((IXmlLineInfo) x).LineNumber, ((IXmlLineInfo) x).LinePosition,
                version != null
                    ? $"{version} is not a valid NuGetVersion and is being ignored. See 'https://docs.microsoft.com/en-us/nuget/concepts/package-versioning' for more info on valid versions"
                    : "Unable to find a version for this package. It will be ignored."));
            return false;
        }

        /// <summary>
        ///     Uses 'dotnet list' to get a list of resolved versions and dependencies
        /// </summary>
        /// <param name="projectFile"></param>
        /// <returns></returns>
        public static Dictionary<string, NuGetPackage> dotnetListPackages(string projectFile, string targetFramework)
        {
            Dictionary<string, NuGetPackage> pkgs;
            var startInfo = new ProcessStartInfo("dotnet")
            {
                Arguments =
                    $"list \"{projectFile}\" package --include-transitive{(string.IsNullOrWhiteSpace(targetFramework) ? "" : $" --framework {targetFramework}")}",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var dotnet = new Process {StartInfo = startInfo};
            dotnet.Start();
            dotnet.WaitForExit();
            var output = dotnet.StandardOutput.ReadToEnd();

            pkgs = ParseListPackages(output);

            return pkgs;
        }

        public static Dictionary<string, NuGetPackage> ParseListPackages(string dotnetListOutput)
        {
            var lines = dotnetListOutput.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
            if (lines.Length < 3) throw new Exception("Invalid dotnet list output. Run `dotnet restore` then build again.");
            var topLevelPackageResolvedIndex = lines[2].IndexOf("Resolved");
            var topLevelPackageRequestedIndex = lines[2].IndexOf("Requested");
            var transitiveHeaderIndex = Array.FindIndex(lines, l => l.Contains("Transitive Package"));
            var pkgs = lines.Skip(3).Take(transitiveHeaderIndex - 4)
                .Select(l => new NuGetPackage
                    {Id = l.Substring(l.IndexOf(">") + 2, topLevelPackageRequestedIndex - l.IndexOf(">") - 2).Trim(), Version = l.Substring(topLevelPackageResolvedIndex).Trim()})
                .ToDictionary(p => p.Id);

            var transitiveResolvedColumnStart = lines[transitiveHeaderIndex].IndexOf("Resolved") - 8;

            var transitives = lines.Skip(transitiveHeaderIndex + 1)
                .SkipLast(2).Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(l => new NuGetPackage
                {
                    Id = l.Substring(l.IndexOf(">") + 2, transitiveResolvedColumnStart - l.IndexOf(">") + 3).Trim(),
                    Version = l.Substring(transitiveResolvedColumnStart).Trim()
                });

            foreach (var transitiveDep in transitives) pkgs.Add(transitiveDep.Id, transitiveDep);

            return pkgs;
        }
    }
}