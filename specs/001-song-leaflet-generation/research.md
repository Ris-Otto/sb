# Phase 0 Research: Song Leaflet Generation

## 1. Target framework: net7.0 vs. .NET 10

**Decision**: Update `sb/sb.csproj` to target `net10.0` as part of this feature's
implementation.

**Rationale**: `sb/specification.md` (the project's source-of-truth document) states explicitly
"SB is built with C#, .NET 10", but the current `sb.csproj` targets `net7.0` (which is out of
support). Since the current date is well past .NET 10's release, aligning the project with its
own stated target is a small, low-risk change (a single `<TargetFramework>` edit plus a rebuild)
and removes a standing inconsistency before new feature code is added on top of it.

**Alternatives considered**:
- *Keep `net7.0` for this feature, migrate separately*: Rejected — leaves the codebase
  inconsistent with its own specification while active development continues on top of it, and
  the migration itself is low-risk enough not to warrant deferral.

## 2. LaTeX document generation approach

**Status: chapter assembly corrected 2026-09-26** — see the update below; the original decision's
"insert per-song content directly into main.tex" and its rejection of per-chapter fragments were
both wrong (a pre-existing, undocumented deviation from `sb/specification.md` §2.5.1/§4, caught by
a `/speckit-clarify` round). Retained for history.

**Original decision (superseded)**: Generate the complete leaflet document by combining plain C#
string/`StringBuilder` assembly for the per-song formatted content with the existing
`sb/template-main.tex` as the literal master-document skeleton — no templating library.
Concretely:
- `sb/template-main.tex` (already present) supplies the `\documentclass`, package preamble,
  title page, and page-numbering scaffold. It has one substitution point (the literal `Title`
  text inside the `titlepage` block) and one insertion point (the `%Chapters come here` /
  `\input{*relative_path_to_chapter*}` comment block) that this feature's `DocumentTemplate`
  component locates and fills in — the title with the leaflet's requested title, and the
  insertion point with the generated per-song content directly (rather than an `\input` per
  chapter).
- The per-song content itself (the actual formatted lyrics) is generated as plain strings
  following the exact structural pattern already established by the existing fixture
  `sb/Tests/TestFiles/TeX/test.tex`: `\noindent` + `\begin{minipage}{<width>\textwidth}` per
  column, `\paragraph*{<Title>\\}` for the song title, `\textit{Mel: <melody>}\\` when a melody
  is present, lyric lines each ending in `\\`, and `\hspace{0.075\textwidth}` between sibling
  columns on the same row.

**2026-09-26 decision**: Each chapter is rendered independently into its own `chapter.tex`
fragment (`\chapter*{<title-or-name>}` heading, escaped, followed by that chapter's own songs in
the exact per-song structural pattern above — unchanged), and `template-main.tex`'s
`%Chapters come here` insertion point is filled with a sequence of `\input{Chapters/<name>/
chapter.tex}` lines, one per chapter that has at least one song (an empty chapter is skipped
entirely — no heading, no `\input` line, since there is nothing to head). Song ordering and
column-balancing (single-/multi-column) are scoped to *within* one chapter's own songs only —
never pairing two songs from different chapters into the same two-column row, since `\chapter`
forces a physical break in a `book`-class document regardless of what the layout algorithm might
otherwise prefer. `LeafletRequest` takes `IReadOnlyList<LeafletChapter>` (name, optional title,
its songs) instead of a flat song list, and `Leaflet` gains `ChapterFiles`
(`IReadOnlyList<LeafletChapterFile>`, one per non-empty chapter) alongside `Document` (the main
file, now containing only `\input` lines, never song content directly).

**Rationale**: Directly matches `sb/specification.md` §2.5.1 ("Documents and chapters are stored
as .tex-files") and §4 ("Anytime a chapter is added to the document, insert it into the
document"), which this project's own template (`%\input{*relative_path_to_chapter.tex*}` comment)
and `data-model.md`/this section's own storage-layout tree already described as the intended
design since 2026-09-11 — the flat, single-file rendering was an implementation gap, not a
deliberate simplification. FR-011's "complete, self-contained... document" is still satisfied: a
main document plus its `\input`-referenced fragments, sitting in the exact folders the index
already points at, compiles into one PDF without any further manual editing or unspecified
assembly step — `\input` is ordinary, always-available LaTeX plumbing, not a second undefined
feature. Scoping ordering/column-balancing to within a chapter (not globally) is forced by
`\chapter`'s own semantics (a book-class chapter is a hard break), not a new independent design
choice.

**Alternatives considered**:
- *Templating engine (Scriban/Razor)*: Rejected — unjustified dependency for a handful of fixed
  substitution points; would also require learning/maintaining a second syntax alongside LaTeX
  itself.
- *Existing .NET LaTeX-generation libraries*: None found that are idiomatic or actively
  maintained for this narrow use case; hand-rolled generation is simpler and fully under test
  control.
- *Keep everything flattened into one file (the original, pre-2026-09-26 decision)*: Rejected —
  this was the bug the user's `/speckit-clarify` round identified: `sb/specification.md`
  §2.5.1/§4 and this project's own template/docs already specified per-chapter files; the
  original research.md rationale's claim that a fragment would violate FR-011 was itself mistaken
  (see Rationale above).
- *Keep ordering/column-balancing global across the whole document, only splitting the rendered
  OUTPUT into per-chapter files after the fact*: Rejected — a `\chapter` heading forces a page/
  section break in the `book` class, so a two-column row straddling that break (one song from
  chapter A, one from chapter B, in the same physical row) would be visually broken; there is no
  coherent way to "split after the fact" once two songs from different chapters have been paired.
- *Render an empty chapter's heading with no songs under it, rather than skipping it*: Rejected —
  an empty `\chapter{}` heading followed by nothing is a worse reading experience than omitting it,
  and nothing in spec.md requires every chapter to appear in the built output regardless of content
  (only that a chapter *may* validly exist with zero songs, FR-013's chapter analog — not that it
  must be visually represented once built).

## 3. Line-formatting strategy

**Status: phrase-splitting REMOVED (2026-09-26)**. This section is retained for history; do not
implement against the original decision below.

**Original decision (superseded)**: Rule-based tokenizer: split lyric text first on existing line
breaks (respecting the source's own line structure), then on sentence-ending punctuation, then on
commas, treating each resulting unit as one "phrase" (per spec.md's original definition:
sub-sentence up to a comma, a sentence, or a full line, whichever applies). When no punctuation
was present, fell back to treating the existing line as the phrase unit.

**2026-09-26 decision**: No tokenizing at all. `LeafletBuilder` splits a song's `Text` only on
`\n` (trimming each line), and treats each resulting non-blank line as one output line directly
(FR-003's revised wording), wrapping it with the existing `LineWrapper` only if it's too wide for
the column (FR-004, unchanged). `PhraseSplitter`/`Phrase` (`sb/Formatting/`) are deleted rather
than kept unused (Constitution Principle I).

**Rationale**: Directly matches spec.md's 2026-09-26 Clarification and the revised
`sb/specification.md` §2.1 ("SB does not try to detect musical phrasing... one input line becomes
one output line"). Removing the sentence/comma-splitting logic (rather than keeping it dormant)
avoids dead code implementing a rule the product no longer wants (YAGNI, Principle II).

**Alternatives considered**:
- *NLP sentence-segmentation library*: Rejected (pre-2026-09-26, moot now that phrase-splitting
  itself is removed) — general-purpose sentence splitters are tuned for prose, not song lyrics,
  and would add a heavy dependency for a rule the domain no longer wants at all.
- *Keep `PhraseSplitter`, just stop calling it from `LeafletBuilder`*: Rejected — dead,
  unreachable code with its own tests to maintain for a capability that no longer exists is worse
  than deleting it outright (Principle I: Readability First).

**2026-09-26 addendum — blank lines**: A blank input line (a trimmed-empty line — e.g. a stanza
separator) was being silently dropped (correct per the old, now-superseded fallback rule this
section documented above, but wrong under this section's own current decision: "each resulting
line" includes blank ones, per FR-003's literal wording). Fixed per a further `/speckit-clarify`
round: `RenderSongBlock` now appends **two** empty strings to the column's line list for every
blank input line, instead of `continue`-ing past it.

**Rationale**: In LaTeX, `\\` (ending the previous line) immediately followed by only *one* blank
source line does not render a visible empty line — there is no paragraph content yet to produce
a gap from. It takes a second blank line after the `\\` to actually show one empty output line.
Since this content is plain `\\`-terminated lines (not using `\parskip`/an explicit spacing
command), reproducing "one visually blank line" for one blank input line requires emitting two
blank physical lines in the generated `.tex` source.

**Alternatives considered**:
- *Emit a single blank line per blank input line*: Rejected — verified empirically not to render
  as a visible gap in this context (per the Rationale above); would silently under-render blank
  lines instead of over-rendering them, the opposite of a safe default.
- *Emit an explicit `\vspace{...}` or `\\[<length>]` instead of blank lines*: Rejected — a
  length-based command couples the visual gap to a specific expected line height, is more prone
  to producing a visibly different gap size than a real blank line, and is more complex than
  simply reusing the existing plain-text-line rendering path uniformly.
- *Collapse multiple consecutive blank input lines into a single visual gap*: Rejected —
  unrequested scope; each blank line independently contributes its own two blank output lines,
  keeping the rule simple and its effect linear in the number of blank lines (Constitution
  Principle II, YAGNI).

**2026-10-19 addendum — blank lines superseded**: (a) Blank lines at the start/end of a song's text
are trimmed and never rendered (FR-003). (b) Interior blank lines are now emitted as `\mbox{}\\`
(one entry = one rendered line, so `HeightInLines` is unchanged), replacing the "two blank source
lines" mechanism above. Reason: lyrics are now `\raggedright`, under which a bare `\\` ends the
paragraph and fails with "There's no line here to end"; `\mbox{}` gives the line break something
to end. (c) Planned multi-column max width is lowered to 41 characters (committed constant: 50)
and lyrics are `\raggedright`, so a line LaTeX still has to wrap is never justified/stretched
(FR-004). Alternatives rejected: keep justified text and only shrink width (still stretches any
wrapped line); a `\vspace` per blank line (unchanged rejection above).

## 4. Space-efficient ordering algorithm

**Decision (revised 2026-10-19 — `/speckit-clarify`, direct user requirement)**: First Fit
Descending (FFD), applied page by page. Starting at page 1 of a chapter, with the chapter's songs
as the candidate pool sorted by length descending (equal lengths keep their original relative
order — deterministic tie-break): (1) place the longest song that fits the page; (2) keep placing
the longest remaining song that still fits the page's remaining space until none fits; (3) remove
the placed songs from the pool; (4) repeat for the next page until the pool is empty. "Fits" is
decided by the same `ColumnBalancer.Fill`/`PageMetrics` simulation used to render the chapter, so
FR-025's even-page rule and FR-026's capacity math are respected automatically. A song longer than
a page needs no special case: when nothing fits an empty page it is placed anyway (it is the
longest remaining song), spans as many pages as it needs, and FFD continues filling the space left
on its final page. There is no search bound and no `max-permutations` option.

**Rationale**: Satisfies FR-007 (front-load density; only the final page may be under-full) with a
simple, deterministic, O(n²) algorithm — no factorial blowup, no tunable bound, and no second
fallback algorithm to keep in sync (Constitution Principle II). It is the user's stated choice.

**Alternatives considered (2026-10-19)**:
- *Bounded exact permutation search + greedy fallback* (the prior design) — Rejected by the user:
  two algorithms, a tunable bound, and a fallback that needed repeated correction.
- *Keep `--max-permutations` as an accepted-but-ignored option* — Rejected: dead surface area.

*(Everything below in this section, down to §5, is history. The bounded exact search,
`GreedyFallback` and `--max-permutations` it describes are superseded by the decision above and
are to be removed from `SongOrderer.cs`.)*

**2026-09-27 correction (`/speckit-analyze` finding U1 remediation)**: This decision's own wording
("shortest column/page," a bin-packing heuristic) always described simulating real column-fill
packing — but the shipped `SongOrderer.ComputeWastedSpace`/`GreedyFallback` (T045) never matched
it: they modeled cost as the pairwise line-count difference between *adjacent* songs (`waste +=
abs(Size(i) - Size(i+1))` for `i, i+2, i+4, ...`), an approximation of the old one-song-per-column
row-pairing render model (§5, now also corrected). Now that a column can hold multiple stacked
songs (per the user's 2026-09-27 clarification) and real page capacity is known
(`PageMetrics.LinesPerPage`, FR-026), the cost function is corrected to actually simulate §5's
column-fill algorithm for each candidate permutation and sum the true unused vertical space
across all resulting pages — matching what this section always said, not what was built. See
tasks.md's reworked Phase 17.

**Alternatives considered**:
- *Always brute-force permutations*: Rejected — factorial blowup makes it impractical beyond
  ~8–10 songs, which is well within the realistic leaflet size range (10–50 songs).
- *Always greedy, no exact search*: Rejected — sacrifices the achievable optimum for the small
  leaflets where exact search is cheap and the "most space-efficient" guarantee matters most.
- *Keep the pairwise-adjacent-diff cost function*: Rejected (2026-09-27) — it scores orderings
  against a row-pairing model that no longer reflects how songs are actually packed into columns,
  so it can recommend an ordering that isn't actually the most space-efficient one under real
  column-fill placement.

**2026-09-28 correction (direct user requirement)**: The greedy fallback's "largest-first" sort
(line 159-160 above) is superseded — it abandoned the original (singing) order entirely except as
a tie-break, which structurally stranded every short song at the end of the leaflet, unable to
fill earlier gaps (real-world evidence: `/speckit-analyze` findings I1/I2, `sb/PageMetrics.png`'s
sibling test data — pages ending balanced-but-half-empty while the shortest songs sat unpaired at
the very end). `GreedyFallback` (`sb/Ordering/SongOrderer.cs`) now instead walks songs in
**original order**, simulating one column's remaining capacity at a time: a song that fits is
placed immediately; one that doesn't is left in place in favor of the largest not-yet-placed song
that *does* fit the remaining gap (tie-broken by original position) — using the space rather than
wasting it, per spec.md's revised FR-007/new SC-012, while still tracking singing order as closely
as possible. `ExactSearch` is unaffected — it already favored the original order for free (via
`Permute`'s enumeration order and strict-less-than tie-breaking), so no change was needed there.
`SongOrdererTests`'s greedy-fallback case was rewritten accordingly (`sb/Tests/SongOrdererTests.cs`).

**2026-09-28 second correction (`/speckit-clarify`, real-world evidence against `C:\Test`)**: the
hand-rolled per-column capacity model above was itself superseded — it treated any over-long song
as unconditionally placeable at zero cost, so it never knew about FR-025's even-page forced-break
rule and could still strand an entire column next to a long song (evidence: a 16-song chapter left
a whole page's second column empty because the model didn't foresee that inserting the next
(over-long) song there would trigger a forced break, when a smaller later song could have filled it
first — see the real fixture in `C:\Test`). `GreedyFallback` is now simulation-driven: at each
step, it builds the full hypothetical ordering for every remaining candidate placed next (rest kept
in original order) and picks whichever minimizes `ComputeWastedSpace` (i.e., actually calls
`ColumnBalancer.Fill`), exactly like `ExactSearch` already does — ties still favor the original
order. This closes the FR-025 blind spot for good (any future `Fill` change is automatically
respected, nothing to keep in sync by hand) at the cost of O(n) simulations per placement instead
of a one-shot fit check — acceptable at this project's realistic scale (10-50 songs/chapter).
Verified against the real `C:\Test` fixture: the previously-stranded song now packs into the same
page as its neighbors, and the chapter's page count drops from 6 to 5 content pages.
`SongOrdererTests`'s greedy-fallback case was rewritten again to demonstrate a case where
reordering yields *strictly* less total waste (reusing the same data shape as the `ExactSearch`
test above, forced through `GreedyFallback` via `maxPermutations: 1`), since the earlier
demonstration case turned out to be a waste-neutral tie under real simulation, not a genuine
improvement.

## 5. Multi-column vertical balancing

**Decision (revised 2026-09-27 — see `/speckit-analyze` finding U1 remediation)**: Columns are
filled *sequentially*, not paired one song per column per row: given a chapter's ordered songs,
each column (starting with page 1's left column) is filled top-to-bottom with consecutive songs
— a song's whole rendered block (title, attribution, lyrics) is appended to the current column —
until the *next* song wouldn't fit within `PageMetrics.LinesPerPage` (FR-026), at which point
layout moves to the next column (or, after a page's right column, the next page). A column has
no fixed song-count limit; it holds as many whole songs as fit. After all songs for a given page
have been assigned to that page's two columns, a `\vspace` is appended to the bottom of the
shorter column so both end at approximately the same height (FR-005/SC-003) — this is a purely
cosmetic correction for whatever leftover space naturally exists once no further song would fit,
not a signal that no further song *could* fit there (by definition, the next song already didn't).
Column widths and spacing are unchanged from the original per-song rendering: `sb/Tests/TestFiles/TeX/test.tex`'s
`.475\textwidth` / `.45\textwidth` split with a `\hspace{0.075\textwidth}` gutter (the asymmetry
accounts for the gutter so the row still sums to `1.0\textwidth`).

*(Superseded 2026-09-28 — see spec.md Clarifications, `/speckit-analyze` findings I1/I2/D1: the
"(FR-005/SC-003)" citation above and the "by definition, the next song already didn't [fit]"
assumption are both stale. `SC-003` was removed from spec.md; end-of-column alignment is no
longer required (FR-005). More importantly, that assumption is false for the shipped
`GreedyFallback` heuristic (`SongOrderer.cs`, used for any chapter beyond ~4 songs, since
`5! > 100`): it sorts songs longest-first and packs them in that fixed order, so when a song
doesn't fit a column, only *smaller* songs remain to consider — several of which frequently would
have fit, but are never revisited because `ColumnBalancer.Fill` never looks back at a closed
column. Real-world evidence (`sb/PageMetrics.png`'s sibling test data) showed pages ending
roughly balanced yet leaving ~half the page unused, while several of the shortest songs were
pushed to the very end instead of filling those gaps. FR-007 (revised) and new SC-012 now capture
the actual requirement: prefer pulling a later, smaller-fitting song forward to fill available
column space, except on a chapter's final page. `\vspace` end-balancing remains, but is now a
cosmetic finishing step for whatever residual gap remains *after* that gap-filling, not the
primary mechanism — matching `sb/specification.md` §2.1's 2026-09-28 update. Fixing
`GreedyFallback` itself, e.g. to a best-fit/first-fit-decreasing packing strategy, is a follow-up
implementation task, not yet done.)*

**Rationale**: The user confirmed (responding to finding U1) that a column may hold multiple
songs stacked vertically, with balanced column heights being a *preference* the ordering search
(§4/FR-007) optimizes for, not a hard per-column song-count constraint — sequential column-fill
is the only model that supports this, whereas the original one-song-per-column-per-row pairing
made "more than one song per column" structurally impossible. This also fixes §4's cost function
to measure something real. Still satisfies `sb/specification.md` §2.1 ("Proper vertical alignment
between two or more columns is achieved by adding `\vspace` to the end of the shortest minipage")
and FR-005/SC-003 — that guidance was never actually about one-song-per-column, just about how
the final height difference is corrected. Reusing the exact widths/gutter already present in the
project's own fixture keeps generated output consistent with what's already been hand-verified to
look right.

**2026-09-27 correction**: The original decision above (pairing exactly two songs — one per
column — into each rendered row, matching `RenderRow(Column left, Column? right)`) is
**superseded**. It matched `test.tex`'s minimal two-song fixture's *width/gutter/vspace* values
but not its actual internal structure (see the title/lyrics-separation addendum below) — and was
never actually required by `sb/specification.md` §2.1 or spec.md's FR-005/FR-007, both of which
leave the exact packing granularity as a planning detail (spec.md's Assumptions: "this
specification only requires that multi-column output exists and is vertically balanced").

**Alternatives considered**:
- *LaTeX `multicol` package auto-balancing*: Rejected — `sb/specification.md` explicitly
  specifies the manual `minipage` + `\vspace` approach, and `multicol`'s automatic balancing
  does not give the same column-break control needed for song-boundary-aware layout (in
  particular, FR-025's forced break before an even-page-starting long song).
- *One song per column per row (original design)*: Superseded 2026-09-27 — see above.

**2026-09-27 addendum — title/lyrics separation**: Re-reading `test.tex` closely during
implementation revealed it actually renders each song's title/melody as its own row of
minipage(s), *separate* from a second row containing the lyrics — not one combined per-song
block, which is what every implementation since Phase 3 (including the sequential column-fill
rework above) had rendered. A second, newly-added reference example
(`sb/Kottar i karburatorn.tex`, matching the project notes' "Use example from Kottar i
karburatorn.tex") confirmed the same pattern for a single long song: its title renders alone, in
a minipage of its own column's width, *before* its lyrics begin. **Decision**: whenever a song
starts a fresh column (whether placed whole or as the first chunk of an over-long song), its
title/melody lines render as their own leading block — paired with the sibling column's own
leading title if it *also* starts a fresh song there (matching `test.tex`), or alone, still at its
own column's designated width (not full page width), if the sibling column has no leading title
at that point (matching `Kottar i karburatorn.tex`, e.g. because it's mid-continuation of a
chunked song). A song that stacks into a column after another song keeps its title embedded with
its own lyrics — there is no cross-column alignment concern to solve there, since it isn't
starting anything a sibling column needs to stay in sync with. This is a correction to
already-shipped behavior (Phase 3 onward), found via evidence already in the repo, not new scope
introduced by this feature. See data-model.md's `SongBlock.TitleLineCount` and
`Column.LeadingTitleLineCount`.

**2026-09-27 addendum 2 — two real-world rendering bugs found and fixed**: manual testing (real
Swedish-language fixtures, a long song correctly overflowing into a second column and a following
one correctly continuing onto the next page) surfaced two defects in the title/lyrics-separation
work above, both confirmed via a minimal LaTeX reproduction compiled with the real template:
1. A lone title on the *right* column rendered with no `\hspace` offset, so it sat flush against
   the page's left margin instead of under the right column — visually indistinguishable from a
   stray/misplaced line. Fixed: `ColumnBalancer.RenderLoneMinipage` now takes an optional
   preceding-offset width, supplied whenever the lone box is the right column.
2. `\paragraph*{...}` (as `titlesec` implements it) silently produces **no visible output at all**
   when nothing follows it in the same paragraph — which is exactly what happens whenever a
   title has no melody line, since it then has nothing else in its own extracted box. This is a
   LaTeX-level footgun, not a logic bug: the generated source was structurally correct, but the
   title text simply never appeared in the compiled PDF. Fixed: every extracted title (lone or
   paired) gets a trailing `\mbox{}` — a harmless no-op when a melody line already follows, but
   necessary when it doesn't. Both were verified fixed by recompiling the exact real-world
   scenario that surfaced them (`xelatex`, zero errors/overfull-vbox warnings, both previously-
   missing titles now present in the output).

Neither fix changes pagination decisions (`Fill`'s packing logic, already covered by
`ColumnBalancerTests`) — both are purely about `Render` producing LaTeX that actually displays
what the (already-correct) `PageLayout` data says it should.

*(Superseded 2026-09-28 — see `sb/Leaflets/ColumnBalancer.cs`'s own doc comment and
`contracts/latex-output-contract.md`'s revised "Title/lyrics rendering" bullet: the title/lyrics
separation described in both addenda above was implemented, then **reverted** by direct
instruction — a song's title/melody/lyrics render together as one unbroken block again, with no
separate leading-title row and no cross-column title pairing. Neither `SongBlock.TitleLineCount`
nor `Column.LeadingTitleLineCount` exists on the shipped types; both addenda above, and
data-model.md's corresponding field entries, describe a design no longer in the codebase. This
revert itself was never recorded anywhere at the time it happened — no Clarifications entry, no
plan.md Complexity Tracking note, no tasks.md phase — so there is no first-hand record of *why*;
this note only documents *that* it happened, confirmed directly against the shipped source.)*

## 6. LaTeX special-character escaping

**Decision**: A small static lookup/replacement covering LaTeX's reserved characters
(`\ { } % & # _ ~ ^`) applied to all user-supplied text (song titles, lyrics, writer/melody
names) immediately before it is embedded in generated LaTeX output.

**Rationale**: Directly satisfies FR-010; a small, well-tested static mapping is simple, has no
dependency, and is easy to verify exhaustively with unit tests.

**Alternatives considered**:
- *Third-party LaTeX-escaping package*: Rejected — no such .NET package is in common use for
  this; the escape set is small and stable enough to own directly (Principle II).

**2026-09-26 addendum — `Unescape`**: Since a document's title/subtitle now live only inside its
rendered `<name>.tex` (§9's 2026-09-26 update), recovering them requires reversing `Escape` exactly.
Added `LatexEscaper.Unescape`, a regex-alternation replace over the same nine mappings in reverse.
Safe because the nine escaped forms are mutually distinguishable by construction (none is a
prefix of another in a way that creates ambiguity at a shared start position — e.g.
`\textbackslash{}` and `\textasciitilde{}`/`\textasciicircum{}` diverge after `\t`, and `\{`/`\}`
never appear as a sub-sequence of the longer `\text...{}` forms), so a single pass correctly
reconstructs the original raw text for any string produced by `Escape`.

## 7. Testing strategy

**Decision**: NUnit unit tests per component (line wrapper, escaper, orderer, column balancer),
each exercised against both synthetic edge-case inputs (empty text, special characters, empty
song list) and the real fixture files already present under `Tests/TestFiles/`. One end-to-end
integration test builds a small leaflet from 2–3 fixture songs and asserts on the resulting
document's structural invariants (contains expected `minipage`/`\vspace` markers, each song's
assigned singing-order number displayed correctly, no leftover special characters).

**Rationale**: Matches Constitution Principles III (Test-First) and IV (Comprehensive Coverage);
using real fixtures instead of only synthetic ones guards against line-wrapping/formatting
regressions that only show up on real song text.

**Alternatives considered**:
- *Synthetic-only test data*: Rejected — the existing real-song fixtures are already present and
  specifically valuable for catching line-wrapping edge cases synthetic data would miss.

## 8. CLI command surface

**Decision**: Wire `System.CommandLine` subcommands directly against `sb/specification.md`
§2.6's shape, scoped to what spec.md's functional requirements actually need. As of 2026-09-26
(a further Clarification; §2.6 already leaves "whether something should be a flag or an argument"
to the implementer), every previously-required `--flag` became a positional argument, and every
remaining (optional) flag gained a single-letter alias:
- `sb documents create <name> <title> [-p|--path] [-s|--subtitle]`,
  `list [-c|--chapters] [-s|--songs]`, `delete <name>`, `rename <name> <new-name>`,
  `build <document-name> [ordering] [-m|--max-permutations] [-f|--format]`. (`--format` moved from
  `create` to `build` 2026-09-26 — see §9's update: a document has no persisted default to
  override, so it's supplied fresh on every build instead. `ordering` is a further 2026-09-26
  Clarification: an *optional positional argument*, defaulting to `as-listed` — the sole exception
  to "required flags become arguments, optional flags stay options," made because the user asked
  for it specifically; it's the last positional in `build`, so making it optional doesn't create
  any parsing ambiguity with `document-name` before it.)
- `sb chapters add <document-name> [-n|--name] [-t|--title]`,
  `remove <document-name> <names...>` (`names` is variadic — one or more space-separated tokens,
  not comma-separated — and must be the last positional argument), `list <document-name>
  [-s|--songs]`.
- `sb songs add <document-name> <chapter-name> <title> <text> <singing-number> [-w|--writer]
  [-m|--melody]`, `remove <document-name> <song-name>`, `list <document-name>
  [-c|--chapter-name]`, `modify <document-name> <song-name> [-t|--title] [-w|--writer]
  [-m|--melody] [-c|--chapter] [-s|--singing-number]`. `text` accepts either literal text or a
  path to an existing file (its contents are read and used instead — a 2026-09-26 Clarification,
  resolved in `SongCommands.Add` via `File.Exists`, so `SongStore`/`Song` never see a file path,
  only resolved text).
- Config's `sb config set <key> <value>` was removed 2026-09-26 — see §11.

Aliases are scoped per-command in `System.CommandLine`, so the same letter (e.g. `-m`) safely
means different things in different commands (`--melody` in `songs add`/`modify`,
`--max-permutations` in `documents build`) — no cross-command collision is possible since each
`Command` has its own independent `Options` collection.

Two things from §2.6 are explicitly **deferred**, not built now:
- **CWD-based name inference** ("CLI arguments like 'document name'... are inferred if working
  directory is a document/chapter directory") — a UX convenience with no functional requirement
  behind it; explicit `document-name`/`chapter-name` arguments satisfy every FR. Adding inference
  now would be speculative generality (Constitution Principle II).
- **`scores`** (attaching image/score files to songs, §2.6.4) — no functional requirement in
  spec.md covers images at all; out of scope entirely for this feature.

**Rationale**: `sb/specification.md` is the project's own authoritative CLI design; reusing its
command/flag shape avoids inventing a second, divergent interface. Scoping strictly to what
spec.md's FRs require (and explicitly deferring the rest) keeps the surface small and testable
per Constitution Principle II.

**Alternatives considered**:
- *Build the full §2.6 surface including CWD inference and `scores` now*: Rejected — neither is
  backed by a functional requirement in this spec; building them now is unrequested scope that
  would need its own tests and maintenance with no corresponding acceptance criteria to verify
  against.
- *Design a new, different CLI shape*: Rejected — `sb/specification.md` already defines one, and
  the user has directed planning to use it.
- *A separate `--text-file` flag instead of overloading `--text`*: Rejected as of 2026-09-26 — the
  user asked for the existing `--text` flag itself to accept files, not a second flag; a plain
  `File.Exists` check is unambiguous for this CLI's realistic inputs (song lyrics are never
  themselves valid filesystem paths) and needs no new flag or `@file`-style prefix convention.
- *Keep every currently-required value as a `--flag`*: Rejected as of a further 2026-09-26
  Clarification — the user asked specifically for required flags to become positional arguments,
  matching common CLI convention (e.g. `git commit -m <msg> <file>`, not
  `--file <file> --message <msg>`) and shortening the common case.
- *Long-only flags for the remaining optional options*: Rejected — the user asked for a
  single-letter alias per remaining option; `System.CommandLine`'s `Option<T>(name, params
  aliases)` constructor supports this directly with no extra plumbing.
- *One global short-alias namespace shared across commands*: Rejected — unnecessary and
  impossible to satisfy cleanly anyway (e.g. both `songs add`'s `--melody` and `documents build`'s
  `--max-permutations` want `-m`); `System.CommandLine` scopes an `Option`'s aliases to the
  `Command` it's added to, so each command gets its own independent, locally-unambiguous set of
  single-letter aliases instead.
- *Keep `--ordering`/`-o` as an option (matching the general "optional stays an option" rule)*:
  Rejected as of a further 2026-09-26 Clarification — the user specifically asked for `ordering`
  to become an optional positional argument; `System.CommandLine`'s `Argument<T>` supports this
  directly via `DefaultValueFactory` (or `Arity = ArgumentArity.ZeroOrOne`), and since `ordering`
  is the last positional argument in `build`, making it optional creates no ambiguity with
  `document-name` before it.

## 9. Storage layout and format

**Decision** *(superseded — see §13; song file shape superseded 2026-09-26, document storage
superseded again 2026-09-26, see below)*: Content is filesystem-backed, one directory per
document:
```text
<document-path>/                  # documents.path in the SQLite index (§13) — user-chosen, not a fixed convention
├── <Name>.tex                    # rendered from sb/template-main.tex immediately at `create` time, named after --name/documents.filename (see 2026-09-26 updates below)
└── Chapters/                     # created empty at `create` time too
    └── <chapter-name>/            # <chapter-name> = chapters.name in the index
        ├── chapter.tex            # \chapter*{<title-or-name>} + generated per-song content, \input'd from <Name>.tex — absent if the chapter has zero songs (§2's 2026-09-26 update)
        └── <song-name>.yml        # title, writer?, melody?, number, text (lyric lines), score? (see below)
```
A document with zero chapters is a folder containing `<Name>.tex` and an empty `Chapters/` —
consistent with FR-013 (a document may exist with zero chapters/songs).

**2026-09-26 update (document storage)**: A document has no metadata file of its own — no
`document.yml` (an earlier, undocumented deviation the running code had taken from this very
design). Its `.tex` file is written by `DocumentStore.Create` immediately, not deferred to
`build`: `DocumentTemplate.Render` substitutes the (escaped) title/subtitle into the file's
`{title}`/`{subtitle}` tokens with an empty chapter-content region, and an empty `Chapters/`
folder is created in the same call. Every later read of a document's title/subtitle (`list`,
`find`, and the `Document` `build` reloads to construct a `LeafletRequest` from) re-parses them
out of whatever the file currently contains, via `DocumentTemplate.ParseTitleAndSubtitle`
reversing the same escaping (`Formatting.LatexEscaper.Unescape`, new alongside `Escape`) —
`documents build` overwrites it with the full leaflet content but re-substitutes the same
title/subtitle tokens each time, so this keeps working after a build too.

**2026-09-26 update (filename)**: The file is named `<Name>.tex` — `Name` being
`DocumentIndexEntry.Filename`/`--name` — not a fixed `main.tex`, per a further 2026-09-26
Clarification. `DocumentStore.Rename` renames the file alongside `documents.filename` so the two
never drift; `DocumentCommands.Build`'s output path is likewise computed from `document.Name`,
not a hardcoded literal.

**2026-09-26 update**: `sb/specification.md` §2.5.2 now stores each song as a **single** `.yml`
file (no separate `.txt`), shaped as:
```yaml
title: {--title}
writer: {--writer}
melody: {--melody}
number: {--singing-number}
text:
  - {line 1}
  - ...
  - {line N}
score:            # Optional
  - {score_filename}
```
`text` (a list of lyric lines) replaces the old `<song-name>.txt`; `number` (the singing-order
number) is now part of the song's own file — see the revised §12 below for how this interacts
with the index. `score` is a new optional field (a list of score/image filenames attached to the
song, per `sb/specification.md`'s `scores` CLI section) with no behavior specified yet beyond
being stored and round-tripped — no functional requirement currently reads or displays it.

**Rationale**: Directly matches the current `sb/specification.md` §2.5 (a single `.yml` per song,
per §2.5.2's literal schema). The earlier `.txt` + `.yml` split matched an *older* revision of
that same source-of-truth document; this supersedes it now that the document itself changed.

**Alternatives considered**:
- *Keep the split `.txt` (lyrics) + `.yml` (metadata) pair*: Rejected as of 2026-09-26 —
  `sb/specification.md` §2.5.2 now explicitly gives a single combined `.yml` schema including
  `text` and `number`; keeping the split would diverge from the project's own documented format
  for no stated benefit.
- *A fixed `<output>/` root all documents live under, discoverable by directory scanning*:
  **Rejected as of the 2026-09-11 clarification** — the user explicitly stated documents may be
  saved at arbitrary, user-chosen locations, which directory scanning under one root cannot
  support. Superseded by the SQLite index in §13, which records each document's actual path.
- *Keep a `document.yml`, just document it properly this time*: Rejected as of the 2026-09-26
  `/speckit-clarify` — the user was explicit that a document "should not be a .yml-file," matching
  `sb/specification.md` §2.5.1 ("Documents and chapters are stored as .tex-files") — which the
  running code had silently deviated from.
- *Defer `main.tex` generation to `build` time, as the prior (undocumented) implementation did*:
  Rejected — the user asked for it at `create` time; this also removes the awkward "what is
  `Document.Title` before the first build?" question a deferred-generation design would otherwise
  need `document.yml` (or something like it) to answer.
- *Add a `format` column to the `documents` index table to keep a persisted per-document default*:
  Rejected — the user chose to move `--format` to `build` instead (spec.md's 2026-09-26
  Clarification); this also avoids extending the index schema beyond what was explicitly
  specified (FR-019).
- *Keep the fixed filename `main.tex` regardless of `--name`*: Rejected as of a further 2026-09-26
  `/speckit-clarify` — the user asked for `<name>.tex` explicitly; `DocumentStore.Rename` renaming
  the file alongside `documents.filename` is the direct, only-reasonable consequence of that (a
  stale filename would otherwise silently break the next `list`/`find`/`build`).

## 10. YAML library choice

**Decision**: Use `YamlDotNet` for reading/writing each song's single `.yml` file (originally
also `config.yml`, before Config was removed 2026-09-26 — see §11).

**Rationale**: It's the de facto standard .NET YAML library (actively maintained, widely used),
and hand-rolling a YAML parser/serializer would be exactly the kind of non-trivial logic
Constitution Principle II says a dependency may legitimately replace.

**Alternatives considered**:
- *Hand-rolled YAML parsing*: Rejected — YAML's indentation/quoting/type rules are non-trivial to
  get right; this is precisely the case the constitution carves out for adding a dependency.
- *Switch song metadata format to JSON*: Rejected — `sb/specification.md` explicitly
  specifies `.yml` for song metadata; changing format would diverge from the
  project's own documented design for no stated benefit.

## 11. Config loading and validation

**Status: REMOVED (2026-09-26)**. `sb/specification.md` no longer has a §2.4/Config section as
of the 2026-09-26 update — the global `config.yml`/`AppConfig`/`sb config set` concept is out of
scope for this feature entirely (see spec.md's 2026-09-26 Clarification and its updated
Assumptions). This section is retained only for history; do not implement against it.

**Original decision (superseded)**: `ConfigLoader` read `config.yml` once at CLI startup,
validated known fields (LaTeX constants/preamble shorthand per the old §2.4) against expected
types/value shapes, and failed fast with a descriptive error on an invalid field. Valid config
was exposed as an in-memory `AppConfig` used wherever `template-main.tex` substitution needed a
config-sourced value (e.g., font size, margins — marked `%from config.yml` in the template's
comments at the time).

**Consequence for `template-main.tex`**: any `%from config.yml` substitution comments in the
template must be resolved some other way (a literal default, or a future non-config mechanism) —
that is an implementation concern for the tasks that remove Config, not a new requirement this
spec introduces.

## 12. Singing-order number storage and uniqueness

**Decision** *(superseded again 2026-09-26 — see below)*: ~~The singing-order number is a
required integer field, stored only as `songs.singing_order_number` in the SQLite index (§13),
not in each song's `.yml` file.~~

**2026-09-26 update**: Per `sb/specification.md` §2.5.2, the singing-order number (`number`) is
now a field in the song's own `.yml` file — that file is the **source of truth** for it (per
spec.md's 2026-09-26 Clarification). The index's `songs.singing_order_number` column (§13) is
kept as a **fast-lookup mirror**, written whenever `SongStore` writes the `.yml` file, so the
existing uniqueness check (a single indexed SQL query joining `chapters.document_id`, FR-016)
does not need to open and parse every song's `.yml` file in the document. `LocationMismatchDetector`
-style drift detection (FR-023's pattern) applies here too: if the index's mirrored number and
the `.yml` file's `number` ever disagree (e.g., the `.yml` was hand-edited outside SB), that is a
mismatch to surface to the user, not silently resolve in either direction.

**Rationale**: Matches the current `sb/specification.md` §2.5.2/§2.5.3 exactly (the song's own
file holds `number`; "the database should be the source of truth for fast lookup" — a lookup
optimization, not a claim that the index outranks the file's own content). Still satisfies
FR-016/FR-017/FR-021 (the index continues to record the number, per FR-021's literal wording) and
still keeps the number out of the physical-layout code path (`Ordering/`/`Leaflets/` read it, but
only `SongStore` ever writes it) — FR-009 is unaffected.

**Alternatives considered**:
- *Keep the number index-only, not in the `.yml` file (the pre-2026-09-26 decision above)*:
  Rejected as of 2026-09-26 — `sb/specification.md` §2.5.2 now explicitly includes `number` in
  the song's own file; keeping it index-only would diverge from the documented schema.
  Two copies of the same value does reintroduce a drift question, but that is exactly the kind of
  index-vs-reality mismatch FR-023 already establishes a pattern for — no new mechanism is
  needed, just applying the existing one to a second kind of drift (content, not just location).
- *Auto-assign the next free number on conflict*: Rejected — spec.md (FR-016) requires rejecting
  the conflicting assignment with an explanation, not silently renumbering, since the number is a
  user decision (Assumptions in spec.md).

## 13. SQLite index: driver, schema, and mismatch detection

**Decision**: Use `Microsoft.Data.Sqlite` directly (no ORM) against one SQLite database file,
with a schema matching exactly what the 2026-09-11 clarification specified:

```sql
CREATE TABLE documents (
    id       INTEGER PRIMARY KEY,
    path     TEXT NOT NULL UNIQUE,   -- FR-019: filesystem path to the document's main folder
    filename TEXT NOT NULL           -- FR-019: derived from the folder name at create time
);

CREATE TABLE chapters (
    id          INTEGER PRIMARY KEY,
    name        TEXT NOT NULL,       -- FR-020
    document_id INTEGER NOT NULL REFERENCES documents(id),
    UNIQUE (document_id, name)
);

CREATE TABLE songs (
    id                   INTEGER PRIMARY KEY,
    name                 TEXT NOT NULL,               -- FR-021
    singing_order_number INTEGER NOT NULL,             -- FR-021, §12
    chapter_id           INTEGER NOT NULL REFERENCES chapters(id)
);
-- Uniqueness for singing_order_number is enforced per-document (FR-016), not per-chapter,
-- so it is checked via a query joining songs -> chapters -> documents rather than a UNIQUE
-- constraint on (chapter_id, singing_order_number), which would be the wrong scope.
```

Chapters and songs have **no stored path of their own** — their location is always computed as
`<parent document's path>/Chapters/<chapter.name>/` and
`<...>/<song.name>.txt`/`.yml`, exactly matching the schema given in the clarification (only
`documents` carries a `path`). `LocationMismatchDetector` (FR-023) walks the index and, for each
document/chapter/song, checks the corresponding computed filesystem path exists; any that don't
are reported to the user as a list of mismatches (document/chapter/song identifier + expected
path), surfaced by the relevant CLI command (e.g., `documents list`, `documents build`) rather
than a silent fix-up.

**Rationale**: `Microsoft.Data.Sqlite` is the standard, actively-maintained low-level .NET SQLite
driver; an ORM (EF Core, Dapper, etc.) would add a second dependency and abstraction layer for
three simple tables with straightforward CRUD — unjustified under Constitution Principle II. The
schema mirrors the clarification's exact field list rather than adding speculative columns
(e.g., no `title`/`subtitle` duplicated into the index — those stay in `Document`/on-disk
sources, since the index's stated job is identity + location + singing-order number, not a full
content mirror, per spec.md's `Index` Key Entity). Note (2026-09-26, see §12): the index's
`singing_order_number` column is now a *mirror* of the song's own `.yml` file rather than that
value's sole home, but the schema itself is unchanged — only which side is authoritative moved.

**Alternatives considered**:
- *Entity Framework Core*: Rejected — meaningful overhead (migrations, change tracking, a second
  learning surface) for three tables with no complex relationships or queries beyond
  lookup-by-id and one uniqueness check.
- *Store a path per chapter/song too*: Rejected — the clarification's given schema only puts
  `path` on `documents`; deriving chapter/song locations from the parent document's path keeps
  the schema minimal and matches exactly what was specified, and there's no stated need for a
  chapter or song to live somewhere other than under its document.
- *Silent auto-repair of the index on mismatch (e.g., re-point `path` to wherever the folder
  seems to have moved)*: Rejected — FR-023 explicitly calls for informing the user, not
  guessing/auto-fixing; a moved folder is exactly the ambiguous case a human should confirm.

## 14. Deriving page/column capacity from `template-main.tex` (FR-026)

**Decision**: Add a small `PageMetricsReader` that parses `sb/template-main.tex` with targeted,
single-purpose regexes (not a general LaTeX parser) for:
- The `geometry` package's options — `tmargin`/`bmargin` (falling back to the single `margin` key
  for both when either is absent), in whatever unit is given (currently `in`).
- The top-level `\fontsize{<size>}{<baselineskip>}` command's second argument, in `pt`.

Combined with a fixed A4 paper height (297mm converted via `1in = 72.27pt = 25.4mm` ≈ 845.05pt) —
`template-main.tex`'s `geometry`
package always specifies the `a4paper` option (added 2026-09-27, per the user's explicit
statement that paper size will always be A4) — it computes:

```text
UsableHeightPt = PaperHeightPt - TopMarginPt - BottomMarginPt
LinesPerPage   = floor(UsableHeightPt / BaselineSkipPt)
```

using a small unit-conversion constant (`1in = 72.27pt`, TeX's true point) for the margin values
(currently given in `in`) — only the units actually present in the template (`in`, `pt`) are
supported; a future template introducing a new margin unit (`cm`/`mm`) would extend this table,
not require it upfront (Constitution Principle II, YAGNI). The paper height itself is not parsed
from the template at all — since the project always uses `a4paper` (confirmed 2026-09-27), it is
a fixed constant (≈845.05pt), not a general "detect whichever named paper size `geometry` was
given" lookup (Principle II: build the one case that's actually used, not a speculative general
mapping of every `geometry` paper-size keyword). `LinesPerPage` becomes the capacity
`LeafletBuilder`/`ColumnBalancer` compare a song's `HeightInLines` (§5) against, instead of
assuming unlimited space — the root cause of the clipping bug the project's working notes
describe ("the tool currently does not understand how much space is on a page, so long songs are
clipped").

**Rationale**: Directly implements FR-026 and matches the project notes' own framing — "Margins
are found in template-main.tex, as well as fontsize and spacing" — treating the template as the
source of truth for the margin/fontsize values that can plausibly change, while paper size is
fixed at A4 per explicit user confirmation rather than something the tool needs to detect.

**Alternatives considered**:
- *Hardcoded `LinesPerPage` constant in code*: Rejected by the 2026-09-27 clarification (FR-026)
  — would silently go stale the moment `template-main.tex`'s margins/fontsize change.
- *Parsing/detecting the `geometry` paper-size keyword generically (`a4paper`, `letterpaper`,
  etc.)*: Rejected — the user has confirmed paper size will always be A4; building a general
  paper-size lookup table would be speculative generality with no second case to justify it
  (Principle II, YAGNI). If a non-A4 template is ever needed, this constant becomes a parsed value
  at that point, not before.
- *Full LaTeX layout engine / invoking `pdflatex` to measure real box heights*: Rejected — far too
  heavy a dependency for this project (Constitution Principle II); the estimate only needs to be
  good enough to decide *when* FR-025's placement rule applies, not to hand-place every glyph —
  the existing `\vspace`-based balancing (§5) already treats "within one line's height" as its
  accuracy bar. *(SC-003 citation superseded 2026-09-28 — SC-003 was removed; see §5's superseded
  note and spec.md's new SC-012.)*

## 15. Long-song pagination across columns/pages (FR-025)

**Decision (revised 2026-09-27 — see `/speckit-analyze` finding U1 remediation)**: `LeafletBuilder`
tracks a running physical page counter as it lays out a chapter's songs — starting at 1 for the
document's first content page, and advancing to the next page number whenever a chapter boundary
is crossed (matching `\chapter`'s own unconditional page break in this `oneside` template —
§2/§9). Within §5's general sequential column-fill algorithm, a song whose `HeightInLines` exceeds
`PageMetrics.LinesPerPage` (§14) is simply a song too big to fit in the *current* column — the
column-fill loop's normal "doesn't fit, move to next column/page" behavior already handles this:
the song's content is chunked into consecutive pieces of at most `LinesPerPage` lines, occupying
the rest of the current column and then as many further whole columns/pages as needed, exactly
like any other song, until exhausted — at which point column-filling resumes normally with
whatever song comes next (which MAY land in the same column, right after the long song's last
chunk, if space remains). This is the concrete meaning of "split into two columns" from the
2026-09-27 clarification: ordinary code-driven column flow (§5), not a `multicol`-package
environment. FR-025 layers exactly one *additional* constraint on top of this general behavior,
based on the parity of the page such a song starts on:
- **EVEN page**: the song MUST be the page's first song — if a preceding song's column-fill would
  otherwise leave it starting mid-page (i.e., not at the top of the left column), a page break is
  forced before it instead. This constrains only where it *starts*; once its content is exhausted,
  column-filling with subsequent songs resumes normally on whatever column/page it left off at
  (2026-09-27 clarification — a page is never reserved exclusively for one song).
- **ODD page**: no such placement constraint — the song may start mid-column-fill (e.g., right
  after a preceding song in the same column), and its overflow continues via the same chunking
  mechanism.

**Rationale**: Directly implements FR-025 as one placement rule layered on top of §5's general
column-fill algorithm, rather than a separate rendering path — a long song is not a special case
needing its own mechanism, just the general algorithm's normal "doesn't fit, continue elsewhere"
behavior, with one added constraint on where it may start. No new LaTeX construct, no new
dependency (Constitution Principle II).

**Alternatives considered**:
- *`\begin{multicols}{2}...\end{multicols}` for over-long songs only*: Rejected — reintroduces the
  exact LaTeX-native auto-balancing mechanism §5 already rejected project-wide, and would make one
  song's rendering structurally inconsistent with every other song's `minipage` rendering.
- *Manually inserting raw `\newpage`/page-break commands and trusting LaTeX to reflow the rest*:
  Rejected — the project already computes line-based heights itself for `\vspace` balancing (§5);
  computing chunk boundaries from `PageMetrics.LinesPerPage` in C# is the same technique applied
  one level up, not a new one.
- *Treating a long song as exclusively occupying every page it touches (original 2026-09-27
  design)*: Superseded — the user clarified a page/column is never reserved for one song; once a
  long song's content is exhausted, remaining space is fair game for the next song, same as
  anywhere else in the column-fill sequence.

## 16. `as-listed` ordering vs. layout-forced page/column breaks (FR-006)

**Decision**: `SongOrderer.Order` (§4) is unchanged — it only ever determines *which song comes
before which*, never where a page/column break falls. Break placement (including FR-025's forced
break before an even-page-starting long song) is decided entirely downstream, in
`LeafletBuilder`/`ColumnBalancer`, after ordering has already been applied.

**Rationale**: Directly implements the 2026-09-27 clarification's resolution of FR-006 —
`as-listed` guarantees relative song sequence only. Keeping ordering and break-placement as
separate concerns (rather than teaching `SongOrderer` about page capacity) avoids a new coupling
between two previously independent modules (Constitution Principle II).

**Alternatives considered**:
- *`SongOrderer` reorders songs itself to avoid forced breaks*: Rejected by the 2026-09-27
  clarification (Option A) — `as-listed`'s relative song order must never change, only break
  placement may adjust.

All `NEEDS CLARIFICATION` items from Technical Context are resolved above.

## 14a. Capacity corrections after real-world evidence (2026-10-19, `C:\Test`)

Compiling a real chapter showed First Fit Descending looking erratic because its inputs were wrong,
not the algorithm. Three fixes:

1. **`\fontsize` was never applied.** `\fontsize{12pt}{14.4pt}` alone does nothing, and a bare
   `\selectfont` in the preamble is undone by `\begin{document}` (LaTeX resets to `\normalsize`).
   The template now redefines `\normalsize` itself as `\fontsize{12pt}{14.4pt}\selectfont` (an
   `\AtBeginDocument` hook sized the body but left `\paragraph*` titles, which use `\normalsize`,
   at 10pt); measured on a compiled page the line pitch is 14.3pt, matching
   `PageMetrics.BaselineSkipPt`. `PageMetricsReader`'s
   `\fontsize` regex is unchanged.
2. **A blank input line is one rendered line, not two.** It still needs two blank *source* lines
   after `\\`, but is now a single `SongBlock` entry (`"\n"`), so `HeightInLines` and
   `SongOrderer.EstimateHeightInLines` count it once. Generated `.tex` text is unchanged.
3. **The chapter heading takes room on page 1.** Chapters now use `\chapter*{...}` (no "Chapter N"
   line, no TOC entry). The heading's height (≈9.4 lines; declared as 10) is read from the
   template's `% sb:chapter-heading-lines=N` comment and reserved on the chapter's first page by
   `ColumnBalancer.Fill(..., firstPageReservedLines)`; `SongOrderer` simulates with the same
   reservation. A song that fits a full column but not the heading-reduced one moves to the next
   column unless its column is still empty, in which case it is split like any over-long song.

## 17. Song-title gap (FR-027) and per-chapter title spacing

**Decision (2026-10-19)**: Exactly one blank line above every song title is emitted explicitly,
not via `\titlespacing`. `SongBlock.LeadingBlankLines` (= 1) is stored outside `Lines`;
`ColumnBalancer.Fill` adds it to a song's height only when the song is not first in its column, and
`Render` emits `\mbox{}\` for it only then. Every chapter file begins with
`\titlespacing*{\paragraph}{0pt}{0pt}{0pt}` so a main `.tex` from an older template (which still
carries its own `\titlespacing`) cannot add a second gap or push content onto an empty page.
Existing documents are not migrated; chapter files are regenerated on every build anyway.

**Rationale**: `\titlespacing` is unreliable inside column minipages; an explicit line is countable
by FR-026's capacity math. Column-leading titles skip the line so column tops stay at the
template's top margin (FR-005). Zeroing inside the chapter file removes dependence on template
version.

**Alternatives considered**: rewriting old main files at build time (rejected — each chapter file
sets it itself); `\vspace{\baselineskip}` for the gap (rejected — `\mbox{}\` already matches the
blank-lyric mechanism and counts as exactly one line).
