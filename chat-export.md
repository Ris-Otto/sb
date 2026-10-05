# SB Working History

Reconstructed from the raw Claude Code session transcripts after the previous `chat-export.md`
was accidentally overwritten. Covers all three sessions on record for this project.

---

## 2026-09-11 — Constitution, Feature 001, SQLite Index, First Implementation

### 1. Project Constitution

**Command:** `/speckit-constitution` — "Create principles focused on code readability,
maintainability and testing standards"

Ratified constitution **v1.0.0** (first ratification, not an amendment — the prior file was an
unfilled template) with five principles:
1. **Readability First** — intent-revealing names, short single-purpose methods, comments only for
   non-obvious "why"
2. **Maintainability & Simplicity** — YAGNI, no premature abstraction, nullable reference types
   enabled
3. **Test-First Development (NON-NEGOTIABLE)** — Red-Green-Refactor, regression tests for every bug
   fix
4. **Comprehensive Test Coverage** — every public method/branch tested, golden-file fixtures under
   `Tests/TestFiles/` exercised
5. **Consistent Style & Documentation** — uniform C#/.NET conventions, documented CLI surface

Plus Quality Gates (clean build, full NUnit suite passing) and a Development Workflow section.
Ratification date set to 2026-09-11 (no earlier date found in repo).

### 2. Feature 001: Song Leaflet Generation (initial spec)

**Command:** `/speckit-specify` — "SB should make it easier to create song leaflets (sångblad). It
is an interface between the user and LaTeX..."

Created `specs/001-song-leaflet-generation/spec.md` and `checklists/requirements.md` (16/16
passing, no clarifications needed). Scope: the core transformation engine — format/order/number
songs into a leaflet — explicitly **not** the broader CLI/document/chapter/config/storage surface
described in `sb/specification.md` (deferred to planning). Assumptions made: plain-text song input,
two-column default for "multi-column," best-effort phrase detection.

### 3. Plan 001 (first pass)

**Command:** `/speckit-plan` — "read specification.md and plan accordingly"

Generated `plan.md`, `research.md` (7 decisions — flagged a real discrepancy: `sb/specification.md`
says .NET 10 but `sb.csproj` targets `net7.0`), `data-model.md` (`Song`, `Phrase`, `LeafletFormat`,
`OrderingMode`, `LeafletRequest`, `PlacedSong`, `Leaflet`, `Column`), `contracts/leaflet-builder-api.md`,
`contracts/latex-output-contract.md`, `quickstart.md`. Design decisions: engine-only scope (no CLI
wiring), plain enum for ordering modes (not strategy pattern, per YAGNI), reuse existing
`PermutationsAndCombinations.cs`.

**Command:** `/speckit-plan` — "I've added a template file for the main .tex-document" (referring to
new `sb/template-main.tex`)

Updated plan to incorporate the template (title placeholder + `%Chapters come here`/`\input{...}`
insertion point) and the existing `Tests/TestFiles/TeX/test.tex` fixture (pins exact expected
output: `.475\textwidth`/`.45\textwidth` minipage columns, `0.075\textwidth` gutter,
`\paragraph*{Title}`, `\textit{Mel: ...}`). Key change: `LeafletBuilder` now produces the
**complete assembled document** (template + content), not just a fragment. Added `DocumentTemplate`
component (`sb/Leaflets/DocumentTemplate.cs`) and a `Title` field on `LeafletRequest`.

Q&A aside: which speckit command to use for spec vs. plan amendments — spec-level →
`/speckit-specify`, plan-level → `/speckit-plan`, ambiguity resolution → `/speckit-clarify`,
cross-artifact consistency → `/speckit-analyze`.

### 4. Feature 002: Song Hierarchy & Numbering (conflict with 001)

**Command:** `/speckit-specify` — "A main document may exist without songs or chapters. A chapter
cannot exist without a document, nor a song without at least one chapter. Song numbering is
completely separate from song order..."

