# Service KPIs and Metrics

## Mission

Contribute to the successful release of .NET Core 3.0 by improving developer productivity through efficient build/test iterations

## New Service Guidelines

Telemetry on the health of a service should be considered when the service is designed.

For Azure services, Application Insights is a preferred channel to report telemetry.

As general guidance, consider:

- Telemetry related to the _value_ the service provides to its users, example:
  - Number of events or triggers
  - Queries of a particular payload
  - Count of completed tasks

- Telemetry related to the _health_ of the service, example:
  - Successful and failed operations
  - Unknown events or triggers
  - Malformed payloads
  - Recoverable and unrecoverable errors

Document the service's telemetry. Include for each metric:

- Technical detail on how the data is collected (to help provide context)
- _Why_ the data is collected.
- What is _good_ and _bad_ for the data? How will a monitor determine when to act?
- What _action_ should be taken if telemetry indicates "bad"?

Work with Leadership to identify any additional telemetry for current business efforts.

## First Responder

As GitHub issues is the primary means of work management for DNCEng, it is expected that all First Responder efforts of note be captured as a GitHub issue and added to the "First Responder" Epic. Telemetry and workload data is collected through this channel.

Customers may initiate communication with the First Responder team through other means, such as team alias and direct communication. Substantive communication should be moved to GitHub to allow telemetery gathering, improve workflow clarity, and increase information sharing.

Open a new issue for any customer support effort taking more than fifteen minutes.

Ensure issues are labeled appropriately.

These telemetry guidelines are meant to extend but not superseed operational guidelines set in [First Responder Responsibilities](https://github.com/dotnet/core-eng/wiki/%5Bint%5D-First-Responders).

### Metrics

Name: Count of issues opened by members outside of DNCEng
Goal: Understand FR workload balance
Action:
- Significant external customer load indicates issue in documentation or service quality
- Significant internal customer load indicate technical debt
