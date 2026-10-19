---

description: "Task list template for feature implementation"
---

# Tasks: Song Leaflet Generation

**Input**: Design documents from `/specs/001-song-leaflet-generation/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included — Constitution Principle III (Test-First Development, NON-NEGOTIABLE) mandates
a failing test before any production code for new behavior.

**Organization**: Tasks are grouped by user story (spec.md, priority order P1→P4) to enable
independent implementation and testing of each story.

**Regeneration note**: this file was regenerated from the current state of spec.md/plan.md/
research.md/data-model.md/contracts/ via `/speckit-tasks` on 2026-09-28. The feature is already
fully implemented (every task below is checked, `dotnet test`: 142/142 passing) — this reflects
the final, current architecture as a clean reference, not the incremental day-by-day delta history
that built it. That history (18 phases, T001-T125, extensive superseded-annotation trail) is
preserved at `tasks.md.bak` in this same folder.

**2026-09-28 update (same day, after `/speckit-clarify`)**: T045/T048 updated in place —
`GreedyFallback` was found (via real-world testing against a 16-song fixture) to still strand a
whole column next to a long song, since its capacity model didn't know about FR-025's even-page
forced-break rule. Now simulation-driven (calls `ColumnBalancer.Fill` directly, like `ExactSearch`
does) instead of approximating. `dotnet test`: 142/142 still passing. See research.md §4's second
2026-09-28 correction.

**2026-10-19 update (`/speckit-clarify` → `/speckit-plan` → `/speckit-tasks`)**: space-efficient
ordering is replaced by page-by-page First Fit Descending (FR-007, research.md §4); the permutation
search, `GreedyFallback` and `--max-permutations` are removed. T045, T047, T048 and T049 below are
marked superseded and re-opened in effect by **Phase 8** (T061-T069), which is the only unfinished
work. Everything outside Phase 8 reflects already-shipped behavior.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on an incomplete task)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- File paths are exact and relative to the repository root

## Path Conventions

Single project (`sb/sb.csproj`) — see `plan.md` Project Structure. All source paths are under
`sb/`; all test paths are under `sb/Tests/` (NUnit tests live inside the same project).

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project scaffolding and dependency setup — no story-specific logic yet.

- [X] T001 Target `net10.0` in `sb/sb.csproj` (research.md §1) and add `<PackageReference>`
  entries for `YamlDotNet` (research.md §10), `Microsoft.Data.Sqlite` (research.md §13, pinning
  `SQLitePCLRaw.bundle_e_sqlite3` to clear a transitive advisory), `System.CommandLine` (CLI
  framework), and `NUnit`/`NUnit3TestAdapter`/`Microsoft.NET.Test.Sdk` (testing, with
  `GenerateProgramFile=false` so the test SDK doesn't shadow `Program.Main`).
- [X] T002 [P] Create feature folders per `plan.md` Project Structure: `sb/Index/`,
  `sb/Documents/`, `sb/Chapters/`, `sb/Songs/`, `sb/Formatting/`, `sb/Ordering/`, `sb/Leaflets/`,
  `sb/Cli/`, `sb/Tests/Cli/`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entities and the SQLite schema every user story's stores and the engine build on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 [P] `Song` entity in `sb/Songs/Song.cs`: `Title` (string, required, non-empty), `Text`
  (string, required, non-empty), `Writer` (string?, optional), `Melody` (string?, optional),
  `SingingOrderNumber` (int, required), `Score` (`IReadOnlyList<string>?`, optional).
- [X] T004 [P] `LeafletFormat` enum in `sb/Leaflets/LeafletFormat.cs`: `SingleColumn`,
  `MultiColumn` (data-model.md).
- [X] T005 [P] `OrderingMode` enum in `sb/Ordering/OrderingMode.cs`: `AsListed`,
  `SpaceEfficient` (data-model.md).
- [X] T006 [P] `SbIndexDatabase` in `sb/Index/SbIndexDatabase.cs`: opens/creates a SQLite file via
  `Microsoft.Data.Sqlite` and applies the exact schema from `contracts/storage-format.md`:
  ```sql
  CREATE TABLE documents (id INTEGER PRIMARY KEY, path TEXT NOT NULL UNIQUE, filename TEXT NOT NULL);
  CREATE TABLE chapters (id INTEGER PRIMARY KEY, name TEXT NOT NULL, document_id INTEGER NOT NULL REFERENCES documents(id), UNIQUE (document_id, name));
  CREATE TABLE songs (id INTEGER PRIMARY KEY, name TEXT NOT NULL, singing_order_number INTEGER NOT NULL, chapter_id INTEGER NOT NULL REFERENCES chapters(id));
  ```
- [X] T007 [P] `DocumentIndexEntry` in `sb/Index/DocumentIndexEntry.cs`: `Id` (int), `Path`
  (string, required, unique), `Filename` (string, required, derived from `Path`'s folder name).
- [X] T008 [P] `ChapterIndexEntry` in `sb/Index/ChapterIndexEntry.cs`: `Id` (int), `Name` (string,
  required, unique within `DocumentId`), `DocumentId` (int, required — FR-014/FR-020).
- [X] T009 [P] `SongIndexEntry` in `sb/Index/SongIndexEntry.cs`: `Id` (int), `Name` (string,
  required), `SingingOrderNumber` (int, required, unique across the parent document — FR-016),
  `ChapterId` (int, required — FR-015/FR-021).

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Generate a formatted leaflet from a list of songs (Priority: P1) 🎯 MVP

**Goal**: Turn a document's chapters/songs into a complete, compilable single- or multi-column
LaTeX leaflet, trusting the user's own line breaks (FR-001/FR-003/FR-004/FR-010/FR-011), and
never leaving a long song truncated (FR-025/FR-026).

**Independent Test**: Call `LeafletBuilder.Build` directly with a `LeafletRequest` (no CLI/storage
involved), choose single- vs. multi-column, and verify the output satisfies
`contracts/leaflet-builder-api.md`/`contracts/latex-output-contract.md`.

### Tests for User Story 1 ⚠️

> Write these tests FIRST; confirm they FAIL before implementing.

- [X] T010 [P] [US1] Failing tests for `LatexEscaper` in `sb/Tests/LatexEscaperTests.cs`: every
  reserved character (`\ { } % & # _ ~ ^`) escapes and round-trips via `Unescape` (FR-010).