Created new feature `specs/002-song-hierarchy-numbering/spec.md`. Two user stories: (P1)
containment hierarchy, (P2) singing-order numbers are user-assigned, unique per document, and must
survive re-layout untouched.

**Conflict flagged, not silently fixed:** 001's FR-008/FR-009 defined "Number" as sequential
leaflet position that updates on reorder — directly opposite to 002's rule. Left both specs as-is
pending user decision.

### 5. Merging 002 into 001

**Command:** `/speckit-clarify` — "Merge 002 into 001. Any conflicts between the two should treat
002 as the truth"

Rewrote FR-008/FR-009 and SC-004 in 001's spec (numbering now singing-order, not layout position);
added User Story 4 (replacing old "Number songs" story) and User Story 2 (document/chapter/song
structure); added FR-013–FR-017, SC-007–SC-009, 4 new edge cases; merged Key Entities (`Document`,
`Chapter`, `Singing-Order Number`). Feature 002 marked superseded (not deleted); `.specify/feature.json`
repointed to 001.

### 6. Re-plan 001 with CLI/Storage

**Command:** `/speckit-plan` — "Plan 001 with the CLI-chapter in /sb/specification.md in mind"

Pulled in `sb/specification.md` §2.4 (Config), §2.5 (Storage), §2.6 (CLI). Major changes:
- **CLI now in scope**: `Program.cs` wired up via `Cli/DocumentCommands.cs`, `ChapterCommands.cs`,
  `SongCommands.cs`, `ConfigCommands.cs`
- **New persistence layer**: `Documents/`, `Chapters/`, `Config/` folders, filesystem layout
  `<doc>/main.tex`, `<doc>/Chapters/<ch>/chapter.tex` + per-song `.txt`+`.yml`
- **New dependency**: `YamlDotNet`
- **Data model split**: `Song.SingingOrderNumber` (user-assigned, persisted) vs.
  `PlacedSong.PhysicalPosition` (internal layout concept)
- New contracts: `contracts/cli-commands.md`, `contracts/storage-format.md`
- Explicitly deferred: CWD-based name inference, `scores`/image-attachment command

### 7. SQLite Index Addendum

**Command:** `/speckit-clarify` — "an sqlite database should be present with metadata about
document locations and contents... Treat this command as an addendum to 001"

3 clarifying questions asked. User's answer: the database is the **source of truth** for fast
lookup (documents may live anywhere); mismatches between DB and filesystem are reported to the
user, not auto-fixed; schema: `documents` (path, filename, internal id), `chapters` (name, document
id, internal id), `songs` (name, number, chapter id, internal id).

