# Implementation Plan: Song Leaflet Generation

**Branch**: `001-song-leaflet-generation` | **Date**: 2026-09-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-song-leaflet-generation/spec.md`

## Summary

Build the document/chapter/song data model and CLI, plus the engine that turns a document's
songs into a complete, compilable LaTeX leaflet. This plan supersedes the earlier "engine only,
no CLI" version: the spec's Assumptions explicitly defer "specific CLI commands, storage file
formats, and naming/renaming/listing/deletion mechanics" to planning, and this pass designs them
concretely against `sb/specification.md` §2.5 (Storage) and §2.6 (CLI) — the project's own
source-of-truth doc for that surface. **Config (formerly §2.4) was removed from
`sb/specification.md` on 2026-09-26 and is out of scope for this feature** (spec.md's 2026-09-26
Clarification). Songs are formatted with the user's own line breaks trusted as-is — one input
line per output line, wrapped further only if too wide for the column (phrase-splitting was
removed 2026-09-26, spec.md's Clarification) — laid out as single- or multi-column (LaTeX
`minipage`s balanced with `\vspace`, matching
`sb/Tests/TestFiles/TeX/test.tex`), physically ordered either as-listed or space-efficiently, and
numbered with a user-assigned **singing-order number** that is stored on the song and never
changes when physical layout changes (per the 2026-09-11 merge of `002-song-hierarchy-numbering`
into this spec). `sb/template-main.tex` supplies the master document skeleton that `documents
create` fills in immediately (title/subtitle substituted, empty content) and `documents build`
later overwrites (full content, same title/subtitle re-substituted), writing to `<name>.tex` (not
a fixed `main.tex` — a further 2026-09-26 Clarification) — as of 2026-09-26, a document has no
metadata file of its own; `<name>.tex` is both its only persisted file and where its
title/subtitle are read back from on every later read (spec.md's 2026-09-26 Clarification,
research.md §9); `sb documents rename` renames it alongside the index's `filename`. `--format`
(single-/multi-column) is no longer a persisted document default — it moved from `documents
create` to `documents build`, supplied fresh per invocation.

This pass also adds long-song pagination and, per `/speckit-analyze` finding U1's remediation,
corrects multi-column layout from one-song-per-column-per-row (the original design) to
**sequential column-fill**: each column holds as many consecutive whole songs as fit, not exactly
one (research.md §5). Page/column capacity (`PageMetrics.LinesPerPage`) is computed from
`template-main.tex`'s own `geometry`/`\fontsize` values (FR-026, research.md §14); a song
exceeding that capacity is chunked across columns/pages instead of clipped, with an
even/odd-physical-page *starting* rule (FR-025, research.md §15) — it constrains only where such
a song starts, never the whole page's exclusive use, so a page/column is never reserved for one
song. None of this changes song order (FR-006's `as-listed` guarantee is unaffected, research.md
§16) — per the 2026-09-27 clarification, driven by the project's own working notes on
multi-column layout for long songs and a follow-up user clarification during `/speckit-analyze`
remediation. `SongOrderer`'s `SpaceEfficient` cost function is corrected to score orderings by
simulating real column-fill packing (research.md §4) instead of the stale pairwise-adjacent
approximation it shipped with, which never matched what this section always said.

This pass also supersedes the previous plan's storage design: per the 2026-09-11 clarification
addendum (FR-018–FR-023), documents may be created at **arbitrary, user-chosen filesystem
locations**, not a fixed `<output>/` convention, so a **SQLite index** is now the authoritative
record of every document/chapter/song's *identity and location* (documents/chapters/songs
tables, research.md §13). The filesystem (`.tex`/`.yml` files) remains the content store,
including — as of the 2026-09-26 update — each song's singing-order number, which now lives in
its own `.yml` file (research.md §12) rather than only in the index; the index mirrors that
number for fast lookup. A mismatch between what the index records (location, or its mirrored
number) and the actual filesystem/file content is surfaced to the user (FR-023's pattern), never
silently resolved.

## Technical Context

**Language/Version**: C# targeting .NET 10 (per `sb/specification.md`); current `sb.csproj`
targets `net7.0` and must be updated as part of this feature's implementation (see research.md).

**Primary Dependencies**:
- `System.CommandLine` (existing) — now actively wired: `documents`, `chapters`, and `songs`
  subcommands per `sb/specification.md` §2.6 (the `config` subcommand is **removed**, 2026-09-26).
- `YamlDotNet` (**new**) — parses/serializes each song's single `.yml` file (§2.5.2: title,
  writer, melody, number, text, score). Justified under Constitution Principle II: YAML parsing
  is non-trivial hand-rolled logic a dependency legitimately replaces.
- `Microsoft.Data.Sqlite` (**new**) — the SQLite index (FR-018–FR-023, research.md §13).
  Justified: talking to a SQLite database file requires a driver; there is no hand-rolled
  alternative to "replace." No ORM (e.g., EF Core) is added on top — three small tables with
  simple CRUD don't need one (Constitution Principle II).
- `NUnit` (existing, testing).
- No dependency is added for LaTeX generation itself — see research.md §2 (unchanged from the
  prior plan pass): plain string assembly against `template-main.tex` and the `test.tex` fixture
  shape.

**Storage**: Two-part, per `sb/specification.md` §2.5 and the 2026-09-11/2026-09-26 clarifications:
- **Index (authoritative for identity/location; a mirror for the singing-order number)**: one
  SQLite database file (research.md §13) with `documents` (id, path, filename), `chapters` (id,
  name, document_id), and `songs` (id, name, singing_order_number, chapter_id) tables.
- **Content (filesystem, located via the index)**: each document's folder lives wherever the
  index says it does (`documents.path` — user-chosen at `create` time, not a fixed convention);
  under it, `<name>.tex` (rendered from `sb/template-main.tex` immediately at `create` time, named
  after `--name`/`documents.filename` rather than a fixed `main.tex` — no separate document
  metadata file, and it's also where `Title`/`Subtitle` are read back from on every later read,
  research.md §9; renamed alongside `documents.filename` by `sb documents rename`) and an empty
  `Chapters/` subfolder, also created at `create` time. Each chapter is `Chapters/<chapter-name>/`
  (derived from the parent document's path + the chapter's
  indexed name — chapters/songs have no separately stored path), containing a `chapter.tex`
  (generated song content, the `test.tex`-shaped fragment the document's `.tex` file `\input`s)
  and **one single
  `<song-name>.yml` file per song** (title, writer, melody, number, text lines, optional score
  filenames — research.md §9) — as of 2026-09-26 this is the source of truth for the
  singing-order number too, which the `songs` index table only mirrors for fast lookup
  (research.md §12), replacing the earlier split `<song-name>.txt` + `<song-name>.yml` pair.
- Config (a global `config.yml`) is **removed** — no longer part of this feature (research.md
  §11).

**Testing**: NUnit, using the existing `Tests/TestFiles/*.txt` real-song fixtures as
golden-style input for line-wrapping/formatting tests, plus new focused unit tests per
component, storage round-trip tests (using a temp directory), and a CLI-level integration test
per command group.

**Target Platform**: Cross-platform .NET CLI (Windows dev environment observed; no OS-specific
APIs used).

**Project Type**: Single project — CLI/engine library in one executable project (`sb/`), matching
the repository's existing structure.

**Performance Goals**: Generating a leaflet for a typical 10–20 song leaflet completes well under
1 second of engine processing time, excluding external LaTeX-to-PDF compilation (out of scope).

**Constraints**: Space-efficient ordering MUST use page-by-page First Fit Descending (FR-007,
2026-10-19 Clarification; research.md §4) — no permutation search, no `--max-permutations` flag.
Tie-breaking during ordering MUST be deterministic (equal-length songs keep original order). A song's singing-order number MUST be unique within its document (FR-016) and
MUST NOT be mutated by any ordering/layout operation (FR-009). Page/column capacity
(`PageMetrics.LinesPerPage`) MUST be derived from `template-main.tex`'s own `geometry`/`\fontsize`
values at build time, never hardcoded (FR-026, 2026-09-27 Clarification, research.md §14); a song
exceeding that capacity MUST be chunked across columns/pages rather than clipped, with an
even/odd-page placement rule (FR-025, research.md §15) — this never reorders songs (`as-listed`'s
relative order is unaffected, FR-006, research.md §16), only where breaks fall.

**Scale/Scope**: Typical leaflets contain roughly 10–50 songs across one or a few chapters; the
design must degrade gracefully well beyond the brute-force-feasible ordering range.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Readability First**: PASS. CLI command handlers, storage read/write, and the formatting
  engine are kept as separate, small, intention-named components rather than one large
  `Program.cs`.
- **II. Maintainability & Simplicity**: PASS. `YamlDotNet` and `Microsoft.Data.Sqlite` are the
  two new dependencies, both justified (non-trivial parsing / a required database driver, not
  speculative additions). No ORM is layered onto SQLite access — three tables with simple CRUD
  are queried directly. Storage access is a small set of concrete classes (`DocumentStore`,
  `ChapterStore`, `SongStore`, `SbIndexDatabase`), not an interface hierarchy — no second storage
  backend is required, so no abstraction is introduced ahead of need (YAGNI). CWD-based
  document/chapter inference (mentioned in `sb/specification.md` §2.6) is explicitly **deferred**
  (research.md §8) since no functional requirement depends on it yet.
- **III. Test-First Development (NON-NEGOTIABLE)**: PASS (procedural gate). Every component below
  is scoped so a failing test can be written first, including storage round-trips and CLI
  command behavior.
- **IV. Comprehensive Test Coverage**: PASS. Reuses `Tests/TestFiles/*.txt` fixtures; every FR in
  spec.md maps to a testable unit (see contracts/).
- **V. Consistent Style & Documentation**: PASS. New public CLI surface is documented via
  `System.CommandLine` command descriptions at the point of definition.

No violations requiring justification — Complexity Tracking table is not needed.

*Post-Phase-1 re-check*: Adding the CLI/storage/config layer introduces more files than the
previous engine-only plan, but no new abstraction layer beyond what's needed for one filesystem
backend, one SQLite index, and one CLI framework already in the project. All five principles
remain PASS.

*Post-clarification re-check (SQLite index, 2026-09-11)*: `Microsoft.Data.Sqlite` is a second new
dependency, but it directly implements FR-018–FR-023 (a requirement the user explicitly stated,
not a speculative addition), and no ORM/repository-abstraction layer was added on top of it — the
`DocumentStore`/`ChapterStore`/`SongStore` classes from the prior pass absorb index reads/writes
directly rather than gaining a new abstraction tier. All five principles remain PASS.

## Project Structure

### Documentation (this feature)

```text
specs/001-song-leaflet-generation/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
sb/                             # existing single CLI project (sb.csproj)
├── Program.cs                  # existing CLI entry point — now wired to Cli/*Commands.cs
├── PermutationsAndCombinations.cs  # existing combinatorics helpers (no longer used by Ordering/ as of 2026-10-19 — remove if unused elsewhere)
├── template-main.tex           # existing master document template (title page + \input point)
├── Index/
│   ├── SbIndexDatabase.cs        # opens/creates the SQLite file, applies schema (documents/chapters/songs tables)
│   ├── DocumentIndexEntry.cs     # id, path, filename — mirrors the `documents` table
│   ├── ChapterIndexEntry.cs      # id, name, document_id — mirrors the `chapters` table
│   ├── SongIndexEntry.cs         # id, name, singing_order_number, chapter_id — mirrors the `songs` table
│   └── LocationMismatchDetector.cs # compares indexed paths against the filesystem (FR-023)
├── Documents/
│   ├── Document.cs               # Document entity (id, name, title, subtitle?, path, chapters) — no Format field (2026-09-26)
│   └── DocumentStore.cs          # create/list/delete/rename; writes the `documents` index row, the single <name>.tex (via DocumentTemplate, no metadata file), and an empty Chapters/ folder, all at `create` time; rename also renames <name>.tex
├── Chapters/
│   ├── Chapter.cs                 # Chapter entity (id, name, title?, parent document, songs)
│   └── ChapterStore.cs            # add/remove/list chapters; writes the `chapters` index row (location derived from parent document's indexed path)
├── Songs/
│   ├── Song.cs                    # Song entity (id, title, text lines, writer?, melody?, SingingOrderNumber, score?)
│   └── SongStore.cs               # add/remove/list/modify; writes the single <name>.yml content file (source of truth, incl. number) and mirrors the singing-order number + uniqueness check into the `songs` index row
├── Formatting/
│   ├── LineWrapper.cs            # word-wraps a line that's too wide for the column (FR-004) — the only line-splitting logic left after phrase-splitting's removal (2026-09-26)
│   └── LatexEscaper.cs           # escapes LaTeX-special characters in output text; Escape/Unescape (2026-09-26)
├── Ordering/
│   └── SongOrderer.cs            # AsListed / SpaceEfficient physical ordering (page-by-page First Fit Descending, 2026-10-19; replaces bounded permutation + greedy fallback); (2026-09-27, `/speckit-analyze` finding U1) SpaceEfficient's cost function simulates real column-fill packing (ColumnBalancer/PageMetrics) instead of the stale pairwise-adjacent approximation
├── Leaflets/
│   ├── LeafletFormat.cs          # SingleColumn / MultiColumn enum
│   ├── LeafletChapter.cs         # (name, title?, songs) — one chapter's input to a build (2026-09-26)
│   ├── LeafletChapterFile.cs     # (chapterName, content) — one non-empty chapter's rendered chapter.tex (2026-09-26)
│   ├── Leaflet.cs                # Leaflet entity (format, physically-ordered songs incl. singing-order numbers, main document, ChapterFiles — 2026-09-26)
│   ├── PageMetrics.cs            # (2026-09-27) PaperHeightPt/TopMarginPt/BottomMarginPt/BaselineSkipPt/UsableHeightPt/LinesPerPage — FR-026
│   ├── PageMetricsReader.cs      # (2026-09-27) parses template-main.tex's `geometry` options + `\fontsize` command into a PageMetrics (research.md §14)
│   ├── SongBlock.cs              # (2026-09-27) Lines/HeightInLines — one song's own rendered content, the atomic unit `ColumnBalancer.Fill` packs (data-model.md's `SongBlock`; `Column` now means the assembled physical column instead)
│   ├── PageLayout.cs             # (2026-09-27) data-model.md's `PageLayout`: one chapter's column-fill result — `Pages` (Left/Right `Column` per physical page) + `PagesConsumed`
│   ├── ColumnBalancer.cs         # (2026-09-27, rewritten — `/speckit-analyze` finding U1) `Fill(SongBlock[], PageMetrics, startingPageNumber) -> PageLayout` fills columns *sequentially* with as many whole `SongBlock`s as fit per `PageMetrics.LinesPerPage`, not one song per column per row; one too big for its column is chunked across as many further columns/pages as needed (subsuming the even/odd-page starting rule, FR-025, research.md §5/§15) — supersedes the original per-pair `RenderRow` (T019)
│   ├── DocumentTemplate.cs       # reads template-main.tex; substitutes {title}/{subtitle} tokens + content insertion point; ParseTitleAndSubtitle reverses the substitution (2026-09-26)
│   └── LeafletBuilder.cs         # LeafletChapter[] + options -> main document + per-chapter chapter.tex fragments (2026-09-26 — previously flattened all songs into one file); (2026-09-27) derives PageMetrics once, threads it into SongOrderer.Order and ColumnBalancer.Fill, and tracks a running physical page counter per chapter (advanced by each chapter's `PageLayout.PagesConsumed`)
├── Cli/
│   ├── CliRoot.cs                # assembles the full System.CommandLine tree; previously-required flags are positional Arguments, remaining options get single-letter aliases (2026-09-26) — kept separate from *Commands.cs so parsing/wiring stays independently testable-free of business logic
│   ├── DocumentCommands.cs       # `sb documents create|list|delete|rename|build` (create takes --path per §2.6.1's "output folder" flag; build takes --format, moved from create 2026-09-26; build now also writes each chapter's chapter.tex — 2026-09-26)
│   ├── ChapterCommands.cs        # `sb chapters add|remove|list`
│   └── SongCommands.cs           # `sb songs add|remove|list|modify` (incl. --singing-number); Add resolves --text to a file's contents when it names an existing file (2026-09-26)
└── Tests/
    ├── TestFiles/                 # existing real-song fixtures (already present)
    ├── TestFiles/TeX/test.tex     # existing expected-output fixture (per-song minipage shape)
    ├── LatexEscaperTests.cs
    ├── SongOrdererTests.cs        # (2026-09-27, `/speckit-analyze` finding U1) `SpaceEfficient` cases updated to assert against real column-fill-simulated waste, not the superseded pairwise-adjacent metric
    ├── ColumnBalancerTests.cs     # (2026-09-27) sequential column-fill (multiple songs per column), long-song chunking, and the even/odd starting-page rule (FR-025) against a small test-supplied LinesPerPage
    ├── PageMetricsReaderTests.cs  # (2026-09-27) geometry/\fontsize parsing + LinesPerPage computation (FR-026)
    ├── DocumentStoreTests.cs      # structural rules: FR-013/FR-014/FR-015; index rows FR-018/019
    ├── SongStoreTests.cs          # singing-order number persistence + uniqueness (FR-016/FR-017/FR-021)
    ├── LocationMismatchDetectorTests.cs  # FR-022/FR-023: index-vs-filesystem drift detection
    ├── LeafletBuilderTests.cs     # end-to-end engine test using TestFiles fixtures
    └── Cli/
        ├── DocumentCommandsTests.cs
        ├── ChapterCommandsTests.cs
        └── SongCommandsTests.cs
```

**Structure Decision**: Single-project structure is retained. Compared to the previous plan pass,
`Program.cs` is no longer left untouched: it now wires the `Cli/*Commands.cs` handlers into the
existing `RootCommand` (its `#region Commands`/`#region Handlers` placeholders were left empty
for exactly this). New top-level folders (`Index/`, `Documents/`, `Chapters/`) group
the newly-in-scope persistence/CLI concerns, kept as small concrete classes rather than
interfaces/repositories-with-abstractions, since there is exactly one storage backend (filesystem
+ one SQLite file) and one CLI framework (`System.CommandLine`) — introducing an abstraction
layer for either would violate Constitution Principle II (YAGNI) without a second implementation
to justify it. `sb/specification.md` §2.6.4 (`scores`, i.e., attaching images) is intentionally
**not** included: no functional requirement in spec.md covers it, so building it now would be
unrequested scope (see research.md §8 for other deferred CLI details). Following the
2026-09-11 clarification (documents may live at arbitrary, user-chosen locations), `documents
create` gains a `--path` flag (mapping to `sb/specification.md` §2.6.1's already-sketched
"output folder" flag) that `DocumentStore` records in the `documents` index table — chapters and
songs remain located purely by convention relative to their parent document's indexed path (no
separate path field for them, matching the schema given in the clarification), so only the
document's location is ever user-chosen.

## Complexity Tracking

*No violations — table omitted.*

*Post-clarification re-check (song storage format + Config removal, 2026-09-26)*: No new
dependency or abstraction is introduced. `sb/Config/AppConfig.cs`, `sb/Config/ConfigLoader.cs`,
`sb/Cli/ConfigCommands.cs`, and `sb/Tests/ConfigLoaderTests.cs` — previously implemented under
T055–T058 — are now **to be removed** as part of applying this plan update, since Config is out
of scope, not merely unbuilt (removing shipped, no-longer-specified code rather than leaving it
as dead/undocumented functionality keeps Principle I, Readability First, satisfied). `SongStore`'s
song-file handling is rewritten from a `<name>.txt` + `<name>.yml` pair to a single `<name>.yml`
per research.md §9/§12 — a like-for-like format change within the same `YamlDotNet` dependency
already justified above, not a new one. All five principles remain PASS.

*Post-clarification re-check (document storage + `--format` relocation, 2026-09-26)*: No new
dependency or abstraction. `sb/Documents/DocumentMetadataFile.cs` is **removed**: a document's
own `<name>.tex` (via `DocumentTemplate.Render`/`ParseTitleAndSubtitle`, reusing the already-present
`LatexEscaper`, now with a new `Unescape`) replaces it entirely — one file doing one job, not two
concrete classes standing in for each other (Principle I/II). `Document.Format` is removed rather
than kept-but-unused, since `--format` moved to `documents build` (research.md §8/§9); no
speculative "maybe read it from somewhere later" placeholder was left behind (YAGNI). All five
principles remain PASS.

*Post-clarification re-check (`.tex` filename follows `--name`, 2026-09-26)*: No new dependency
or abstraction. `DocumentStore` computes the filename from `name`/`documents.filename` instead of
a hardcoded `"main.tex"` constant, and `Rename` performs one additional `File.Move` to keep the
on-disk file in sync — a direct, necessary consequence of the file being named after a value that
can change, not new complexity layered on top (Principle II). All five principles remain PASS.

*Post-clarification re-check (`--text` accepts a file path, 2026-09-26)*: No new dependency. One
`File.Exists` check plus `File.ReadAllText` in `SongCommands.Add` — no new abstraction (no
"content source" interface/strategy for what is a single conditional), consistent with Principle
II (YAGNI: there is exactly one alternative source — a file — not an open-ended set requiring a
plugin point). All five principles remain PASS.

*Post-clarification re-check (phrase-splitting removed, 2026-09-26)*: **Reduces** complexity —
`sb/Formatting/PhraseSplitter.cs`, `sb/Formatting/Phrase.cs`, and `sb/Tests/PhraseSplitterTests.cs`
are deleted outright (not kept dormant), since the capability they implemented no longer exists
(Principle I: Readability First — no dead code left implementing an unwanted rule).
`LeafletBuilder.RenderSongBlock` now splits on `\n` directly instead of calling a tokenizer; no
new dependency or abstraction replaces it. All five principles remain PASS.

*Post-clarification re-check (chapters render as separate `\input`-linked files, 2026-09-26)*:
Corrects a bug, not new complexity — the running code had flattened all songs directly into
`main.tex`, deviating from `sb/specification.md` §2.5.1/§4 and this project's own template/docs,
which had specified per-chapter `chapter.tex` files `\input`'d from the main document since
2026-09-11. `LeafletChapter`/`LeafletChapterFile` are two small, concrete data-carrier types (no
interface/abstraction layer) mirroring the shape the storage layer already has
(`Chapters/<name>/chapter.tex`); `LeafletBuilder` loops over chapters instead of a flat song list,
using the exact same per-song rendering/column-balancing logic as before, just scoped to one
chapter's songs per iteration. All five principles remain PASS.

*Post-clarification re-check (required flags → positional arguments; remaining flags gain
aliases, 2026-09-26)*: No new dependency or abstraction — purely reshuffling which
`System.CommandLine` primitive (`Argument<T>` vs `Option<T>`) each existing value uses, and adding
a second constructor parameter (an alias string) to already-existing `Option<T>` declarations.
`Cli/*Commands.cs` (the testable business-logic layer) is completely untouched — this is confined
to `CliRoot.cs`'s wiring, matching that file's own stated purpose ("kept independent of the
System.CommandLine wiring... so it can be unit tested directly"). All five principles remain
PASS.

*Post-clarification re-check (`--ordering` → optional positional argument, 2026-09-26)*: No new
dependency or abstraction — `Argument<string>("ordering") { DefaultValueFactory = _ => "as-listed"
}` replaces `Option<string>("--ordering", "-o") { DefaultValueFactory = ... }`; the existing
`ParseOrdering` validation is unchanged. A deliberate, user-requested one-off exception to the
general rule from the prior re-check, not a new pattern. All five principles remain PASS.

*Post-clarification re-check (page/column capacity + long-song pagination, 2026-09-27)*: No new
dependency — `PageMetricsReader` is a small, concrete regex-based parser (two `geometry` options,
one `\fontsize` command), not a general LaTeX-parsing library or abstraction layer (Principle II).
Long-song chunking reuses the existing `minipage`/`\vspace` architecture from `ColumnBalancer`
(research.md §5/§15) rather than introducing the `multicol` package or a second rendering path —
`multicol` was already rejected project-wide, so this keeps that decision consistent rather than
carving out an exception for one case (Principle I: no divergent special-casing). `SongOrderer`
(ordering) and `ColumnBalancer`/`LeafletBuilder` (break placement) remain separate components with
no new coupling (research.md §16). All five principles remain PASS.

*Post-clarification re-check (paper size confirmed fixed at A4, 2026-09-27)*: No plan/spec change
— the user confirmed `template-main.tex`'s `geometry` package will always specify `a4paper` (now
added to the template) and this feature will never need to support another paper size. This
**reduces** complexity versus the prior pass's plan: `PageMetricsReader` treats `PaperHeightPt` as
a fixed A4 constant (≈845.05pt) rather than parsing/detecting a `geometry` paper-size keyword —
one less thing to parse, not a new abstraction (Principle II: build only the one case that's
actually used). `research.md` §14 and `data-model.md`'s `PageMetrics` updated accordingly. All
five principles remain PASS.

*Post-`/speckit-analyze` re-check (multi-column corrected to sequential column-fill, finding U1
remediation, 2026-09-27)*: `/speckit-analyze` found FR-025's placement rule underspecified for
songs spanning 3+ pages (finding U1); the user's answer revealed the *general* multi-column model
was never one-song-per-column-per-row in the first place — a column always could (and should)
hold multiple stacked songs, with balanced heights being a preference the space-efficient search
optimizes for, not a hard constraint. This corrects, rather than adds to, the design: `research.md`
§4's own decision text ("shortest column/page," a bin-packing heuristic) already described this
general model — only the shipped `SongOrderer.ComputeWastedSpace`/`GreedyFallback` (T045) and
`ColumnBalancer.RenderRow` (T019) never matched it, using a pairwise-adjacent/one-song-per-row
approximation instead. No new dependency or abstraction is introduced by the fix — `ColumnBalancer`
gains one algorithm (sequential fill) in place of another (pair-and-balance), and `SongOrderer`'s
cost function is corrected to actually simulate that algorithm rather than approximate a different
one (Principle II: this replaces a wrong implementation of an already-decided design, it does not
add speculative generality). `data-model.md`'s new `PageLayout` entity formalizes one chapter's
column-fill result (`Pages` + `PagesConsumed`) so tests can inspect per-page placement directly,
rather than reverse-engineering it from rendered LaTeX text. FR-025's spec wording and
`contracts/latex-output-contract.md` are corrected to state the rule constrains only where a long
song *starts*, never a whole page's exclusive use. All five principles remain PASS; Principle IV
(Comprehensive Test Coverage) specifically requires `SongOrdererTests`' existing `SpaceEfficient`
cases to be updated alongside the cost-function fix, not left asserting against the superseded
metric.

*Post-implementation correction (title/lyrics separation, 2026-09-27)*: mid-implementation,
`git status` surfaced `sb/Kottar i karburatorn.tex` — the exact reference the project's working
notes named ("Use example from Kottar i karburatorn.tex") but which hadn't been present in the
repo until this point in the session. Re-reading it alongside `sb/Tests/TestFiles/TeX/test.tex`
revealed both render a song's title/melody separate from its lyrics — a correction to
already-shipped behavior (Phase 3 onward), not new scope. Confirmed with the user before
implementing. No new dependency; `SongBlock.TitleLineCount`/`Column.LeadingTitleLineCount` are two
small added fields, and `ColumnBalancer.Render`'s title/body-row split reuses the exact same
`\vspace`-balancing helper already used for the general body-row case (Principle II: no
duplicated balancing logic). All five principles remain PASS; full suite re-confirmed green
(126/126) after this addendum.

*(Superseded 2026-09-28)*: this correction was itself later reverted by direct instruction —
`sb/Leaflets/ColumnBalancer.cs`'s own doc comment confirms a song's title/melody and lyrics render
together as one unbroken block again, and neither `SongBlock.TitleLineCount` nor
`Column.LeadingTitleLineCount` exists on the shipped types. The revert has no Complexity Tracking
entry of its own — it was never recorded at the time it happened. See research.md §5's
superseded note and `contracts/latex-output-contract.md`'s revised "Title/lyrics rendering"
bullet.

*Post-`/speckit-analyze` re-check (FR-007 front-loading rule + new SC-012, findings I1/I2
remediation, 2026-09-28)*: No new dependency or abstraction. FR-007 gained an explicit rule
(prefer filling a page's remaining column space with a later, smaller-fitting song over starting a
new page, except on a chapter's final page) and SC-012 replaces the intent SC-003 used to serve —
both tighten wording of an already-declared requirement/measure, not new scope. `sb/specification.md`
§2.1 updated to match (gap-filling is now the primary mechanism; `\vspace` is a cosmetic
finishing step). All five principles remain PASS.

*Post-`/speckit-analyze` re-check (`GreedyFallback` rewritten to follow singing order with
gap-fill, Phase 18/T123-T125, 2026-09-28)*: No new dependency or abstraction — `SongOrderer`'s
existing `GreedyFallback` method is rewritten in place (one algorithm swapped for another within
the same file/method signature area, `PageMetrics` now threaded through where it wasn't before);
`ExactSearch`/the bounded-search/fallback *structure* is unchanged (Principle II: this replaces a
requirement-violating implementation with a compliant one, it does not add speculative
generality). `sb/Tests/SongOrdererTests.cs`'s greedy-fallback case rewritten alongside it
(Principle IV). All five principles remain PASS; full suite re-confirmed green (142/142).

*Post-`/speckit-clarify` re-check (`GreedyFallback` made simulation-driven, second correction,
2026-09-28)*: No new dependency or abstraction — `GreedyFallback` now calls the already-existing
`ComputeWastedSpace`/`ColumnBalancer.Fill` (the same simulation `ExactSearch` already uses)
instead of a hand-rolled capacity model, removing duplicated packing logic rather than adding any
(Principle II: this is a simplification — one fewer place FR-025's rules need to be kept in sync
by hand). `sb/Tests/SongOrdererTests.cs`'s greedy-fallback case rewritten again, since the prior
demonstration case turned out to be a waste-neutral tie under real simulation (Principle IV: the
test now demonstrates a genuine, non-tied improvement). Verified against the real `C:\Test`
fixture, not just synthetic test data. All five principles remain PASS; full suite re-confirmed
green (142/142).

*Post-`/speckit-clarify` re-check (First Fit Descending replaces permutation search + greedy
fallback, 2026-10-19)*: No new dependency or abstraction. `SongOrderer.SpaceEfficient` is
rewritten as page-by-page First Fit Descending, still using the existing `ColumnBalancer.Fill`/
`PageMetrics` simulation to decide "fits". This is a net simplification (Principle II): the
exact-search phase, `GreedyFallback`, the `MaxPermutations` bound, the `--max-permutations`
option and (if unused elsewhere) the `PermutationsAndCombinations.cs` reuse are removed.
Principles III/IV: `SongOrdererTests` and `LeafletBuilderTests` cases for the removed paths are
replaced by failing FFD tests written first (longest-first placement, gap fill, over-long song,
equal-length tie-break, final-page under-fill allowed). All five principles remain PASS.