- [X] T011 [P] [US1] Failing tests for `PageMetricsReader` in `sb/Tests/PageMetricsReaderTests.cs`:
  parsing `sb/template-main.tex`'s `geometry` `tmargin`/`bmargin` options (falling back to
  `margin` when either is absent) and top-level `\fontsize{<size>}{<baselineskip>}` into
  `PaperHeightPt`/`TopMarginPt`/`BottomMarginPt`/`BaselineSkipPt`/`UsableHeightPt`/`LinesPerPage`
  (FR-026); failure cases for a template missing `geometry` or `\fontsize`.
- [X] T012 [P] [US1] Failing tests for `ColumnBalancer.Fill` in `sb/Tests/ColumnBalancerTests.cs`,
  against a small test-supplied `PageMetrics`: several short `SongBlock`s stack sequentially into
  the same column until the next wouldn't fit (a column has no fixed song-count limit); an
  over-long `SongBlock` is chunked across as many further columns/pages as needed, with a page
  break forced first only when it would otherwise start mid-page on an EVEN physical page
  (FR-025); `PageLayout.PagesConsumed` counts a partially-filled last page as one full page; two
  same-page columns get `\vspace`-balanced to end within one line's height of each other
  (cosmetic finishing touch, FR-005 — start-alignment only is required).
- [X] T013 [P] [US1] Failing tests for `SongOrderer.Order(..., OrderingMode.AsListed, ...)` in
  `sb/Tests/SongOrdererTests.cs`: preserves "exactly the order they were provided" (Acceptance
  Scenario 1, FR-006).
- [X] T014 [P] [US1] Failing end-to-end tests for `LeafletBuilder.Build` in
  `sb/Tests/LeafletBuilderTests.cs` using real fixtures from `sb/Tests/TestFiles/`: single-column
  output preserving line breaks (Scenario 1); multi-column output with columns starting level
  (Scenario 2); a line too wide for its column broken across lines rather than overflowing
  (Scenario 3, FR-004); a blank input line rendering as a blank output line (two blank source
  lines after the preceding `\\`); rejection of an empty song list (FR-012) and of a
  missing/malformed `sb/template-main.tex`; determinism (identical input → byte-identical
  output); each song's `\paragraph*{...}` displaying `Song.SingingOrderNumber` (FR-008) — title,
  optional melody, and lyrics always render together as one unbroken block, never a separate
  leading-title row.

### Implementation for User Story 1

- [X] T015 [P] [US1] `LatexEscaper` in `sb/Formatting/LatexEscaper.cs`: `Escape`/`Unescape` for
  `\ { } % & # _ ~ ^`; make T010 pass.
