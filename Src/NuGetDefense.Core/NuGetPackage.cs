using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace NuGetDefense
{
    public class NuGetPackage
    {
        public string[] Dependencies = { };
        public int? LineNumber;
        public int? LinePosition;

        public string Id { get; set; }

        public string Version { get; set; }

        public string PackageUrl => $@"pkg:nuget/{Id}@{Version}";
    }
}