# SB - Implementation tasks

Derived from `requirements.md` (FR/NFR ids referenced throughout). Follow NFR-2.2.3 (TDD): write the failing
test(s) for a task before writing the implementation, then add the edge-case tests listed under "Tests" after
(NFR-2.2.4). Tasks are grouped into milestones; within a milestone, tasks can generally be done in any order unless
a dependency is noted.

Current repo state this plan starts from: a single `sb` project (exe) with an empty `Program.cs`, a leftover
`PermutationsAndCombinations.cs` (factorial/permutation helpers from the abandoned brute-force approach — see
Milestone 0), `template-main.tex` / `PageMetrics.png` as LaTeX design references, and `sb/Tests/TestFiles/*.txt`
containing 17 real Swedish song lyrics of varying length — reuse these as fixtures for layout/ordering tests
instead of inventing synthetic ones.

## Milestone 0 — Solution architecture (NFR-2.2.1)

- [x] **0.1** Split the solution into three projects: `Sb.Core` (class library — all domain logic), `Sb.Cli`
  (console app, `OutputType=Exe`, references `Sb.Core` + `System.CommandLine`), `Sb.Tests` (NUnit, references
  `Sb.Core`). The CLI project must contain no domain logic — only argument parsing and translating `Sb.Core`
  results into exit codes/console output (NFR-2.2.2).
  - Move `Program.cs` → `Sb.Cli/Program.cs` (gutted; filled in per-command in later milestones).
  - Move `sb/Tests/TestFiles/*` → `Sb.Tests/TestFiles/*`.
  - Decide the fate of `PermutationsAndCombinations.cs`: its `nCr`/`nPr`/`Factorial`/`CreateCombinationsList` helpers
    belong to the brute-force permutation search the spec explicitly rejected (§2.2, FR-1.3.11). Delete it; if any
    piece is still useful (e.g. power-set generation for tiny inputs in a test), move it into `Sb.Tests` only.
  - Move `template-main.tex` and `PageMetrics.png` into `Sb.Core` as embedded resources (or a known relative path)
    for the LaTeX generation milestone to consume.
  - **Tests**: none yet (structural task) — but confirm `dotnet test` runs (even with zero tests) and
    `dotnet build` succeeds for all three projects before moving on.

- [x] **0.2** Define the front-end-agnostic result contract (NFR-2.2.2): a `Result`/`Result<T>` type (or a small
  hierarchy of typed exceptions — pick one approach) that every `Sb.Core` public operation returns/throws, carrying
  enough structured information (error kind, message, offending name) for a front-end to react to. `Sb.Cli` maps
  this to process exit codes (0 = success; reserve distinct non-zero ranges for validation errors, not-found
  errors, and filesystem/DB errors) and stderr messages.
  - **Tests**: unit tests for the `Result` type itself (success/failure construction, mapping helpers) in
    `Sb.Tests/Core/ResultTests.cs`.

## Milestone 1 — Domain model & song file I/O (FR-1.6, FR-1.4)

- [x] **1.1** `Song` model: `Title`, `Writer?`, `Melody?`, `Number`, `IReadOnlyList<string> Text`. (FR-1.6.1 — no
  `Score` field.)
  - **Tests**: construction/validation tests (e.g. title required, text required).

