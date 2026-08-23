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

## Packaging and publishing

```
script/package.sh
```

This builds both runtimes into `dist/`, one subfolder per target framework,
copies `manifest.yml` alongside them, and runs `yak build`. Rhino resolves the
subfolder matching the runtime it is hosting.

Bump `version` in [manifest.yml](manifest.yml) and `<Version>` in
[Schlepp.csproj](Schlepp/Schlepp.csproj) together before publishing. Yak stamps
the Rhino version it was built against into the file name, so the package comes
out as `schlepp-0.1.0-rh9_0-any.yak`.

Publish against the test server first:

```
yak push --source https://test.yak.rhino3d.com dist/schlepp-0.1.0-rh9_0-any.yak
```

and then, once it installs cleanly, against the real one:

```
yak push dist/schlepp-0.1.0-rh9_0-any.yak
```

`yak login` is required before the first push.

Yak harvests the plugin `[assembly: Guid]` and every component `IoId` into the
package keywords by itself, which is how the server can tell a document which
package supplies a component it is missing.

## Adding a component

Copy [RandomWalkComponent.cs](Schlepp/RandomWalkComponent.cs) and give the new
class a fresh `IoId` GUID. The GUID identifies the component in saved documents
for good, so generate a new one rather than editing digits by hand:

```
uuidgen | tr 'A-Z' 'a-z'
```

An icon is picked up automatically from a `*.ghicon` resource in `Icons/` whose
file name matches the class name.

## Licence

MIT. See [LICENSE](LICENSE).
