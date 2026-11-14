"""The optional full campaign: inputs <wc-dir> <work-dir>, verify <wc-dir> <work-dir>.

Both halves first verify every pinned raw-byte hash, input format and usable count.
identity <wc-dir> checks corpus identity only; self-test runs portable refusal controls.
Only numerical verification imports the multiprecision oracle.
"""
import multiprocessing
import os
from pathlib import Path
import re
import sys
import unittest

import worst_case_inputs as inputs

FUNCTIONS = tuple(inputs.ARITY)


def read_inputs(wc_dir, function):
    return inputs.wc_entries(os.path.join(wc_dir, function + ".wc"), function)


def write_inputs(wc_dir, work_dir):
    inputs.validate_corpus(wc_dir)
    os.makedirs(work_dir, exist_ok=True)
    for function in FUNCTIONS:
        entries = read_inputs(wc_dir, function)
        with open(os.path.join(work_dir, function + ".in"), "w", encoding="utf-8", newline="\n") as handle:
            handle.write("\n".join(" ".join(inputs.hexs(value) for value in args) for args in entries) + "\n")
        print(function, len(entries), "inputs")


def expected(job):
    import oracle
    function, args = job
    return inputs.f2h(oracle.oracle(function, args))


def read_results(path, count):
    lines = Path(path).read_text(encoding="utf-8").splitlines()
    if len(lines) != count or any(re.fullmatch(r"[0-9a-fA-F]{16}", line) is None for line in lines):
        raise ValueError("%s: expected exactly %d binary64 results, each sixteen hexadecimal digits" % (path, count))
    return [int(line, 16) for line in lines]


def verify(wc_dir, work_dir, workers=4):
    inputs.validate_corpus(wc_dir)
    failures = 0
    for function in FUNCTIONS:
        entries = read_inputs(wc_dir, function)
        results = read_results(os.path.join(work_dir, function + ".out"), len(entries))
        with multiprocessing.Pool(workers) as pool:
            wanted = pool.map(expected, [(function, entry) for entry in entries], chunksize=2000)
        wrong = [index for index in range(len(entries)) if wanted[index] != results[index]]
        print(function, "%d inputs, %d not correctly rounded" % (len(entries), len(wrong)))
        for index in wrong[:10]:
            print("  ", function, " ".join(inputs.hexs(value) for value in entries[index]),
                  "got %016X want %016X" % (results[index], wanted[index]))
        failures += len(wrong)
    return 1 if failures else 0


def main():
    arguments = sys.argv[1:]
    if arguments == ["self-test"]:
        suite = unittest.defaultTestLoader.discover(str(Path(__file__).parent), pattern="corpus_integrity_tests.py")
        return 0 if unittest.TextTestRunner(verbosity=2).run(suite).wasSuccessful() else 1
    if len(arguments) == 2 and arguments[0] == "identity":
        for function, (_, count) in inputs.validate_corpus(arguments[1]).items():
            print(function, count, "usable inputs; pinned SHA256 and format verified")
        return 0
    if len(arguments) != 3 or arguments[0] not in ("inputs", "verify"):
        raise ValueError("usage: inputs|verify <wc-dir> <work-dir>, identity <wc-dir>, or self-test")
    command, wc_dir, work_dir = arguments
    if command == "inputs":
        write_inputs(wc_dir, work_dir)
        return 0
    return verify(wc_dir, work_dir)


if __name__ == "__main__":
    sys.exit(main())