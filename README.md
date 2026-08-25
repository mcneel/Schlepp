# Schlepp

Random walk algorithms for [Grasshopper 2](https://www.rhino3d.com/).

## Requirements

* Rhino 9 or later, on Windows or macOS.
* Grasshopper 2.

The package carries both a `net48` and a `net8.0` build, so it loads whether
Rhino is hosting .NET Framework or .NET Core.

## Building

```
dotnet build
```

The project targets `net48` and `net8.0` together. The .NET Framework reference
assemblies come in as a NuGet package, so both targets build on macOS and Linux
as well as Windows — no Windows machine is needed to produce a complete package.

## Debugging

`launch.json` is git-ignored, since it has to name a particular Rhino installation.
Create one with a `coreclr` launch configuration whose `program` is the Rhino
executable — on macOS that is
`/Applications/RhinoBETA.app/Contents/MacOS/Rhinoceros` — and a `preLaunchTask` of
`build-debug`.

Then install the plugin once, by hand, through Grasshopper's `GH2Plugins` command,
pointing it at `Schlepp/bin/Debug/net8.0/Schlepp.rhp`. Grasshopper records the path
and reloads it on every subsequent start, re-examining the file whenever its
timestamp changes, so rebuilding is enough — there is no need to install again.

Setting `RHINO_PACKAGE_DIRS` to the build folder also makes Rhino discover the
plugin without installing it, and it exercises the same runtime-variant resolution
a real package install goes through. It is the more faithful test, but it has been
seen to intermittently mis-bind the `Grasshopper2` reference and fail while
harvesting types, so the manual install is the reliable choice for day-to-day work.

## Documentation

Component documentation is authored in [Documentation In Progress](Documentation%20In%20Progress)
using Grasshopper's own authoring pipeline (the `GH2DocsAuthoring` command), which
moves each spec, term and topic through drafting stages. Nothing in that folder
ships directly: `script/package.sh` gathers the files which have reached the
`5. Finished` stage — plus the example files — into a `Documentation` folder at
the root of the package, beside the runtime folders.

That location is what Grasshopper resolves at load time: it looks for a folder
called `Documentation` next to the plugin assembly, climbing up out of the
`net48`/`net8.0` folder, so a single copy at the package root serves both
runtimes. Language subfolders (`English` and so on) are picked based on the
user's language settings.

## Licence

MIT. See [LICENSE](LICENSE).
