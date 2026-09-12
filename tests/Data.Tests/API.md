# API.md — Data.Tests

The node exposes nothing outward: nobody references a test project. Its contract
points upward: it is what the parent may consider proven about `Data`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| every record of the committed NASA files is parsed, and the fixture records parse to the transcribed values exactly | L1, load tests over `data/` with generated counts and fixture files | ⏳ |
| the column parser handles every numeric form present in the files | L0, fixture strings | ⏳ |
| a corrupted record fails the load with the line number | L1, mutation tests on copies | ⏳ |
| atomic weights come from the monatomic species records | L1 | ⏳ |

## What the tests rely on

- The repository root is found from the test source file; data files are read from
  `data/` under it.
- Fixture records are JSON files in this node's `records/` directory, one per species
  or transport block, transcribed by hand from the file with the line numbers noted.
- The independent record count is a regular-expression scan over the file text
  implemented in the test, separate from the loader.
