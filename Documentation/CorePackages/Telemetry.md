# Telemetry

### What is it?

Component responsible for notifying [Helix](https://helix.dot.net/) of build start and finish events. Essentially, it interacts with the Helix API to notify that a new build is starting or have just finished.

The current implementation for sending start events can be found [here](../../eng/common/templates/steps/telemetry-start.yml) and, for build finish events, it's available [here](../../eng/common/templates/steps/telemetry-end.yml).

### When is it run?

If telemetry is enabled on the build definition it will be executed for every build that execute.

### What is the result?

As a result of this interaction the status of the build will be displayed on the [Mission Control](https://mc.dot.net/) web site. Also, you'll be able to see the build status history for the project.


### Will I use it automatically when onboarding?

Yes. As part of the onboarding process in Arcade you will copy the `eng` folder from the root of the repository. The YAML build definition scripts on that folder take care of setting up the telemetry infrastructure.

Note that this component is not included in the Arcade SDK.