using System.Runtime.InteropServices;

using Rhino.PlugIns;

// The plugin id. Rhino identifies the plugin by this value and Grasshopper reads
// the same attribute, so one assembly needs only one id. The package server
// indexes plugins by it too: never regenerate this GUID.
[assembly: Guid("050e2bb5-04fd-4db1-8817-51f3122c280a")]

[assembly: PlugInDescription(DescriptionType.Organization, "Robert McNeel & Associates")]
[assembly: PlugInDescription(DescriptionType.Email, "tech@mcneel.com")]
[assembly: PlugInDescription(DescriptionType.WebSite, "https://github.com/mcneel/Schlepp")]
[assembly: PlugInDescription(DescriptionType.Address, "146 N Canal St, Suite 320, Seattle, WA 98103")]
[assembly: PlugInDescription(DescriptionType.Country, "United States")]
[assembly: PlugInDescription(DescriptionType.Phone, "+1 (206) 545-6877")]