- [X] T016 [P] [US1] `LineWrapper` in `sb/Formatting/LineWrapper.cs`: word-wraps a line too wide
  for the column (FR-004) — the only line-splitting logic (no phrase/sentence detection, FR-003).
- [X] T017 [P] [US1] `PageMetrics` in `sb/Leaflets/PageMetrics.cs`: immutable holder for
  `PaperHeightPt`, `TopMarginPt`, `BottomMarginPt`, `BaselineSkipPt`, `UsableHeightPt`,
  `LinesPerPage` (FR-026). Paper height is a fixed A4 constant (≈845.05pt), confirmed against the
  real template's computed geometry — not parsed from a `geometry` paper-size keyword, since this
  project's template always specifies `a4paper`.
- [X] T018 [P] [US1] `PageMetricsReader` in `sb/Leaflets/PageMetricsReader.cs` (depends on T017):
  parses `geometry`/`\fontsize` per T011; throws a descriptive `InvalidOperationException` when
  either is missing/unparseable; make T011 pass.
- [X] T019 [P] [US1] `SongBlock` in `sb/Leaflets/SongBlock.cs`: `Lines` (`string[]`),
  `HeightInLines` (int) — one song's own rendered content (title, optional melody, lyrics
  together as one unbroken block), the atomic unit column-fill packs.
- [X] T020 [P] [US1] `Column` in `sb/Leaflets/Column.cs`: `Lines` (`string[]`), `HeightInLines`
  (int) — one physical column's assembled content.
- [X] T021 [P] [US1] `PageLayout` in `sb/Leaflets/PageLayout.cs` (depends on T020): `Pages`
  (`IReadOnlyList<(Column Left, Column? Right)>`), `PagesConsumed` (int — a page counts as
  consumed as soon as any content lands on it, even partially).
- [X] T022 [US1] `ColumnBalancer` in `sb/Leaflets/ColumnBalancer.cs` (depends on T019, T020,
  T021): `Fill(SongBlock[], PageMetrics, startingPageNumber) -> PageLayout` fills each column
  *sequentially* with as many whole `SongBlock`s as fit per `LinesPerPage` (never one song per
  column) — an over-long block chunks across further columns/pages, forcing a fresh EVEN-page
  start only when required (FR-025); `Render(PageLayout) -> string` emits paired
  `.475\textwidth`/`.45\textwidth` minipages (`0.075\textwidth` gutter, `[t]`-aligned, a lone
  right-side box gets a preceding `\hspace` offset) with a closing `\vspace` on the shorter column
  as a cosmetic finishing touch; make T012 pass.
- [X] T023 [P] [US1] `LeafletChapter`/`LeafletChapterFile` in `sb/Leaflets/LeafletChapter.cs` /
  `LeafletChapterFile.cs`: one chapter's input to a build (`Name`, `Title?`, `Songs`) and its
  rendered `chapter.tex` content (`ChapterName`, `Content`).
- [X] T024 [P] [US1] `LeafletRequest` in `sb/Leaflets/LeafletRequest.cs`: `Title` (required),
  `Subtitle` (optional), `Chapters` (`LeafletChapter[]`, required — at least one song across all
  chapters combined, FR-012), `Format` (required), `Ordering` (required), `MaxPermutations`
  (optional positive integer, default 100).
- [X] T025 [P] [US1] `PlacedSong`/`Leaflet` in `sb/Leaflets/PlacedSong.cs` / `Leaflet.cs`:
  `PlacedSong` (`Song`, `PhysicalPosition` — internal only, never displayed, never
  `SingingOrderNumber`); `Leaflet` (`Format`, `Songs`, `Document`, `ChapterFiles`).
- [X] T026 [P] [US1] `DocumentTemplate` in `sb/Leaflets/DocumentTemplate.cs`: reads
  `sb/template-main.tex`, substitutes its `{title}`/`{subtitle}` tokens and `%Chapters come here`
  insertion point; `ParseTitleAndSubtitle` reverses the substitution; fails descriptively if the
  template is missing or lacks the expected markers.
- [X] T027 [US1] `SongOrderer.Order(..., OrderingMode.AsListed, ...)` in
  `sb/Ordering/SongOrderer.cs`: identity pass-through preserving input order; make T013 pass.