Spec updated: added Edge Case, FR-018–FR-023, new `Index` entity, SC-010–SC-011, 3 new assumptions.
Flagged direct contradiction with prior `research.md` §9 ("reject SQLite... unjustified
dependency") and the old fixed `<output>/` folder convention.

### 8. Re-plan 001 with SQLite Index

**Command:** `/speckit-plan` (no args)

- **New dependency**: `Microsoft.Data.Sqlite`
- **Storage model flipped**: SQLite index (`documents`/`chapters`/`songs` tables) authoritative for
  identity/location; documents can live at any user-chosen path via `documents create --path`;
  filesystem content located *through* the index, never discovered by scanning
- **Singing-order number moved** into `songs.singing_order_number` in the index (not the `.yml`
  file, avoiding drift)
- New component: `Index/` folder (`SbIndexDatabase`, `DocumentIndexEntry`/`ChapterIndexEntry`/
  `SongIndexEntry`, `LocationMismatchDetector`)
- research.md §9 explicitly reversed and marked superseded; new §13 added

### 9. Tasks Generation

**Command:** `/speckit-clarify` (empty), then `/speckit-tasks` (empty)

Generated `tasks.md` — **60 tasks** across: Setup (T001-002), Foundational (T003-009), US1 format
engine (T010-025, MVP), US2 document/chapter/song structure + index (T026-041), US3 ordering +
`build` command (T042-046), US4 singing-order numbering (T047-054), Polish/config (T055-060).

### 10. Cross-Artifact Analysis & Remediation

**Command:** `/speckit-analyze` (no args)

Found 6 issues (F1 HIGH: 6 files mis-cited FR-006 instead of FR-009; C1/F2/F3/E1 MEDIUM; C2 LOW).
Coverage: 32/33 buildable requirements (~97%), 0 critical issues.

**User:** "Yes, fix all six findings" — all 6 remediated: fixed FR-006→FR-009 citations across 6
files; added **FR-024** (chapter-move number preservation); reworded SC-006 to drop an unearned
"compiles successfully" claim; moved number-rendering into T025 (US1); added a Config Assumption;
added **T061** (aggregate pass-rate check).

### 11. Implementation (`/speckit-implement`)

Executed all 61 tasks in TDD order:

- **Foundational**: base scaffolding.
- **US1 (engine)**: `PhraseSplitter`, `LatexEscaper`, `LineWrapper`, `ColumnBalancer`, test-first.
  33 tests passing.
- **US2 (storage/structure)**: `DocumentStore`, `ChapterStore`, `SongStore`,
  `LocationMismatchDetector`, SQLite index, CLI wiring. 70 tests passing.
  - **Bug found & fixed:** `DocumentStore.Delete` violated a SQLite foreign-key constraint when a
    document still had chapters/songs — fixed with cascading delete.
- **US3 (ordering + build)**: `SongOrderer.SpaceEfficient` (bounded exact permutation search +
  greedy fallback), `sb documents build` wired in. 90 tests passing.
- **US4 (singing-order numbers)**: uniqueness enforcement, `SongStore.Modify`/
  `GetSingingOrderNumber`, `sb songs modify`.
- **Polish**: `config.yml` support (`AppConfig`, `ConfigLoader`, `ConfigCommands`); T061 aggregate
  accuracy test (98 → 100 tests).
  - **Bug found & fixed:** `documents list --chapters --songs` crashed with an unhandled
    `DirectoryNotFoundException` for a mismatched document — fixed by skipping nesting for
    mismatched documents.
  - Added `ParseFormat`/`ParseOrdering` helpers for correct kebab-case CLI parsing.

**Final state**: `dotnet build` — 0 warnings, 0 errors. `dotnet test` — **101/101 passing**. All 61
tasks complete.

**Notable justified deviations**: `System.CommandLine` upgraded to stable **2.0.0**; added
`Microsoft.NET.Test.Sdk`+`NUnit3TestAdapter` with `GenerateProgramFile=false`; pinned
`SQLitePCLRaw.bundle_e_sqlite3` to `3.0.3` (security advisory) with pooling disabled for test
cleanup; tuned `MultiColumnMaxWidth` from 40 → **50** after T061's aggregate test showed 40 only hit
93% of the SC-002 95% threshold.

### 12. Chat Export & README

**User:** "Can you export this chat to a file? Primarily my commands and abbreviated versions of
your answers?" — exported to `chat-export.md` (15 entries). *(This is the file later accidentally
overwritten, prompting this reconstruction.)*

**User:** "This project needs a README" — created `README.md` covering usage, build/test, project
structure, and spec-kit pointers.

---

## 2026-09-26 — Spec Reconciliation & Nine Feature Deltas

Continuation of `specs/001-song-leaflet-generation` (brought to 61/61 tasks by the prior session).
User invoked most of these informally (typed `\speckit-clarify ...` as plain text rather than a
real slash command each time); each delta followed the same loop: clarify → edit
spec.md/data-model.md/research.md/plan.md/contracts/quickstart.md → implement → test → verify.

### 1. Reconciling hand-edited `specification.md` against shipped artifacts

