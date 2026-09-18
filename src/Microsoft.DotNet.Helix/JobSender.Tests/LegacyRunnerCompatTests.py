# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

import importlib.util
import io
import logging
import os
import pathlib
import subprocess
import sys
import tempfile
import types
import unittest


SOURCE_COMPATIBILITY_ROOT = (
    pathlib.Path(__file__).parents[1] / "JobSender" / "legacy-runner" / "helix"
)
OUTPUT_COMPATIBILITY_ROOT = (
    pathlib.Path(__file__).parent / "JobSender" / "legacy-runner" / "helix"
)
COMPATIBILITY_ROOT = (
    SOURCE_COMPATIBILITY_ROOT
    if SOURCE_COMPATIBILITY_ROOT.is_dir()
    else OUTPUT_COMPATIBILITY_ROOT
)
RUNNER_PATH = pathlib.Path(__file__).parent / "LegacyRunner" / "scriptrunner.py"

# Helix work items export HELIX_* variables, including live result-container SAS
# tokens. Child processes started here must not inherit them: on a legacy queue
# the installed `helix` package wins over this shim, and an inherited token would
# make a test upload to a real results container.
_INHERITED_ENVIRONMENT_NAMES = (
    "COMSPEC",
    "DYLD_LIBRARY_PATH",
    "HOME",
    "LANG",
    "LC_ALL",
    "LD_LIBRARY_PATH",
    "PATH",
    "PATHEXT",
    "SYSTEMROOT",
    "TEMP",
    "TMP",
    "TMPDIR",
    "USERPROFILE",
    "WINDIR",
)


def child_environment(**overrides):
    environment = {
        name: value
        for name, value in os.environ.items()
        if name in _INHERITED_ENVIRONMENT_NAMES
    }
    environment["PYTHONPATH"] = str(COMPATIBILITY_ROOT.parent)
    environment["PYTHONDONTWRITEBYTECODE"] = "1"
    environment.update(overrides)
    return environment


def resolve_child_helix_logs():
    """Return the helix.logs file a child process actually imports.

    A regular `helix` package installed on the machine wins over this
    namespace-package shim even when the shim comes first on PYTHONPATH. That
    precedence is intentional so legacy queues keep their installed package.
    """
    result = subprocess.run(
        [sys.executable, "-c", "import helix.logs; print(helix.logs.__file__)"],
        env=child_environment(),
        cwd=str(RUNNER_PATH.parent),
        capture_output=True,
        text=True,
    )

    if result.returncode != 0 or not result.stdout.strip():
        return None

    return pathlib.Path(result.stdout.strip())


def load_module(name):
    if name == "helix.azure_storage":
        helix = types.ModuleType("helix")
        helix.__path__ = [str(COMPATIBILITY_ROOT)]
        sys.modules["helix"] = helix
        helix.logs = load_module("helix.logs")

    spec = importlib.util.spec_from_file_location(
        name, COMPATIBILITY_ROOT / (name.rsplit(".", 1)[-1] + ".py")
    )
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


