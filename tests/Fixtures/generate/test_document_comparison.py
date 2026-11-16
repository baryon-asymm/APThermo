"""Guard the sampled comparison against non-finite inputs."""
import math
import unittest
from unittest.mock import patch

import document_comparison


class DocumentComparisonFiniteTests(unittest.TestCase):
    def test_finite_comparisons(self):
        comparison = document_comparison.DocumentComparison()
        self.assertEqual([], comparison.differences({"x": 1.0}, {"x": 1.0}))
        self.assertTrue(comparison.differences({"x": 1.0}, {"x": 2.0}))

    def test_nonfinite_operands_are_differences(self):
        comparison = document_comparison.DocumentComparison()
        for value in (math.nan, math.inf, -math.inf):
            with self.subTest(value=value):
                self.assertTrue(comparison.differences({"x": value}, {"x": 1.0}))
                self.assertTrue(comparison.differences({"x": 1.0}, {"x": value}))
                self.assertTrue(comparison.differences({"x": value}, {"x": value}))
        self.assertTrue(comparison.differences({"x": "1.0e999"}, {"x": "1.0e999"}))

    def test_nonfinite_and_negative_tolerances_are_refused(self):
        for value in (math.nan, math.inf, -math.inf, -1.0):
            with self.subTest(value=value), patch.object(document_comparison.json, "load", return_value={
                "fields": {"regeneration": {"relative": value}, "throat": {"relative": value}}}):
                with self.assertRaises(ValueError):
                    document_comparison.DocumentComparison()


if __name__ == "__main__":
    unittest.main()
