# Addendums and bugfixes

Changes to the specification (`specification.md`) and requirements (`requirements.md`) made after the original
milestones were implemented, found during visual acceptance testing. Each entry names the affected
spec/requirement sections.

## A-1: Song header rendered on separate lines (2026-10-13)

**Problem.** In the built document the song title appeared on the same line as the first lyric line. The title
was emitted as `\paragraph*{name}`, which is a run-in heading in LaTeX: the following text continues on the
same line. Melody and writer were not rendered at all.

**Expected.** The title, melody (if any) and writer (if any) each reside on their own line above the lyrics.
Melody and writer are prefixed with `Mel:` and `Text:` respectively.

**Fix.**
- `ChapterEmitter` (`Sb.Core/LaTeX`) emits the header via `AppendHeader`:
  `\paragraph*{Title}\mbox{}\\`, then `Mel: …\\` and `Text: …\\` when present. Used by both single-column and
  multi-column rendering.
- `SongLayout.HeaderLines` counts the header lines (1 + melody + writer); `SongLayout.Height` and
  `SongSplitter` use it so pagination and column/page splitting budget for the extra lines.

**Documentation updated.**
- `requirements.md`: FR-1.12.7 unchanged; FR-1.12.8 reworded; new FR-1.12.11 – FR-1.12.14.
- `specification.md`: §4.3 Songs.

