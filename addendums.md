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
