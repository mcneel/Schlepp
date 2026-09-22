using System;
using System.Collections.Generic;
using System.Threading;

using Rhino.Geometry;
using Rhino.Geometry.Intersect;

using GrasshopperIO;

using Grasshopper2.UI;
using Grasshopper2.Components;

namespace Schlepp
{
  [IoId("83701cba-7dd0-441a-9e87-7b7dc8409b9d")]
  public sealed class RandomWalkAnalysis : Component
  {
    public RandomWalkAnalysis()
      : base(new Nomen("Random Walk Analysis", "Measure the statistics which characterise a random walk.", "Maths", "Random", 1111, Rank.Obscure))
    { }
    public RandomWalkAnalysis(IReader reader) : base(reader) { }

    protected override void AddInputs(InputAdder inputs)
    {
      inputs.AddPolyline("Walk", "Pl", "Walk to analyse.");
    }
    protected override void AddOutputs(OutputAdder outputs)
    {
      outputs.AddNumber("Displacement", "Ds", "Straight line distance from the start of the walk to its end.");
      outputs.AddNumber("Length", "Ln", "Total distance travelled along the walk.");
      outputs.AddNumber("Gyration", "Rg", "Radius of gyration. The root mean square distance of the walk from its own centre.");
      outputs.AddNumber("Exponent", "Ex", "Scaling exponent of the walk. One half for a walk which is free to cross itself, about three quarters for a self-avoiding walk in the plane.");
      outputs.AddInteger("Crossings", "Xn", "Number of places where the walk intersects itself.");
    }

    protected override void Process(IDataAccess access)
    {
      access.GetItem(0, out Polyline walk);

      if (walk is null || walk.Count < 2)
      {
        access.AddError("Empty Walk", "A walk needs at least a single step before there is anything to measure.");
        return;
      }

      access.GetTolerance(out double tolerance);
      var token = access.Solution.Token;

      var exponent = Exponent(walk, token);
      if (double.IsNaN(exponent))
        access.AddRemark("Short Walk", "The scaling exponent is fitted to how quickly the walk spreads out over a range of step counts, and this walk is too short to provide that range.");

      access.SetItem(0, walk[0].DistanceTo(walk[walk.Count - 1]));
      access.SetItem(1, walk.Length);
      access.SetItem(2, Gyration(walk));
      access.SetItem(3, exponent);
      access.SetItem(4, Intersection.CurveSelf(walk.ToPolylineCurve(), tolerance)?.Count ?? 0);
    }

    /// <summary>
    /// Compute the radius of gyration; the root mean square distance of the
    /// vertices from their own centroid. Vertices are weighted equally rather
    /// than by the length of the segments they bound, which is the convention
    /// for a walk of discrete steps.
    /// </summary>
    private static double Gyration(Polyline walk)
    {
      double x = 0.0, y = 0.0, z = 0.0;
      foreach (var vertex in walk)
      {
        x += vertex.X;
        y += vertex.Y;
        z += vertex.Z;
      }

      var count = walk.Count;
      var centre = new Point3d(x / count, y / count, z / count);

      var sum = 0.0;
      foreach (var vertex in walk)
        sum += centre.DistanceToSquared(vertex);

      return Math.Sqrt(sum / count);
    }

    /// <summary>
    /// Estimate the scaling exponent of a walk from its internal distance
    /// profile. The square distance between two vertices which are s steps apart
    /// grows as s to the power 2v, so a straight line fitted through the profile
    /// on log-log axes has a slope of twice the exponent.
    /// <para>
    /// Measuring the profile rather than only the end to end distance is what
    /// makes a single walk worth measuring at all: every pair of vertices at a
    /// given separation contributes, where the end to end distance is one
    /// sample of one separation and far too noisy to say anything.
    /// </para>
    /// <para>
    /// The separations are reduced by their median rather than their mean. A
    /// stride distribution with a heavy tail has no finite variance, so the mean
    /// square distance is decided by whichever single jump happened to be the
    /// largest, and the fit slumps back towards one half however far the flight
    /// actually spreads. Every quantile of a self similar walk scales with the
    /// same exponent, so the median answers the same for an ordinary walk and
    /// keeps answering where the mean cannot.
    /// </para>
    /// </summary>
    /// <returns>The exponent, or NaN when the walk is too short to fit one.</returns>
    private static double Exponent(Polyline walk, CancellationToken token)
    {
      // Separations beyond half the walk have too few vertex pairs left to
      // average over, and the tail of the profile bends away from the straight
      // line as a result.
      var steps = walk.Count - 1;
      var longest = steps / 2;
      if (longest < 2)
        return double.NaN;

      var logSeparation = new List<double>(MaximumLags);
      var logDistance = new List<double>(MaximumLags);
      var squares = new List<double>(MaximumSamples);

      foreach (var lag in Lags(longest))
      {
        token.ThrowIfCancellationRequested();

        // Every pair at this separation could be measured, but that costs the
        // square of the walk length for no more accuracy than a few hundred
        // evenly spread pairs provide.
        var available = steps - lag + 1;
        var stride = Math.Max(1, available / MaximumSamples);

        squares.Clear();
        for (var i = 0; i <= steps - lag; i += stride)
          squares.Add(walk[i].DistanceToSquared(walk[i + lag]));

        // A walk which returns exactly to where it was has no logarithm, and a
        // grid walk does that often enough to be worth guarding against.
        var median = Median(squares);
        if (!(median > 0.0))
          continue;

        logSeparation.Add(Math.Log(lag));
        logDistance.Add(Math.Log(median));
      }

      if (logSeparation.Count < 4)
        return double.NaN;

      return 0.5 * Slope(logSeparation, logDistance);
    }
    /// <summary>
    /// Choose the separations at which to measure the profile. They are spaced
    /// evenly on a logarithmic scale, since that is the scale the fit is made
    /// on, and short separations would otherwise dominate it.
    /// </summary>
    private static int[] Lags(int longest)
    {
      var lags = new List<int>(MaximumLags);
      var factor = Math.Log(longest) / (MaximumLags - 1);

      for (var i = 0; i < MaximumLags; i++)
      {
        var lag = (int)Math.Round(Math.Exp(i * factor));
        lag = Math.Max(1, Math.Min(longest, lag));

        if (lags.Count == 0 || lags[lags.Count - 1] != lag)
          lags.Add(lag);
      }

      return lags.ToArray();
    }
    /// <summary>
    /// Median of a set of values. The list is sorted in place, which the caller
    /// is welcome to since it clears the list before each use.
    /// </summary>
    private static double Median(List<double> values)
    {
      if (values.Count == 0)
        return 0.0;

      values.Sort();

      var middle = values.Count / 2;
      if (values.Count % 2 == 1)
        return values[middle];

      return 0.5 * (values[middle - 1] + values[middle]);
    }
    /// <summary>
    /// Slope of the least squares line through a set of points.
    /// </summary>
    private static double Slope(List<double> x, List<double> y)
    {
      double sx = 0.0, sy = 0.0;
      for (var i = 0; i < x.Count; i++)
      {
        sx += x[i];
        sy += y[i];
      }

      var mx = sx / x.Count;
      var my = sy / y.Count;

      double sxy = 0.0, sxx = 0.0;
      for (var i = 0; i < x.Count; i++)
      {
        var dx = x[i] - mx;
        sxy += dx * (y[i] - my);
        sxx += dx * dx;
      }

      if (!(sxx > 0.0))
        return double.NaN;

      return sxy / sxx;
    }

    private const int MaximumLags = 32;
    private const int MaximumSamples = 512;
  }
}
