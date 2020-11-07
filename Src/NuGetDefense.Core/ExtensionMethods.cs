using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NuGetDefense.Core
{
    public static class ExtensionMethods
    {
        internal static IEnumerable<T> SkipLast<T>(this IEnumerable<T> iEnumberable, int count)
        {
            return iEnumberable.Take(iEnumberable.Count() - count);
        }

        public static XAttribute AttributeIgnoreCase(this XElement x, string name)
        {
            return x.Attributes().FirstOrDefault(a => a.Name.ToString().Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static Dictionary<string, VulnerabilityEntry> FindPackageVulnerabilities(this Dictionary<string, Dictionary<string, VulnerabilityEntry>> vulnDict, string packageId)
        {
            return vulnDict[packageId];
        }

        public static VulnerabilityEntry? FindCve(this Dictionary<string, Dictionary<string, VulnerabilityEntry>> vulnDict, string cve)
        {
            return vulnDict.Values.FirstOrDefault(p => p.ContainsKey(cve))?[cve];
        }
    }
}