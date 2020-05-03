using System.Collections.Generic;
using System.Linq;

namespace NuGetDefense.NVD
{
    public static class VulnDictExtensionMethods
    {
        public static Dictionary<string, VulnerabilityEntry> FindPackageVulnerabilities(this Dictionary<string, Dictionary<string, VulnerabilityEntry>> vulnDict,
            string packageId)
        {
            return vulnDict[packageId];
        }

        public static VulnerabilityEntry FindCve(
            this Dictionary<string, Dictionary<string, VulnerabilityEntry>> vulnDict, string cve)
        {
            return vulnDict.Values.FirstOrDefault(p => p.ContainsKey(cve))?[cve];
        }
    }
}