- [X] T028 [US1] `LeafletBuilder.Build` in `sb/Leaflets/LeafletBuilder.cs` (depends on
  T015-T027): validates preconditions (FR-012, non-empty `Title`/`Text`, positive
  `MaxPermutations`), derives `PageMetrics` once, loops over non-empty chapters — ordering each
  chapter's own songs via `SongOrderer`, rendering each song block (title/melody/lyrics as one
  block, escaped, `LineWrapper`-wrapped where needed, blank input lines as two blank output
  lines), column-balancing via `ColumnBalancer` when `Format` is `MultiColumn` — and assembles
  `Leaflet.ChapterFiles` (one `\chapter*{...}`-prefixed entry per non-empty chapter) plus
  `Leaflet.Document` (the main template with one `\input` line per chapter file, never song
  content directly); make T014 pass.

**Checkpoint**: User Story 1 is fully functional and independently testable via direct
`LeafletBuilder.Build` calls — no CLI or storage required.

---

## Phase 4: User Story 2 - Maintain valid document/chapter/song structure (Priority: P2)

**Goal**: Documents/chapters/songs are created through the CLI with correct containment rules
(FR-013/014/015) and tracked in the SQLite index so they're locatable regardless of where their
folder lives (FR-018–FR-023).

**Independent Test**: Create a document with no chapters/songs (succeeds), attempt to add a song
before any chapter exists (rejected), add a chapter (succeeds), add a song to it (succeeds); move
a document's folder and confirm a mismatch is reported.

### Tests for User Story 2 ⚠️

- [X] T029 [P] [US2] Failing tests for `DocumentStore` in `sb/Tests/DocumentStoreTests.cs`:
  `Create` with `--path` writes only `<name>.tex` (title/subtitle substituted, no metadata file)
  and an empty `Chapters/` folder, plus a `documents` index row (FR-018/FR-019); a document with
  zero chapters is valid (FR-013); a duplicate `--path` is rejected; `List`/`Find` recover
  title/subtitle by parsing `<name>.tex`; `Rename` renames both the index row and the `.tex` file
  together; unknown-name `Delete`/`Rename` rejected.
- [X] T030 [P] [US2] Failing tests for `ChapterStore` in `sb/Tests/ChapterStoreTests.cs`: `Add`
  rejects an unknown parent document (FR-014/FR-020); `Name` unique within a document; `Remove`
  cascades to delete all songs in the chapter.
- [X] T031 [P] [US2] Failing tests for `SongStore` in `sb/Tests/SongStoreTests.cs`: `Add` rejects
  an unknown parent chapter (FR-015/FR-021) and, on success, writes a single `<name>.yml`
  (title/writer?/melody?/number/text/score?) plus a `songs` index row mirroring the number.
- [X] T032 [P] [US2] Failing tests for `LocationMismatchDetector` in
  `sb/Tests/LocationMismatchDetectorTests.cs`: a deleted/moved document/chapter/song folder or
  file produces a `MismatchReport` (`EntityKind`, `Id`, `ExpectedPath`) rather than silently
  succeeding or throwing (FR-022/FR-023); a `.yml` `number` disagreeing with its indexed mirror
  also surfaces a mismatch.
- [X] T033 [P] [US2] Failing CLI tests for `sb documents create|list|delete|rename` in
  `sb/Tests/Cli/DocumentCommandsTests.cs` per `contracts/cli-commands.md`.
- [X] T034 [P] [US2] Failing CLI tests for `sb chapters add|remove|list` in
  `sb/Tests/Cli/ChapterCommandsTests.cs` per `contracts/cli-commands.md`.
- [X] T035 [P] [US2] Failing CLI tests for `sb songs add|remove|list` (basic fields) in
  `sb/Tests/Cli/SongCommandsTests.cs` per `contracts/cli-commands.md`, including `--text`
  resolving an existing file path to its contents.

### Implementation for User Story 2

