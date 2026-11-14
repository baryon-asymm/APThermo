"""Portable refusal controls for the optional full campaign, without a numerical oracle."""
import hashlib
import os
import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path

import check_worst_cases as campaign
import worst_case_inputs as inputs


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


class CorpusFormatTests(unittest.TestCase):
    def test_strict_parser_rejects_malformed_tokens_and_wrong_arity(self):
        for function, text in (("exp", "nonsense"), ("exp", "1,2"), ("pow", "1"), ("pow", "1,")):
            with self.subTest(function=function, text=text), tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / "rows.wc"
                path.write_text("# comment\n" + text, encoding="utf-8")
                with self.assertRaisesRegex(ValueError, r"rows\.wc:2: malformed"):
                    inputs.wc_entries(path, function)

    def test_special_sections_and_usable_filter_are_not_format_failures(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "rows.wc"
            path.write_text("# specials\n+snan\n-nan\n+inf\n-1\n0\n0x1p+0 # usable\n", encoding="utf-8")
            self.assertEqual([(1.0,)], inputs.wc_entries(path, "log"))

    def test_each_list_count_and_hash_are_checked(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            provenance = {}
            for function, text in (("exp", "0"), ("log", "1"), ("pow", "2,2")):
                data = text.encode("utf-8")
                (root / (function + ".wc")).write_bytes(data)
                provenance[function] = (hashlib.sha256(data).hexdigest(), 1)
            with patch.object(inputs, "read_provenance", return_value=provenance):
                self.assertEqual(provenance, inputs.validate_corpus(root))
                for function in inputs.ARITY:
                    original = provenance[function]
                    provenance[function] = (original[0], 2)
                    with self.assertRaisesRegex(ValueError, "usable count"):
                        inputs.validate_corpus(root)
                    provenance[function] = ("0" * 64, 1)
                    with self.assertRaisesRegex(ValueError, "SHA256 identity"):
                        inputs.validate_corpus(root)
                    provenance[function] = original
                (root / "pow.wc").write_text("3,2", encoding="utf-8")
                work = root / "work"
                for operation in (campaign.write_inputs, campaign.verify):
                    with self.assertRaisesRegex(ValueError, "SHA256 identity"):
                        operation(root, work)
                self.assertFalse(work.exists(), "all three lists must be validated before any effects")

    def test_manifest_revision_is_an_independent_trust_anchor(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "provenance.txt"
            path.write_text(inputs.PROVENANCE.read_text(encoding="utf-8").replace(inputs.PINNED_REVISION, "0" * 40), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "pinned revision"):
                inputs.read_provenance(path)

    def test_outputs_require_complete_binary64_words_and_exact_count(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "exp.out"
            for text in ("", "1\n", "10000000000000000\n", "0000000000000000\n0000000000000000\n", "XXXXXXXXXXXXXXXX\n"):
                with self.subTest(text=text):
                    path.write_text(text, encoding="utf-8")
                    with self.assertRaises(ValueError):
                        campaign.read_results(path, 1)
            path.write_text("3FF0000000000000\n", encoding="utf-8")
            self.assertEqual([inputs.f2h(1.0)], campaign.read_results(path, 1))


if __name__ == "__main__":
    unittest.main()