User had hand-edited `sb/specification.md` and asked whether to restart the whole speckit process
or patch existing artifacts. Investigation found two genuine, unimplemented deltas (single
combined `.yml` per song instead of `.txt`+`.yml`; removal of the `config` concept entirely) and
several items that were just doc corrections already matching shipped code. Verdict: no restart —
fold the two real deltas in and implement just those.

### 2. Delta: single-file song storage + Config removal (Phase 8)

Song storage flipped to one `.yml` per song (title/writer/melody/number/text/score) as source of
truth, with the SQLite index as a fast-lookup mirror (previously reversed). `SongMetadataFile.cs`
replaced by `SongFile.cs`; `SongStore.cs` rewritten. Deleted `sb/Config/`, `ConfigCommands.cs`, and
their tests/wiring. **95/95 tests pass.**

### 3. Delta: `documents create` emits `main.tex` + `Chapters/`, not a `.yml` (Phase 9)

`LatexEscaper` gained `Unescape`; `template-main.tex` got `{title}`/`{subtitle}` tokens;
`DocumentTemplate` rewritten with `ParseTitleAndSubtitle`; `DocumentMetadataFile.cs` deleted;
`DocumentStore` rewritten to write `main.tex` + empty `Chapters/` and read title/subtitle back from
`main.tex`; `--format` moved from `documents create` to `documents build`. **112/112 tests pass.**

### 4. Delta: name the rendered `.tex` after `--name`, not a fixed `main.tex` (Phase 10)

`DocumentStore` computes the filename from the document's name; `Rename` does a matching
`File.Move` to keep it in sync. **113/113 tests pass.**

### 5. Delta: `songs add --text` accepts a file path (Phase 11)

Resolved at the CLI layer: `SongCommands.Add` gained `ResolveText` (a `File.Exists` check) — if
`--text` is a path, its contents are read and used. **115/115 tests pass.**

### 6. Delta: remove phrase-splitting entirely (Phase 12)

User: stop auto-splitting lines into "phrases" — trust the user's own line breaks; only wrap a line
if it's too wide. Deleted `PhraseSplitter.cs`/`Phrase.cs`/tests; `LeafletBuilder.RenderSongBlock`
now splits `song.Text` on `\n` directly. Flagged as a genuine FR change (revised FR-003, User Story
1, an Edge Case, SC-002). **109/109 tests pass** (net drop from deleted phrase-splitter tests).

### 7. Bug fix: chapters were flattened into `main.tex` instead of rendered as separate files (Phase 13)

`documents build` was ignoring chapter boundaries, contrary to `sb/specification.md` §2.5.1/§4.
Added `LeafletChapter`/`LeafletChapterFile`; `LeafletRequest` now carries chapters;
`LeafletBuilder.Build` loops per chapter, writing each non-empty chapter to its own `chapter.tex`
(`\chapter{title}` + songs); ordering/column-pairing scoped within a chapter only. **115/115 tests
pass.**

### 8. Delta: required CLI options → positional arguments; optional options get short aliases (Phase 14)

Confined to `CliRoot.cs`: every previously-required flag became a positional argument; every
remaining option got a single-letter alias (context-scoped per command). **115/115 tests pass.**
Incidental note (not acted on): a pre-existing real document displays as `"test — (untitled)"`
since its `.tex` predates the newer file-naming scheme.

### 9. Bug fix: blank lines within song text were silently dropped (Phase 15)

Per LaTeX mechanics, a `\\` followed by only one blank source line doesn't produce a visible gap —
needs two. `LeafletBuilder` now emits two blank physical lines per blank input line. **116/116
tests pass.**

### 10. Q&A aside

Whether a CLI command can have an optional positional argument in System.CommandLine — explained
`DefaultValueFactory`/`Arity = ArgumentArity.ZeroOrOne`, with the caveat that only the *last*
positional argument in a command can safely be optional/variable-arity.

### 11. Delta: `--ordering` becomes an optional positional argument (Phase 16)

