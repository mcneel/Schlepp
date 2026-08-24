using System;
using System.Collections.Generic;

using Rhino.Geometry;

using GrasshopperIO;

using Grasshopper2.UI;
using Grasshopper2.Components;
using Grasshopper2.Extensions;
using Grasshopper2.Types.Random;
using Grasshopper2.Types.Shapes;
using Grasshopper2.Types.Functions.Standard;
using Grasshopper2.Types.Functions;

namespace Schlepp
{
  [IoId("cd7279b1-7c33-41fa-b69f-50be8f1b06ca")]
  public sealed class RandomRegionWalk : Component
  {
    public RandomRegionWalk()
      : base(new Nomen("Random Region Walk", "Generate a random walk across a planar region.", "Maths", "Random", 1111, Rank.Obscure))
    { }
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

      // The walk lives in the plane of the region. A start point off that plane
      // is projected onto it rather than rejected, so only a point which is
      // genuinely outside the region fails.
      var plane = region.Plane;
      start = plane.ClosestPoint(start);

      switch (region.Contains(start))
      {
        case RegionRelation.Outside:
        case RegionRelation.InHole:
          access.AddError("Invalid Start", "The start point must lie on the region interior.");
          return;
      }

      // Collect all region boundaries as a flat list of curves.
      // We need this for distance-to-boundary measurements.
      var boundaries = new List<Curve>();
      for (var outer = 0; outer < region.OuterCount; outer++)
        boundaries.AddRange(region.ToCurves(outer));

      double DistanceToBoundary(Point3d point)
      {
        var distance = double.PositiveInfinity;
        foreach (var boundary in boundaries)
          if (boundary.ClosestPoint(point, out var t))
            distance = Math.Min(distance, point.DistanceTo(boundary.PointAt(t)));

        return distance;
      }

      var random = engine.CreateInstance();
      var walk = new Polyline(steps + 1) { start };

      var here = start;
      for (var i = 0; i < steps; i++)
      {
        access.Solution.Token.ThrowIfCancellationRequested();

        // The stride function is allowed to fail: NaN and thrown exceptions
        // alike are treated as an indication the walk is over.
        double stepSize;
        try { stepSize = Math.Abs(stride.Y(DistanceToBoundary(here))); }
        catch { stepSize = double.NaN; }

        if (!(stepSize > 1e-12))
        {
          access.AddWarning("Zero Stride", "The stride distance dropped to zero or became undefined, signaling the premature end of the random walk.");
          break;
        }

        var accepted = false;
        for (var attempt = 0; attempt < 100; attempt++)
        {
          // Idea: pick a single random vector, then if that step fails to stay
          // within the region, polararray that vector and randomly try all remaining
          // directions. That will prevent the clumping of random direction tests.
          var direction = random.NextUnitVector2D();
          var candidate = here + (plane.XAxis * direction.X + plane.YAxis * direction.Y) * stepSize;

          if (region.ContainsFast(candidate))
          {
            here = candidate;
            walk.Add(here);
            accepted = true;
            break;
          }
        }

        if (!accepted)
        {
          access.AddWarning("Walk Cornered", $"Step {i + 1} was given up after 100 attempts to stay within the region. The stride is too long for the space around the walk.");
          break;
        }
      }

      if (walk.Count >= 2)
        access.SetItem(0, walk);
      else
        access.AddWarning("Empty Walk", "A valid walk needs at least a single step.");

      access.SetItem(1, walk.Last);
    }
  }
}