- [X] T036 [P] [US2] `Document` entity in `sb/Documents/Document.cs`: `Id`, `Name` (required,
  unique), `Title` (required), `Subtitle` (optional), `Chapters` (may be empty) — no `Format`
  field (FR-002's format is supplied per-build, never persisted).
- [X] T037 [P] [US2] `Chapter` entity in `sb/Chapters/Chapter.cs`: `Id`, `Name` (required, unique
  within its document), `Title` (optional), `Songs` (may be empty).
- [X] T038 [US2] `DocumentStore` in `sb/Documents/DocumentStore.cs` (depends on T006, T007, T026,
  T036): `Create` writes `<name>.tex` (via `DocumentTemplate`) and an empty `Chapters/` folder at
  `--path` (or `./<name>`) plus a `documents` index row; `List`/`Find` parse title/subtitle back
  out of `<name>.tex`; `Delete`/`Rename` (renaming the `.tex` file alongside the index); make T029
  pass.
- [X] T039 [US2] `ChapterStore` in `sb/Chapters/ChapterStore.cs` (depends on T038, T008, T037):
  `Add` (validates parent document, writes a `chapters` index row and `Chapters/<name>/` folder,
  plus a `chapter.yml` sidecar when `--title` is given), `Remove` (cascading), `List`,
  `ResolveChapterPath`; make T030 pass.
- [X] T040 [US2] `SongStore` in `sb/Songs/SongStore.cs` (depends on T039, T009, T003): `Add`
  (validates parent chapter, writes the single `<name>.yml`, mirrors the number into the `songs`
  index row), `Remove`, `List`; make T031 pass.
- [X] T041 [P] [US2] `LocationMismatchDetector` in `sb/Index/LocationMismatchDetector.cs` (depends
  on T006-T009): computes each index entry's expected path (or, for a song, compares its `.yml`
  `number` against the indexed mirror) and reports a `MismatchReport` per miss; make T032 pass.
- [X] T042 [US2] `sb documents create|list|delete|rename` in `sb/Cli/DocumentCommands.cs`, wired
  into `Program.cs`/`sb/Cli/CliRoot.cs` (depends on T038, T041); make T033 pass.
- [X] T043 [US2] `sb chapters add|remove|list` in `sb/Cli/ChapterCommands.cs`, wired into
  `CliRoot.cs` (depends on T039); make T034 pass.
- [X] T044 [US2] `sb songs add|remove|list` in `sb/Cli/SongCommands.cs`, wired into `CliRoot.cs`
  (depends on T040): `Add` resolves `--text` to a file's contents when it names an existing file;
  make T035 pass.

**Checkpoint**: User Stories 1 AND 2 both work independently — documents/chapters/songs can be
created, stored, and located correctly, alongside the standalone formatting engine.

---

## Phase 5: User Story 3 - Order songs automatically or as specified (Priority: P3)

**Goal**: `sb documents build` produces a leaflet with each chapter's songs either in listed order
or automatically arranged to minimize unused space via page-by-page First Fit Descending
(FR-007, 2026-10-19; implemented by Phase 8 — the T045/T047/T048 text below describes the
superseded permutation search).

**Independent Test**: Build the same document twice — `as-listed` and `space-efficient` — and
verify the resulting order differs accordingly and is deterministic for a given input/choice.

### Tests for User Story 3 ⚠️

- [X] *(superseded by T061)* T045 [P] [US3] Failing tests for `SongOrderer.Order(..., OrderingMode.SpaceEfficient, ...)`
  in `sb/Tests/SongOrdererTests.cs`: exact search (small song counts, within `MaxPermutations`)
  finds the true minimum-waste ordering by simulating `ColumnBalancer.Fill`; beyond the bound, the
  deterministic greedy fallback also simulates `ColumnBalancer.Fill` for every candidate placement
  (not an approximate capacity model) so it correctly accounts for FR-025's even-page forced-break
  rule for over-long songs, and finds the same true-optimal arrangement `ExactSearch` would for a
  small case forced through the greedy path (SC-012); deterministic tie-breaking, favoring original order on
  ties (spec.md Edge Cases, FR-007's front-loading rule).
- [X] T046 [P] [US3] Extend `sb/Tests/LeafletBuilderTests.cs`: `SpaceEfficient` vs. `AsListed`
  output for the same songs minimizes unused space/pages relative to the original order
  (Acceptance Scenario 2); ordering never mixes songs across chapters.
- [X] *(superseded by T064)* T047 [P] [US3] Extend `sb/Tests/Cli/DocumentCommandsTests.cs`: `sb documents build
  <name> [ordering] --max-permutations` per `contracts/cli-commands.md`, including the
  not-a-positive-integer error.

### Implementation for User Story 3

- [X] *(superseded by T062)* T048 [US3] Extend `SongOrderer` in `sb/Ordering/SongOrderer.cs` (depends on T018, T022,
  T027): `ExactSearch` evaluates every permutation (bounded by `MaxPermutations`, default 100) via
  `PermutationsAndCombinations.cs`, keeping the first minimum-waste ordering found — since the
  original order is tried first and only a strictly better one replaces it, ties favor the
  original order "for free." Beyond that bound, `GreedyFallback` builds, at each step, the full
  hypothetical ordering for every not-yet-placed song as the next one (rest kept in original
  order) and commits whichever minimizes `ComputeWastedSpace` — i.e., actually simulates
  `ColumnBalancer.Fill`, exactly like `ExactSearch` — so it automatically respects FR-025's
  page-parity rule rather than approximating column capacity by hand; ties favor the original
  order. Costs O(n) steps × O(n) candidates × O(n) simulation — fine at this project's realistic
  scale (10-50 songs/chapter); make T045 pass.
- [X] T049 [US3] `sb documents build` in `sb/Cli/DocumentCommands.cs` (depends on T028, T038-T040,
  T042-T044, T048): loads the named `Document`, groups its chapters' songs into a `LeafletRequest`,
  calls `LeafletBuilder.Build`, writes each `LeafletChapterFile` to its chapter's `chapter.tex` and
  the main document to `<name>.tex`, per `contracts/cli-commands.md`'s full error list (unknown
  document, zero songs/FR-012, non-positive `--max-permutations`, index/filesystem mismatch); make
  T046 and T047 pass.

