# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

import ast
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
import unittest.mock


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


# Helix only guarantees Python >= 3.4 on the client (see HELIX_PYTHONPATH in
# the Helix SDK readme). The shipped shim targets that interpreter, and so does
# this suite, so the shim can be verified on the oldest Python it must support.
# These helpers stand in for APIs that arrived later:
#   subprocess.run            3.5 (capture_output and text are 3.7)
#   pathlib.Path.read_text    3.5, along with write_text/read_bytes
#   importlib.util.module_from_spec  3.5
def run_child(args, **popen_arguments):
    process = subprocess.Popen(
        args,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        universal_newlines=True,
        **popen_arguments
    )
    stdout, stderr = process.communicate()
    return process.returncode, stdout, stderr


def read_bytes(path):
    with open(str(path), "rb") as handle:
        return handle.read()


def read_text(path, encoding="utf-8"):
    with io.open(str(path), "r", encoding=encoding) as handle:
        return handle.read()


def write_text(path, text):
    with io.open(str(path), "w") as handle:
        handle.write(text)


# PathFinder locates a module on the search path without importing it.
# importlib.util.find_spec would import the parent 'helix' package first,
# executing whatever the installed legacy package runs at import time.
_RESOLVE_HELIX_LOGS = """
import sys
from importlib.machinery import PathFinder

package = PathFinder.find_spec('helix', sys.path)
locations = list(package.submodule_search_locations or []) if package else []
module = PathFinder.find_spec('helix.logs', locations) if locations else None
print(module.origin if module and module.origin else '')
"""


def resolve_child_helix_logs():
    """Return the helix.logs file a child process would load, and any failure text.

    A regular `helix` package installed on the machine wins over this
    namespace-package shim even when the shim comes first on PYTHONPATH. That
    precedence is intentional so legacy queues keep their installed package.

    This resolves the module without importing anything. The installed legacy
    package reads HELIX_CONFIG_ROOT and HELIX_LOG_ROOT at import time and
    raises without them, so importing it here would fail for a reason unrelated
    to the shim.
    """
    returncode, stdout, stderr = run_child(
        [sys.executable, "-c", _RESOLVE_HELIX_LOGS],
        env=child_environment(),
        cwd=str(RUNNER_PATH.parent),
    )

    origin = stdout.strip()
    if returncode != 0 or not origin:
        return None, "exit code {}\nstdout:\n{}\nstderr:\n{}".format(
            returncode, stdout, stderr
        )

    return pathlib.Path(origin), None


def load_module(name):
    if name == "helix.azure_storage":
        helix = types.ModuleType("helix")
        helix.__path__ = [str(COMPATIBILITY_ROOT)]
        sys.modules["helix"] = helix
        helix.logs = load_module("helix.logs")

    path = COMPATIBILITY_ROOT / (name.rsplit(".", 1)[-1] + ".py")
    spec = importlib.util.spec_from_file_location(name, str(path))
    # Build the module the way importlib.util.module_from_spec (3.5) would.
    module = types.ModuleType(spec.name)
    module.__spec__ = spec
    module.__loader__ = spec.loader
    module.__file__ = str(path)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


# Constructs newer than the oldest interpreter Helix guarantees, keyed by the
# version that introduced them. A regression here is not a style issue: the
# shipped shim raised AttributeError on every staged upload when it reached for
# os.path.commonpath, so TestResults.zip could not be staged at all.
_TOO_NEW_DOTTED_NAMES = {
    "asyncio.run": "3.7",
    "functools.cached_property": "3.8",
    "importlib.util.module_from_spec": "3.5",
    "math.isclose": "3.5",
    "os.path.commonpath": "3.5",
    "os.scandir": "3.5",
    "subprocess.CompletedProcess": "3.5",
    "subprocess.run": "3.5",
}

# pathlib.Path methods. The receiver is not always statically known, so these
# match on the attribute name alone.
_TOO_NEW_ATTRIBUTES = {
    "read_bytes": "3.5",
    "read_text": "3.5",
    "write_bytes": "3.5",
    "write_text": "3.5",
}

_TOO_NEW_MODULES = {
    "dataclasses": "3.7",
    "graphlib": "3.9",
    "secrets": "3.6",
    "zoneinfo": "3.9",
}

