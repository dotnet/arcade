# CLI and configuration

`Program` parses `JobMonitorOptions`, configures compact console logging, and links three lifetime
signals: CTRL+C, Unix SIGTERM, and `--max-wait-minutes`.

The first CTRL+C requests graceful cancellation; a later signal may terminate normally. Unix
SIGTERM is intercepted so the monitor can report timeout state and request Helix cancellation.

Important defaults:

| Option | Default |
| --- | --- |
| Helix endpoint | `https://helix.dot.net/` |
| Poll interval | 30 seconds, with a five-second runtime floor |
| Maximum wait | 360 minutes |
| Monitor job name | `HelixJobMonitor` |
| Result upload parallelism | 4 |
| Result attachments | failed tests |
| Fail on uploaded test failures | true |
| Allow no Helix jobs | false |

Build, project, repository, branch, stage, attempt, tokens, and working directory may default from
Azure DevOps environment variables. Required identity and endpoint values are validated before
services are created.

Help exits `2`. Parse and unhandled runtime failures emit an Azure DevOps error and exit `1`.
Normal success exits `0`; monitored failure or cancellation exits `1`.
