"""The two halves of the optional full worst-case check (FullWorstCaseListsTests), around the .NET evaluation in between.

  python check_worst_cases.py inputs <wc-dir> <work-dir>   writes <work-dir>/<f>.in: the usable inputs of CORE-MATH's list
                                                            for f = exp, log, pow, one line of bit patterns per input
  python check_worst_cases.py verify <wc-dir> <work-dir>   reads <work-dir>/<f>.out (the bit pattern of the tree's result,
                                                            one line per input) and compares it with the oracle (mpmath,
                                                            400 bits, correct rounding); prints one summary line per
                                                            function and exits 1 if any result is not correctly rounded
"""
import multiprocessing
import os
import sys

import oracle

FUNCTIONS = ("exp", "log", "pow")


def read_inputs(wc_dir, func):
    return oracle.wc_entries(os.path.join(wc_dir, func + ".wc"), func)


def write_inputs(wc_dir, work_dir):
    os.makedirs(work_dir, exist_ok=True)
    for func in FUNCTIONS:
        entries = read_inputs(wc_dir, func)
        with open(os.path.join(work_dir, func + ".in"), "w", encoding="utf-8", newline="\n") as handle:
            handle.write("\n".join(" ".join(oracle.hexs(a) for a in args) for args in entries) + "\n")
        print(func, len(entries), "inputs")


def expected(job):
    func, args = job
    return oracle.f2h(oracle.oracle(func, args))


def verify(wc_dir, work_dir, workers=4):
    failures = 0
    for func in FUNCTIONS:
        entries = read_inputs(wc_dir, func)
        with open(os.path.join(work_dir, func + ".out"), encoding="utf-8") as handle:
            results = [int(line, 16) for line in handle.read().split()]
        if len(results) != len(entries):
            print(func, "FAILED: %d results for %d inputs" % (len(results), len(entries)))
            failures += 1
            continue
        with multiprocessing.Pool(workers) as pool:
            wanted = pool.map(expected, [(func, e) for e in entries], chunksize=2000)
        wrong = [i for i in range(len(entries)) if wanted[i] != results[i]]
        print(func, "%d inputs, %d not correctly rounded" % (len(entries), len(wrong)))
        for i in wrong[:10]:
            print("  ", func, " ".join(oracle.hexs(a) for a in entries[i]), "got %016X want %016X" % (results[i], wanted[i]))
        failures += len(wrong)
    return 1 if failures else 0


def main():
    command, wc_dir, work_dir = sys.argv[1:4]
    if command == "inputs":
        write_inputs(wc_dir, work_dir)
        return 0
    return verify(wc_dir, work_dir)


if __name__ == "__main__":
    sys.exit(main())
