using Grasshopper2.UI.Icon;

namespace Schlepp
{
  public sealed class SchleppPlugin : Grasshopper2.Framework.Plugin
  {
    public override string Name => "Schlepp";
    public override IIcon Icon => AbstractIcon.FromResource("Schlepp.ghicon", GetType().Assembly);

    public override string LicenceDescription => "MIT";
    public override string LicenceAgreement => "https://github.com/mcneel/Schlepp/blob/main/LICENSE";
  }
}