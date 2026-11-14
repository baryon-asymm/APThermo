"""Standard-library input parsing and pinned identity of the complete CORE-MATH corpus."""
import hashlib
from pathlib import Path
import re
import struct

ARITY = {"exp": 1, "log": 1, "pow": 2}
PINNED_REVISION = "284b3b0e198042c38f5c30316f696786b10816b0"
PROVENANCE = Path(__file__).with_name("fixtures") / "PROVENANCE.txt"


def f2h(value):
    """The bit pattern of a binary64 as an integer."""
    return struct.unpack("<Q", struct.pack("<d", value))[0]


def h2f(bits):
    """The binary64 of a bit pattern."""
    return struct.unpack("<d", struct.pack("<Q", bits))[0]


def hexs(value):
    """Sixteen hexadecimal digits of the bit pattern of value."""
    return "%016X" % f2h(value)


def parse_token(token):
    """A C99 hexadecimal literal or a special value from the published lists."""
    token = token.strip()
    if token.lower().lstrip("+-") == "snan":
        return float("nan")
    return float.fromhex(token) if "0x" in token.lower() else float(token)


def usable(function, args):
    if any(value != value or value in (float("inf"), float("-inf")) for value in args):
        return False
    if function == "exp":
        return -746.0 <= args[0] <= 710.0
    if function == "log":
        return args[0] > 0
    x, y = args
    return x != 0 and y != 0 and (x > 0 or y == int(y))


def iter_entries(path, function):
    """Yield usable arguments, refusing every malformed non-comment row with its location."""
    with open(path, encoding="utf-8") as handle:
        for number, raw in enumerate(handle, 1):
            text = raw.split("#", 1)[0].strip()
            if not text:
                continue
            try:
                args = tuple(parse_token(token) for token in text.split(","))
                if len(args) != ARITY[function]:
                    raise ValueError("wrong arity")
            except (ValueError, OverflowError) as error:
                raise ValueError("%s:%d: malformed %s corpus row: %s" % (path, number, function, text)) from error
            if usable(function, args):
                yield args


def wc_entries(path, function):
    return list(iter_entries(path, function))


def read_provenance(path=PROVENANCE):
    """Read the existing fixture metadata, anchored to the reviewed upstream revision."""
    text = Path(path).read_text(encoding="utf-8")
    revisions = re.findall(r"^\s*commit:\s*([0-9a-f]{40})\s*$", text, re.MULTILINE)
    if revisions != [PINNED_REVISION]:
        raise ValueError("CORE-MATH corpus provenance must name the pinned revision")
    hashes = re.findall(r"\b(exp|log|pow)\.wc ([0-9a-f]{64})\b", text)
    counts_line = re.findall(r"^\s*usable inputs per list \(before the selection\): (.+)$", text, re.MULTILINE)
    counts = re.findall(r"\b(exp|log|pow) ([0-9]+)\b", counts_line[0]) if len(counts_line) == 1 else []
    if len(hashes) != len(ARITY) or set(dict(hashes)) != set(ARITY):
        raise ValueError("CORE-MATH corpus provenance has missing or duplicate SHA256 entries")
    if len(counts) != len(ARITY) or set(dict(counts)) != set(ARITY) or any(int(count) <= 0 for _, count in counts):
        raise ValueError("CORE-MATH corpus provenance has missing, duplicate or empty counts")
    return {function: (dict(hashes)[function], int(dict(counts)[function])) for function in ARITY}


def validate_corpus(directory):
    """Validate all raw-byte identities, formats and usable counts before a campaign has effects."""
    provenance = read_provenance()
    for function, (expected_hash, expected_count) in provenance.items():
        path = Path(directory) / (function + ".wc")
        digest = hashlib.sha256()
        with path.open("rb") as handle:
            for block in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(block)
        if digest.hexdigest() != expected_hash:
            raise ValueError("%s: CORE-MATH corpus SHA256 identity mismatch" % path)
        count = sum(1 for _ in iter_entries(path, function))
        if count != expected_count:
            raise ValueError("%s: CORE-MATH corpus usable count %d, expected %d" % (path, count, expected_count))
    return provenance