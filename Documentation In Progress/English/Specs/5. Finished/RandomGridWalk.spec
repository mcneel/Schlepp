Id: 3129ad72-ad43-484d-8065-0bf3e70f0f37

Simple: Perform a random walk along the edges of an infinite square grid. This components offers three distinct algorithms for generating the walk\:

| presets for input 4 |

The naïve solver randomly goes left, right or straight at every step, preventing it from immediately re-visiting the grid node it came from, but it does not prevent the walk from re-visiting previously occupied nodes.

The backtracking solver undoes the most recent steps in case of a self-collision and tries again with a different turn. As the walk length grows, this solver is more and more likely to hem itself in, requiring ever longer backtrack phases.

The pivot solver starts with a straight walk (which is guaranteed to be non-self-intersecting), then applies a series random bends, rejecting each change which results in a self-intersection. The resulting walk is much less bunched up than the backtracking solver, but its performance scales even worse with walk length.

Advanced: 

Keywords: 

Examples: RandomGridWalk.ghz

Internal: 

External: 

Author: Robert McNeel & Associates

Notes: 

State: Finished