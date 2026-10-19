"""Make a small CC0 catalog from a local MusicBrainz recording JSON/JSONL dump.

No downloads, API calls or database writes. Run with --help for usage.
"""
import argparse
import bz2
import gzip
import json
from pathlib import Path
import tarfile
import unicodedata
import uuid
import lzma


def normalize(text):
    return "".join(c for c in unicodedata.normalize("NFD", text.strip().lower().replace("ı", "i")) if unicodedata.category(c) != "Mn")


def slim(recording):
    """Retain core fields only, matching the import endpoint's text limits."""
    if not isinstance(recording, dict):
        return None
    try:
        recording_id = str(uuid.UUID(recording.get("id", "")))
    except (ValueError, TypeError, AttributeError):
        return None
    if recording_id == str(uuid.UUID(int=0)):
        return None
    title = recording.get("title", "")
    credits = recording.get("artist-credit", [])
    if not isinstance(title, str) or not isinstance(credits, list):
        return None
    parts = []
    for credit in credits:
        if isinstance(credit, str):
            parts.append(credit)
        elif isinstance(credit, dict):
            artist = credit.get("artist", {})
            name = credit.get("name") or (artist.get("name", "") if isinstance(artist, dict) else "")
            joinphrase = credit.get("joinphrase", "")
            if not isinstance(name, str) or not isinstance(joinphrase, str):
                return None
            parts.append(name + joinphrase)
    title, artist = title.strip(), "".join(parts).strip()
    if not all(1 <= len(v.encode("utf-16-le")) // 2 <= 160 and not any(unicodedata.category(c) == "Cc" for c in v) for v in (title, artist)):
        return None
    return {"id": recording_id, "title": title, "artist-credit": [{"name": artist}]}


def choose(records, artists, limit):
    seen_ids, seen_keys, result = set(), set(), []
    for recording in records:
        item = slim(recording)
        if item is None:
            continue
        artist = item["artist-credit"][0]["name"]
        if artists and not any(normalize(a) in normalize(artist) for a in artists):
            continue
        key = normalize(item["title"]) + " | " + normalize(artist)
        if item["id"] in seen_ids or key in seen_keys:
            continue
        seen_ids.add(item["id"]); seen_keys.add(key); result.append(item)
        if len(result) == limit:
            break
    return result


def json_lines(stream):
    for number, line in enumerate(stream, 1):
        if not line.strip():
            continue
        try:
            yield json.loads(line)
        except json.JSONDecodeError as exc:
            raise ValueError(f"Invalid JSONL on line {number}") from exc


def load_records(path):
    if path.name.endswith((".tar.xz", ".tar.bz2", ".tar.gz", ".tar")):
        # Read members without extracting any path to the filesystem.
        with tarfile.open(path, "r|*") as archive:
            for member in archive:
                if member.isfile() and Path(member.name).name in ("recording", "recording.jsonl", "recording.json"):
                    with archive.extractfile(member) as stream:
                        yield from json_lines(stream)
                    return
        raise ValueError("No recording JSONL member found in archive")
    if path.suffix == ".json":
        if path.stat().st_size > 16 * 1024 * 1024:
            raise ValueError("Use JSONL or a recording archive for files larger than 16 MB")
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        if isinstance(data, dict):
            data = data.get("recordings")
        if not isinstance(data, list):
            raise ValueError("Expected recordings array or JSONL dump")
        yield from data
        return
    opener = {".xz": lzma.open, ".gz": gzip.open, ".bz2": bz2.open}.get(path.suffix, open)
    with opener(path, "rt", encoding="utf-8-sig") as stream:
        yield from json_lines(stream)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="Local recording JSON, JSONL, compressed JSONL or recording.tar.xz")
    parser.add_argument("output", type=Path, help="New JSON file (will not overwrite an existing file)")
    parser.add_argument("--artist", action="append", default=[], help="Artist substring, repeatable; matching any is sufficient")
    parser.add_argument("--limit", type=int, default=200, help="Maximum unique songs, 1–500 (default 200)")
    args = parser.parse_args()
    if not 1 <= args.limit <= 500:
        parser.error("--limit must be between 1 and 500")
    if args.source.resolve() == args.output.resolve() or args.output.exists():
        parser.error("Output must be a new file, separate from source")
    try:
        rows = choose(load_records(args.source), args.artist, args.limit)
        if not rows:
            raise ValueError("No matching valid recordings; no output written")
        content = json.dumps({"recordings": rows}, ensure_ascii=False, indent=2) + "\n"
        if len(content.encode("utf-8")) > 512 * 1024:
            raise ValueError("Output exceeds 512 KB; use a smaller --limit")
        with args.output.open("x", encoding="utf-8", newline="\n") as target:
            target.write(content)
    except (OSError, ValueError, tarfile.TarError) as exc:
        parser.exit(1, f"{exc}\n")
    print(f"Prepared {len(rows)} songs: {args.output}")


if __name__ == "__main__":
    main()
