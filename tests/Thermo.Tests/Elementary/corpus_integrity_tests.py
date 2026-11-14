"""Portable refusal controls for the optional full campaign, without a numerical oracle."""
import os
import tempfile
import unittest
from pathlib import Path

import check_worst_cases as campaign


class CorpusIdentityTests(unittest.TestCase):
    def test_both_campaign_halves_refuse_a_tiny_mathematically_valid_sample(self):
        with tempfile.TemporaryDirectory(prefix="apthermo-corpus-control-") as directory:
            root = Path(directory)
            lists = root / "lists"
            lists.mkdir()
            work = root / "work"
            work.mkdir()
            for function, text, result in (("exp", "0x0p+0\n", "3FF0000000000000\n"),
                                           ("log", "0x1p+0\n", "0000000000000000\n"),
                                           ("pow", "0x1p+1,0x1p+1\n", "4010000000000000\n")):
                (lists / (function + ".wc")).write_text(text, encoding="utf-8")
                (work / (function + ".out")).write_text(result, encoding="utf-8")
            for operation in (campaign.write_inputs, campaign.verify):
                with self.subTest(operation=operation.__name__):
                    with self.assertRaisesRegex(ValueError, "(?i)(identity|sha256|corpus)"):
                        operation(str(lists), str(work))
            self.assertEqual([], list(work.glob("*.in")), "refusal must precede writing any inputs")

    def test_missing_empty_truncated_and_corrupted_lists_are_refused(self):
        for content in (None, b"", b"0x1", b"invalid\xffdata"):
            with self.subTest(content=content), tempfile.TemporaryDirectory(prefix="apthermo-corpus-control-") as directory:
                root = Path(directory)
                if content is not None:
                    (root / "exp.wc").write_bytes(content)
                for operation in (campaign.write_inputs, campaign.verify):
                    with self.assertRaises((ValueError, FileNotFoundError)):
                        operation(directory, os.path.join(directory, "work"))
                self.assertFalse((root / "work").exists())


if __name__ == "__main__":
    unittest.main()