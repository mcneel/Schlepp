using System;

using Rhino.Geometry;

using GrasshopperIO;

using Grasshopper2.UI;
using Grasshopper2.Components;
using Grasshopper2.Extensions;
using Grasshopper2.Types.Random;
using Grasshopper2.Types.Fields;
using Grasshopper2.Parameters.Standard;

namespace Schlepp
{
  [IoId("d54d2f29-89a5-48c5-bef9-29a20695d07f")]
  public sealed class RandomSurfaceWalk : Component
  {
    public RandomSurfaceWalk()
      : base(new Nomen("Random Surface Walk", "Generate a random walk along a 2D surface.", "Maths", "Random", 1111, Rank.Obscure))
    { }
    public RandomSurfaceWalk(IReader reader) : base(reader) { }

    protected override void AddInputs(InputAdder inputs)
    {
      var sphere = new Sphere(new Point3d(0, 0, 10), 10);

      inputs.AddPoint("Start", "Pt", "Start location of walk.").Set(Point3d.Origin);
      inputs.AddInteger("Steps", "Sn", "Number of steps to take.").Set(100);
      inputs.AddField("Stride", "St", "Length of a single step.").Set(1.0);
      inputs.AddGeneric("Surface", "Sf", "Surface constraint. May be a plane, a surface or a mesh.").Set(sphere);
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
      access.GetItem(3, out object constraint);
      access.GetItem(4, out RandomEngine engine);

      access.RectifyPositive(ref steps, "Steps");

      // Resolve the constraint into a single projection up front, rather than
      // re-deciding what kind of geometry it is on every step of the walk.

      Func<Point3d, Plane> project;
      if (constraint is Plane pl)
        project = PlaneProjector(pl);
      else if (constraint is Mesh ms)
        project = MeshProjector(ms);
      else
        switch (SurfaceBroker.CastOrConvert(constraint, out var p1, out var p3, out var p4))
        {
          case SurfaceLikeType.Brep:
            project = BrepProjector(p3);
            break;

          case SurfaceLikeType.Surf:
            project = BrepProjector(p1.ToBrep());
            break;

          case SurfaceLikeType.SubD:
            project = MeshProjector(Mesh.CreateFromSubD(p4, SubDDisplayParameters.Density.DefaultDensity));
            break;

          default:
            access.AddError("Invalid Constraint", "The surface constraint was not a plane, surface or mesh.");
            return;
        }

      if (project is null)
      {
        access.AddError("Invalid Constraint", "The surface constraint could not be turned into something projectable.");
        return;
      }

      var random = engine.CreateInstance();

      var here = project(start);
      if (!here.IsValid)
      {
        access.AddError("Projection Failed", "The start point could not be projected onto the surface constraint.");
        return;
      }

      var walk = new Polyline(steps + 1) { here.Origin };
      var token = access.Solution.Token;
      for (var i = 0; i < steps; i++)
      {
        token.ThrowIfCancellationRequested();

        // Comparison inverted on purpose. A field evaluates to NaN 
        // outside its domain, and NaN fails every comparison.
        var step = Math.Abs(stride.ScalarAt(here.Origin));
        if (!(step > 1e-12))
        {
          access.AddWarning("Zero Stride", "The stride distance dropped to zero or became undefined, signaling the premature end of the random walk.");
          break;
        }

        var v2 = random.NextUnitVector2D();
        var v3 = here.PointAt(v2.X, v2.Y) - here.Origin;
        var pn = here.Origin + v3 * step;

        var next = project(pn);
        if (!next.IsValid)
        {
          access.AddWarning("Projection Failed", "A step could not be projected back onto the surface constraint, signaling the premature end of the random walk.");
          break;
        }

        here = next;
        walk.Add(here.Origin);
      }

      if (walk.Count >= 2)
        access.SetItem(0, walk);
      else
        access.AddWarning("Empty Walk", "A valid walk needs at least a single step.");

      access.SetItem(1, walk.Last);
    }

    /// <summary>
    /// Create a projection onto an infinite plane.
    /// </summary>
    /// <param name="plane">Plane to project onto.</param>
    /// <returns>Projection, or null if the plane is unusable.</returns>
    private static Func<Point3d, Plane> PlaneProjector(Plane plane)
    {
      if (!plane.IsValid)
        return default;

      return point =>
      {
        var frame = plane;
        frame.Origin = plane.ClosestPoint(point);
        return frame;
      };
    }

    /// <summary>
    /// Create a projection onto the closest point of a mesh.
    /// </summary>
    /// <param name="mesh">Mesh to project onto.</param>
    /// <returns>Projection, or null if the mesh is unusable.</returns>
    private static Func<Point3d, Plane> MeshProjector(Mesh mesh)
    {
      if (mesh is null)
        return default;

      return point =>
      {
        var mp = mesh.ClosestMeshPoint(point, 0.0);
        if (mp is null)
          return Plane.Unset;

        return new Plane(mesh.PointAt(mp), mesh.NormalAt(mp));
      };
    }

    /// <summary>
    /// Create a projection onto the closest point of a brep.
    /// </summary>
    /// <param name="brep">Brep to project onto.</param>
    /// <returns>Projection, or null if the brep is unusable.</returns>
    private static Func<Point3d, Plane> BrepProjector(Brep brep)
    {
      if (brep is null)
        return default;

      return point =>
      {
        if (!brep.ClosestPoint(point, out var p, out var ci, out _, out _, 0.0, out var n))
          return Plane.Unset;

        // When the closest point falls on an edge rather than on the interior of 
        // a face, ClosestPoint hands back the edge *tangent* instead of a normal.
        if (ci.ComponentIndexType == ComponentIndexType.BrepEdge)
          n = EdgeNormal(brep, ci.Index, p);

        return new Plane(p, n);
      };
    }

    /// <summary>
    /// Average the normals of all faces that meet at a brep edge, evaluated at
    /// a point on that edge.
    /// </summary>
    /// <param name="brep">Brep which owns the edge.</param>
    /// <param name="index">Index of the edge within the brep.</param>
    /// <param name="point">Point on the edge at which to evaluate.</param>
    /// <returns>Averaged unit normal, or an invalid vector if no face could be evaluated.</returns>
    private static Vector3d EdgeNormal(Brep brep, int index, Point3d point)
    {
      var edge = brep.Edges[index];
      var total = Vector3d.Zero;
      var first = Vector3d.Unset;

      foreach (var f in edge.AdjacentFaces())
      {
        var face = brep.Faces[f];
        if (!face.ClosestPoint(point, out var u, out var v))
          continue;

        // NormalAt evaluates the underlying surface, which points the other way
        // on a face whose orientation was flipped. Brep.ClosestPoint accounts for
        // this when it reports a face normal, so we must too, or the two branches
        // would disagree about which side of the brep is out.
        var normal = face.NormalAt(u, v);
        if (face.OrientationIsReversed)
          normal = -normal;

        if (!normal.Unitize())
          continue;

        if (!first.IsValid)
          first = normal;

        total += normal;
      }

      if (total.Unitize())
        return total;

      return first;
    }
  }
}