**Tests.** `ChapterEmitterTests`: new test for header lines; expected `\\` count in
`RenderMultiColumn_WrapsLineTooWideForColumn` raised from 4 to 5 (the title's line break).

## A-2: Songs must not start with less than one paragraph; continuation marker is "(forts.)" (2026-10-13)

**Problem.** In the built document the song "bankvisan" was started on the last two lines of a page, so only its
title and a `(continued)` marker were left there, with all the lyrics on the next page. `SongSplitter` allowed a
split to leave a song with just its title (or a title and a single line) in the remaining space.

**Expected.**
- A song may only be started in a slot if at least one whole paragraph fits there. A paragraph is the lines in
  the `.yml` text before an empty line; a song with no empty line is a single paragraph.
- The continuation markers `(continued)` and `(continued from previous page)` are both replaced by `(forts.)`.

**Fix.**
- `SongSplitter.Split` returns `(null, null)` (the song does not start here) when the first fragment would
  contain no complete paragraph. A new `allowPartialFirstParagraph` parameter lifts this; `ColumnFiller` sets it
  only when the song would start at the top of an empty column 1, so a song whose first paragraph is taller than
  a column cannot stall pagination.
- `ChapterEmitter` renders both continuation notices as `\textit{(forts.)}`.

**Side effect.** Slots that used to be topped up with a title and a stub are now left empty, and the
height-based space-efficient search could pick sets that no longer placed well, leaving some pages sparsely
populated (an 85% page was observed in the synthetic test). Resolved by A-3.

**Documentation updated.**
- `requirements.md`: new FR-1.3.17; FR-1.12.10 marker text.
- `specification.md`: §4.3 Songs.

**Tests.** `SongSplitterTests`: the title-only-split test now asserts a refusal; new tests for first paragraph
not fitting, fitting, and the fresh-column exemption. `ColumnFillerTests`, `PaginatorTests`,
`SpaceEfficientPaginatorTests` and `SongSplitterTests` fixture songs now have an empty second line, so that
paragraph 1 is one line. `ChapterEmitterTests` expects two `(forts.)` markers.

## A-3: Paragraph-aware space-efficient search (2026-10-13)

**Problem.** After A-2, the space-efficient search still chose songs by height alone (FFD, then exact
subset-sum against the page's combined capacity). A set whose heights summed to the capacity could be impossible
to place under the paragraph rule, e.g. column 1 ending with a few lines nothing may start in, so the last song
spilled off the page. Some pages ended up scarcely populated.

**Fix.** `SpaceEfficientPaginator.ChooseCompanions` replaces the direct FFD/subset-sum selection. It builds
candidate sets from FFD and from `SubsetSumSearch` at the full capacity and at progressively smaller ones (down to
one column's height less), tries each in as-listed, longest-first and shortest-first order, and scores every
attempt by actually placing it with `ColumnFiller`. The set that places the most lines wins; FFD is still tried
first and short-circuits when its simulated page reaches the 95% target. The pagination code that places the
chosen songs is unchanged.

**Documentation updated.**
- `requirements.md`: new FR-1.3.18.
- `specification.md`: §2.2, a paragraph after the best-fit description.

**Tests.** New `Paginate_ChoosesSetThatReallyPlaces_NotJustOneWhoseHeightsAddUp` (fails on the old search with 14
lines placed instead of 18). The synthetic fill-target test is back to the strict 95% (A-2's 10 point tolerance
removed) and passes.

## A-4: Song number shown before the title (2026-10-13)

**Problem.** The song number was stored and used for ordering but never shown in the document.

**Expected.** The number appears just before the title: `1. Title`.

**Fix.** `ChapterEmitter.AppendHeader` emits `\paragraph*{<number>. <title>}`, for both single-column and
multi-column rendering. The prefix is part of the title line, so pagination heights are unchanged.

**Documentation updated.**
- `requirements.md`: new FR-1.12.15.
- `specification.md`: §4.3 Songs, header line list.

**Tests.** New `RenderMultiColumn_PrefixesTitleWithSongNumber`; existing `\paragraph*{...}` assertions in
`ChapterEmitterTests` now expect the `1. ` prefix.

## A-5: Layout consistency fixes from the codebase review (2026-10-13)

Three defects found in the review of the code against the docs.

**1. `(forts.)` notice lines were not counted in heights.** Both notices render an extra line, but fragment
heights and split budgets ignored them, so a page-boundary split could render 1–2 lines taller than planned (column
overflow, wrong `\vspace`). `SongSplitter.Split` now reserves a line for the trailing notice when splitting at a
page boundary, a line for the leading notice of a fragment that resumes from a previous page, and gives the
remainder a height including its leading notice. Requirement text: FR-1.12.14.

**2. A song could spill out of an even page in as-listed (and, via simulation, space-efficient) ordering.**
`ColumnFiller` did not know the page parity: a song starting in column 1 and overflowing column 2 spilled onto the
next page even if it fitted on a single page (violating FR-1.3.6). `ColumnFiller.Fill` takes
`allowPageOverflow` (true on odd pages); when false, a song that would overflow, is not already a continuation and
fits on a page (height <= 2 lines per column) is left whole for the next page. `Paginator` and
`SpaceEfficientPaginator` (including the `ChooseCompanions` simulation) pass the page parity. A split whose
second part could not be placed at all now carries the whole remainder forward instead of dropping it.

**3. The paragraph rule was stricter than FR-1.3.17.** The terminating empty line also had to fit. It no longer does:
the paragraph is complete if an empty line follows it inside the placed lines, or the placed lines end exactly
before an empty line (or the end of the text). An empty line at the break is dropped rather than shown at the top
of the next column or page.

**Documentation updated.**
- `requirements.md`: FR-1.3.6 clarified; FR-1.3.17 and FR-1.12.14 reworded.
- `specification.md`: §4.3 (paragraph rule, notice lines, even-page rule).

**Tests.** `SongSplitterTests`: notice heights at a page boundary, none at a column boundary, paragraph fitting without
its terminating empty line. `ColumnFillerTests`: even-page spill waits whole, odd-page spill allowed, song longer
than a page still spills (FR-1.3.15). Existing split tests adjusted for notice heights and dropped break lines.

## A-6: Song numbers are document-wide (2026-10-13)

**Problem.** The documents (FR-1.4.1, §2.3, FR-1.12.15) said a song's number is its position in the document, but
`SongOperations` numbered per chapter: a document with two chapters printed "1. A" in chapter 1 and "1. B" in
chapter 2, and `--number` was validated against, and shifted, only the songs of one chapter. Spec §2.5.3 said
"in the chapter".

**Decision.** Per document is the intended behaviour.

**Fix.** `SongOperations.Add`, `Delete` and `Modify` now load every song of the document (all chapters, in number
order) and renumber that whole sequence, rewriting the `.yml` file and DB row of each shifted song in its own
chapter. Details:
- `songs add` without `--number` appends at the end of the document; a given `--number` must be in
  1..(songs in document + 1) and shifts later songs in every chapter.
- `songs delete` closes the gap across the whole document.
- `songs modify --chapter` keeps the song's number (its position in the document is unchanged); `--number`
  moves it within the document sequence, whichever chapter it ends up in.
- A document numbered per chapter before this change loads with duplicate numbers; they are ordered by number and
  then chapter, and the next add/delete/modify renumbers them into one sequence.
- While rewriting, an IO failure while saving one shifted song's file is now collected with the other renumbering
  failures instead of escaping as an exception.

**Documentation updated.**
- `requirements.md`: FR-1.4.3 states the numbering is document-wide.
- `specification.md`: §2.3 and §2.5.3 (`--number`). `Sb.Cli` help text for `--number` says "position in the whole
  document". TASKS.md's `max+1` wording replaced by "next sequential number".

**Tests.** `SongOperationsTests`: numbering continues across chapters; an explicit number shifts songs in other
chapters; delete shifts later songs in other chapters; a chapter move keeps the number; a number change across
chapters renumbers the whole document. The old "chapter move renumbers source and destination" test is replaced by
the move-keeps-number test.

## A-7: Name validation hardened; case-sensitive names are intentional (2026-10-13)

From the codebase review (FR-1.8.6, FR-1.8.8).

**Decision: name uniqueness stays case-sensitive, but SB refuses to overwrite.** "Hymn" and "hymn" are distinct names
to SB. On a case-insensitive file system (the Windows default) they map to the same file. Rather than only documenting
that, `songs add` and `songs modify` now check the target file first and fail with a Conflict error that names the
file (and leaves the database and files untouched) if saving would overwrite an existing file that is not the song's
own — another case-variant title, or a stray file. Also fixed in `songs modify`: changing only the case of a title on a
case-insensitive file system used to write the new-case file and then delete the old path, which is the same file,
losing the song file; the old file is now only removed when it really is a different file. Documented in spec §2.5 and
FR-1.8.9. Document and chapter folders were already protected (their creation/rename fails if the folder exists).

**Problem.** `NameValidator` accepted names that are unsafe on Windows: `.` (a chapter named `.` resolves to the
`Chapters` folder itself, so `chapters delete` would remove the whole folder, and a document named `.` resolves to
its output folder), names with leading or trailing whitespace or a trailing dot (Windows silently strips them, so the
folder is not the name SB recorded), and reserved device names such as CON or NUL, which cannot be created at all.

**Fix.** `NameValidator.Validate` additionally rejects: the name `.`; leading/trailing whitespace; a trailing `.`
(not for song titles, which are only a file name stem, via `allowTrailingDot` set by `Song.Create`); and the
reserved device names CON, PRN, AUX, NUL, COM1–9, LPT1–9, case-insensitively and also when followed by an extension
(`CON.backup`). Names that merely start like one (`Console`, `COM10`) are fine.

**Documentation updated.**
- `requirements.md`: FR-1.8.8 extended, new FR-1.8.9 (case-sensitive uniqueness).
- `specification.md`: §2.5, the uniqueness and name-validation paragraphs.

**Tests.** `SongOperationsTests`: add and modify refuse to overwrite a stray file (nothing changed), a case-variant title
is refused and a case-only rename keeps the file (both run only where the file system ignores case).
`NameValidatorTests`: single dot, leading/trailing whitespace, trailing dot (default rejected, allowed for
songs), reserved names in several cases and with an extension, and look-alike names that must still be accepted.

## A-8: `documents list -s`, full argument inference, atomic renumbering, top-level error handling (2026-10-13)

The four open behavioural items from the codebase review.

**1. `documents list -s` without `-c` showed nothing.** The spec says `-s` lists songs; the code only read it when
`-c` was also given (the help text even said "requires --chapters"). `-s` alone now lists each document's songs
directly under the document, in number order (`DocumentListing.Songs`); with `-c` they are still grouped under
their chapters.

**2. Working-directory inference only covered some commands.** FR-1.8.4 and NFR-2.3.2 say document and chapter
names are inferred from the working directory, but the commands with more than one positional argument
(`documents rename`, `chapters delete`, `songs add`/`delete`/`modify`) required the document name, and nothing
inferred a chapter name. System.CommandLine cannot leave a leading positional unfilled, so these commands now
declare a single array argument whose arity lets the leading name(s) be absent, and `InferredPositionals.Resolve`
assigns the values: the last ones are the command's own arguments, and what precedes them is the document name
and/or chapter name. For `songs add`, a single leading value is the chapter name (the document is inferred),
none at all means both come from the working directory. Explicit names always win; where inference fails a
Validation error says which name is missing. Trade-off: `--help` for these commands shows one `arguments`
placeholder with a usage description rather than separate named arguments. (On Windows, renaming the document
folder you are standing in is refused by the operating system, which is unrelated to inference.)

**3. Renumbering was not part of the atomic step.** The add/delete/modify itself was atomic, but the renumbering of
other songs ran afterwards with no rollback, so a failure could leave duplicate or gapped numbers (FR-1.7.6,
FR-1.4.3); an IO error there could even escape as an exception. Now: the changed files (the song's own and every
shifted song's) are written as a list of steps each with an undo, the database rows are changed in one SQLite
transaction (`Database.InTransaction`), and a failure in either puts the files back and commits nothing. Delete
rewrites the shifted files and updates the rows first, and only afterwards removes the deleted song's own file;
that file removal has no cheap undo, so, as for every delete, a failure there is reported rather than rolled back.

**4. `Program.Main` had no top-level error handling.** An unusable `SB_DATA_DIR`, a locked database or similar
crashed with an unhandled exception. IO, access and SQLite errors outside the commands are now reported as
`Error: <message>` with the Storage exit code (4). Genuine bugs still surface as exceptions.

**Documentation updated.**
- `requirements.md`: FR-1.7.6 (renumbering atomic), FR-1.8.4 (inference everywhere), FR-1.9.2 (`-s` alone),
  NFR-2.2.2 (environmental failures).
- `specification.md`: §2.4 (atomicity), §2.5 (inference, top-level failures), `documents list` flags.
- `TASKS.md` 10.1: the inference limitation it described is lifted. Stale `ExitCodes` and
  `DocumentNameResolution` code comments corrected.

**Tests.** `SongOperationsTests`: add, delete and modify roll everything back when rewriting another song's file
fails (read-only file; Windows only). `DatabaseTests`: `InTransaction` commits, rolls back on a failed result, and
rolls back on an exception. `SongsCommandsTests`, `ChaptersCommandsTests`, `DocumentsCommandsTests`: inference for
`songs add` (document and chapter, document only, explicit override, missing document, missing chapter),
`songs delete`/`modify`, `chapters delete` and `documents rename`; `documents list -s`.
`ProgramExitCodeTests`: an unusable data directory yields a Storage error, not a crash.

## A-9: Documentation sweep (2026-10-13)

A pass over the documents and code comments for text that no longer matched the code after A-1 to A-8. No behaviour
changed.

**`requirements.md`**
- FR-1.3.2 / FR-1.9.5: "as-listed" and "number-wise" are the same mode (song-number order); the CLI values are
  `as-listed` and `space-efficient`; single-column builds always use as-listed.
- FR-1.3.10 / FR-1.3.12: the 95% target is measured on the page's two columns in lines actually placed.
- FR-1.3.16: single-column mode has no continuation notices (SB cannot see the page breaks).
- FR-1.8.5: the delete confirmation prompt belongs to the CLI; the core refuses with a Conflict unless forced.
- FR-1.9.5 / NFR-2.6.1: describe the simulate-and-score search and why it stays pseudo-polynomial.
- FR-1.12.6: the columns, not the songs, are the minipages. FR-1.12.7 / FR-1.12.11: the title placeholder is
  `N. Title` followed by a line break.
- NFR-2.2.2: exit-code numbers and the database location / `SB_DATA_DIR` are now stated.

**`specification.md`**
- §2.2: the best-fit target is page-level; single-column has no continuation notices.
- §2.4.2: the `.yml` is a plain mapping (not a list of `- key:` items), `writer`/`melody` omitted when absent,
  scalars written double-quoted. §2.4.3 is titled and lists the display title/subtitle fields that were missing.
- §2.5: exit codes and `SB_DATA_DIR`. §4.3: minipages are columns; title is `N. Title`.

**`TASKS.md`**: 4.3 (`\vspace` makes the columns *end* together), 5.4 (real-fixture test checks fidelity; the search
is now `ChooseCompanions`), 5.5 (the flags exist; `ColumnFiller` now knows page parity), 6.4 (header lines, number
prefix), 6.6 (`(forts.)`, no notice in single-column, marker lines counted), 9.1–9.4 (document-wide numbers,
inference, atomic renumbering).

**Code comments** corrected in `ChapterEmitter`, `SpaceEfficientPaginator`, `FirstFitDecreasing`, `SpaceEfficiency`,
`DocumentLayout`, `ChaptersRepository`, and `MismatchDetector` (which now says plainly that nothing calls it yet).

**`README.md`** was empty; it now covers the documents, projects, build/test/install, where data lives, usage, exit
codes and compiling the output.

**Known gaps, not documentation problems:** `MismatchDetector` is not yet wired into any command (FR-1.7.5 is only
met for a missing song file, which is reported when read); `songs modify` cannot clear a writer or melody.

## A-10: Mismatch detection in use; clearing a song's writer and melody (2026-10-13)

The two code gaps left from the review.

**1. FR-1.7.5 was only half in effect.** `MismatchDetector` existed but nothing called it, and it only knew document
folders. A document, chapter or song file moved or deleted outside SB showed up as a generic IO error, or — in
`documents build` — a missing chapter folder was silently recreated.
- `MismatchDetector` now also finds missing chapter folders and song files, and has `CheckDocumentFolder` /
  `CheckChapterFolder`, which return a Storage error naming the path and what to do (move it back, or remove the entry).
- `documents list`, `chapters list` and `songs list` mark anything missing as `[NOT FOUND on disk: <path>]`
  (`MissingAt` on the listing records). Listing is informational, the exit code stays 0.
- `documents build` checks the document folder and every chapter folder before writing anything (no half-written
  output, no silent recreation); `documents rename`, `chapters add`, `songs add` and `songs modify` check the folders
  they need.
- Unchanged on purpose: deleting something whose files are already gone still removes the database entry and reports
  the file-system cleanup problem (the existing, tested FR-1.7.6 behaviour).

**2. `songs modify` could not remove a writer or melody.** `null` meant "keep", so the only way to change a value
was to give a new one. An empty (or whitespace-only) value now clears the field: it is stored as absent, so it is
not printed and is left out of the `.yml`. On `songs add` an empty value is the same as omitting it. (Help text of
`--writer`/`--melody` on `modify` says so.)

**Documentation updated.** `requirements.md`: FR-1.7.5 (concrete behaviour), FR-1.11.4. `specification.md`: §2.4.3,
`modify` flags. `README.md`: `modify` usage. The `MismatchDetector` comment no longer says it is unused.

**Tests.** New `MismatchHandlingTests` (listings flag missing document/chapter/song, build/rename/chapter add/song add/
song modify refuse with a Storage error and change nothing, empty writer/melody clear them, empty writer on add);
`MismatchDetectorTests` for chapter and song mismatches and the checks; a CLI test that `documents list` flags a
missing folder.

## A-11: Single-column mode keeps a song's header with its first paragraph (2026-10-13)

**Problem.** In single-column mode LaTeX decides where pages break, so the paragraph rule of A-2 (never leave a title
and a stub at the end of a column or page) could not be applied, and a song's title could end up alone at the bottom
of a page.

**Fix.** `ChapterEmitter.RenderSingleColumn` now ends the header lines and the song's first paragraph with `\\*`
(a line break after which LaTeX may not start a new page) instead of `\\`. The last line of that group, and all the
rest of the song, keep the plain `\\`. The group is the header plus the first paragraph, capped at 10 lyric lines
(`MaxLyricLinesKeptWithHeader`) so that a song without any empty line cannot produce a page that does not fit, and it
always contains at least the first lyric line, even if that is an empty line. Multi-column output is unchanged.

**Not changed.** Single-column mode still has no `(forts.)` notices and always uses as-listed order (see FR-1.3.16,
FR-1.9.5): those would need SB to know the page breaks, i.e. a multi-pass LaTeX mechanism, which was judged too
heavy for the mode that is meant to be plain LaTeX flow.

**Documentation updated.** `requirements.md`: new FR-1.12.16. `specification.md`: §2.1.

**Tests.** `ChapterEmitterTests`: header and first paragraph joined with `\\*` and the rest plain; a first paragraph
longer than the cap is only kept together up to the cap; a song starting with an empty line still keeps its title with
the next line; multi-column output contains no `\\*`.
