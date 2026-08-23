namespace Schlepp
{
  /// <summary>
  /// Plugin descriptor for the Schlepp component collection.
  /// <para>
  /// Identity and authorship are not repeated here. The base class reads them
  /// from the assembly attributes — the id from <c>[assembly: Guid]</c>, and the
  /// name, description, version, company and copyright from the attributes
  /// generated out of Schlepp.csproj and Directory.Build.props. That is the same
  /// metadata Rhino itself reads, so a single set serves both. Override a
  /// property here only to say something the attributes cannot.
  /// </para>
  /// </summary>
  public sealed class SchleppPlugin : Grasshopper2.Framework.Plugin
  {
    /// <summary>
    /// Gets the licence under which this plugin is distributed.
    /// </summary>
    public override string LicenceDescription => "MIT";

    /// <summary>
    /// Gets the licence agreement, or a web address at which to find it.
    /// </summary>
    public override string LicenceAgreement => "https://github.com/mcneel/Schlepp/blob/main/LICENSE";
  }
}
