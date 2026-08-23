using System;

using Rhino.Geometry;

using GrasshopperIO;

using Grasshopper2.UI;
using Grasshopper2.Components;
using Grasshopper2.Extensions;
using Grasshopper2.Types.Random;

namespace Schlepp
{
  /// <summary>
  /// An isotropic random walk of fixed step length, in three dimensions.
  /// </summary>
  [IoId("000d2b23-7d7f-4760-9d92-934278455854")]
  public sealed class RandomWalkComponent : Component
  {
    /// <summary>
    /// Default constructor.
    /// </summary>
    public RandomWalkComponent()
      : base(new Nomen("Random Walk", "Generate an isotropic random walk of fixed step length.", "Schlepp", "Walks"))
    { }

    /// <summary>
    /// Deserialisation constructor.
    /// </summary>
    /// <param name="reader">Reader to deserialise from.</param>
    public RandomWalkComponent(IReader reader) : base(reader) { }

    protected override void AddInputs(InputAdder inputs)
    {
      inputs.AddPoint("Start", "St", "Point where the walk begins.").Set(Point3d.Origin);
      inputs.AddInteger("Steps", "Sp", "Number of steps to take.").Set(100);
      inputs.AddNumber("Length", "Ln", "Length of a single step.").Set(1.0);
      inputs.AddRandom("used to drive the walk.");
    }

    protected override void AddOutputs(OutputAdder outputs)
    {
      outputs.AddPolyline("Walk", "Wk", "Polyline through every visited position.");
    }

    protected override void Process(IDataAccess access)
    {
      access.GetItem(0, out Point3d start);
      access.GetItem(1, out int steps);
      access.GetItem(2, out double length);
      access.GetItem(3, out RandomEngine engine);

      access.RectifyNonNegative(ref steps, "Steps");
      access.RectifyPositive(ref length, "Length");

      if (steps == 0)
      {
        access.SetItem(0, new Polyline(new[] { start }));
        return;
      }

      var random = engine.CreateInstance();
      var walk = new Polyline(steps + 1) { start };

      var here = start;
      for (var i = 0; i < steps; i++)
      {
        here += random.NextUnitVector3D() * length;
        walk.Add(here);
      }

      access.SetItem(0, walk);
    }
  }
}
