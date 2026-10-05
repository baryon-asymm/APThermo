"""The correctly rounded oracle of the elementary functions (mpmath, 400 bits) and the input families of the fixtures.

Shared by generate_fixtures.py. Every function here is deterministic: the random families draw from random.Random
with a fixed seed, so a regeneration writes the same bytes (for the same Python and mpmath versions named in the
fixtures' headers).
"""
import random
import struct
from fractions import Fraction

import mpmath

mpmath.mp.prec = 400
TWO = mpmath.mpf(2)
MIN_NORMAL_EXPONENT = -1022


def f2h(v):
    """The bit pattern of a binary64 as an integer."""
    return struct.unpack("<Q", struct.pack("<d", v))[0]


def h2f(h):
    """The binary64 of a bit pattern."""
    return struct.unpack("<d", struct.pack("<Q", h))[0]


def hexs(v):
    """Sixteen hexadecimal digits of the bit pattern of v."""
    return "%016X" % f2h(v)


def parse_token(token):
    """A C99 hexadecimal floating literal, or one of the words of the CORE-MATH special sections, as a float."""
    token = token.strip()
    if "0x" in token.lower():
        return float.fromhex(token)
    return float(token)


def rn(v):
    """Round an mpf to the nearest binary64, ties to even, subnormals and overflow included."""
    if mpmath.isnan(v):
        return float("nan")
    if mpmath.isinf(v):
        return float(v)
    if v == 0:
        return 0.0
    a = abs(v)
    if a < TWO ** MIN_NORMAL_EXPONENT:
        n = int(mpmath.nint(a * TWO ** 1074))
        r = n * 2.0 ** -1074
    else:
        e = int(mpmath.floor(mpmath.log(a, 2)))
        while TWO ** e > a:
            e -= 1
        while TWO ** (e + 1) <= a:
            e += 1
        if e > 1023:
            return float("inf") if v > 0 else float("-inf")
        n = int(mpmath.nint(a / TWO ** (e - 52)))
        if n == 2 ** 53:
            n = 2 ** 52
            e += 1
        if e > 1023:
            return float("inf") if v > 0 else float("-inf")
        r = float(n) * 2.0 ** (e - 52) if e - 52 >= -1074 else n * 2.0 ** (e - 52)
    return r if v > 0 else -r


def exact(func, args):
    """The exact value of the function as an mpf (infinity beyond the thresholds), or None for a special input."""
    x = mpmath.mpf(args[0])
    if func == "exp":
        if args[0] > 710:
            return mpmath.inf
        if args[0] < -750:
            return mpmath.mpf(0)
        return mpmath.exp(x)
    if func == "log":
        return mpmath.log(x)
    if func == "pow":
        y = mpmath.mpf(args[1])
        sign = 1
        if args[0] < 0:
            if y != int(y):
                return None
            sign = -1 if int(y) % 2 else 1
            x = -x
        log_z = y * mpmath.log(x)
        if log_z > 710:
            return sign * mpmath.inf
        if log_z < -750:
            return sign * TWO ** -2000  # underflows to a zero of the sign of the result
        value = mpmath.exp(log_z)
        if midpoint_distance(value) < TWO ** -300:
            # Within the approximation's own noise of a rounding boundary: x^y is then an exact dyadic rational (a midpoint
            # of two doubles, or exactly a double), which the exp/log approximation cannot round; compute it on the integers.
            rational = exact_rational_power(args[0] if args[0] > 0 else -args[0], args[1])
            if rational is not None:
                value = mpmath.mpf(rational.numerator) / mpmath.mpf(rational.denominator)
        return sign * value
    raise ValueError(func)


