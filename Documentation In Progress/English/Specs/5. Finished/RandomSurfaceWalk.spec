Id: d54d2f29-89a5-48c5-bef9-29a20695d07f

Simple: Perform a random walk along a two-dimensional surface constraint. The constraint may be an infinite plane, a surface or a mesh.

Each step of the walk is taken on the plane tangent to the surface at the position of the previous step, and then projected back onto the surface. For curved surfaces this yields a final step size smaller than the requested step size. This effect is particularly noticeable when the step crosses a sharp surface seam.

Advanced: 

Keywords: 

Examples: RandomSurfaceWalk.ghz

Internal: 

External: 

Author: Robert McNeel & Associates

Notes: 

State: Finished