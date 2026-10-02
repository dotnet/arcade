import asyncio
import importlib.util
import pathlib
import tempfile
import types
import unittest
from unittest.mock import AsyncMock, patch

import aiohttp


SCRIPT_PATH = pathlib.Path(__file__).parents[1] / "install-debs.py"
SPEC = importlib.util.spec_from_file_location("install_debs", SCRIPT_PATH)
INSTALL_DEBS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(INSTALL_DEBS)


class FakeResponse:
    def __init__(self, status, content=b"package"):
        self.status = status
        self.content = content

    async def __aenter__(self):
        return self

    async def __aexit__(self, exc_type, exc_value, traceback):
        return False

    async def read(self):
        return self.content

    def raise_for_status(self):
        raise aiohttp.ClientResponseError(
            request_info=types.SimpleNamespace(real_url="https://example/package.deb"),
            history=(),
            status=self.status,
        )


class FakeSession:
    def __init__(self, responses):
        self.responses = iter(responses)
        self.request_count = 0

    def get(self, url, timeout):
        self.request_count += 1
        return next(self.responses)


class DownloadFileTests(unittest.IsolatedAsyncioTestCase):
    async def test_retries_server_error_then_succeeds(self):
        session = FakeSession([FakeResponse(502), FakeResponse(200)])

        with tempfile.TemporaryDirectory() as tmp_dir:
            destination = pathlib.Path(tmp_dir) / "package.deb"
            with patch.object(asyncio, "sleep", new=AsyncMock()) as sleep:
                await INSTALL_DEBS.download_file(session, "https://example/package.deb", destination)

            self.assertEqual(destination.read_bytes(), b"package")
            self.assertEqual(session.request_count, 2)
            sleep.assert_awaited_once()

    async def test_retries_request_timeout_and_rate_limit(self):
        for status in (408, 429):
            with self.subTest(status=status):
                session = FakeSession([FakeResponse(status), FakeResponse(200)])

                with tempfile.TemporaryDirectory() as tmp_dir:
                    destination = pathlib.Path(tmp_dir) / "package.deb"
                    with patch.object(asyncio, "sleep", new=AsyncMock()):
                        await INSTALL_DEBS.download_file(session, "https://example/package.deb", destination)

                self.assertEqual(session.request_count, 2)

    async def test_does_not_retry_not_found(self):
        session = FakeSession([FakeResponse(404)])

        with tempfile.TemporaryDirectory() as tmp_dir:
            destination = pathlib.Path(tmp_dir) / "package.deb"
            with patch.object(asyncio, "sleep", new=AsyncMock()) as sleep:
                with self.assertRaisesRegex(Exception, "Status Code: 404"):
                    await INSTALL_DEBS.download_file(session, "https://example/package.deb", destination)

        self.assertEqual(session.request_count, 1)
        sleep.assert_not_awaited()

    async def test_stops_after_configured_attempts(self):
        session = FakeSession([FakeResponse(502), FakeResponse(502), FakeResponse(502)])

        with tempfile.TemporaryDirectory() as tmp_dir:
            destination = pathlib.Path(tmp_dir) / "package.deb"
            with patch.object(asyncio, "sleep", new=AsyncMock()):
                with self.assertRaisesRegex(Exception, "after 3 attempts"):
                    await INSTALL_DEBS.download_file(
                        session,
                        "https://example/package.deb",
                        destination,
                        max_retries=3,
                    )

        self.assertEqual(session.request_count, 3)

    async def test_does_not_retry_checksum_mismatch(self):
        session = FakeSession([FakeResponse(200, b"unexpected")])

        with tempfile.TemporaryDirectory() as tmp_dir:
            destination = pathlib.Path(tmp_dir) / "package.deb"
            with patch.object(asyncio, "sleep", new=AsyncMock()) as sleep:
                with self.assertRaisesRegex(Exception, "SHA256 mismatch"):
                    await INSTALL_DEBS.download_file(
                        session,
                        "https://example/package.deb",
                        destination,
                        checksum="incorrect",
                    )

        self.assertEqual(session.request_count, 1)
        sleep.assert_not_awaited()


if __name__ == "__main__":
    unittest.main()