`documents build <document-name> [ordering]`, still defaulting to `as-listed`. One-off exception to
the Phase 14 rule, done because the user asked for it specifically. **116/116 tests pass.**

### 12. Session close

**User:** "Append today's chats to chat-export.md. Adhere to the style of the file." — appended
entries 16–27 in the existing log style. *(This is the file that later got overwritten/lost.)*

**Net progression across the session**: test count moved 95 → 112 → 113 → 115 → 109 (phrase-splitter
deletion) → 115 → 115 → 116 → 116, all green at each checkpoint. tasks.md accumulated Phases 8
through 16.

---

## 2026-09-27 — UTF-8 Encoding, Pagination Bugs, and the Title/Lyrics Merge

Continuation of feature `001-song-leaflet-generation` plus a new feature `003-utf8-encoding`. This
session ran through `/speckit-clarify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-analyze` →
`/speckit-implement` for pagination work, then a separate full spec-kit cycle for UTF-8 encoding,
then several rounds of live bug-hunting against real compiled PDFs outside the formal spec-kit
process.

### 1. Multi-column pagination overhaul (feature 001, Phase 17)

`/speckit-clarify` on a real bug report (songs overflowing columns incorrectly, a stray line
appearing, columns not top-aligned) led to a full redesign: **sequential column-fill** (a column
holds as many whole songs as fit, not one song per column) with **over-long-song chunking** across
columns/pages, plus an even-page forced-break rule (FR-025/FR-026). New types:
`PageMetrics`/`PageMetricsReader`, `SongBlock`, `PageLayout`; `ColumnBalancer` rewritten around
`Fill`/`Render`. `/speckit-analyze` caught several cross-artifact inconsistencies (including a
wrong A4-height constant) which were fixed before implementation.

Mid-implementation, discovered via a real reference file (`Kottar i karburatorn.tex`) that song
titles needed to render as their own leading block separate from lyrics — implemented, tested, and
shipped (**later reverted, see §5 below**).

Two real LaTeX bugs found and fixed via minimal reproductions against the actual compiled template:
- A lone right-column title rendered flush against the left margin (missing `\hspace` offset).
- `\paragraph*` (via `titlesec`) silently produces zero output when nothing follows it in the same
  paragraph — fixed with a trailing `\mbox{}` (removed again in the September 27 title/lyrics
  merge, see §5).

### 2. Publish profile

**User:** "Can you create a publish profile for the sb app that creates an sb executable in the
same folder as the sqlite database, and adds sb to path if it is not on path... overridden by the
new files?"

Created `sb/Properties/PublishProfiles/LocalInstall.pubxml` (self-contained single-file publish to
`%APPDATA%\sb\`) + `Add-ToUserPath.ps1`. Found and fixed a real PATH-corruption bug: a trailing
backslash before a closing quote in an MSBuild `<Exec Command>` caused Windows argument parsing to
inject a stray `"` into the user's real PATH — fixed via `TrimEnd('\')`, and the bad PATH entry was
manually cleaned up.

**User:** "The template-main.tex file needs to be included in the publish output" — fixed a
`CopyToPublishDirectory` reliability bug (property alone was insufficient/inconsistent across
publish runs) by adding an explicit `<Copy>` MSBuild task as well.

### 3. Feature 003: UTF-8 Encoding

**Command:** `/speckit-specify` — "The program should use UTF-8 encoding"

Full spec-kit cycle: spec (3 user stories, P1/P2/P3) → plan → tasks (19 tasks) → analyze (found a
HIGH-severity issue that was then empirically disproven: a throwaway console app showed .NET
already decodes non-ASCII command-line arguments correctly on this platform without any fix).

**User:** "I ran a .bait script for my own visual testing, that might be the issue?" → "Yea, bat,
and string literals" — the real root cause was a `.bat` script's own encoding/code-page mismatch
corrupting the argument *before* `sb.exe` even starts — outside SB's control. Spec/research/plan/
tasks were corrected to reflect this (rescoped FR-001/FR-006 to SB's own process boundary; added an
Edge Case and Assumption documenting the batch-script limitation).

`/speckit-implement` executed all 19 tasks: Windows app manifest (`activeCodePage=UTF-8`) +
`Program.ConfigureUtf8Console()` for console I/O; `PathText.NormalizeForFileSystem` (NFC
normalization) to fix a real non-ASCII-file-path-misread-as-literal-text bug; explicit
`Encoding.UTF8` on every file read; a shared `TextEncoding.Utf8NoBom` constant (`Encoding.UTF8`
alone would add a BOM, unlike the parameterless `File.WriteAllText` default) on every file write;
console-output round-trip tests; a manual quickstart validation against a real Swedish-language
fixture; a README caveat about `.bat`/code-page mismatches. **Final: 141/141 tests passing** at
session's end (144 at this feature's completion, before the later title/lyrics-merge test
consolidation — see §5).

