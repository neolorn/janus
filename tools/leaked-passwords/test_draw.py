"""The draw of the offline leaked-password list, run against a local fake of the range API.

Run with: python -B -m unittest discover tools/leaked-passwords
"""

import gzip
import os
import tempfile
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import draw

DRAWN = "2026-09-26"
RANGES = 64
MOST = 40


def body(prefix):
    """What the fake answers for one range: five suffixes, their counts tied across ranges."""
    number = int(prefix, 16)

    return "\r\n".join(
        f"{number * 5 + line:035X}:{(number * 7 + line * 3) % 23 + 1}" for line in range(5)
    )


class RangeApi(BaseHTTPRequestHandler):
    """The range API as the draw reads it, recording every request it is asked."""

    asked = []
    refused = set()
    lock = threading.Lock()

    def do_GET(self):
        prefix = self.path.rsplit("/", 1)[-1]

        with self.lock:
            self.asked.append((prefix, self.headers.get("User-Agent")))

        if prefix in self.refused:
            self.send_error(503)
            return

        answer = body(prefix).encode("ascii")

        # Half the ranges come back compressed, as the API compresses on request.
        compressed = int(prefix, 16) % 2 == 0 and "gzip" in self.headers.get("Accept-Encoding", "")

        if compressed:
            answer = gzip.compress(answer)

        self.send_response(200)
        self.send_header("Content-Length", str(len(answer)))

        if compressed:
            self.send_header("Content-Encoding", "gzip")

        self.end_headers()
        self.wfile.write(answer)

    def log_message(self, format, *args):
        pass


class DrawTests(unittest.TestCase):
    def setUp(self):
        RangeApi.asked = []
        RangeApi.refused = set()
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), RangeApi)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.api = f"http://127.0.0.1:{self.server.server_address[1]}/range/"
        self.folder = tempfile.TemporaryDirectory()

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()
        self.folder.cleanup()

    def run_draw(self, name):
        return draw.draw(
            os.path.join(self.folder.name, name + ".txt"),
            os.path.join(self.folder.name, name + ".json"),
            DRAWN,
            api=self.api,
            ranges=RANGES,
            most=MOST,
            workers=4,
            batch=8,
            attempts=1,
        )

    def read(self, name):
        with open(os.path.join(self.folder.name, name + ".txt"), encoding="ascii", newline="") as handle:
            return handle.read()

    def test_CONV_VCS_005_AC4_EveryRangeRequestNamesTheDraw(self):
        self.run_draw("list")

        self.assertEqual(sorted(f"{each:05X}" for each in range(RANGES)), sorted(prefix for prefix, _ in RangeApi.asked))
        self.assertEqual({draw.agent(DRAWN)}, {user_agent for _, user_agent in RangeApi.asked})
        self.assertIn(DRAWN, draw.agent(DRAWN))

    def test_AUTH_PASS_004_TheListIsTheMostPrevalentHashesInOrderUnderItsDate(self):
        self.run_draw("list")

        every = [
            (int(line.partition(":")[2]), f"{each:05X}" + line.partition(":")[0])
            for each in range(RANGES)
            for line in body(f"{each:05X}").split("\r\n")
        ]
        # The highest counts, the lower hash first among equal counts.
        kept = sorted(whole for _, whole in sorted(every, key=lambda ranked: (-ranked[0], ranked[1]))[:MOST])

        self.assertEqual(f"# {DRAWN}\n" + "".join(whole + "\n" for whole in kept), self.read("list"))

    def test_CONV_VCS_005_AC4_AStoppedDrawResumesFromItsCheckpoint(self):
        RangeApi.refused = {"00013"}

        with self.assertRaises(RuntimeError):
            self.run_draw("resumed")

        first = {prefix for prefix, _ in RangeApi.asked}
        done, _ = draw.load(os.path.join(self.folder.name, "resumed.json"), RANGES)
        checkpointed = {f"{each:05X}" for each in range(RANGES) if done[each]}

        self.assertFalse(os.path.exists(os.path.join(self.folder.name, "resumed.txt")))
        self.assertTrue(checkpointed)
        self.assertNotIn("00013", checkpointed)
        self.assertTrue(checkpointed <= first)

        RangeApi.asked = []
        RangeApi.refused = set()
        self.run_draw("resumed")

        self.assertEqual(
            sorted(f"{each:05X}" for each in range(RANGES) if f"{each:05X}" not in checkpointed),
            sorted(prefix for prefix, _ in RangeApi.asked))

        RangeApi.asked = []
        self.run_draw("whole")

        self.assertEqual(self.read("whole"), self.read("resumed"))


if __name__ == "__main__":
    unittest.main()
