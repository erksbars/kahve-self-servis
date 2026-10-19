import importlib.util
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest
import uuid

spec = importlib.util.spec_from_file_location("prepare_musicbrainz", Path(__file__).with_name("prepare-musicbrainz-catalog.py"))
catalog = importlib.util.module_from_spec(spec)
spec.loader.exec_module(catalog)


class CatalogTests(unittest.TestCase):
    def row(self, title="Şarkı", artist="Sanatçı"):
        return {"id": str(uuid.uuid4()), "title": title, "artist-credit": [{"name": artist}], "tags": ["excluded"], "rating": {"value": 5}}

    def test_core_only_dedup_filter_and_limit(self):
        first = self.row()
        result = catalog.choose(iter([first, first, self.row("sarki", "sanatci"), self.row("Başka", "Diğer"), self.row("İkinci")]), ["sanatci"], 2)
        self.assertEqual(["Şarkı", "İkinci"], [r["title"] for r in result])
        self.assertEqual({"id", "title", "artist-credit"}, set(result[0]))

    def test_artist_credit_join_and_invalid_records(self):
        row = self.row()
        row["artist-credit"] = [{"artist": {"name": "A"}, "joinphrase": " & "}, {"name": "B"}]
        self.assertEqual("A & B", catalog.slim(row)["artist-credit"][0]["name"])
        self.assertIsNone(catalog.slim(self.row("x" * 161)))
        self.assertIsNone(catalog.slim(self.row("bad\nname")))
        self.assertIsNone(catalog.slim({"id": "not a uuid"}))

    def test_archives_are_read_without_extracting_members(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "recording.tar.xz"
            raw = (json.dumps(self.row()) + "\n").encode()
            with tarfile.open(path, "w:xz") as archive:
                member = tarfile.TarInfo("../../recording"); member.size = len(raw)
                archive.addfile(member, io.BytesIO(raw))
            self.assertEqual(1, len(list(catalog.load_records(path))))
            self.assertEqual([path], list(Path(folder).iterdir()))

    def test_json_and_jsonl_sources(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "catalog.json"
            row = self.row()
            path.write_text(json.dumps({"recordings": [row]}), encoding="utf-8")
            self.assertEqual([row], list(catalog.load_records(path)))
            path = Path(folder) / "recording.jsonl"
            path.write_text(json.dumps(row) + "\n", encoding="utf-8")
            self.assertEqual([row], list(catalog.load_records(path)))


if __name__ == "__main__":
    unittest.main()
