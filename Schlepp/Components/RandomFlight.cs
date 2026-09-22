using Rhino.Geometry;
using GrasshopperIO;

using Grasshopper2.UI;
using Grasshopper2.Components;
using Grasshopper2.Extensions;
using Grasshopper2.Types.Random;

namespace Schlepp
{
  [IoId("bccf72b2-3bdb-4adb-8a9c-e3988ab09a3d")]
  public sealed class RandomFlight : Component
  {
    public RandomFlight()
      : base(new Nomen("Random Flight", "Generate a random walk in unbounded 3D space whose stride lengths are drawn from a statistical distribution.", "Maths", "Random", 1111, Rank.Obscure))
    { }
    public RandomFlight(IReader reader) : base(reader) { }

    protected override void AddInputs(InputAdder inputs)
    {
      inputs.AddPoint("Start", "Pt", "Start location of flight path.").Set(Point3d.Origin);
      inputs.AddPlane("Plane", "Pl", "Optional flight plane.", requirement: Grasshopper2.Parameters.Requirement.MayBeMissing);
      inputs.AddInteger("Steps", "Sn", "Number of steps to take.").Set(100);
      inputs.AddContinuous("Stride", "St", "Stride length distribution.").Set(ContinuousDistribution.CreatePareto(1.5, 1.0));
      inputs.AddRandom("Random engine used to drive the flight.");
    }
    protected override void AddOutputs(OutputAdder outputs)
    {
      outputs.AddPolyline("Flight", "Pl", "Polyline representing the flown path.");
      outputs.AddPoint("Terminus", "Pt", "End of flight.");
    }

    protected override void Process(IDataAccess access)
    {
      access.GetItem(0, out Point3d start);
      var hasPlane = access.GetItem(1, out Plane plane);
      access.GetItem(2, out int steps);
      access.GetItem(3, out ContinuousDistribution stride);
      access.GetItem(4, out RandomEngine engine);

      access.RectifyPositive(ref steps, "Steps");

      if (hasPlane)
        start = plane.ClosestPoint(start);

      var random = engine.CreateInstance();
      var strides = stride.CreateSampling(random, steps);

      var flight = new Polyline(steps + 1) { start };

      var here = start;
      var token = access.Solution.Token;
      for (var i = 0; i < steps; i++)
      {
        token.ThrowIfCancellationRequested();

        // Unlike the other walks a zero step is not a reason to stop, since a
        // distribution which can return zero will return it now and again
        // without being exhausted. Only a length which is not a number at all
        // ends the flight.
        var step = strides[i];
        if (double.IsNaN(step) || double.IsInfinity(step))
        {
          access.AddWarning("Undefined Stride", "The stride distribution yielded a value which is not a finite length, signaling the premature end of the random flight.");
          break;
        }

        if (hasPlane)
        {
          var vec = random.NextUnitVector2D() * step;
          here += plane.XAxis * vec.X + plane.YAxis * vec.Y;
        }
        else
          here += random.NextUnitVector3D() * step;
        flight.Add(here);
      }

      if (flight.Count >= 2)
        access.SetItem(0, flight);
      else
        access.AddWarning("Empty Flight", "A valid flight needs at least a single step.");

      access.SetItem(1, flight.Last);
    }
  }
}