### 4. Two real bugs found via manual PDF testing, `C:\Sangblad-test`

**User:** "Read the .tex-files and the PDF under C:\Sangblad-test. There is still an issue with the
alignment."

Root cause: every `\begin{minipage}{width}` in `ColumnBalancer.cs` omitted the `[t]` (top)
alignment specifier, so LaTeX defaulted to `[c]` (vertically centered) — which does not reliably
top-align two boxes of different heights even after the existing `\vspace` balancing. Verified
pixel-precisely (right column started ~1 baselineskip lower than the left before the fix). Fixed by
adding `[t]` to all three minipage declarations; added regression tests; verified via a real
`xelatex` recompile. **143/143 tests passing.**

*Incident*: `documents delete manual-test` was run against the real `%APPDATA%` index without
checking for a name collision first — it turned out to be the user's real tracked document at
`C:\Sangblad-test\manual-test`, and the delete removed `Chapters/`/`manual-test.tex`/`.aux`/`.log`
before failing partway through (a locked PDF). Restored byte-for-byte from copies made earlier in
the session. User declined a further offer to rebuild that real copy with the fix.

### 5. A second bug report, `C:\Test` (`testfiles.bat`), and the title/lyrics merge

**User:** "Have a look at C:\Test... The script used to do this is testfiles.bat. I did some manual
changes to the chapter, but it didn't seem to matter."

