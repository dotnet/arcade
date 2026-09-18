# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

"""Dependency-free process execution required by the legacy script runner."""

import os
import subprocess

import helix.logs


def run_and_log_output(args, cwd=None, env=None):
    process_environment = os.environ.copy()
    if env is not None:
        process_environment.update(env)

    log = helix.logs.get_logger()
    log.info("Running: %s", " ".join(args))
    if cwd is not None:
        log.info("CWD: %s", cwd)

    process = subprocess.Popen(
        args,
        cwd=cwd,
        env=process_environment,
        stderr=subprocess.STDOUT,
        stdout=subprocess.PIPE,
        stdin=subprocess.PIPE,
        shell=False,
    )
    process.stdin.close()

    with process.stdout:
        for line in iter(process.stdout.readline, b""):
            log.info("Output: %s", line.rstrip(b"\n\r").decode("utf-8", errors="replace"))

    return_code = process.wait()
    log.info("Exit Code: %s", return_code)
    return return_code
