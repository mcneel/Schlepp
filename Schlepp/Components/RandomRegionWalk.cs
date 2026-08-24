using System;

using Rhino.Geometry;

using GrasshopperIO;

using Grasshopper2.UI;
using Grasshopper2.Components;
using Grasshopper2.Extensions;
using Grasshopper2.Types.Random;
using Grasshopper2.Types.Fields;
using Grasshopper2.Types.Shapes;
using Grasshopper2.Types.Functions.Standard;
using Grasshopper2.Types.Functions;

namespace Schlepp
{
  [IoId("cd7279b1-7c33-41fa-b69f-50be8f1b06ca")]
  public sealed class RandomRegionWalk : Component
  {
    /// <summary>
    /// Default constructor.
    /// </summary>
    public RandomRegionWalk()
      : base(new Nomen("Random Region Walk", "Generate a random walk in unbounded 3D space.", "Maths", "Random", 1111, Rank.Obscure))
    { }

    /// <summary>
    /// Deserialisation constructor.
    /// </summary>
    /// <param name="reader">Reader to deserialise from.</param>
    public RandomRegionWalk(IReader reader) : base(reader) { }

    protected override void AddInputs(InputAdder inputs)
    {
      var outer = new ArcCurve(new Circle(Point3d.Origin, 10));
      var inner = new[]
      {
        new ArcCurve(new Circle(new Point3d(+4, 0, 0), 3)),
        new ArcCurve(new Circle(new Point3d(-3, 0, 0), 2)),
      };
      var region = Region.CreateFromCurves(outer, inner);

      inputs.AddPoint("Start", "Pt", "Start location of walk.").Set(Point3d.Origin);
      inputs.AddInteger("Steps", "Sn", "Number of steps to take.").Set(100);
      inputs.AddRegion("Region", "Rg", "Region").Set(region);
      inputs.AddFunction("Stride", "St", "Stride size as a function of distance to region boundary.").Set(new LinearFunction(0.5, 0.0));
      inputs.AddRandom("used to drive the walk.");
    }

    protected override void AddOutputs(OutputAdder outputs)
    {
      outputs.AddPolyline("Walk", "Pl", "Polyline representing the walked path.");
      outputs.AddPoint("Terminus", "Pt", "End of walk.");
    }

    protected override void Process(IDataAccess access)
    {
      access.GetItem(0, out Point3d start);
      access.GetItem(1, out int steps);
      access.GetItem(2, out Region region);
      access.GetItem(3, out Function stride);
      access.GetItem(4, out RandomEngine engine);

      access.RectifyPositive(ref steps, "Steps");

      var random = engine.CreateInstance();
      var walk = new Polyline(steps + 1) { start };

      // TODO: make sure the start point is inside the region.
      // If not, just abort with an error.

      // Repeat:
      // 1. Measure distance to nearest region boundary, 
      // 2. evaluate step size.
      // 3. take step, ensuring the new point is inside the region (there's a fast method we should use.)
      // 4. if 100 step attempts all fail to remain within the region, return early with a warning.

      if (walk.Count >= 2)
        access.SetItem(0, walk);
      else
        access.AddWarning("Empty Walk", "A valid walk needs at least a single step.");

      access.SetItem(1, walk.Last);
    }
  }
}