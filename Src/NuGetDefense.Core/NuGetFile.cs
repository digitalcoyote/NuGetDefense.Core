using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace NuGetDefense.Core
{
    public class NuGetFile
    {
        /// <summary>
        ///     Boolean indicating the Path is for a packages.config and not a project file.
        /// </summary>
        public bool PackagesConfig;

        public bool SolutionScan;

        public string Path;
        
        // TODO: LoadSolutionPackages -> solution level scan returns list of nuget files OR expose dotnet list parsing for NuGetDefense usage.

        public NuGetFile(string path)
        {
            if (path.EndsWith(".sln"))
            {
                Path = path;
                SolutionScan = true;
            }
            else
            {
                var pkgConfig = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), "packages.config");
                PackagesConfig = File.Exists(pkgConfig);
                Path = PackagesConfig ? pkgConfig : path;
            }
        }

        [Obsolete("The check referencedProjects functonality does not currently work and may not be fixed as part of this method" )]
        public Dictionary<string, NuGetPackage> LoadPackages(string targetFramework,
            bool checkTransitiveDependencies, bool checkReferencedProjects)
        {
            return LoadPackages(targetFramework, checkTransitiveDependencies);
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
                    }).ToDictionary(p => p.PackageUrl);
            else
                // The element may have a namespace of http://schemas.microsoft.com/developer/msbuild/2003 for old-style projects.
                pkgs = XElement.Load(Path, LoadOptions.SetLineInfo).DescendantsAndSelf()
                    .Where(x => x.Name.LocalName == "PackageReference" && RemoveInvalidVersions(x))
                    .Select(
                        x => new NuGetPackage
                        {
                            Id = (x.AttributeIgnoreCase("Include") ?? x.AttributeIgnoreCase("Update")).Value,
                            Version = ExtractPackageReferenceVersion(x),
                            LineNumber = ((IXmlLineInfo) x).LineNumber,
                            LinePosition = ((IXmlLineInfo) x).LinePosition
                        }).ToDictionary(p => p.PackageUrl);
            if (PackagesConfig)
            {
                if (checkTransitiveDependencies)
                {
                    Console.WriteLine(MsBuild.Log(Path, MsBuild.Category.Warning,
                        "Transitive dependency checking skipped. 'dotnet list package --include-transitive' only supports SDK style NuGet Package References"));
                }
            }
            else
            {
                if (pkgs.Count > 0)
                {
                    var projectDirectory = Path;
                    if (Path.ToLower().EndsWith(".csproj") || Path.ToLower().EndsWith(".vbproj") || Path.ToLower().EndsWith(".fsproj"))
                    {
                        projectDirectory = Directory.GetParent(Path)?.ToString();
                    }
                    
                    var lockFilePath = System.IO.Path.Combine(projectDirectory, "project.assets.json");
                    if (!File.Exists(lockFilePath))
                    {
                        lockFilePath = System.IO.Path.Combine(projectDirectory, "obj", "project.assets.json");
                        {
                            if (!File.Exists(lockFilePath))
                            {
                                throw new Exception(
                                    $"Failed to find project.aassets.json in {projectDirectory} or {System.IO.Path.Combine(projectDirectory, "obj")}");
                            }
                        }
                    }
                    
                    var resolvedPackages = parseLockFile(lockFilePath, targetFramework);
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
                        foreach (var pkg in pkgs.Where(p => resolvedPackages.ContainsKey(p.Key)))
                            pkgs[pkg.Key].Version = resolvedPackages[pkg.Key].Version;
                    }
                }
                else
                {
                    Console.WriteLine($"Skipping dotnet list package. No Packages found for {Path}.");
                }
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
            if (NuGetVersion.TryParse(ExtractPackageReferenceVersion(x), out var version)) return true;
            Console.WriteLine(MsBuild.Log(Path, MsBuild.Category.Warning, ((IXmlLineInfo) x).LineNumber, ((IXmlLineInfo) x).LinePosition,
                version != null
                    ? $"{version} is not a valid NuGetVersion and is being ignored. See 'https://docs.microsoft.com/en-us/nuget/concepts/package-versioning' for more info on valid versions"
                    : "Unable to find a version for this package. It will be ignored."));
            return false;
        }

        private string ExtractPackageReferenceVersion(XElement x)
        {
            // New-style projects have the version as an attribute.
            // Old-style projects have the Version as a child node and use a namespace.
            return x.AttributeIgnoreCase("Version")?.Value ?? x.Descendants().Where(x => x.Name.LocalName == "Version").FirstOrDefault()?.Value;
        }

        /// <summary>
        ///     Uses 'dotnet list' to get a list of resolved versions and dependencies
        /// </summary>
        /// <param name="projectFile"></param>
        /// <param name="targetFramework"></param>
        /// <param name="projectectReferencePackages"></param>
        /// <returns></returns>
        [Obsolete("Per Issue #180 `dotnet list` parsing is not reliable use parseLockFile instead")]
        public static Dictionary<string, NuGetPackage> dotnetListPackages(string projectFile,
            string targetFramework,
            out Dictionary<string, NuGetPackage[]> projectectReferencePackages)
        {
            projectectReferencePackages = null;

            Dictionary<string, NuGetPackage> pkgs;
            var startInfo = new ProcessStartInfo("dotnet")
            {
                Arguments =
                    $"list \"{projectFile}\" package --include-transitive{(string.IsNullOrWhiteSpace(targetFramework) ? "" : $" --framework {targetFramework}")}",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            var dotnet = new Process {StartInfo = startInfo};
            var outputBuilder = new StringBuilder();
            var errorOutputBuilder = new StringBuilder();

            dotnet.OutputDataReceived += (sender, args) =>
            {
                lock (outputBuilder)
                {
                    outputBuilder.AppendLine(args.Data);
                }
            };
            dotnet.ErrorDataReceived += (sender, args) =>
            {
                lock (outputBuilder)
                {
                    errorOutputBuilder.AppendLine(args.Data);
                }
            };
            dotnet.Start();
            dotnet.BeginOutputReadLine();
            dotnet.BeginErrorReadLine();
            
            if (dotnet.WaitForExit( 60000 ))
            {
                dotnet.WaitForExit();

                if (errorOutputBuilder.Length > Environment.NewLine.Length) Console.WriteLine($"`dotnet list` Errors: {errorOutputBuilder}");

                pkgs = ParseListPackages(outputBuilder.ToString());

                return pkgs;
            }
            
            Console.WriteLine("dotnet list failed to exit after one minute");
                
            return new Dictionary<string, NuGetPackage>();    
        }

        [Obsolete("Per Issue #180 `dotnet list` parsing is not reliable use parseLockFile instead")]
        public static Dictionary<string, NuGetPackage> ParseListPackages(string dotnetListOutput)
        {
            var lines = dotnetListOutput.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 3)
            {
                Console.WriteLine($"DEBUG: dotnet list output | {dotnetListOutput}");
                throw new Exception($"Invalid dotnet list output. Run `dotnet restore` then build again. DOTNET LIST OUTPUT: {dotnetListOutput}");
            }
            var pkgs = ParseDotnetListProjectSection(lines);

            return pkgs;
        }


        /// <summary>
        /// Parses the project.assets.json file which is created when running `dotnet restore`
        /// </summary>
        /// <param name="projectAssetsJsonPath">This is a path to the project.assets.json file. It's usually located in the obj folder of the project</param>
        /// <param name="targetFrameworkMoniker">This is the tfm used to determine the dependencies for a specific target framework. If not provided it will check all present</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Dictionary<string, NuGetPackage> parseLockFile(string projectAssetsJsonPath, string? targetFrameworkMoniker)
        {
            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Read(projectAssetsJsonPath);
            IEnumerable<LockFileTargetLibrary>? libraries;
            if (!string.IsNullOrWhiteSpace(targetFrameworkMoniker))
            {
                libraries = lockFile.Targets.FirstOrDefault(t => t.Name == targetFrameworkMoniker)?.Libraries;
            }
            else libraries = lockFile.Targets.SelectMany(t => t.Libraries);

            if (libraries == null) return new Dictionary<string, NuGetPackage>();

            Dictionary<string, NuGetPackage> dictionary = new Dictionary<string, NuGetPackage>();
            foreach (var l in libraries)
            {
                if (l.Version != null && !string.IsNullOrWhiteSpace(l.Name))
                {
                    var pkg = new NuGetPackage { Id = l.Name!, Version = l.Version!.ToString() };
                    if (!dictionary.TryAdd(pkg.PackageUrl, pkg))
                    {
                        Console.WriteLine($"Skipping {pkg.PackageUrl} as it was already added to the packages from this project");
                    }
                }
            }

            return dictionary;

        }

        [Obsolete("Per Issue #180 `dotnet list` parsing is not reliable use parseLockFile instead")]
        private static Dictionary<string, NuGetPackage> ParseDotnetListProjectSection(string[] lines)
        {
            var pkgs = lines
                // Skip the informational Text at hte beginning
                .SkipWhile(l => l.IndexOf('>') == -1)
                // Only Take Pacakges to avoid footers or The Transitive Header
                .TakeWhile(l => l.IndexOf('>') != -1)
                .Select(l =>
                {
                    var splitline = l.Split(new string[0], StringSplitOptions.RemoveEmptyEntries);
                    return new NuGetPackage
                    {
                        Id = splitline[1],
                        Version = splitline[3]
                    };
                })
                .ToDictionary(p => p.Id);

            var transitivelines = lines
                // Skip the informational Text at the beginning
                .SkipWhile(l => l.IndexOf('>') == -1)
                // Skip the Direct Dependencies
                .SkipWhile(l => l.IndexOf('>') != -1)
                // Skip the Header(s)
                .SkipWhile(l => l.IndexOf('>') == -1)
                // Only Take lines that still reference packages
                .TakeWhile(l => l.IndexOf('>') != -1)
                .ToArray();

            if (transitivelines.Any())
            {
                var transitives =
                    transitivelines
                        .Select(l =>
                        {
                            var splitline = l.Split(Array.Empty<string>(), StringSplitOptions.RemoveEmptyEntries);

                            return new NuGetPackage
                            {
                                Id = splitline[1],
                                Version = splitline[2]
                            };
                        }).ToArray();

                for (var index = 0; index < transitives.Length; index++)
                {
                    pkgs.Add(transitives[index].Id, transitives[index]);
                }
            }

            return pkgs;
        }


        [Obsolete("Per Issue #180 `dotnet list` parsing is not reliable use parseSlnLockFile instead")]
        public static Dictionary<string, Dictionary<string, NuGetPackage>> ParseListSlnPackages(string dotnetListOutput)
        {
            var projects = new Dictionary<string, Dictionary<string, NuGetPackage>>();
            //Chunk by indexes of ' or "" containing lines
            //Each Chunk is a project
            var lines = dotnetListOutput.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);

            var projectLines = DotNetListOutputByProject(lines);
            // I don't like throwing an exception here. An Early return for a bool method with Dict out may be better
            if (lines.Length < 3) throw new Exception("Invalid dotnet list output. Run `dotnet restore` then build again.");
            foreach( var projlines in projectLines)
            {
                var start = projlines[0].IndexOfAny(new[] {'\'', '\"', '`'}) + 1;
                var length = projlines[0].LastIndexOfAny(new[] {'\'', '\"', '`'}) - start;
                var name = projlines[0].Substring(start, length);
                projects.Add(name, ParseDotnetListProjectSection(projlines));
            }

            return projects;
        }
        
        [Obsolete("Per Issue #180 `dotnet list` parsing is not reliable use parseLockFile instead")]
        public static IEnumerable<string[]> DotNetListOutputByProject(
            string[] source)
        {
            int skip = 0;
            while(skip < source.Length)
            {
                var list = source.Skip(skip).Take(1).ToList();
                list.AddRange(source.Skip(skip + 1).TakeWhile(x => x.IndexOf('`', StringComparison.Ordinal) == -1 && x.IndexOf('\"', StringComparison.Ordinal) == -1 && x.IndexOf('\'', StringComparison.Ordinal) == -1));
                var lines = list.ToArray();
                skip += lines.Length;
                    
                yield return lines;
            }
        }

        [Obsolete("Per Issue #180 `dotnet list` parsing is not reliable use parseLockFile instead")]
        private static bool ParseProjectLines(in IEnumerable<string> projectReferenceLines)
        {
            throw new NotImplementedException();
        }
    }
}