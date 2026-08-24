using System;
using System.Collections.Generic;
using System.Threading;

using Rhino.Geometry;

using GrasshopperIO;

using Grasshopper2.UI;
using Grasshopper2.Components;
using Grasshopper2.Types.Random;
using Grasshopper2.UI.Toolbar;

namespace Schlepp
{
  /// <summary>
  /// The algorithms available for generating a grid walk. They differ in whether
  /// the walk may cross itself, and in what a self-avoiding walk costs to make.
  /// </summary>
  public enum WalkMethod
  {
    /// <summary>
    /// Each step picks a random direction, never doubling straight back but
    /// otherwise free to cross and revisit earlier parts of the walk. Instant
    /// at any length.
    /// </summary>
    [UiInfo("Fast walk which never doubles back, but may cross itself."), UiTint("Orange7")]
    Naive = 0,

    /// <summary>
    /// A self-avoiding walk sampled fairly by the pivot method: a straight walk
    /// of the full length is crumpled by repeated random symmetries. The cost
    /// grows with the square of the step count, but it is predictable, and the
    /// requested length is always delivered.
    /// </summary>
    [UiInfo("Fair self-avoiding walk. Doesn't scale well to long walks."), UiTint("Pink7")]
    Pivot = 1,

    /// <summary>
    /// A self-avoiding walk grown step by step, backtracking out of dead ends.
    /// Much faster than the pivot below several thousand steps, but it favours
    /// walks which head for open ground, and its running time on long walks is
    /// erratic.
    /// </summary>
    [UiInfo("Self-avoiding walk grown step by step. Fast, but erratic on long walks."), UiTint("Violet7")]
    Backtrack = 2,
  }

  [IoId("3129ad72-ad43-484d-8065-0bf3e70f0f37")]
  public sealed class RandomGridWalk : Component
  {
    /// <summary>
    /// Default constructor.
    /// </summary>
    public RandomGridWalk()
      : base(new Nomen("Random Grid Walk", "Generate a random walk along the edges of an orthogonal grid.", "Maths", "Random", 1111, Rank.Obscure))
    { }

    /// <summary>
    /// Deserialisation constructor.
    /// </summary>
    /// <param name="reader">Reader to deserialise from.</param>
    public RandomGridWalk(IReader reader) : base(reader) { }

    protected override void AddInputs(InputAdder inputs)
    {
      inputs.AddPlane("Start", "Pl", "Start and orientation of walk.").Set(Plane.WorldXY);
      inputs.AddInteger("Steps", "Sn", "Number of steps to take.").Set(100);
      inputs.AddNumber("Stride", "St", "Length of a single step.").Set(1.0);
      inputs.AddEnum("Method", "Md", "Walk generation algorithm.", WalkMethod.Backtrack);
      inputs.AddRandom("used to drive the walk.");
    }
    protected override void AddOutputs(OutputAdder outputs)
    {
      outputs.AddPolyline("Walk", "Pl", "Polyline representing the walked path.");
      outputs.AddPoint("Terminus", "Pt", "End of walk.");
    }

    protected override void Process(IDataAccess access)
    {
      access.GetItem(0, out Plane plane);
      access.GetItem(1, out int steps);
      access.GetItem(2, out double stride);
      access.GetItem(3, out WalkMethod method);
      access.GetItem(4, out RandomEngine engine);

      access.RectifyPositive(ref stride, "Stride");
      access.RectifyPositive(ref steps, "Steps");

      var random = engine.CreateInstance();
      var token = access.Solution.Token;

      List<I2> walk;
      switch (method)
      {
        case WalkMethod.Naive:
          walk = WalkNaïve(random, steps, token);
          break;
        case WalkMethod.Pivot:
          walk = WalkPivot(random, steps, token);
          break;
        case WalkMethod.Backtrack:
          walk = WalkBacktrack(random, steps, token);
          break;
        default:
          access.AddError("Unknown Method", $"There is no walk method number {method}.");
          return;
      }

      var path = new Polyline(walk.Count);
      foreach (var cell in walk)
        path.Add(plane.PointAt(cell.I * stride, cell.J * stride));

      if (path.Count >= 2)
        access.SetItem(0, path);
      else
        access.AddWarning("Empty Walk", "A valid walk needs at least a single step.");

      access.SetItem(1, path.Last);
    }

