# Developer Workflow - Shared Test Infrastructure - Measuring success

This document describes how we will measure the progress and success of the shared test
infrastructure described in https://github.com/dotnet/arcade/issues/6948.

The epic aims to provide both a series of improvements to existing test execution infrastructure,
and to deliver new features and tooling driven by long standing customer requests. 

We will take three different approaches to measuring the success of these efforts:

- Providing measurable improvements to existing infrastructure
- Tracking the usefulness of new features added by the effort by tracking feature adoption
- Evaluating ongoing qualitative customer feedback

## Measuring improvements in existing infrastructure

Any changes we make to existing infrastructure need to be measurable to determine whether they
actually represent an improvement over the existing system.

## Tracking the usefulness of new features through adoption

As we are planning to introduce completely new tooling and features, we need to be able to measure
whether they are considered useful by the product teams. 

We will operate under the assumption that onboarding and continued usage of the shared
infrastructure features directly correlates with their usefulness. 

Under this assumption, we can measure the usefulness of the shared infrastructure by tracking and
monitoring the feature adoption rate of the components that we build.

Feature adoption rate is usually defined as the percentage of users of a system that adopt a new
feature. It is calculated as $(New Feature users / Total Users)
* 100$.

We can adapt this metric to track the adoption of various pieces of the new proposed infrastructure
on a pipeline, repository, or branch level.

- Helix jobs going through the test-aware layer out of the total of Helix jobs
- Helix jobs using the agentless waiting feature out of the total of Helix jobs
- Tests in a pipeline using the new infrastructure out of the total number of tests in a pipeline

Keeping track of these metrics lets us know which pieces of the shared infrastructure are being used
by repositories, and by keeping track of which features see the most and least usage, we should be
able to react to areas that require attention.

## Tracking customer Feedback and acting on it

Measuring improvements in the space of developer workflow and productivity has a heavily qualitative
component. As such, we should continue to gather feedback from our users to ensure we are moving in
the right direction.

    Successful tracking of these issues involves:
    - Reaching out to the reporting customer
    - Making sure next steps are agreed on by the customer
    - In cases where there is followup work needed to resolve the feedback, the issue is prioritized into the correct milestone.

## Shared Test Infrastructure

Along these three categories, some concrete goals:

### Improvements

* Agentless Helix jobs result in a measurable improvement in at least one of these areas:
    * Decrease in overall time spent waiting for a build machine
    * Increase in number of builds that the 1es hosted pools can run per day due to more free build
      agents
    * Cost per PR for a given repo + branch goes down (TBD, we might not have this data available)

### New Feature Adoption

- Users find the new system useful, and onboarded users don't stop using it:
    - Feature adoption rate for the shared infrastructure layer doesn't decrease over time for repos
      that have started onboarding

- Stretch goal for adoption:
    -  60% of daily Helix jobs go through the shared test infrastructure layer 

### Customer feedback

- Continue using the sentiment feedback tools built during previous Developer Workflow efforts to
  gauge the reception of our features. Any new features that users will directly interact with
  should include the customer satisfaction widget.

- As stretch goal, use the Engineering services customer satisfaction survey to gauge customer
  interest in the shared test infrastructure. As with previous efforts, we will use Net Promoter
  Score (NPS) style questions in the surveys, and aim to get a score of 7 or higher. 


## Proof of Concept

The first feature that the shared infrastructure will provide is the [agentless waiting of Helix
jobs during builds](./agentless-helix.md). This is a good feature to test the measurements described
in this document and how we track them throughout the effort.

- Make sure that we collect all the necessary data to calculate time spent waiting for Helix jobs to
  complete and its impact in build times for individual pipelines. 

- Create a dashboard to track overall build time and time spent waiting on helix per pipeline in
  order to be able to measure improvements once pipelines start using the feature.

- We will also want to track the feature adoption rate for agentless jobs on a per repo, branch and
  pipeline level: $(Agentless Jobs/Total Number Of Helix Jobs) * 100$


## Usage Telemetry

- We will need to add telemetry to differentiate flows that use the components of the shared test
  infrastructure from those that don't in order to track feature adoption metrics.
- We have all the data already that we need to track existing usage of Helix. We will need to create
  dashboards where we can visualize any measured improvements.
- Any new components we build need to have enough telemetry to be able to measure its usage.

## Service-ability of Feature

Some of the created dashboards to track epic progress may result useful for monitoring and hand-off
to First responders after enough documentation has been written.

The usage adoption metrics might be something we want to keep track of after the project is
completed, but it will require a process of continued monitoring for them to be valuable. Once the
project is nearing completion we can decide whether they are something we want to keep, or if the
sentiment feedback is enough for ongoing monitoring.
