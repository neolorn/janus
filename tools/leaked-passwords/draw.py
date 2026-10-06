"""Draws the offline leaked-password list from the Pwned Passwords range API.

AUTH-PASS-004 and CONV-VCS-005: the list is the 100,000 hashes of highest count across
all 1,048,576 ranges, upper-case SHA-1 in hexadecimal, one per line in ascending order,
under a first line naming the date drawn. The release steps run it once per release.

The draw is resumable. Progress is checkpointed to the state file after every batch of
ranges, and a draw that stops, for a range the API would not answer or for any other
reason, starts again from the last checkpoint. The state holds which ranges are drawn
and the hashes kept from them, and nothing else.

Every request carries a User-Agent that names the draw, as the API's acceptable use asks
of its callers.
"""

import argparse
import base64
import datetime
import gzip
import heapq
import json
import os
import sys
import tempfile
import time
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed

API = "https://api.pwnedpasswords.com/range/"
RANGES = 1 << 20
KEEP = 100_000
WORKERS = 48
BATCH = 4096
ATTEMPTS = 8
HERE = os.path.dirname(os.path.abspath(__file__))
OUTPUT = os.path.join(HERE, "..", "..", "src", "Janus.Hosting", "Passwords", "leaked-passwords.txt")
STATE = os.path.join(tempfile.gettempdir(), "leaked-passwords-draw.json")


def agent(drawn):
    """The User-Agent of a draw made on the given date."""
    return (
        f"leaked-password-list-draw/{drawn} "
        "(drawing the 100,000 most prevalent hashes once for an offline screening list)"
    )


def fetch(api, prefix, user_agent, attempts):
    """One range's body, retried with a growing pause before the draw gives up."""
    request = urllib.request.Request(
        api + prefix,
        headers={"User-Agent": user_agent, "Accept-Encoding": "gzip"},
    )
    failure = None

    for attempt in range(attempts):
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                body = response.read()

                if response.headers.get("Content-Encoding") == "gzip":
                    body = gzip.decompress(body)

                return prefix, body.decode("ascii")
        except OSError as error:
            failure = error

            if attempt + 1 < attempts:
                time.sleep(min(60, 2 ** attempt))

    raise RuntimeError(f"range {prefix} was not answered: {failure}")


def load(path, ranges):
    """The checkpoint, or a draw not yet begun."""
    if not os.path.exists(path):
        return bytearray(ranges), []

    with open(path, encoding="ascii") as handle:
        state = json.load(handle)

    done = bytearray(base64.b64decode(state["done"]))

    if len(done) != ranges:
        raise ValueError(f"{path} checkpoints a draw of {len(done)} ranges, not {ranges}")

    heap = [(count, -int(whole, 16)) for count, whole in state["kept"]]
    heapq.heapify(heap)

    return done, heap


def save(path, done, heap):
    """Writes the checkpoint whole, so a draw stopped while writing keeps the last one."""
    state = {
        "done": base64.b64encode(bytes(done)).decode("ascii"),
        "kept": [[count, f"{-negated:040X}"] for count, negated in heap],
    }

    with open(path + ".tmp", "w", encoding="ascii", newline="\n") as handle:
        json.dump(state, handle)

    os.replace(path + ".tmp", path)


def keep(heap, prefix, body, most):
    """Keeps the most prevalent hashes of one range among those kept so far.

    A hash is ranked by its count, and among equal counts the lower hash ranks higher,
    so the draw is the same whatever order the ranges are answered in.
    """
    for line in body.splitlines():
        suffix, _, count = line.partition(":")
        ranked = (int(count), -int(prefix + suffix.strip(), 16))

        if len(heap) < most:
            heapq.heappush(heap, ranked)
        elif ranked > heap[0]:
            heapq.heapreplace(heap, ranked)


def draw(output, state, drawn, api=API, ranges=RANGES, most=KEEP, workers=WORKERS, batch=BATCH, attempts=ATTEMPTS):
    """Draws every range not yet drawn, then writes the list once every range is."""
    done, heap = load(state, ranges)
    user_agent = agent(drawn)
    pending = [each for each in range(ranges) if not done[each]]

    with ThreadPoolExecutor(workers) as pool:
        for start in range(0, len(pending), batch):
            asked = [pool.submit(fetch, api, f"{each:05X}", user_agent, attempts) for each in pending[start:start + batch]]

            for answered in as_completed(asked):
                prefix, body = answered.result()
                keep(heap, prefix, body, most)
                done[int(prefix, 16)] = 1

            save(state, done, heap)
            print(f"{sum(done)} of {ranges} ranges drawn", file=sys.stderr)

    hashes = sorted(f"{-negated:040X}" for _, negated in heap)

    if len(hashes) != most or len(set(hashes)) != most:
        raise RuntimeError(f"the draw kept {len(set(hashes))} distinct hashes, not {most}")

    with open(output, "w", encoding="ascii", newline="\n") as handle:
        handle.write(f"# {drawn}\n")
        handle.writelines(value + "\n" for value in hashes)

    return heap[0][0]


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--output", default=OUTPUT, help="where the list is written")
    parser.add_argument("--state", default=STATE, help="where the draw is checkpointed")
    arguments = parser.parse_args()

    drawn = datetime.datetime.now(datetime.timezone.utc).date().isoformat()
    lowest = draw(os.path.normpath(arguments.output), arguments.state, drawn)

    print(f"drawn {drawn}, lowest count kept {lowest}", file=sys.stderr)


if __name__ == "__main__":
    main()