**Checkpoint**: User Stories 1, 2, and 3 all work independently; `sb documents build` produces a
complete leaflet with a chosen physical ordering.

---

## Phase 6: User Story 4 - Assign and preserve each song's singing-order number (Priority: P4)

**Goal**: Every song's singing-order number is user-assigned, unique within its document,
displayed in the built leaflet, retrievable independent of physical position, and immune to any
reordering/relayout or chapter-move operation.

**Independent Test**: Assign singing-order numbers, build with `as-listed`, rebuild with
`space-efficient`, and verify every song displays the exact same number in both outputs.

### Tests for User Story 4 ⚠️

- [X] T050 [P] [US4] Extend `sb/Tests/SongStoreTests.cs`: `Add` rejects a `SingingOrderNumber`
  already used by another song in the same document, across chapters (FR-016); `Modify` moving
  `--chapter` MUST NOT change `SingingOrderNumber` (FR-024); changing `--singing-number` goes
  through the same uniqueness check; `GetSingingOrderNumber` returns the number independent of
  physical position (FR-017).
- [X] T051 [P] [US4] Extend `sb/Tests/LeafletBuilderTests.cs`: across both `AsListed` and
  `SpaceEfficient` builds of the same songs, every displayed number is `Song.SingingOrderNumber`
  (never `PlacedSong.PhysicalPosition`) and is unchanged between the two builds (FR-008/FR-009,
  SC-004).
- [X] T052 [P] [US4] Extend `sb/Tests/Cli/SongCommandsTests.cs`: `sb songs modify` cases and the
  singing-number conflict error, per `contracts/cli-commands.md`.

### Implementation for User Story 4

- [X] T053 [US4] Add singing-order-number uniqueness enforcement to `SongStore.Add` in
  `sb/Songs/SongStore.cs` (depends on T040): query all songs whose `chapter_id` joins to the same
  `document_id` and reject on conflict; make T050 pass.
- [X] T054 [US4] `SongStore.Modify`/`GetSingingOrderNumber` in `sb/Songs/SongStore.cs` (depends on
  T053): preserves `SingingOrderNumber` across a chapter move; re-runs the uniqueness check on a
  number change; make T050 pass.
- [X] T055 [US4] Confirm `LeafletBuilder` (T028) still displays `Song.SingingOrderNumber` once
  `SongOrderer.SpaceEfficient` (T048) is in the mix (depends on T028, T048); make T051 pass.
- [X] T056 [US4] `sb songs modify` in `sb/Cli/SongCommands.cs` (depends on T054, T044): wires the
  singing-number conflict error; make T052 pass.

**Checkpoint**: All four user stories are independently functional; the full `sb documents build`
flow (format + structure + ordering + numbering) works end-to-end.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation across all user stories.

- [X] T057 [P] Run every scenario in `quickstart.md` end-to-end against a built `sb` CLI (document
  creation at an arbitrary `--path`, chapter/song add, `build` with both ordering modes, the
  mismatch-detection scenario, and long-song pagination) and confirm each documented "Expected
  outcome."
- [X] T058 Review help/description text for every `System.CommandLine` command and option across
  `sb/Cli/*.cs` (Constitution Principle V).
