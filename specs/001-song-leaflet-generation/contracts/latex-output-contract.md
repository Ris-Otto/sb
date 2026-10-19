# Contract: Generated LaTeX Output

This defines the structural guarantees the generated `Leaflet.Document`/`Leaflet.ChapterFiles`
strings MUST satisfy, independent of exact formatting/whitespace, so tests can assert on
structure rather than brittle full-text matches. Concrete syntax elements referenced below are
fixed by the existing project assets `sb/template-main.tex` and `sb/Tests/TestFiles/TeX/test.tex`.

As of 2026-09-26 (spec.md's Clarification correcting an implementation gap — `sb/specification.md`
§2.5.1/§4), song content lives in per-chapter `chapter.tex` fragments (`Leaflet.ChapterFiles`),
never directly in the main document (`Leaflet.Document`).

## Main document assembly

- `Leaflet.Document` is `sb/template-main.tex` with:
  - its `titlepage` block's `{title}`/`{subtitle}` tokens replaced by the (escaped) requested
    leaflet title/subtitle (empty string when no subtitle; see `data-model.md`'s
    `LeafletRequest`), and
  - its `%Chapters come here` insertion comment replaced by one
    `\input{Chapters/<chapter-name>/chapter.tex}` line per entry in `Leaflet.ChapterFiles`, in
    that order — and nothing else. `Leaflet.Document` never contains a song's title, lyrics,
    writer, or melody directly.
- All other parts of `template-main.tex` (document class, package preamble, margins, font size,
  page-numbering setup) pass through unchanged — this feature does not alter them.

## Chapter file assembly (`Leaflet.ChapterFiles`)

- One entry per chapter that has at least one song — a chapter with zero songs produces no entry
  at all (no heading, no corresponding `\input` line in `Leaflet.Document`).
- Each entry's content begins with `\titlespacing*{\paragraph}{0pt}{0pt}{0pt}` (FR-027; overrides
  any older main-document `\titlespacing`), then `\chapter*{<heading>}` (escaped), where
  `<heading>` is the chapter's own `Title` when present, otherwise its `Name`.
- Followed by that chapter's own song blocks only — the single-/multi-column guarantees below
  apply *within* one `ChapterFiles` entry; a song block never appears in more than one entry, and
  column-filling (2026-09-27: sequential, not paired — see below) never stacks two songs from
  *different* chapters into the same column (a `\chapter` heading forces a physical break
  regardless of layout, so cross-chapter column-sharing would be structurally broken).

## Single-column format (within one chapter file)

- The chapter's content contains one song block per its own input song, in that chapter's
  `Leaflet.Songs` order.
- Each song block includes, in order: the song's `SingingOrderNumber` and `Title` via
  `\paragraph*{...}` (escaped per `research.md` §6) — never `PlacedSong.PhysicalPosition`, which
  is an internal layout detail not shown to the user — then, if present, `\textit{Mel:
  <melody>}\\` attribution, then its lyric lines — each of the song's own input lines becomes one
  output line ending in `\\` as-is (2026-09-26: phrase-splitting removed, FR-003), wrapped further
  only per the next section.
- A **blank** input line between text lines (e.g. a stanza separator) becomes one `\mbox{}\\`
  output line (2026-10-19; supersedes the earlier two-blank-source-lines rule, research.md §3).
  Blank lines at the very start or end of a song's text are dropped (FR-003). A bare `\\` line
  never appears (invalid under `\raggedright`).
- Lyrics are set `\raggedright`; the planned max line width in multi-column layout is 41
  characters (FR-004). `\raggedright` is set *inside* each minipage and inside a
  `\begingroup…\par\endgroup` for the single-column body and for a full-width lone-column page —
  never at chapter-file level, where it leaks into the paragraphs holding the minipage rows and
  makes compiled pages overflow (verified against a real LuaLaTeX compile, 2026-10-19).
- A song's title is `\paragraph*{<n>. <title>}\mbox{}\\` — the line break sits *outside* the title
  argument, because `titlesec` sets a run-in title in an `\sbox` (LR mode), where `\\` raises
  "Not allowed in LR mode" under `\raggedright`.
- Exactly one blank line (`\mbox{}\\`) precedes every song title except one that is first in its
  column (FR-027).
- No LaTeX-reserved character from a `Song`'s `Title`, `Text`, `Writer`, or `Melody` appears
  unescaped in the output.

## Multi-column format (within one chapter file)