    private readonly struct I2
    {
      public readonly int I;
      public readonly int J;
      public readonly long H;

      public I2(int i, int j)
      {
        I = i;
        J = j;

        // Each half is masked down to 32 bits before being packed. Casting a
        // negative int straight to long sign-extends it, which sets every high
        // bit and collapses whole rows of the grid onto a single value.
        H = (uint)i | (long)(uint)j << 32;
      }

      public I2 TurnAndStep(int direction)
      {
        switch (direction)
        {
          case 0: return new I2(I - 1, J);
          case 1: return new I2(I, J - 1);
          case 2: return new I2(I + 1, J);
          case 3: return new I2(I, J + 1);
          default: throw new ArgumentOutOfRangeException(nameof(direction));
        }
      }
      public I2 Symmetry(I2 pivot, int symmetry)
      {
        var i = I - pivot.I;
        var j = J - pivot.J;

        switch (symmetry)
        {
          case 0: (i, j) = (-j, +i); break; // Rotate a quarter turn.
          case 1: (i, j) = (-i, -j); break; // Rotate a half turn.
          case 2: (i, j) = (+j, -i); break; // Rotate three quarter turns.
          case 3: (i, j) = (+i, -j); break; // Mirror across the I axis.
          case 4: (i, j) = (-i, +j); break; // Mirror across the J axis.
          case 5: (i, j) = (+j, +i); break; // Mirror across the rising diagonal.
          case 6: (i, j) = (-j, -i); break; // Mirror across the falling diagonal.
          default: throw new ArgumentOutOfRangeException(nameof(symmetry));
        }

        return new I2(pivot.I + i, pivot.J + j);
      }
    }

    private static List<I2> WalkNaïve(Random random, int count, CancellationToken token)
    {
      var here = new I2(0, 0);
      var walk = new List<I2>(count + 1) { here };

      var direction = random.Next(4);
      for (var i = 0; i < count; i++)
      {
        token.ThrowIfCancellationRequested();
        here = here.TurnAndStep(direction);
        walk.Add(here);

        var turn = random.Next(3);
        if (turn == 2) turn = 3;

        direction = (direction + turn) & 3;
      }

      return walk;
    }
    private static List<I2> WalkPivot(Random random, int steps, CancellationToken token)
    {
      // Start with a straight path, then repeatedly pick pivots 
      // along it and try to bend the path while avoiding self-intersections.
      var cells = new I2[steps + 1];
      var indices = new Dictionary<long, int>(steps + 1);
      for (var i = 0; i <= steps; i++)
      {
        cells[i] = new I2(i, 0);
        indices[cells[i].H] = i;
      }

      var pivoted = new I2[steps + 1];

      // A handful of accepted pivots per cell is enough to crumple the rod into
      // a convincing wander, and roughly one attempt in three is accepted at
      // these lengths, so the attempts are budgeted at a multiple of that.
      // Raising the multiplier buys statistical quality linearly in time.
      // A walk of a single step has no cell to pivot about: the two-cell rod
      // already is every one-step walk there is, up to the symmetry applied at
      // the end.
      var attempts = steps < 2 ? 0L : 16L * steps;

      for (var attempt = 0L; attempt < attempts; attempt++)
      {
        token.ThrowIfCancellationRequested();

        // Pick a pivot cell and a symmetry, and transform whichever piece of the
        // walk is shorter — the walk is translated back onto the origin at the
        // end, so spinning the head about the pivot is as good as spinning the
        // tail, and on average half as much work.
        var pivot = random.Next(1, steps);
        var symmetry = random.Next(7);

        int from, until;
        if (pivot * 2 < steps)
          (from, until) = (0, pivot - 1);
        else
          (from, until) = (pivot + 1, steps);

        // The moving portion is checked against the statix portion.
        var accepted = true;
        var outward = from == 0 ? until : from;
        var inward = from == 0 ? from : until;
        for (var i = outward; ; i += Math.Sign(inward - outward))
        {
          var cell = cells[i].Symmetry(cells[pivot], symmetry);
          if (indices.TryGetValue(cell.H, out var occupant) && (occupant < from || occupant > until))
          {
            accepted = false;
            break;
          }

          pivoted[i] = cell;
          if (i == inward)
            break;
        }

        if (!accepted)
          continue;

        for (var i = from; i <= until; i++)
          indices.Remove(cells[i].H);

        for (var i = from; i <= until; i++)
        {
          cells[i] = pivoted[i];
          indices[cells[i].H] = i;
        }
      }

      // Move the final walk back to the origin.
      var di = cells[0].I;
      var dj = cells[0].J;
      // var anchor = cells[0];
      // var origin = new I2(0, 0);
      // var facing = random.Next(8);

      var walk = new List<I2>(steps + 1);
      foreach (var cell in cells)
        walk.Add(new I2(cell.I - di, cell.J - dj));
      // foreach (var cell in cells)
      // {
      //   var anchored = new I2(cell.I - anchor.I, cell.J - anchor.J);
      //   walk.Add(facing < 7 ? anchored.Symmetry(origin, facing) : anchored);
      // }

      return walk;
    }
    private static List<I2> WalkBacktrack(Random random, int steps, CancellationToken token)
    {
      var walker = new GridWalker(steps);

      // Consecutive entrapments since the walk last put its trouble behind it,
      // and the length which counts as having done so: the length at the most
      // recent entrapment. Growing past the spot where the walk last got caught
      // proves the escape worked; merely regaining the junction proves nothing.
      // The bar deliberately tracks the latest entrapment rather than the
      // deepest one ever reached. Requiring the walk to beat its all-time record
      // sounds stricter, but it spirals: after a deep teardown the record lies
      // hundreds of trap-free steps away, the escalation then never resets, and
      // every teardown from there on is a huge one. Walks used to hit a hard
      // ceiling near a thousand steps that way, with any amount of patience.
      var setbacks = 0;
      var escapeLength = 0;

      while (walker.StepCount < steps)
      {
        token.ThrowIfCancellationRequested();

        if (walker.Advance(random))
        {
          if (walker.Length > escapeLength)
            setbacks = 0;

          continue;
        }

        // Boxed in. Remember how far the walk got, retreat to where it committed
        // to the doomed corridor, and when entrapments keep coming, abandon ever
        // larger stretches: a trap which survives several corridor retreats is
        // regional rather than local, and picking at it corridor by corridor is
        // what makes exhaustive backtracking hopeless.
        escapeLength = walker.Length;
        walker.RetreatToJunction();
        walker.Retreat(random.Next(2 << Math.Min(setbacks, 12)));
        setbacks++;
      }

      return walker.CopyCells();
    }

