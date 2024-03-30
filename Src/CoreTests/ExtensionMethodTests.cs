using System.Collections.Generic;
using NuGetDefense;
using NuGetDefense.Core;
using Xunit;

namespace CoreTests
{
    public class ExtensionMethodTests
    {
        private readonly Dictionary<string, Dictionary<string, VulnerabilityEntry>> _vulnDict =
            new()
            {
                {
                    "bootstrap", new Dictionary<string, VulnerabilityEntry>
                    {
                        {
                            "CVE-2018-14040", new VulnerabilityEntry
                            {
                                Cwe = "TestCWE",
                                Description = "TestDescription",
                                References = new[] {"TestReference1", "TestReference2"},
                                Score = 4.55,
                                Vector = Vulnerability.AccessVectorType.LOCAL,
                                Vendor = "XUnit",
                                Versions = new[]
                                {
                                    "[4.0.0, 4.1.2)",
                                    "(, 3.4.0)"
                                }
                            }
                        }
                    }
                }
            };

        [Fact]
        public void FindCveNullTest()
        {
            Assert.Null(_vulnDict.FindCve("non-existant cve"));
        }

        [Fact]
        public void FindCveTest()
        {
            Assert.Equal(_vulnDict.FindCve("CVE-2018-14040"), _vulnDict["bootstrap"]["CVE-2018-14040"]);
        }

        [Fact]
        public void FindPackageVulnerabilitiesTest()
        {
            Assert.Equal(_vulnDict["bootstrap"], _vulnDict.FindPackageVulnerabilities("bootstrap"));
        }
    }
}