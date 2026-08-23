using System.Runtime.InteropServices;

using Rhino.PlugIns;

// The plugin id. Rhino identifies the plugin by this value and Grasshopper reads
// the same attribute, so one assembly needs only one id. The package server
// indexes plugins by it too: never regenerate this GUID.
[assembly: Guid("050e2bb5-04fd-4db1-8817-51f3122c280a")]

// Rhino's own plugin-description attributes. Grasshopper falls back to
// AssemblyCompany for the author, which Directory.Build.props already sets, so
// only the address needs saying here.
[assembly: PlugInDescription(DescriptionType.WebSite, "https://github.com/mcneel/Schlepp")]