- [X] T059 [P] Aggregate-accuracy test (`sb/Tests/AggregateAccuracyTests.cs`) confirming SC-002
  (≥95% of real fixtures' own input lines fit one output line at the narrower multi-column width)
  holds across every fixture in `sb/Tests/TestFiles/*.txt`, not just individually-covered cases.
- [X] T060 `dotnet test` full suite green (142/142; re-run after Phase 8 as T069).

---

## Phase 8: User Story 3 rework — First Fit Descending ordering (2026-10-19)

**Goal**: Replace `SongOrderer`'s bounded permutation search + `GreedyFallback` with page-by-page
First Fit Descending (FR-007; research.md §4) and remove every `MaxPermutations` /
`--max-permutations` trace. Algorithm, per chapter: sort the pool by length descending (equal
lengths keep original order); for each page, place the longest remaining song that fits, then keep
placing the longest remaining song that still fits the page's remaining space until none does;
remove placed songs from the pool; repeat for the next page until the pool is empty. "Fits" is
decided by simulating `ColumnBalancer.Fill` with `PageMetrics` (so FR-025's even-page rule holds). A
song longer than a page is placed when nothing else fits an empty page and simply spans pages; FFD
then fills the space left on its last page. The chapter's final page may stay under-full.

**Independent Test**: `dotnet test` green; `sb documents build <name> space-efficient` places the
longest songs first per page and fills each non-final page's gaps with the longest songs that fit.

### Tests (write first; confirm each FAILS before T062 — Constitution Principle III)

- [X] T061 [P] [US3] Replace the `SpaceEfficient` cases in `sb/Tests/SongOrdererTests.cs` (delete
  the exact-search and `GreedyFallback`/`maxPermutations: 1` cases) with First Fit Descending
  cases: (a) the longest song is placed first; (b) after it, the longest remaining songs that fit
  fill the page's remaining space before a new page starts; (c) placed songs are removed so page 2
  restarts with the longest remaining song; (d) equal-length songs keep original relative order;
  (e) a song longer than one page is placed (spans pages, FR-025) and FFD fills the space left on its
  final page; (f) only the final page may be under-full (SC-012); (g) output is deterministic across
  repeated calls; (h) `Order` no longer accepts a max-permutations argument (signature is
  `Order(songs, mode, pageMetrics)`).
- [X] T063 [P] [US3] Update `sb/Tests/LeafletBuilderTests.cs`: `SpaceEfficient` vs. `AsListed` still
  differs and uses fewer-or-equal pages; ordering never mixes chapters; remove any
  `MaxPermutations` usage in `LeafletRequest` construction.
- [X] T064 [P] [US3] *(unknown-option case verified manually via `sb documents build x -m 5`, no CLI parse-test harness exists)* Update `sb/Tests/Cli/DocumentCommandsTests.cs`: delete the `--max-permutations`
  /`-m` cases (including the not-a-positive-integer error); add a case asserting `--max-permutations`
  is now an unrecognized option (non-zero exit, nothing written), per `contracts/cli-commands.md`.

### Implementation

- [X] T062 [US3] Rewrite `SongOrderer.SpaceEfficient` in `sb/Ordering/SongOrderer.cs` (depends on
  T061): implement the page-by-page First Fit Descending described in the phase goal using
  `ColumnBalancer.Fill`/`PageMetrics` to test fit; delete `ExactSearch`, `GreedyFallback`,
  `ComputeWastedSpace` (if now unused) and the `maxPermutations` parameter; `AsListed` unchanged;
  make T061 pass. Keep methods short and named for intent (Constitution Principle I).
- [X] T065 [P] [US3] Remove `MaxPermutations` from `sb/Leaflets/LeafletRequest.cs` (the property,
  the `maxPermutations` constructor parameter and its "positive integer" validation) and update
  the `SongOrderer.Order(...)` call in `sb/Leaflets/LeafletBuilder.cs` (line ~41) and its
  precondition comment/validation (depends on T062; make T063 pass).
- [X] T066 [US3] Remove the `--max-permutations`/`-m` option from `sb/Cli/CliRoot.cs` (the
  `maxPermutationsOption` declaration, `build.Options.Add(...)` and the value passed to the
  `LeafletRequest`) so `sb documents build <document-name> [ordering] [-f|--format]` matches
  `contracts/cli-commands.md` (depends on T065; make T064 pass).
- [X] T067 [P] [US3] Delete `sb/PermutationsAndCombinations.cs` if nothing else references it
  (`grep` first; keep it if any other caller exists) (depends on T062).
- [X] T068 [P] [US3] Update `sb/specification.md`: remove the `max-permutations === default 100?`
  note (~line 151) and describe space-efficient ordering as First Fit Descending, matching the spec.
- [X] T069 [US3] Run `dotnet build` (no new warnings) and `dotnet test` — full suite green; then
  re-run `quickstart.md`'s `space-efficient` scenario and update its expected outcome if the
  resulting order changed (depends on T061-T068).

**Checkpoint**: space-efficient ordering is First Fit Descending only; no permutation search,
greedy fallback or `--max-permutations` remains in code, tests, contracts or docs.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational only. No dependency on US2/US3/US4.
- **User Story 2 (Phase 4)**: Depends on Foundational only (uses `Song` from T003, not on US1's
  engine). Independently testable without US1.
- **User Story 3 (Phase 5)**: Depends on US1 (`LeafletBuilder`/`ColumnBalancer`, T022/T028) and
  US2 (`DocumentStore`/`ChapterStore`/`SongStore`, T038-T040) to implement `sb documents build`;
  extends US1's `SongOrderer` (T027).
- **User Story 4 (Phase 6)**: Depends on US2's `SongStore` (T040) and US1's `LeafletBuilder`
  (T028); independent of US3's ordering logic itself (only shares the `build` command surface).
- **Polish (Phase 7)**: Depends on all four user stories being complete.

### Within Each User Story

- Tests are written first and MUST fail before implementation (Constitution Principle III).
- Entities/value types before the components that consume them.
- Store/engine logic before the CLI command that wires it up.

### Parallel Opportunities

- All Setup tasks marked [P] can run together; all Foundational tasks marked [P] (T003-T009) can
  run together.
- Once Foundational is done, US1's and US2's test-writing tasks can all run in parallel with each
  other (different files, no shared dependency) — and, since neither story depends on the other,
  both stories' implementation tasks can proceed in parallel too if staffed by different people.
- US3 and US4 each depend on both US1 and US2 being complete, but do not depend on each other for
  their test-writing tasks, though US3's `build` command should land before US4's CLI-facing tests
  are exercised end-to-end.

---

## Parallel Example: User Story 1

```bash
# Launch all test-writing tasks for User Story 1 together:
Task: "Failing tests for LatexEscaper in sb/Tests/LatexEscaperTests.cs"
Task: "Failing tests for PageMetricsReader in sb/Tests/PageMetricsReaderTests.cs"
Task: "Failing tests for ColumnBalancer.Fill in sb/Tests/ColumnBalancerTests.cs"
Task: "Failing tests for SongOrderer (AsListed) in sb/Tests/SongOrdererTests.cs"
Task: "Failing end-to-end tests for LeafletBuilder in sb/Tests/LeafletBuilderTests.cs"

# Launch independent value-type creation together:
Task: "PageMetrics in sb/Leaflets/PageMetrics.cs"
Task: "SongBlock in sb/Leaflets/SongBlock.cs"
Task: "Column in sb/Leaflets/Column.cs"
Task: "LeafletChapter/LeafletChapterFile in sb/Leaflets/LeafletChapter.cs"
Task: "LeafletRequest in sb/Leaflets/LeafletRequest.cs"
Task: "PlacedSong/Leaflet in sb/Leaflets/PlacedSong.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories).
3. Complete Phase 3: User Story 1.
4. **STOP and VALIDATE**: exercise `LeafletBuilder.Build` directly per its Independent Test.
5. This is a usable engine MVP even before any CLI/storage exists.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. Add User Story 1 → validate independently (MVP: the formatting/layout engine).
3. Add User Story 2 → validate independently (documents/chapters/songs exist and are locatable).
4. Add User Story 3 → validate independently (`sb documents build` with a choice of ordering).
5. Add User Story 4 → validate independently (singing-order numbers survive everything).
6. Add Polish → full quickstart validation.

### Parallel Team Strategy

With multiple developers, once Foundational is done: Developer A takes US1 (pure engine, no
storage dependency), Developer B takes US2 (storage/CLI, no engine dependency) — both can proceed
fully in parallel and integrate at US3.

---

## Notes

- [P] tasks touch different files and have no unmet dependency on another incomplete task.
- [Story] labels map every user-story-phase task back to spec.md for traceability.
- Verify each test fails before writing its implementation (Constitution Principle III).
- Commit after each task or logical group.
- Stop at any checkpoint to validate that story independently before moving on.
- The full day-by-day implementation history (including bug fixes, `/speckit-analyze`
  remediations, and every incremental delta from 2026-09-11 through 2026-09-28) is preserved in
  `tasks.md.bak`, `plan.md`'s Complexity Tracking log, and `checklists/requirements.md`'s round-by-
  round notes — this file intentionally reflects only the current, final state.