def integer_root(n, k):
    """The integer k-th root of n when n is a perfect k-th power, else None."""
    if n < 2 or k == 1:
        return n
    low, high = 1, 1 << (n.bit_length() // k + 1)
    while low < high:
        mid = (low + high) // 2
        if mid ** k < n:
            low = mid + 1
        else:
            high = mid
    return low if low ** k == n else None


def exact_rational_power(x, y):
    """x^y as a Fraction when it is rational (x > 0 finite, y finite non-zero floats), else None."""
    x, y = Fraction(x), Fraction(y)
    root_order = y.denominator
    num, den = integer_root(x.numerator, root_order), integer_root(x.denominator, root_order)
    if num is None or den is None:
        return None
    power = abs(y.numerator)
    if power * max(num.bit_length(), den.bit_length()) > 5000:
        return None
    value = Fraction(num ** power, den ** power)
    return value if y > 0 else 1 / value


def oracle(func, args):
    """The correctly rounded result as a float."""
    value = exact(func, args)
    return float("nan") if value is None else rn(value)


def midpoint_distance(value):
    """How far the exact value is from the midpoint of its two neighbouring doubles, in units of an ulp (0 at a midpoint).

    Smaller is harder: the smaller the distance, the more bits beyond the rounding bit a fast path must get right.
    """
    a = abs(value)
    if a == 0 or mpmath.isinf(a):
        return mpmath.mpf(1)
    e = int(mpmath.floor(mpmath.log(a, 2)))
    while TWO ** e > a:
        e -= 1
    while TWO ** (e + 1) <= a:
        e += 1
    ulp_exponent = max(e - 52, -1074)
    scaled = a / TWO ** ulp_exponent
    fraction = scaled - mpmath.floor(scaled)
    return abs(fraction - mpmath.mpf(1) / 2)


def _positive_bits(rnd, low_exponent, high_exponent):
    exponent = rnd.randint(low_exponent, high_exponent)
    return h2f(((exponent + 1023) << 52) | rnd.getrandbits(52))


def exp_inputs():
    """The exp families: uniform, tiny, the subnormal range, the hard-to-round neighbourhoods of 1, the thresholds."""
    rnd = random.Random(12345)
    out = []
    out += [(rnd.uniform(-746, 710),) for _ in range(1500)]
    out += [(rnd.choice((-1, 1)) * _positive_bits(rnd, -60, 5),) for _ in range(800)]
    out += [(rnd.uniform(-746, -708),) for _ in range(500)]
    for m in range(0, 200):
        for sign in (1, -1):
            out.append((sign * (2 * m + 1) * 2.0 ** -53,))
            out.append((sign * (2 * m + 1) * 2.0 ** -54,))
            out.append((sign * (2 * m + 1) * 2.0 ** -45,))
            out.append((sign * (m + 1) * 2.0 ** -40,))
    out += [(v,) for v in (
        709.782712893384, 709.7827128933841, 709.7827128933839, -708.3964185322641, -708.396418532264, -708.3964185322642,
        -745.1332191019411, -745.1332191019412, -745.1332191019413, -745.1332191019410, -744.4400719213812, 0.0, -0.0,
        2.0 ** -54, -2.0 ** -54, 2.0 ** -53, 1.0, -1.0, 0.5 * 0.6931471805599453, 0.6931471805599453)]
    return out


def log_inputs():
    """The log families: the whole range, subnormals, the neighbourhood of 1, every table boundary, powers of 2 and 10."""
    rnd = random.Random(12346)
    out = []
    out += [(_positive_bits(rnd, -1022, 1023),) for _ in range(1200)]
    out += [(h2f(rnd.getrandbits(52) | 1),) for _ in range(400)]
    out += [(1.0 + rnd.uniform(-1 / 32, 1 / 32),) for _ in range(800)]
    for k in range(1, 300):
        out.append((1.0 + k * 2.0 ** -52,))
        out.append((1.0 - k * 2.0 ** -53,))
    for octave in (-20, -1, 0, 1, 5):
        for i in range(33):
            for delta in (-2, -1, 0, 1, 2):
                out.append((h2f(f2h(1.0 + (i + 0.5) / 32) + delta) * 2.0 ** octave,))
    for k in range(-30, 31):
        out.append((10.0 ** k,))
        out.append((2.0 ** k,))
    out += [(1.0,), (5e-324,), (1.7976931348623157e308,)]
    return out


def pow_inputs():
    """The pow families: uniform, the transport and throat exponents, near-1 bases, integer exponents, the midpoint cases."""
    rnd = random.Random(12347)
    out = []
    out += [(rnd.uniform(0.01, 400), rnd.uniform(-8, 8)) for _ in range(1000)]
    for _ in range(200):
        out.append((rnd.uniform(1, 400), 4.6))
        out.append((rnd.uniform(200, 6000), 1.4))
    out += [(rnd.uniform(1.05, 1.7), rnd.uniform(2, 30)) for _ in range(300)]
    out += [(_positive_bits(rnd, -20, 20), float(rnd.randint(-40, 40))) for _ in range(150)]
    out += [(-_positive_bits(rnd, -10, 10), float(rnd.randint(-9, 9))) for _ in range(100)]
    for a in (3, 5, 7, 9, 11, 13, 2 ** 26 + 1, 2 ** 27 - 1, 2 ** 27 - 3, 94906265, 185363, 6, 10):
        for y in range(2, 9):
            out.append((float(a), float(y)))
            out.append((a * 2.0 ** -30, float(y)))
    out += [(4.0, 0.5), (2.0, 10.0), (10.0, 22.0), (0.5, -3.0), (2.0, -1074.0), (2.0, 1023.0), (2.0, 1024.0), (9.0, 1.5)]
    return out


def exp_margin_inputs():
    """Arguments for the fast-path margin fact: uniform, and the reduction's worst case |r| = ln 2/128 and its neighbours."""
    rnd = random.Random(22345)
    out = [(rnd.uniform(-600, 600),) for _ in range(700)]
    step = mpmath.log(2) / 64
    for _ in range(300):
        k = rnd.randint(-55000, 55000)
        centre = float((k + mpmath.mpf(1) / 2) * step)
        for delta in (-2, -1, 0, 1, 2):
            candidate = h2f(f2h(abs(centre)) + delta)
            out.append((candidate if centre > 0 else -candidate,))
    return [entry for entry in out if abs(entry[0]) <= 600]


def log_margin_inputs():
    """Arguments for the fast-path margin fact: log-uniform, every table boundary in several octaves, i = 1 to 2 where e ln 2 ≠ −log c."""
    rnd = random.Random(22346)
    out = [(_positive_bits(rnd, -1000, 1000),) for _ in range(500)]
    for octave in (-30, -2, -1, 0, 1, 2, 30):
        for i in range(33):
            for delta in (-3, -1, 0, 1, 3):
                out.append((h2f(f2h(1.0 + (i + 0.5) / 32) + delta) * 2.0 ** octave,))
    out += [(1.0 + rnd.uniform(1 / 64, 3 / 64),) for _ in range(300)]
    return out


def pow_margin_inputs():
    """Arguments for the fast-path margin fact: the families of the pow fixture whose |y log x| stays within 600."""
    rnd = random.Random(22347)
    out = [(rnd.uniform(0.01, 400), rnd.uniform(-8, 8)) for _ in range(600)]
    for _ in range(150):
        out.append((rnd.uniform(1, 400), 4.6))
        out.append((rnd.uniform(200, 6000), 1.4))
    out += [(rnd.uniform(1.05, 1.7), rnd.uniform(2, 30)) for _ in range(300)]
    out += [(rnd.uniform(0.9, 1.1), rnd.uniform(-300, 300)) for _ in range(300)]
    return [entry for entry in out if abs(entry[1] * float(mpmath.log(entry[0]))) <= 600]


ARITY = {"exp": 1, "log": 1, "pow": 2}


def usable(func, args):
    if any(a != a or a in (float("inf"), float("-inf")) for a in args):
        return False
    if func == "exp":
        return -746.0 <= args[0] <= 710.0
    if func == "log":
        return args[0] > 0
    x, y = args
    return x != 0 and y != 0 and (x > 0 or y == int(y))


def wc_entries(path, func):
    entries = []
    with open(path, encoding="utf-8") as handle:
        for raw in handle:
            text = raw.split("#", 1)[0].strip()
            if not text:
                continue
            try:
                args = tuple(parse_token(t) for t in text.split(","))
            except ValueError:
                continue
            if len(args) == ARITY[func] and usable(func, args):
                entries.append(args)
    return entries
