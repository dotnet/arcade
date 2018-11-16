#!/usr/bin/env python

import sys
import os
import re
import helix.settings
import helix.logs

log = helix.logs.get_logger()

def find_runner(search_dir, framework):
    for root, dirs, files in os.walk(search_dir):
        for file_name in files:
            if re.search(framework.replace(r".", r"\.") + r"[\\/]xunit\.console\.(dll|exe)$", os.path.join(root, file_name)):
                os.path.join(root, file_name)
    return None

def main():
    settings = helix.settings.settings_from_env()

    if len(sys.argv) < 2:
        target_framework = r"netcoreapp2.0"
    else:
        target_framework = sys.argv[1]

    correlation_dir = settings.correlation_payload_dir

    runner_dll_loc = find_runner(correlation_dir, target_framework)
    if runner_dll_loc is None:
        log.error("Unable to run xUnit tests: no xunit.console runner found for framework " + target_framework)
        return 1
    else:
        log.info("Found xUnit console runner of target framework " + target_framework + " at " + runner_dll_loc)

    os.environ["XUNIT_CONSOLE_RUNNER"] = runner_dll_loc
    log.info("Set environment variable XUNIT_CONSOLE_RUNNER to " + runner_dll_loc)
    
    return 0

if __name__ == '__main__':
    sys.exit(main())