_TOO_NEW_KEYWORDS = {
    "capture_output": "3.7",
}

# Syntax that older interpreters cannot even parse. Looked up by name because
# these node types do not exist on the older interpreters themselves.
_TOO_NEW_NODE_TYPES = (
    ("AsyncFunctionDef", "3.5"),
    ("Await", "3.5"),
    ("JoinedStr", "3.6"),
    ("NamedExpr", "3.8"),
)


def _dotted_name(node):
    parts = []
    while isinstance(node, ast.Attribute):
        parts.append(node.attr)
        node = node.value

    if not isinstance(node, ast.Name):
        return None

    parts.append(node.id)
    parts.reverse()
    return ".".join(parts)


def find_constructs_newer_than_python34(path):
    """Report constructs in path that the oldest supported interpreter lacks."""
    tree = ast.parse(read_text(path), filename=str(path))
    found = []

    for node in ast.walk(tree):
        line = getattr(node, "lineno", 0)

        if isinstance(node, ast.Attribute):
            dotted = _dotted_name(node)
            if dotted in _TOO_NEW_DOTTED_NAMES:
                found.append((line, dotted, _TOO_NEW_DOTTED_NAMES[dotted]))
            elif node.attr in _TOO_NEW_ATTRIBUTES:
                found.append((line, "." + node.attr, _TOO_NEW_ATTRIBUTES[node.attr]))
        elif isinstance(node, ast.Import):
            for alias in node.names:
                root = alias.name.split(".")[0]
                if root in _TOO_NEW_MODULES:
                    found.append((line, alias.name, _TOO_NEW_MODULES[root]))
        elif isinstance(node, ast.ImportFrom):
            root = (node.module or "").split(".")[0]
            if root in _TOO_NEW_MODULES:
                found.append((line, node.module, _TOO_NEW_MODULES[root]))
        elif isinstance(node, ast.Call):
            for keyword in node.keywords:
                if keyword.arg in _TOO_NEW_KEYWORDS:
                    found.append(
                        (line, keyword.arg + "=", _TOO_NEW_KEYWORDS[keyword.arg])
                    )

        for type_name, version in _TOO_NEW_NODE_TYPES:
            node_type = getattr(ast, type_name, None)
            if node_type is not None and isinstance(node, node_type):
                found.append((line, type_name, version))

    return found