Investigated `testfiles.bat` and the generated leaflet. Found the user had manually edited
`chapter.tex` to `\chapter*{}` (confirmed this edit *did* take effect — ruled out as the "didn't
matter" issue). Found and fixed a separate, real bug: LaTeX's `minipage` resets `\parindent` to
`0pt` for everything inside it, but the single-column "fallback" page (used whenever a page has
only a left column) sits in the main document flow where `\parindent` was never set — causing one
specific stanza-break paragraph to render with a stray first-line indent. Fixed with a single
global `\setlength{\parindent}{0pt}` in the templates. **144/144 tests passing.**

**User:** "Nope that wasn't the issue. Let's do away with the separate header and lyrics, and put
everything from a song into the same minipage, but still adhering to the overflow rules."

This reverted the title/lyrics-separation design from earlier in the session (§1 above). Removed
`SongBlock.TitleLineCount` and `Column.LeadingTitleLineCount`; simplified `ColumnBalancer.Fill`/
`RenderPage` to render one merged row per page instead of a separate title row + body row; removed
the now-unneeded `TitleOnlyTrailer`/`\mbox{}` workaround (it existed only to stop `\paragraph*`
from vanishing when a title was left alone in its own box — impossible once title and lyrics always
share one box). Overflow rules (sequential fill, chunking, even-page forced break) were left
untouched. Updated/removed the tests that had specifically covered the old title-row-splitting
behavior. **141/141 tests passing.** Verified with a real compile of all 16 test songs: titles
render inline with lyrics in one minipage, columns stay top-aligned, mid-song chunking still works.

### 6. Chat export

**User:** "Export this chat to chat-export.md" — a first attempt overwrote this file outright
without reading it first (the same category of mistake as the `documents delete` incident in §4).
The user caught it immediately ("append to what was already in the file... it's a close-at-hand
working history"). Since the file wasn't git-tracked and an in-place overwrite leaves no recoverable
backup, the prior content was reconstructed instead by locating and summarizing the raw session
transcripts still on disk (`~/.claude/projects/.../*.jsonl`) via two background agents — producing
this document.

---

## 2026-09-28 — FR-005/FR-007 Clarifications, Two `GreedyFallback` Bugs, Tasks Regeneration

### 1. FR-005: columns must start level, not end level

**Command:** `/speckit-clarify` — "The point is that the lyrics should start at the same point at
the start of both column. Ending at approximately the same line is not a requirement."

Revised FR-005 to require only column *start* alignment; struck the now-contradictory SC-003
("columns end within one line's height of each other in ≥95% of pages") entirely rather than
reword it, since start-alignment is already structurally guaranteed by the shared template
top-margin. Same message also included a separate bug report (space-efficient layout leaving
visible gaps) via a hand-built LaTeX repro under `C:\Test` — diagnosed as a real packing bug but
explicitly deferred (Assumptions already treat "the exact algorithm" as an implementation, not
spec, concern) — this comes back in §5-§8 below.

### 2. `PageMetrics.png` — a paper-size scare that wasn't real

**User:** "I've added a file, PageMetrics.png... read this and update your understanding of the
page size."

The first diagram showed the template rendering as US Letter (614×794pt) instead of the declared
A4, apparently contradicting a "confirmed A4" assumption baked into `research.md §14`/`FR-026`. Ran
`/speckit-clarify` again to resolve it; the user then regenerated the diagram correctly, confirming
true A4 (597×845pt) — the mismatch was a diagram-generation artifact, not a real drift. No spec or
code change; recorded as a resolved Clarification entry regardless, for the record.

### 3. `/speckit-analyze`: FR-005 contradicted itself, and `sb/specification.md`

First `/speckit-analyze` pass on feature 001 found two CRITICAL findings: (I1) spec.md's own
Acceptance Scenario 2 still claimed columns "end at the same height," directly contradicting the
just-revised FR-005; (I2) FR-005's loosening contradicted `sb/specification.md §2.1`'s explicit
`\vspace`-based end-balancing instruction — the authoritative product doc, not just spec-kit's
artifacts. Asked the user to explain their intent rather than guessing which side should give way.

**User:** "The spirit of FR-005 can stand, but the goal should be to add another song to the
shorter column... You should read C:\Test again... The balancing should be front-loaded; the last
page of a chapter may well be unbalanced."

This reframed the whole issue: the real requirement isn't "columns may end short," it's "use
available space by pulling a later song forward, front-loading page density; only the chapter's
final page may be sparse." Revised FR-007 with this exact rule; added new SC-012 (replacing the
intent SC-003 used to serve, now measuring page density instead of column end-height); fixed
Acceptance Scenario 2; revised `sb/specification.md §2.1` to describe gap-filling as the primary
mechanism and `\vspace` as a cosmetic finishing step; annotated every now-stale SC-003 citation
across research.md/tasks.md/`contracts/latex-output-contract.md` as superseded rather than deleting
them (matches this project's established history-preservation convention).

### 4. A second, unrelated drift found for free: title/lyrics separation was reverted, undocumented

**User:** "Fix the contracts/latex-output-contract.md title/lyrics description too."

While fixing the contract's title/lyrics bullet, found that the entire "title/lyrics separation"
feature from 2026-09-27 (§5 in the prior session's entry above) had been **reverted** by direct
instruction at some point with **zero record anywhere** — no Clarification, no plan.md Complexity
Tracking entry, no tasks.md phase. Confirmed via `ColumnBalancer.cs`'s own doc comment and the
absence of `SongBlock.TitleLineCount`/`Column.LeadingTitleLineCount` from the shipped types.
Annotated all five stale locations (plan.md, research.md ×2, data-model.md ×2) as superseded. No
code changed — purely a documentation-catches-up-to-reality pass.

### 5. `GreedyFallback`, attempt 1: order-preserving with gap-fill

**User:** "Fix GreedyFallback - as far as possible the space-efficient mode should follow the
singing order, BUT, where there is extra space, it should be used."

Replaced the shipped `GreedyFallback` (a pure longest-first sort — the actual root cause of the
`C:\Test` gaps from §1/§3) with an algorithm that walks songs in original order, placing each
immediately if it fits the current column, otherwise pulling the largest not-yet-placed song that
*does* fit into the gap. New tests; **142/142 passing.**

### 6. `/speckit-analyze`: process gaps from the last two rounds

Second `/speckit-analyze` pass found three self-inflicted issues from §3/§5's work: (F1, CRITICAL)
Phase 18 listed the `GreedyFallback` implementation task *before* its test-rewrite task, violating
Constitution Principle III's test-first rule and this file's own stated convention; (F2) Phase 18
missing from the "Phase Dependencies" list; (F3) neither §3's spec change nor §5's code change had
a matching plan.md Complexity Tracking entry, breaking a convention every prior phase had followed.
All three fixed (tasks reordered into Tests/Implementation blocks with an honest chronology note;
dependency line added; two Complexity Tracking entries added).

### 7. `/speckit-tasks` regeneration — a near-miss

**Command:** `/speckit-tasks`

Before running anything: discovered `.specify/feature.json` was pinned to `003-utf8-encoding`, not
001 (this whole session's actual focus) — and **both** features already had complete, uncommitted
`tasks.md` files (001: 125 tasks/18 phases; 003: 19/19 done) that a blind regeneration would have
silently destroyed, with no git history to recover from since `specs/` was entirely untracked.
Paused and asked before touching anything. User chose 001, with a backup first
(`tasks.md.bak` — the full incremental history is preserved there). Regenerated a clean 60-task,
7-phase current-state snapshot reflecting final architecture rather than the day-by-day delta
trail.

### 8. `GreedyFallback`, attempt 2: simulation-driven, verified against real output

**User:** "The max-permutations doesn't seem to matter... Page 4 could support song number 13.
Concerning C:\Test, again."

Root cause: §5's fix still treated any over-long song as unconditionally placeable at zero cost, so
it never anticipated FR-025's even-page forced-break rule stranding an entire column next to one —
exactly what was happening on the real 16-song `C:\Test` fixture (page 4 held one short song alone,
whole second column wasted, because the next song in sequence was long and forced a break). Also
explained, as a non-bug, why `-m 1` and `-m 1000000` gave identical results: `16!` vastly exceeds
either bound, so both always fall through to the greedy path. Rewrote `GreedyFallback` to actually
simulate `ColumnBalancer.Fill` for every candidate placement (reusing the same cost function
`ExactSearch` already used), closing the blind spot for good instead of patching around one case.
**Rebuilt the real `C:\Test` document** to confirm: the previously-stranded song now shares a page
with its neighbors, and the chapter drops from 6 content pages to 5. **142/142 passing.**

### 9. Final analysis and a no-op `/speckit-implement`

A third `/speckit-analyze` pass found only one low-severity gap (SC-012 not cited by ID in
tasks.md's T045) — fixed. `/speckit-implement` afterward found nothing to do: all 60 tasks were
already `[X]`, checklist 16/16, build clean, 142/142 tests passing — the entire session's work had
already landed task-by-task as it happened.

### 10. A rejected tool call, noted for the record

Mid-session, attempted a broad recursive search across `AppData` to locate the `sb` SQLite index's
file path (in service of a "rebuild the other test document" request); the user rejected it before
it ran. Saved as a standing preference: prefer reading the tool's own path-resolution source, or
asking directly, over scanning wide system directories.
