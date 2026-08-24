Id: cd7279b1-7c33-41fa-b69f-50be8f1b06ca

Simple: Perform a random walk on the interior of a 2D region.

Each step size is determined by the value of the stride function evaluated at the distance to the nearest region boundary. Any step which ends up outside the region is discarded, and 100 failed steps in a row terminate the random walk before the target step count is reached.

Note that a single step may _cross_ region boundaries, provided its end-point is once again on the region interior.

Advanced: 

Keywords: 

Examples: RandomRegionWalk.ghz

Internal: 

External: 

Author: Robert McNeel & Associates

Notes: 

State: Finished