class LegacyRunnerCompatTests(unittest.TestCase):
    def setUp(self):
        self._temporary_directory = tempfile.TemporaryDirectory()
        self.addCleanup(self._temporary_directory.cleanup)
        self._old_upload_root = os.environ.get("HELIX_WORKITEM_UPLOAD_ROOT")
        os.environ["HELIX_WORKITEM_UPLOAD_ROOT"] = self._temporary_directory.name

    def skip_when_installed_helix_wins(self):
        resolved, failure = resolve_child_helix_logs()
        if resolved is None:
            self.fail(
                "A child process could not resolve helix.logs from the compatibility "
                "shim.\n{}".format(failure)
            )

        if resolved.resolve() != (COMPATIBILITY_ROOT / "logs.py").resolve():
            self.skipTest(
                "An installed legacy 'helix' package at {} takes precedence over the "
                "compatibility shim. That is the intended behavior on legacy Helix "
                "queues, so the shim's runner behavior cannot be exercised here.".format(resolved)
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

    def test_sources_run_on_the_oldest_supported_python(self):
        """Helix only guarantees Python >= 3.4 on the client.

        The shipped shim has to run there, and so does this suite, otherwise it
        cannot verify the shim on the interpreter that actually matters.
        """
        sources = sorted(COMPATIBILITY_ROOT.parent.glob("**/*.py"))
        sources.append(pathlib.Path(__file__))
        if RUNNER_PATH.is_file():
            sources.append(RUNNER_PATH)

        reported = []
        for source in sources:
            for line, construct, version in find_constructs_newer_than_python34(source):
                reported.append(
                    "{}:{} uses {} (Python {}+)".format(
                        source.name, line, construct, version
                    )
                )

        self.assertEqual(
            reported,
            [],
            "These sources must run on Python 3.4:\n" + "\n".join(reported),
        )

    def test_upload_stages_stream_under_upload_root(self):
        azure_storage = load_module("helix.azure_storage")

        result = azure_storage.UploadClient().upload(
            io.BytesIO(b"result"), "test/results.bin"
        )

        self.assertEqual(
            read_bytes(result),
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

    def test_upload_rejects_uncomparable_destination(self):
        """A name that lands on another Windows drive must not leak a different error.

        os.path.join resets to the drive in the name, and the containment check
        then fails with its own ValueError instead of the traversal message.
        """
        azure_storage = load_module("helix.azure_storage")

        with unittest.mock.patch(
            "os.path.relpath",
            side_effect=ValueError("path is on mount 'C:', start on mount 'D:'"),
        ):
            with self.assertRaises(ValueError) as caught:
                azure_storage.UploadClient().upload(io.BytesIO(b"result"), "result.bin")

        self.assertIn("HELIX_WORKITEM_UPLOAD_ROOT", str(caught.exception))

    def test_upload_does_not_require_python35(self):
        """Helix only guarantees Python >= 3.4 on the client.

        os.path.commonpath was added in 3.5, so depending on it would make every
        staged upload raise AttributeError on the oldest supported interpreter.
        """
        azure_storage = load_module("helix.azure_storage")

        with unittest.mock.patch("os.path.commonpath") as commonpath:
            result = azure_storage.UploadClient().upload(
                io.BytesIO(b"result"), "compat/results.bin"
            )

        commonpath.assert_not_called()
        self.assertEqual(read_bytes(result), b"result")

    def test_upload_stages_text_streams(self):
        azure_storage = load_module("helix.azure_storage")

        result = azure_storage.UploadClient().upload(
            io.StringIO("text-results"), "text/results.txt"
        )

        self.assertEqual(read_bytes(result), b"text-results")

    def test_staged_log_is_written_as_utf8(self):
        helix_logs = load_module("helix.logs")
        logger = helix_logs.get_logger("compatibility-encoding")

        logger.info("na\u00efve caf\u00e9 \u2713")
        for handler in logging.getLogger().handlers:
            handler.flush()

        log_path = (
            pathlib.Path(self._temporary_directory.name)
            / ".helix-logs"
            / "scriptrunner.log"
        )
        self.assertIn("na\u00efve caf\u00e9 \u2713", read_text(log_path))

    def test_logger_stages_legacy_log(self):
        helix_logs = load_module("helix.logs")
        logger = helix_logs.get_logger("compatibility-test")

        logger.info("compatibility message")
        for handler in logging.getLogger().handlers:
            handler.flush()

        log_path = pathlib.Path(self._temporary_directory.name) / ".helix-logs" / "scriptrunner.log"
        self.assertIn("compatibility message", read_text(log_path))

    def test_legacy_script_runner_import_surface(self):
        self.skip_when_installed_helix_wins()

        returncode, _, stderr = run_child(
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
        )

        self.assertEqual(returncode, 0, stderr)

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
                write_text(
                    script,
                    "@echo off\r\n"
                    "> \"%HELIX_WORKITEM_ROOT%\\TestResults.zip\" <nul set /p =test-results\r\n"
                    "echo appcompat-stdout\r\n"
                    "echo appcompat-stderr 1>&2\r\n",
                )
            else:
                write_text(
                    script,
                    "#!/bin/sh\n"
                    "printf 'test-results' > \"$HELIX_WORKITEM_ROOT/TestResults.zip\"\n"
                    "echo appcompat-stdout\n"
                    "echo appcompat-stderr >&2\n",
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

            returncode, stdout, stderr = run_child(
                [sys.executable, str(RUNNER_PATH), "--script", "RunMstest.cmd"],
                env=environment,
            )

            self.assertEqual(returncode, 0, stdout + stderr)
            staged_result = pathlib.Path(self._temporary_directory.name) / "TestResults.zip"
            self.assertEqual(read_text(staged_result), "test-results")
            staged_log = (
                pathlib.Path(self._temporary_directory.name)
                / ".helix-logs"
                / "scriptrunner.log"
            )
            log_text = read_text(staged_log)
            self.assertIn("appcompat-stdout", log_text)
            self.assertIn("appcompat-stderr", log_text)


if __name__ == "__main__":
    unittest.main()
