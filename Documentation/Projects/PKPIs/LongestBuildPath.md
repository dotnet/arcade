# PKPIs - Longest Build Path

This document will describe how we define longest build path and how we will demonstrate longest build path in the PKPI work.

## Goal

Our goal with longest build path time is to demonstrate how long it will take to create a fully composed product if a runtime change is made, in particular at release time.

## Metrics

We will use three metrics to measure longest build path time:

* Best-case: Best-case is defined by the absolute fastest path through the dependency graph, assuming no issues flowing dependencies through each node of the graph. We will use official build time of each of the nodes in the graph to calculate best-case scenario.
* Worst-case: Worst-case assumes all dependency updates fail and must go through the slowest method for dependency flow. This will be measured by adding official build time of each node with the time a PR takes for each node that is not the leaf node. When we have the new dependency flow, worst case will be defined by doubling the official build time of each node that requires dependency flow.
* Confidence factor: Confidency factor is a metric that will help us determine how likely the longest build path is to be closer to the best-case scenario vs the worst case scenario. This will be determined by our dependency flow metrics, specifically the metric that determines how likely dependencies flow automatically.

## Views

We will have two views of the longest build path time: an average over a specific period of time, and a line graph that displays how our longest build path time is changing over time.

### Longest Build Path time in a given time period

This graph will be a bar graph and display two measurements: the best case measurement in the time period and the worst case measurement in the time period.

![LongestBuildPathStacked](./lbp-stacked.png)

![LongestBuildPathBreakdown](./lbp-bd.png)

### Longest Build Path time over time

This graph will be a line graph and display the same measurements as the first, but over time.

![LongestBuildPathOverTime](./lbp-ot.png)