- [x] **1.2** YAML (de)serialization for `Song` matching the `.yml` shape in spec §2.4.2 (`title`, `writer`,
  `melody`, `number`, `text` as a line list). (FR-1.6.1, FR-1.6.2)
  - **Tests**: round-trip a `Song` through serialize→deserialize and assert equality; deserialize a hand-written
    fixture `.yml` (add one to `Sb.Tests/TestFiles/`) and assert field values; deserialize malformed YAML and assert
    a structured error (not an unhandled exception) is returned (ties to NFR-2.5.2's "fail clearly" principle).

- [x] **1.3** Contiguous renumbering logic (FR-1.4.3): given an ordered list of songs and an insertion/deletion at
  position `p`, shift every song's `Number` at/after `p` by ±1.
  - **Tests**: insert at start/middle/end/append; delete at start/middle/end/only-element; insert with no explicit
    position (append, gets `max+1`); deleting the last song doesn't throw on an empty list afterward.

## Milestone 2 — Database (FR-1.7, NFR-2.5.6)

- [x] **2.1** Choose and wire up a persistence mechanism (e.g. SQLite via a lightweight ORM, or hand-rolled) with
  three tables matching §2.4.3: `documents(id, filename, path)`, `chapters(id, document_id, name)`,
  `songs(id, chapter_id, title, number)`.
  - **Tests**: schema creation test — tables exist with expected columns after initializing a fresh DB.

- [x] **2.2** Atomic filesystem+DB write helper (FR-1.7.6, NFR-2.5.6): a single entry point that performs a
  filesystem change (create/rename/delete file or folder) and the corresponding DB write together, rolling back
  the filesystem change if the DB write fails (and vice versa).
  - **Tests**: simulate a DB-write failure after a successful filesystem create and assert the filesystem change
    was rolled back (no orphaned folder); simulate a filesystem failure and assert no DB row was committed; a
    successful run leaves both in the expected matching state.

- [x] **2.3** Mismatch detection (FR-1.7.5): given a DB record whose recorded path doesn't exist on disk, report a
  structured "mismatch" result rather than throwing or silently continuing.
  - **Tests**: DB row pointing at a missing folder → mismatch reported with the right name/path; DB row pointing at
    an existing, correct path → no mismatch reported.

## Milestone 3 — Name validation (FR-1.8.6, FR-1.8.7, FR-1.8.8)

- [x] **3.1** Name sanitization: reject names containing path separators (`/`, `\`), `..` segments, or other
  filesystem-illegal characters, with a structured validation error (not silent stripping).
  - **Tests**: valid plain names pass; names with `/`, `\`, `..`, `:`, null bytes, and empty string are all
    rejected with a distinguishable error per case.

- [x] **3.2** Scoped uniqueness checks: document name unique across all documents; chapter name unique within its
  document; song title unique within its chapter. Apply on `create`/`add`/`rename` (FR-1.8.6).
  - **Tests**: creating a second document with an existing name is rejected; adding a duplicate chapter name to the
    *same* document is rejected but the *same* chapter name in a *different* document is allowed; duplicate song
    title within one chapter is rejected, same title across two chapters is allowed; renaming a document to an
    already-taken name is rejected (covers the `rename` gap fixed in the spec).

- [x] **3.3** Existence checks: any lookup by document/chapter/song name that finds nothing returns a structured
  "not found" result naming what was looked up (FR-1.8.7).
  - **Tests**: each of document/chapter/song lookup-by-name returns a not-found result (not an exception, not a
    silent null) when given a name that doesn't exist.

## Milestone 4 — Formatting & line-wrapping (FR-1.2)

- [x] **4.1** Line-height quantization: given a song's text lines and a column width, compute the number of output
  lines each input line occupies (1, unless it's too wide and must wrap — FR-1.2.8, FR-1.2.9).
  - **Tests**: a short line → 1 output line; a line wider than the column → wraps to the exact expected count of
    output lines without truncation or dropped words; use a couple of the real `TestFiles/*.txt` lyrics (lines of
    varying length) as fixtures, not just synthetic strings.

- [x] **4.2** Column-fill simulation: given an ordered list of songs and a page's two-column capacity (in lines),
  compute how many whole songs fit in column 1, whether/how much the next song overflows into column 2, and the
  final leftover gap (FR-1.2.4–FR-1.2.6).
  - **Tests**: songs that fill column 1 exactly with no overflow; a song that overflows column 1→2; a chapter's
    last page left partially empty (allowed) vs. a non-last page with leftover space (shouldn't happen once
    Milestone 5's ordering is wired in — this test targets the column-fill mechanism in isolation, so assert it
    reports the leftover rather than silently proceeding).

- [x] **4.3** `\vspace` cosmetic correction: given two columns of known final heights on a page, compute the
  `\vspace` amount needed so both start at the same line (FR-1.2.7).
  - **Tests**: equal-height columns → zero vspace; a known height difference → the exact expected vspace value.

## Milestone 5 — Ordering (FR-1.3)

- [x] **5.1** As-listed ordering: place songs in the user-given order, applying the overflow caveats (FR-1.3.2,
  FR-1.3.4–FR-1.3.7).
  - **Tests**: a list of songs that fits one page exactly; a list that overflows column 1→2 on an odd page vs. must
    start a fresh page on an even page (FR-1.3.6) — use real fixture lyrics of known line counts.

- [x] **5.2** First-Fit-Decreasing fast path (FR-1.3.12): sort remaining songs longest-first, greedily add while
  they fit; return whether the 95% target (FR-1.3.10) was met.
  - **Tests**: a case where FFD alone reaches ≥95% (exact-fit-ish fixture set); a case where it doesn't (so the
    exact search in 5.3 is expected to run next).

- [x] **5.3** Exact subset-sum / bounded-knapsack search (FR-1.3.11): given remaining song heights and a capacity,
  find the subset maximizing total height ≤ capacity.
  - **Tests**: a small hand-crafted set with a known optimal subset (verify against a brute-force reference
    computed in the test itself, not hard-coded, to avoid the test just re-asserting the implementation); confirm
    it finds a strictly better fill than FFD alone in a case engineered for FFD to be suboptimal; a case where even
    the exact search can't reach 95% (too few songs left) returns the best achievable fill without error
    (FR-1.3.13); a performance/complexity smoke test with a larger synthetic song count (e.g. 40–50) asserting it
    completes quickly — this is the regression guard for NFR-2.6.1 (no accidental reintroduction of factorial
    behavior).

- [x] **5.4** Per-page orchestration: longest-song-first placement (FR-1.3.8), then FFD (5.2) with fallback to
  exact search (5.3) per page, repeating until all songs are placed (FR-1.3.9).
  - **Tests**: a multi-page chapter (using the full `TestFiles` fixture set) produces the expected number of pages
    and the expected per-page song groupings; verify every page but the last meets the 95% target.

- [x] **5.5** Page-spanning continuation metadata (FR-1.3.15, FR-1.3.16): when a song is too long to fit within a
  single page's two columns combined, FR-1.3.6's even-page restriction is moot — it continues onto as many further
  pages as needed regardless of parity. This is already true behaviorally after the bug-fix round following M5's
  review (`ColumnFiller`'s oversized-candidate handling and `SpaceEfficientPaginator`'s carry-over fix don't check
  page parity), but isn't yet asserted against this specific requirement, and `SongFragment` doesn't yet expose
  *whether* a given split crossed a page boundary (vs. just column 1→2 within one page) — Milestone 6 needs that
  distinction to know when a continuation notice (FR-1.3.16) is required.
  - Extend `SongFragment`/`SongSplitter` with that page-vs-column distinction (e.g. flags on the fragment, set by
    whichever caller performs the split: `ColumnFiller`'s own column 1→2 split is always same-page;
    `ColumnFiller`'s too-long-for-either-column excess and `Paginator`/`SpaceEfficientPaginator`'s cross-page splits
    are always page-boundary).
  - **Tests**: a song spanning 3+ pages, with at least one transition landing on an even page, continues correctly
    (extends the oversized-carry-over regression test from the M5 review); fragments from a page-boundary split are
    flagged as such; fragments from a column 1→2 split (same page) are not.

## Milestone 6 — LaTeX generation (FR-1.12)

- [x] **6.1** `DocumentTemplate` class: builds the main `.tex` file from `template-main.tex`, injecting document
  metadata (title, subtitle) at creation time and inserting a chapter reference each time one is added (FR-1.12.1–
  FR-1.12.3).
  - **Tests**: a newly created document's generated `.tex` contains the given title/subtitle; adding a chapter
    inserts exactly one new reference to it without disturbing previously added chapters.

- [x] **6.2** Page metrics: apply the A4 page size and margins from `PageMetrics.png` as fixed LaTeX preamble
  settings (FR-1.12.4).
  - **Tests**: generated preamble contains the expected geometry settings (string/regex match is fine here).

- [x] **6.3** Chapter emission: `\chapter*{<title>}` (unnumbered), never `\chapter{<title>}` (FR-1.12.5).
  - **Tests**: generated chapter `.tex` uses `\chapter*{...}` with the chapter's display title substituted, and
    never emits the numbered form.

- [x] **6.4** Song emission: each song in a minipage column, title as `\paragraph*{<title>}`, body as free text
  beneath it (FR-1.12.6–FR-1.12.8), using the column/page placement from Milestone 5.
  - **Tests**: a song's generated LaTeX contains `\paragraph*{<its title>}` followed by its exact text lines
    (byte-for-byte, to guard NFR-2.5.3's fidelity requirement — no reflowing); two songs placed in the same page
    both appear inside `minipage` environments.

- [x] **6.5** No hard-coded LaTeX engine (FR-1.12.9): confirm the generated source doesn't depend on
  engine-specific packages (e.g. `fontspec`, which requires XeLaTeX/LuaLaTeX) without the user opting in; document
  the UTF-8-compilation requirement in generated output (e.g. a comment in the `.tex` preamble) rather than
  enforcing it in code.
  - **Tests**: a snapshot/regression test asserting the preamble doesn't silently start requiring a specific engine
    as the template evolves.

- [x] **6.6** Continuation notices (FR-1.3.16, FR-1.12.10, NFR-2.3.3): when rendering a song fragment flagged (via
  5.5) as ending with more content on a later page, append a continuation marker (e.g. "(continued)"); when
  rendering a fragment flagged as resuming from an earlier page, prepend a corresponding marker (e.g. "(continued
  from previous page)"). No marker for a plain column 1→2 split within the same page.
  - **Tests**: a song split across pages renders both markers on the correct fragments; a song split only across
    columns (same page) renders neither marker; marker text is clearly separate from the song's own lyric lines
    (not mistaken for fidelity-protected content under NFR-2.5.3).

## Milestone 7 — CLI: `documents` (FR-1.9)

- [x] **7.1** `documents create <name> <title> [--subtitle] [--output-folder]`: creates the document folder, main
  `.tex` (via Milestone 6), and DB row atomically (Milestone 2.2); applies name validation (Milestone 3).
  - **Tests** (integration-level, against a temp directory + temp DB): happy path creates folder+file+DB row;
    duplicate name rejected; invalid name rejected; every flag has a working short alias (FR-1.8.3/NFR-2.3.1).

- [x] **7.2** `documents list [-c] [-s]`: lists documents, optionally with chapters/songs nested.
  - **Tests**: empty DB → empty list, not an error; `-c` includes chapter names; `-c -s` groups songs under
    chapters (FR-1.9.2).

- [x] **7.3** `documents delete <document-name> [--force/-f]`: cascades to chapters/songs/files (FR-1.8.5),
  prompting for confirmation unless `--force` is given.
  - **Tests**: deleting a non-empty document without `--force` is blocked pending confirmation (simulate the
    prompt in-process rather than via real stdin); `--force` skips the prompt and cascades fully; deleting a
    nonexistent document returns a not-found result (Milestone 3.3), not a crash.

- [x] **7.4** `documents rename <document-name> <new-name>`: renames folder/file and DB row atomically; subject to
  the same uniqueness/sanitization rules as `create` (the gap fixed in the spec review).
  - **Tests**: happy path; renaming to an already-taken name is rejected; renaming a nonexistent document is
    rejected.

- [x] **7.5** `documents build <document-name> [--ordering] [--format]`: wires Milestone 5 (ordering) and Milestone
  6 (LaTeX generation) together; writes `.tex` source only, no PDF compilation (FR-1.9.6).
  - **Tests**: end-to-end test using the real `TestFiles` fixtures — build a small multi-song, multi-page document
    and assert the output `.tex` files exist and are well-formed; `--ordering as-listed` vs `--ordering
    space-efficient` produce different (and each individually correct) song placement on the same input; building
    with `--format single-column` skips the minipage/column logic entirely.

## Milestone 8 — CLI: `chapters` (FR-1.10)

- [x] **8.1** `chapters add <document-name> [--name] [--title]`: default name pattern `{document}_chp_{n}`
  (FR-1.10.1).
  - **Tests**: default name generation produces a non-colliding name even after chapters have been added and
    deleted (counter doesn't naively reuse a freed number into a collision); explicit name/title both work;
    duplicate name within the same document rejected.

- [x] **8.2** `chapters delete <document-name> <chapters (comma-separated)> [--force/-f]`: cascades to songs
  (FR-1.10.2).
  - **Tests**: deleting multiple chapters in one call; deleting a non-empty chapter without `--force` blocked;
    deleting a chapter that doesn't exist in that document reported as not-found rather than silently skipped.

- [x] **8.3** `chapters list <document-name> [--songs]`.
  - **Tests**: as in 7.2, but chapter-scoped.

## Milestone 9 — CLI: `songs` (FR-1.11)

- [x] **9.1** `songs add <document-name> <chapter-name> <title> <text> [--melody] [--writer] [--number]`: every song
  belongs to a chapter (FR-1.5.3), so `document-name`/`chapter-name` are required even though they were missing from
  the original flag list (gap fixed during implementation, mirroring FR-1.9.1's title/subtitle gap from Milestone 7).
  `text` accepts literal text or a file path (reuse `TestFiles/*.txt` fixtures for the file-path case); `--number`
  omitted → next sequential, given → shifts later songs up (FR-1.11.1, FR-1.4.3).
  - **Tests**: literal text vs. file-path text produce identical stored content; omitting `--number` assigns
    `max+1`; an explicit `--number` in the middle shifts existing songs correctly (reuse Milestone 1.3's logic —
    this test is about the CLI wiring, not re-testing the shift algorithm itself); duplicate title in the same
    chapter rejected.

- [x] **9.2** `songs delete <document-name> <song-title>`: no cascade (songs have no children), but still
  triggers renumbering of later songs (FR-1.4.3).
  - **Tests**: deleting a middle song shifts later numbers down by one; deleting a nonexistent song title reported
    as not-found.

- [x] **9.3** `songs list <document-name> [--chapter-name]`.
  - **Tests**: unfiltered vs. chapter-filtered listing.

- [x] **9.4** `songs modify <document-name> <song-title> [--title] [--writer] [--melody] [--chapter] [--number]`
  (one or more of the optional fields).
  - **Tests**: modifying only one field leaves the others untouched; modifying `--number` triggers the same
    shift semantics as `add` with an explicit number; modifying `--chapter` moves the song's file and DB row
    atomically (ties back to Milestone 2.2); modifying to a title that collides with another song in the
    *destination* chapter is rejected.

## Milestone 10 — Cross-cutting polish

- [x] **10.1** Working-directory argument inference (FR-1.8.4): running a command inside a document/chapter folder
  infers document-name instead of requiring it. Only applies to `documents delete`/`build`, `chapters add`/`list`,
  and `songs list` — the commands where document-name is the *only* positional argument. System.CommandLine has no
  way to leave a leading positional unfilled when other required positionals follow it (confirmed empirically: with
  fewer tokens than declared positions it reports a missing argument rather than skipping the optional one), so
  `documents rename`, `chapters delete`, and `songs add`/`delete`/`modify` keep document-name required rather than
  reshuffling their already-shipped argument order. Chapter-name inference wasn't applicable anywhere in the current
  command set for the same reason, or because inferring it would silently change existing flag semantics (e.g.
  `songs list --chapter-name` omitted means "no filter", not "infer from cwd").
  - **Tests**: running `songs list` with cwd inside a document folder lists that document's songs without passing
    document-name; an explicit argument still overrides the inferred value; cwd outside any document with no
    argument given is a clear Validation error. Same coverage (minus the override case) for the other four commands.

- [x] **10.2** UTF-8 path handling end-to-end (FR-1.1.2, NFR-2.4.2): names/paths containing Swedish characters
  (å/ä/ö) survive create → list → build unchanged, regardless of console code page. `Program.cs` now sets
  `Console.OutputEncoding` to UTF-8 so printed output renders correctly regardless of the terminal's default code
  page; the rest of the pipeline (file system, SQLite, YamlDotNet) already handled UTF-8 correctly with no changes
  needed, confirmed by the round-trip test.
  - **Tests**: create a document/chapter/song with å/ä/ö in its name/title and assert it round-trips correctly
    through the DB, filesystem, and generated `.tex`.

- [x] **10.3** Exit code mapping (NFR-2.2.2): wire every `Sb.Cli` command to translate `Sb.Core` results into the
  exit code ranges defined in Milestone 0.2. Already largely in place via the `CommandReporting`/`ExitCodes`
  abstraction shared by all three command groups since Milestone 8; also fixed a real bug this test surfaced —
  `Program.cs`'s database connection needed `Pooling=False`, since Microsoft.Data.Sqlite's connection pool was
  keeping the database file locked after the process's own `using` disposal, which only matters once a real
  process-level test exercises the full entry point. Added `SB_DATA_DIR` to override the database location, both
  for a user wanting more than one database and so tests can exercise the real CLI without touching the real one.
  - **Tests**: a thin wrapper test around the CLI's entry point (`Program.Main`) asserting success, validation-error,
    not-found, and conflict scenarios each produce their designated exit code.
