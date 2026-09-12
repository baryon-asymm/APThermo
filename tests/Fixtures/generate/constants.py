"""The physical constant the reference package exposes: the universal gas constant it computes with."""
from __future__ import annotations

import sys

import cea

from writer import Writer, main_of


def generate(writer: Writer) -> None:
    writer.case(
        "constants", "R",
        inputs={"quantity": "universal gas constant", "unit": "J/(kmol K)", "source": "cea.R"},
        outputs={"R": cea.R},
        script_path=__file__,
    )


if __name__ == "__main__":
    sys.exit(main_of(generate))