- All single-column guarantees above still hold per song's own content.
- Song content *from that same chapter* is packed into `\noindent` +
  `\begin{minipage}{<width>\textwidth}...\end{minipage}` environments representing columns,
  matching the two-column `.475\textwidth` / `.45\textwidth` split with a
  `\hspace{0.075\textwidth}` gutter shown in `sb/Tests/TestFiles/TeX/test.tex`. Columns are filled
  *sequentially* (2026-09-27 — `/speckit-analyze` finding U1 remediation): each column holds as
  many consecutive whole songs as fit within one page's line capacity (`data-model.md`'s
  `PageMetrics.LinesPerPage`, FR-026), not exactly one song per column — a column may contain
  multiple songs stacked, or, for one over-long song, only part of one. Every column on a given
  page has a closing `\vspace` command sized so that all columns on that page end within one text
  line's height of each other — this corrects whatever leftover space naturally remains once no
  further song would fit, it does not imply a column was capped at one song. *(SC-003 citation
  superseded 2026-09-28 — SC-003 was removed from spec.md; end-of-column alignment is no longer a
  hard requirement, FR-005. This `\vspace` step is now a cosmetic finishing touch for whatever gap
  remains after space-efficient ordering's front-loading rule — spec.md's FR-007/SC-012 — has
  already preferred filling that space with a later, smaller-fitting song.)*
- **Title/lyrics rendering** *(revised 2026-09-27 — see `sb/Leaflets/ColumnBalancer.cs`'s own
  doc comment; this contract's prior "Title/lyrics separation" bullet described a design that was
  reverted, not the shipped behavior)*: a song's title (`\paragraph*{...}`), optional melody
  attribution, and lyrics always render together as one unbroken block within that song's own
  content, regardless of whether the song starts a fresh column or stacks after another song in
  the same column. There is no separate leading-title row, and sibling columns' titles are never
  paired into their own row independent of each other's lyrics — the prior design that did this
  (`SongBlock.TitleLineCount`/`Column.LeadingTitleLineCount`, matching
  `sb/Tests/TestFiles/TeX/test.tex`'s and the project's `Kottar i karburatorn.tex` reference
  example's title-row-then-lyrics-row structure) was implemented, then reverted by direct
  instruction; neither field exists on the shipped `SongBlock`/`Column` types. *(This revert was
  never recorded in spec.md/plan.md/tasks.md/research.md/data-model.md at the time it happened —
  those documents still describe the pre-revert design; see the superseded notes added
  2026-09-28 in research.md §5's addendum, data-model.md's `SongBlock`/`Column` entities, and
  tasks.md's Phase 17 addenda.)*
- **Long songs (FR-025)**: a song whose height exceeds one page's line capacity is never
  truncated/clipped — it is chunked across consecutive `minipage`s, continuing into as many
  further columns/pages as needed (never a `multicol`-style full-width environment), exactly like
  any song too big for its current column. If the song starts on an EVEN-numbered physical page,
  it MUST start that page's left column (a page break is forced before it if it wouldn't
  otherwise); if it starts on an ODD-numbered page, it carries no such placement constraint. In
  either case, once its content is exhausted, a subsequent song MAY continue filling whatever
  column/page space remains — this rule constrains only where the long song itself starts, never
  the whole page's exclusive use.
- A line that does not fit within a column's width is broken across multiple output lines
  rather than overflowing the column or being truncated (FR-004) — the only case where the system
  introduces a line break the user didn't write.

## General invariants

- The main document plus its `\input`-referenced chapter fragments together form a complete,
  self-contained LaTeX source (FR-011) — `\input` is ordinary, always-available LaTeX plumbing,
  not a further, unspecified assembly step; nothing is left for a human to fill in.
- Given zero songs across every chapter, no document (nor any chapter file) is produced at all —
  `Build` fails per `leaflet-builder-api.md`'s error behavior instead of emitting an empty/partial
  result.
- Regenerating from the same `LeafletRequest` produces byte-for-byte identical `Document` and
  `ChapterFiles` output (determinism).

## Verification approach

Tests MUST verify these guarantees structurally (e.g., counting `minipage` environments per
chapter file, checking each expected title/number appears in the right relative order within its
own chapter's content, checking `Leaflet.Document` contains only `\input` lines and no song
content, scanning for un-escaped reserved characters) rather than asserting on the full literal
document text, so that internal formatting refactors (Constitution Principle II) don't require
rewriting unrelated tests (Constitution Principle IV). Long-song pagination (FR-025) MUST be
verified using a fixture song whose line count exceeds a test-supplied `PageMetrics.LinesPerPage`
(a small value chosen so the test doesn't need a real multi-page song), asserting the resulting
`minipage` chunk boundaries and even/odd placement match research.md §15's rule — not by
constructing a fixture large enough to actually span physical LaTeX pages.