    /// <summary>
    /// A self-avoiding walk in progress. The cells of the path double as the
    /// backtracking stack: taking a step pushes a cell, retreating pops one and
    /// thereby frees it up for the walk to pass through again later.
    /// </summary>
    private sealed class GridWalker
    {
      private readonly List<I2> _cells;
      private readonly HashSet<long> _occupied;

      /// <summary>
      /// Create a new walker sitting at the origin.
      /// </summary>
      /// <param name="capacity">Expected number of steps, for pre-allocation.</param>
      public GridWalker(int capacity)
      {
        var origin = new I2(0, 0);
        _cells = new List<I2>(capacity + 1) { origin };
        _occupied = new HashSet<long>(capacity + 1) { origin.H };
      }

      /// <summary>
      /// Gets the number of cells on the path, which is always at least one.
      /// </summary>
      public int Length => _cells.Count;
      /// <summary>
      /// Gets the number of steps taken, which is one less than the cell count.
      /// </summary>
      public int StepCount => _cells.Count - 1;
      /// <summary>
      /// Gets the cell at the walking end of the path.
      /// </summary>
      private I2 Tip => _cells[_cells.Count - 1];

      /// <summary>
      /// Create a copy of the cells along the path.
      /// </summary>
      public List<I2> CopyCells()
      {
        return new List<I2>(_cells);
      }

      /// <summary>
      /// Take one step from the tip onto a randomly chosen free neighbour.
      /// </summary>
      /// <param name="random">Engine driving the choice.</param>
      /// <returns>False if all four neighbours are taken and the walk is boxed in.</returns>
      public bool Advance(Random random)
      {
        var freedom = FreeDirections(Tip, out var count);
        if (count == 0)
          return false;

        // Take the nth free direction. This keeps the choice uniform whether
        // one direction is free or all four are.
        var nth = random.Next(count);
        for (var direction = 0; direction < 4; direction++)
          if ((freedom & 1 << direction) != 0 && nth-- == 0)
          {
            var cell = Tip.TurnAndStep(direction);
            _cells.Add(cell);
            _occupied.Add(cell.H);
            return true;
          }

        throw new InvalidOperationException("A free direction went missing.");
      }

      /// <summary>
      /// Retreat to the junction at which the walk committed to its current dead
      /// end. Popping a cell frees it again, so the way just walked always reads
      /// as open: the first tip with a second free direction is therefore the
      /// most recent point at which the walk had an actual choice.
      /// </summary>
      public void RetreatToJunction()
      {
        do
          Pop();
        while (Length > 1 && FreeCount(Tip) < 2);
      }

      /// <summary>
      /// Retreat a given number of steps, or to the origin, whichever is nearer.
      /// </summary>
      /// <param name="count">Number of steps to undo.</param>
      public void Retreat(int count)
      {
        count = Math.Min(count, Length - 1);
        for (var i = 0; i < count; i++)
          Pop();
      }

      /// <summary>
      /// Undo the most recent step.
      /// </summary>
      private void Pop()
      {
        _occupied.Remove(Tip.H);
        _cells.RemoveAt(_cells.Count - 1);
      }

      /// <summary>
      /// Compute the directions leading from a cell to cells not on the path.
      /// </summary>
      /// <param name="cell">Cell to look around.</param>
      /// <param name="count">Number of free directions, from 0 to 4.</param>
      /// <returns>Bitmask of free directions.</returns>
      private int FreeDirections(I2 cell, out int count)
      {
        var mask = 0;
        count = 0;

        for (var direction = 0; direction < 4; direction++)
          if (!_occupied.Contains(cell.TurnAndStep(direction).H))
          {
            mask |= 1 << direction;
            count++;
          }

        return mask;
      }

      /// <summary>
      /// Count the free directions around a cell.
      /// </summary>
      /// <param name="cell">Cell to look around.</param>
      private int FreeCount(I2 cell)
      {
        FreeDirections(cell, out var count);
        return count;
      }
    }
  }
}