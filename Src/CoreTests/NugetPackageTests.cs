using FluentAssertions;
using Xunit;
using NuGetDefense;

namespace CoreTests
{
    public class NugetPackageTests
    {
        [Fact]
        public void PackageUrlTest()
        {
            var pkg = new NuGetPackage()
            {
                Id = "jquery",
                Version = "1.5.1"
            };

            pkg.PackageUrl.Should().BeEquivalentTo("pkg:nuget/jquery@1.5.1");
        }
    }
}