class LegacyRunnerCompatTests(unittest.TestCase):
    def setUp(self):
        self._temporary_directory = tempfile.TemporaryDirectory()
        self.addCleanup(self._temporary_directory.cleanup)
        self._old_upload_root = os.environ.get("HELIX_WORKITEM_UPLOAD_ROOT")
        os.environ["HELIX_WORKITEM_UPLOAD_ROOT"] = self._temporary_directory.name

    def skip_when_installed_helix_wins(self):
        imported = resolve_child_helix_logs()
        if imported is None:
            self.fail("A child process could not import helix.logs from the compatibility shim.")

        if imported.resolve() != (COMPATIBILITY_ROOT / "logs.py").resolve():
            self.skipTest(
                "An installed legacy 'helix' package at {} takes precedence over the "
                "compatibility shim. That is the intended behavior on legacy Helix "
                "queues, so the shim's runner behavior cannot be exercised here.".format(imported)
            )

    def tearDown(self):
        root_logger = logging.getLogger()
        for handler in list(root_logger.handlers):
            if getattr(handler, "_helix_legacy_runner_compat", False):
                root_logger.removeHandler(handler)
                handler.close()

        if self._old_upload_root is None:
            os.environ.pop("HELIX_WORKITEM_UPLOAD_ROOT", None)
        else:
            os.environ["HELIX_WORKITEM_UPLOAD_ROOT"] = self._old_upload_root

    def test_upload_stages_stream_under_upload_root(self):
        azure_storage = load_module("helix.azure_storage")

        result = azure_storage.UploadClient().upload(
            io.BytesIO(b"result"), "test/results.bin"
        )

        self.assertEqual(
            pathlib.Path(result).read_bytes(),
            b"result",
        )
        self.assertEqual(
            pathlib.Path(result).relative_to(self._temporary_directory.name),
            pathlib.Path("test/results.bin"),
        )

    def test_upload_rejects_parent_traversal(self):
        azure_storage = load_module("helix.azure_storage")

        with self.assertRaises(ValueError):
            azure_storage.UploadClient().upload(io.BytesIO(b"result"), "../result.bin")

    def test_logger_stages_legacy_log(self):
        helix_logs = load_module("helix.logs")
        logger = helix_logs.get_logger("compatibility-test")

        logger.info("compatibility message")
        for handler in logging.getLogger().handlers:
            handler.flush()

        log_path = pathlib.Path(self._temporary_directory.name) / ".helix-logs" / "scriptrunner.log"
        self.assertIn("compatibility message", log_path.read_text())

    def test_legacy_script_runner_import_surface(self):
        self.skip_when_installed_helix_wins()

        result = subprocess.run(
            [
                sys.executable,
                "-c",
                (
                    "import helix.logs, helix.proc, helix.saferequests; "
                    "from helix.cmdline import command_main; "
                    "from helix.helixio import fix_path; "
                    "from helix_test_execution import HelixTestExecution"
                ),
            ],
            env=child_environment(),
            cwd=str(RUNNER_PATH.parent),
            capture_output=True,
            text=True,
        )

        self.assertEqual(result.returncode, 0, result.stderr)

    def test_appcompat_script_runner_stages_results_and_logs(self):
        self.skip_when_installed_helix_wins()

        with tempfile.TemporaryDirectory() as work_root:
            work_root = pathlib.Path(work_root)
            payload = work_root / "payload"
            correlation = work_root / "correlation"
            execution = work_root / "execution"
            log_root = work_root / "logs"
            for directory in (payload, correlation, execution, log_root):
                directory.mkdir()

            script = payload / "RunMstest.cmd"
            if os.name == "nt":
                script.write_text(
                    "@echo off\r\n"
                    "> \"%HELIX_WORKITEM_ROOT%\\TestResults.zip\" <nul set /p =test-results\r\n"
                    "echo appcompat-stdout\r\n"
                    "echo appcompat-stderr 1>&2\r\n"
                )
            else:
                script.write_text(
                    "#!/bin/sh\n"
                    "printf 'test-results' > \"$HELIX_WORKITEM_ROOT/TestResults.zip\"\n"
                    "echo appcompat-stdout\n"
                    "echo appcompat-stderr >&2\n"
                )
                script.chmod(script.stat().st_mode | 0o100)

            environment = child_environment(
                HELIX_WORKITEM_PAYLOAD=str(payload),
                HELIX_CORRELATION_PAYLOAD=str(correlation),
                HELIX_WORKITEM_ROOT=str(execution),
                HELIX_WORKITEM_UPLOAD_ROOT=self._temporary_directory.name,
                HELIX_LOG_ROOT=str(log_root),
                HELIX_CORRELATION_ID="compat-correlation",
                HELIX_WORKITEM_ID="compat-workitem",
                HELIX_WORKITEM_FRIENDLYNAME="AppCompat",
            )

            result = subprocess.run(
                [sys.executable, str(RUNNER_PATH), "--script", "RunMstest.cmd"],
                env=environment,
                capture_output=True,
                text=True,
            )

            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            staged_result = pathlib.Path(self._temporary_directory.name) / "TestResults.zip"
            self.assertEqual(staged_result.read_text(), "test-results")
            staged_log = (
                pathlib.Path(self._temporary_directory.name)
                / ".helix-logs"
                / "scriptrunner.log"
            )
            log_text = staged_log.read_text()
            self.assertIn("appcompat-stdout", log_text)
            self.assertIn("appcompat-stderr", log_text)


if __name__ == "__main__":
    unittest.main()
