using System;

using Rhino.Geometry;

using GrasshopperIO;

using Grasshopper2.UI;
using Grasshopper2.Components;
using Grasshopper2.Extensions;
using Grasshopper2.Types.Random;
using Grasshopper2.Types.Fields;

namespace Schlepp
{
  [IoId("000d2b23-7d7f-4760-9d92-934278455854")]
  public sealed class RandomSpaceWalk : Component
  {
    /// <summary>
    /// Default constructor.
    /// </summary>
    public RandomSpaceWalk()
      : base(new Nomen("Random Space Walk", "Generate a random walk in unbounded 3D space.", "Maths", "Random", 1111, Rank.Obscure))
    { }

    /// <summary>
    /// Deserialisation constructor.
    /// </summary>
    /// <param name="reader">Reader to deserialise from.</param>
    public RandomSpaceWalk(IReader reader) : base(reader) { }

    protected override void AddInputs(InputAdder inputs)
    {
      inputs.AddPoint("Start", "Pt", "Start location of walk.").Set(Point3d.Origin);
      inputs.AddInteger("Steps", "Sn", "Number of steps to take.").Set(100);
      inputs.AddField("Stride", "St", "Length of a single step.").Set(1.0);
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
      access.GetItem(2, out Field stride);
      access.GetItem(3, out RandomEngine engine);

      access.RectifyPositive(ref steps, "Steps");

      var random = engine.CreateInstance();
      var walk = new Polyline(steps + 1) { start };

      var here = start;
      var token = access.Solution.Token;
      for (var i = 0; i < steps; i++)
      {
        token.ThrowIfCancellationRequested();

        // Comparison inverted on purpose. A field evaluates to NaN 
        // outside its domain, and NaN fails every comparison.
        var step = Math.Abs(stride.ScalarAt(here));
        if (!(step > 1e-12))
        {
          access.AddWarning("Zero Stride", "The stride distance dropped to zero or became undefined, signaling the premature end of the random walk.");
          break;
        }

        here += random.NextUnitVector3D() * step;
        walk.Add(here);
      }

      if (walk.Count >= 2)
        access.SetItem(0, walk);
      else
        access.AddWarning("Empty Walk", "A valid walk needs at least a single step.");

      access.SetItem(1, walk.Last);
    }
  }
}