# Telemetry

### What is it?

Component responsible for notifying [Helix](https://helix.dot.net/) of build start and finish events. Essentially, it interacts with the Helix API to notify that a new build is starting or has just finished.

The current implementation for sending start events can be found [here](../../eng/common/templates/steps/telemetry-start.yml) and, for build finish events, it's available [here](../../eng/common/templates/steps/telemetry-end.yml).

### When does it run?

If telemetry is enabled on YAML build definition (file `eng\build.yml` -> `enableTelemetry`), telemetry events will be sent for every queued build.

### What is the result?

Executing these telemetry scripts during the build is part of the prerequisites to see your build results on the [Mission Control](https://mc.dot.net/) web site. Also, you'll be able to see the build status history for the project.

### Will the repo automatically report telemetry after on boarded?

The scripts automate the process, but there is some setup that you have to do. 

As part of the onboarding process in Arcade you're supposed to copy the `eng\common` folder from the root of the repository and reference the  `eng\common\templates\phases\base.yml` file on your YAML build definitions. This will plug in the use of the telemetry scripts. For the telemetry scripts to work you will also have to set `enableTelemetry` (see file `eng\build.yml` -> `enableTelemetry`) to true and specify values for the Helix parameters (_HelixType, _HelixSource and _HelixBuildConfig). You can find some description of these Helix parameters [here](https://github.com/dotnet/arcade/blob/master/eng/common/templates/phases/base.yml).

Note that this component (the telemetry implementation) is not part of the Arcade SDK. Only referencing the SDK isn't sufficient to use the telemetry scripts.

