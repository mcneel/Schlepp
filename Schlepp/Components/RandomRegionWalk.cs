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
    /// <summary>
    /// Default constructor.
    /// </summary>
    public RandomRegionWalk()
      : base(new Nomen("Random Region Walk", "Generate a random walk across a planar region.", "Maths", "Random", 1111, Rank.Obscure))
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

      // Every boundary — outer loops and holes alike — as world curves, gathered
      // once. The distance to the nearest of them drives the stride function.
      var boundaries = new List<Curve>();
      for (var outer = 0; outer < region.OuterCount; outer++)
        boundaries.AddRange(region.ToCurves(outer));

      double BoundaryDistance(Point3d point)
      {
        var distance = double.MaxValue;
        foreach (var boundary in boundaries)
          if (boundary.ClosestPoint(point, out var t))
            distance = Math.Min(distance, point.DistanceTo(boundary.PointAt(t)));

        return distance;
      }

      var random = engine.CreateInstance();
      var token = access.Solution.Token;
      var walk = new Polyline(steps + 1) { start };

      var here = start;
      for (var i = 0; i < steps; i++)
      {
        token.ThrowIfCancellationRequested();

        // The stride function is allowed to fail: NaN and thrown exceptions
        // alike are treated as an undefined stride. The comparison is inverted
        // on purpose — NaN fails every comparison, so 'reach < 1e-12' would wave
        // it through and fill the rest of the walk with NaN.
        double reach;
        try { reach = Math.Abs(stride.Y(BoundaryDistance(here))); }
        catch { reach = double.NaN; }

        if (!(reach > 1e-12))
        {
          access.AddWarning("Zero Stride", "The stride distance dropped to zero or became undefined, signaling the premature end of the random walk.");
          break;
        }

        // Cast about for a direction which keeps the walk inside the region.
        // With a stride no larger than the boundary distance every direction
        // works on the first try; only strides which overreach the free space
        // around the walk need the retries.
        var accepted = false;
        for (var attempt = 0; attempt < 100; attempt++)
        {
          var direction = random.NextUnitVector2D();
          var candidate = here + (plane.XAxis * direction.X + plane.YAxis * direction.Y) * reach;

          if (!region.ContainsFast(candidate))
            continue;

          here = candidate;
          walk.Add(here);
          accepted = true;
          break;
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