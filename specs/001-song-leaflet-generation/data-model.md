# Phase 1 Data Model: Song Leaflet Generation

This feature now spans persisted entities (`Document`, `Chapter`, `Song`, per research.md §9,
§12 — `AppConfig` was removed 2026-09-26, see research.md §11), the SQLite index that makes them
locatable (`DocumentIndexEntry`,
`ChapterIndexEntry`, `SongIndexEntry`, research.md §13), and the stateless leaflet-generation
transformation built on top of them (`LeafletRequest` → `Leaflet`; `LeafletRequest` groups songs
by chapter as of 2026-09-26 — see research.md §2 — rather than the flat shape from the
engine-only plan pass).

## DocumentIndexEntry (persisted — SQLite `documents` table)

The authoritative record of a document's identity and location (FR-018/FR-019/FR-022).

| Field    | Type   | Required | Notes |
|----------|--------|----------|-------|
| Id       | int    | Yes (generated) | Internal identifier; primary key. |
| Path     | string | Yes      | Filesystem path to the document's main folder — user-chosen at `create` time (`--path`), not a fixed convention. Unique. |
| Filename | string | Yes      | Derived from `Path`'s folder name at create time. |

## ChapterIndexEntry (persisted — SQLite `chapters` table)

The authoritative record of a chapter's identity and parent document (FR-020/FR-022). Has no
`Path` of its own — its location is always `<parent DocumentIndexEntry.Path>/Chapters/<Name>/`.

| Field      | Type | Required | Notes |
|------------|------|----------|-------|
| Id         | int  | Yes (generated) | Internal identifier; primary key. |
| Name       | string | Yes    | Unique within `DocumentId`. |
| DocumentId | int  | Yes      | Foreign key to `DocumentIndexEntry.Id`; a `ChapterIndexEntry` cannot exist without one (FR-014/FR-020). |

## SongIndexEntry (persisted — SQLite `songs` table)

The record of a song's identity, a fast-lookup mirror of its singing-order number, and its parent
chapter (FR-021/FR-022). Has no `Path` of its own — its location is always
`<parent chapter's computed folder>/<Name>.yml`.

| Field               | Type | Required | Notes |
|---------------------|------|----------|-------|
| Id                  | int  | Yes (generated) | Internal identifier; primary key. |
| Name                | string | Yes    | Used to derive the `<Name>.yml` filename. |
| SingingOrderNumber  | int  | Yes      | A mirror of `Song.SingingOrderNumber` (research.md §12) — the song's own `.yml` file is the source of truth as of 2026-09-26; kept here for fast uniqueness lookups (FR-016) without opening every song file. Unique across all songs sharing the same document (via `ChapterId` → `DocumentId`). |
| ChapterId           | int  | Yes      | Foreign key to `ChapterIndexEntry.Id`; a `SongIndexEntry` cannot exist without one (FR-015/FR-021). |

**Validation rules (index, all three tables)**:
- A write to `chapters` MUST reference an existing `documents.id`, and a write to `songs` MUST
  reference an existing `chapters.id` — enforced at the store layer (FR-014/FR-015), not left to
  a dangling foreign key.
- `songs.singing_order_number` MUST be unique among all rows whose `chapter_id` joins (via
  `chapters.document_id`) to the same document (FR-016) — checked before every insert/update.

## MismatchReport (in-memory only — FR-023)

Produced by `LocationMismatchDetector` when an indexed path doesn't match the filesystem.

| Field    | Type   | Notes |
|----------|--------|-------|
| EntityKind | `Document`\|`Chapter`\|`Song` | What kind of index entry the mismatch is about. |
| Id       | int    | The index entry's internal id. |
| ExpectedPath | string | The path computed from the index (directly for a document, derived for a chapter/song). |

**Validation rules**:
- A `MismatchReport` is only ever produced, never used to silently repair the index or move
  files — informing the user (FR-023) is the only action taken automatically.

## Document (persisted — content, `DocumentIndexEntry.Id` is its key)

The top-level container for a leaflet project (FR-013). Content lives at
`DocumentIndexEntry.Path` (research.md §9). As of 2026-09-26 (spec.md's 2026-09-26
Clarification), a document has **no metadata file of its own at all** — its single
`<Name>.tex` (rendered from `sb/template-main.tex` immediately at `create` time, named after
`--name`/`Name` rather than a fixed `main.tex` — a later 2026-09-26 Clarification) is both its
only persisted file and the sole source for `Title`/`Subtitle` on every later read, via
`DocumentTemplate.ParseTitleAndSubtitle` reversing the same substitution
(`Formatting.LatexEscaper.Unescape`). Neither is part of the index (research.md §13's scope
note). An empty `Chapters/` folder is created alongside `<Name>.tex` in the same `create` call.

| Field    | Type       | Required | Notes |
|----------|------------|----------|-------|
| Id       | int        | Yes      | `DocumentIndexEntry.Id`. |
| Name     | string     | Yes      | `DocumentIndexEntry.Filename` — also the default CLI reference for the document, and the stem of its `<Name>.tex` file. Changing it (`sb documents rename`) renames `<Name>.tex` too, keeping the two in sync. |
| Title    | string     | Yes      | Display title; substituted into `<Name>.tex`'s `{title}` token at `create` time (escaped) and recovered from it thereafter (unescaped). |
| Subtitle | string?    | No       | Substituted into `<Name>.tex`'s `{subtitle}` token at `create` time (escaped, or left empty when absent) and recovered from it thereafter (unescaped, empty → `null`). |
| Chapters | `Chapter[]`| Yes (may be empty) | Zero or more; a `Document` with an empty `Chapters` array is a valid, persistent state (FR-013). |

**Validation rules**:
- `Name` and `Title` MUST be non-empty.
- `Name` MUST be unique among documents (enforced via `DocumentIndexEntry.Path` uniqueness).
- `Document` has **no `Format` field** — single-/multi-column is supplied fresh on every
  `documents build --format` invocation (FR-002's "for a given generation request") and flows
  directly into `LeafletRequest.Format`; it is never a persisted document default (spec.md's
  2026-09-26 Clarification).

## Chapter (persisted — content, `ChapterIndexEntry.Id` is its key)

A subdivision of exactly one `Document` (FR-014). Content lives at
`<parent Document's path>/Chapters/<Name>/`.

| Field | Type      | Required | Notes |
|-------|-----------|----------|-------|
| Id    | int       | Yes      | `ChapterIndexEntry.Id`. |
| Name  | string    | Yes      | `ChapterIndexEntry.Name`. |
| Title | string?   | No       | Optional display title for the chapter (not part of the index — read from its own `chapter.yml` sidecar file, written only when a `--title` is given; used as the `\chapter{...}` heading when building, falling back to `Name` when absent — research.md §2). |
| Songs | `Song[]`  | Yes (may be empty) | Zero or more songs; a chapter with zero songs is valid. |

**Validation rules**:
- A `Chapter` cannot be created or persisted without an existing parent `Document` (FR-014) — the
  store rejects the operation otherwise rather than creating an orphaned index row or folder.
- `Name` MUST be unique within its parent document.

## Song (persisted — content, `SongIndexEntry.Id` is its key)

A single piece of song content, belonging to at least one `Chapter` (FR-015). As of 2026-09-26,
all of a song's own data — including its singing-order number — lives in a single `<name>.yml`
file under its parent chapter's folder (research.md §9, per `sb/specification.md` §2.5.2); that
file is the source of truth, and the index's `SongIndexEntry.SingingOrderNumber` (research.md
§12) is a fast-lookup mirror of the `number` field within it.

| Field              | Type      | Required | Notes |
|--------------------|-----------|----------|-------|
| Id                 | int       | Yes      | `SongIndexEntry.Id`. |
| Title              | string    | Yes      | Display title, shown in the output; must be non-empty. Yml field: `title`. |
| Text               | string    | Yes      | Lyric text, lines joined by `\n` in memory (unchanged in-memory shape from before 2026-09-26); the `.yml` file stores/round-trips it as a `text:` list of lines (research.md §9) — must contain at least one non-empty line. By the time it reaches `Song`/`SongStore`, `sb songs add --text`'s file-path convenience (research.md §8's 2026-09-26 update) has already been resolved to plain text by `SongCommands.Add` — this field never holds a file path. |
| Writer             | string?   | No       | Optional attribution shown alongside the song if present. Yml field: `writer`. |
| Melody             | string?   | No       | Optional melody reference shown alongside the song if present. Yml field: `melody`. |
| SingingOrderNumber | int       | Yes      | The order this song should be sung in — assigned by the user (FR-008), stored in the song's own `.yml` file (`number`, source of truth as of 2026-09-26) and mirrored in `SongIndexEntry.SingingOrderNumber` for fast lookup, unique within the parent `Document` (FR-016), and never changed by any physical ordering/layout operation (FR-009) or chapter move (FR-024). |
| Score              | string[]? | No       | Optional list of score/image filenames attached to the song (`.yml` field `score`). Stored and round-tripped only — no functional requirement yet reads or displays it; introduced by `sb/specification.md` §2.5.2/`scores` CLI section. |

**Validation rules**:
- `Title` MUST be non-empty; `Text` MUST contain at least one non-empty line (whitespace-only
  counts as empty).
- `SingingOrderNumber` MUST be unique among all songs within the same `Document` (across all of
  its chapters) — enforced by `SongStore` at add/modify time (FR-016) via the index's mirrored
  value (a single indexed query, research.md §13), not by opening every song's `.yml` file.
- A `Song` cannot be created without specifying an existing parent `Chapter` (FR-015).
- If the index's mirrored `SingingOrderNumber` for a song ever disagrees with that song's own
  `.yml` file (e.g., the file was hand-edited outside SB), this is a mismatch to surface to the
  user (following FR-023's existing pattern for location drift), not silently resolve in either
  direction — an implementation concern for planning, not a new functional requirement.
- No other structural constraints on `Text`/`Title` content — arbitrary characters (including
  LaTeX-special ones) are permitted and handled by escaping at output time (research.md §6).

## ~~Phrase~~ (removed 2026-09-26)

Previously a unit of a song's `Text` used for line-formatting decisions (FR-003/FR-004),
containing one musical phrase (a sub-sentence up to a comma, a full sentence, or a full source
line). **Removed** per spec.md's 2026-09-26 Clarification: the system no longer detects
sub-line phrasing — a song's own input line (split only on `\n`, per FR-003's revised wording) is
the unit `LeafletBuilder` renders one-per-output-line, wrapped further only per FR-004 (too wide
for the column). See research.md §3.

## LeafletFormat (enum)

- `SingleColumn`
- `MultiColumn`

Selected once per leaflet generation request (FR-002); also stored as a `Document`'s default.

## OrderingMode (enum)

- `AsListed` — preserve the order songs were supplied/stored in (FR-006 physical ordering,
  distinct from `Song.SingingOrderNumber`).
- `SpaceEfficient` — minimize unused space/pages, bounded by `MaxPermutations` (FR-007).

## LeafletChapter

One chapter's worth of input to a build (in-memory only; 2026-09-26 — see research.md §2's
update). The `documents build` CLI command constructs one per chapter of the loaded `Document`,
populating `Songs` from `SongStore.List(documentName, chapter.Name)` (`ChapterStore.List`'s own
`Chapter.Songs` is always empty — see `Chapter`'s Notes below — so this is looked up separately).

| Field | Type      | Required | Notes |
|-------|-----------|----------|-------|
| Name  | string    | Yes      | `Chapter.Name`; used to compute the `\input{Chapters/<Name>/chapter.tex}` path. |
| Title | string?   | No       | `Chapter.Title`; used as the `\chapter{...}` heading when present, falling back to `Name` when absent. |
| Songs | `Song[]`  | Yes (may be empty) | This chapter's own songs only. An empty chapter is skipped entirely at build time (no heading, no `\input` line) — see research.md §2. |

## LeafletRequest

The input to a single generation operation (in-memory only). As of 2026-09-26, this is a list of
`LeafletChapter`s, not a flat song list — see research.md §2's correction (chapters render as
independent `chapter.tex` fragments, `\input`'d from the main document, matching
`sb/specification.md` §2.5.1/§4).

| Field           | Type                 | Required | Notes |
|-----------------|----------------------|----------|-------|
| Title           | string               | Yes      | Display title for the leaflet; from `Document.Title`. |
| Subtitle        | string?              | No       | From `Document.Subtitle`; substituted (escaped) into the built `<Name>.tex`'s `{subtitle}` token, same as at `create` time (data-model.md `Document`). |
| Chapters        | `LeafletChapter[]`   | Yes      | Must contain at least one song across all chapters combined (FR-012 — a request where every chapter is empty is rejected); one entry per chapter of the requested `Document`. |
| Format          | `LeafletFormat`      | Yes      | Single- or multi-column; supplied per `documents build --format` invocation (2026-09-26 — no longer a `Document`-level default; see `Document`'s Validation rules). Scoped to *within* each chapter — never pairs songs across a chapter boundary (research.md §2). |
| Ordering        | `OrderingMode`       | Yes      | As-listed or space-efficient (physical layout only), applied independently within each chapter's own songs. |
| MaxPermutations | int                  | No       | Bound for the exact-search phase of space-efficient ordering (per chapter); defaults to 100 (per `sb/specification.md` §2.6.1) when not supplied. |

**Validation rules**:
- `Chapters.SelectMany(c => c.Songs)` MUST be non-empty overall (FR-012); a request where every
  chapter is empty (e.g., a `Document` with no chapters, or only empty chapters) is rejected with
  a descriptive error rather than producing output. An *individual* empty chapter alongside
  others that have songs is valid — it is simply skipped at build time.
- `Title` MUST be non-empty.
- `MaxPermutations`, when supplied, MUST be a positive integer.

## PlacedSong

A `Song` after physical ordering has been applied — an entry in the final `Leaflet`. Its
`PhysicalPosition` is an internal layout concern; the number actually displayed to the user next
to the song is always `Song.SingingOrderNumber` (FR-008), never `PhysicalPosition`.

| Field            | Type   | Notes |
|------------------|--------|-------|
| Song             | `Song` | The underlying song content, including its `SingingOrderNumber`. |
| PhysicalPosition | int    | 1-based position in the final physically-laid-out leaflet, used internally for column/page distribution only — NOT displayed, and NOT the same value as `Song.SingingOrderNumber` (FR-009: reordering changes this field, never `SingingOrderNumber`). |

## DocumentTemplate

Represents the fixed master-document skeleton at `sb/template-main.tex`, which this feature
reads (never modifies) and fills in to produce a complete document (research.md §2).

| Field                | Type   | Notes |
|----------------------|--------|-------|
| RawTemplate          | string | The unmodified contents of `sb/template-main.tex`. |
| TitlePlaceholder     | string | The `{title}`/`{subtitle}` tokens inside the template's `titlepage` block (2026-09-26), replaced with the leaflet's requested (escaped) title/subtitle. |
| ContentInsertionPoint| string | The `%Chapters come here` comment block, replaced with a sequence of `\input{Chapters/<name>/chapter.tex}` lines — one per non-empty chapter (2026-09-26; see `Leaflet`/`LeafletChapter` below) — never song content directly. |

**Validation rules**:
- `LeafletBuilder` MUST fail with a descriptive error if `sb/template-main.tex` is missing or
  does not contain the expected placeholder/insertion markers, rather than silently emitting a
  malformed document.

## LeafletChapterFile

One non-empty chapter's rendered `chapter.tex` content (in-memory only; 2026-09-26). Not written
to disk by `LeafletBuilder` itself — the caller (`documents build`, `cli-commands.md`) resolves
`ChapterName` to an actual path via `ChapterStore.ResolveChapterPath` and writes `chapter.tex`
there.

| Field       | Type   | Notes |
|-------------|--------|-------|
| ChapterName | string | Matches the source `LeafletChapter.Name`. |
| Content     | string | `\chapter{<title-or-name>}` (escaped) followed by that chapter's formatted, column-balanced song content — the same structural pattern previously embedded directly in `Leaflet.Document` (research.md §2). |

## Leaflet

The finished output of a generation request.

| Field        | Type                     | Notes |
|--------------|--------------------------|-------|
| Format       | `LeafletFormat`          | Echoes the request's chosen layout. |
| Songs        | `PlacedSong[]`           | Every song across every non-empty chapter, in chapter order then per-chapter physical order; each displays its `Song.SingingOrderNumber`. `PhysicalPosition` restarts at 1 within each chapter (research.md §2) — it is never displayed, so this has no user-visible effect. |
| Document     | string                   | The main document's LaTeX source — `template-main.tex` with its title/subtitle tokens filled in and its content insertion point replaced by a sequence of `\input{...}` lines (2026-09-26), never song content directly. |
| ChapterFiles | `LeafletChapterFile[]`   | One entry per non-empty chapter (2026-09-26) — the actual song content, previously embedded in `Document` directly. Together with `Document`, this is what makes the leaflet "complete, self-contained" (FR-011): a main file plus the fragments it `\input`s. |

**Relationships**: `Leaflet.Songs` is the concatenation, across every non-empty
`LeafletRequest.Chapters` entry in order, of that chapter's own songs after applying the
requested `OrderingMode` (physical layout only, scoped to that chapter) and assigning each entry
a `PhysicalPosition` starting at 1 within the chapter. The per-song content for one chapter
(formatted, displaying `Song.SingingOrderNumber`, column-balanced per `Format`, scoped to that
chapter's songs only) is derived by the formatting/column-balancing logic (research.md §2, §5),
following the structural pattern in `sb/Tests/TestFiles/TeX/test.tex`, and becomes that chapter's
entry in `Leaflet.ChapterFiles` (prefixed with its `\chapter{...}` heading). `Leaflet.Document` is
`DocumentTemplate` filled in with the title/subtitle and the list of `\input` lines pointing at
each `ChapterFiles` entry (research.md §2) — it does not itself contain any song text.

## SongBlock (internal formatting concept, not part of the public contract)

**Added 2026-09-27 — see `/speckit-analyze` finding U1 remediation.** One song's own rendered
content (title/attribution/lyrics), before it is assigned to any physical column — the atomic
unit research.md §5's column-fill algorithm packs. Same shape as the original (pre-2026-09-27)
`Column`, kept as a distinct type now that "one song's content" and "one physical column's
content" are no longer the same thing.

| Field           | Type          | Notes |
|-----------------|---------------|-------|
| Lines           | `string[]`    | This song's formatted output lines (title, optional melody attribution, lyric lines) — unchanged from the pre-2026-09-27 `Column`'s `Lines`. |
| HeightInLines   | int           | `Lines.Length` — compared against `PageMetrics.LinesPerPage` to decide whether this song fits in the column-fill algorithm's current column or must be chunked (research.md §5/§15, FR-025/FR-026). |
| ~~TitleLineCount~~  | int       | **Added 2026-09-27, reverted — does not exist on the shipped `SongBlock` (superseded 2026-09-28).** Was: how many of `Lines`' leading entries are this song's title/melody attribution (1, or 2 when a melody is present), added after discovering `sb/Tests/TestFiles/TeX/test.tex`/`Kottar i karburatorn.tex` render a song's title separate from its lyrics (research.md §5 addendum). The title/lyrics-separation design this field supported was implemented, then reverted by direct instruction — see research.md §5's superseded note and `contracts/latex-output-contract.md`'s revised "Title/lyrics rendering" bullet. |

## Column (internal formatting concept, not part of the public contract)

Used internally by column balancing; not exposed to callers.

| Field                  | Type          | Notes |
|------------------------|---------------|-------|
| Lines                  | `string[]`    | Formatted output lines assigned to this physical column. |
| HeightInLines          | int           | `Lines.Length`, used to determine the shortest column(s) for `\vspace` balancing (research.md §5). |
| ~~LeadingTitleLineCount~~  | int    | **Added 2026-09-27, reverted — does not exist on the shipped `Column` (superseded 2026-09-28).** Was: how many of `Lines`' leading entries are a fresh song's title/melody, used by `ColumnBalancer.Render` to render that leading title as its own block, separate from the body. Reverted alongside `SongBlock.TitleLineCount` above — see research.md §5's superseded note. |

**2026-09-27 addendum (revised same day — see `/speckit-analyze` finding U1 remediation)**: A
`Column` is the concatenation of as many whole (or, for one over-long song, partial) `SongBlock`s
as fit within `PageMetrics.LinesPerPage` (FR-025/FR-026), filled top-to-bottom in song order — it
is no longer one song's own block (that's `SongBlock`, above). A `SongBlock` whose own
`HeightInLines` exceeds `LinesPerPage` is simply too big to finish in the column it starts in —
its content is chunked into consecutive pieces of at most `LinesPerPage` lines, continuing into
as many further columns/pages as needed — exactly the same "doesn't fit, move on" behavior
applied to any song, not a separate mechanism. A song starting on an EVEN physical page is
additionally forced to start a fresh page (FR-025); once its content is exhausted, column-filling
with subsequent songs resumes normally in whatever space is left — a column/page is never
reserved exclusively for one song.

**2026-09-27 addendum 2 (title/lyrics separation)**: Whenever a `SongBlock` starts filling a
fresh column (whether placed whole or as the first chunk of an over-long song), that column's
`LeadingTitleLineCount` records its title's line count; `ColumnBalancer.Render` renders those
leading lines as their own block, separate from the body/lyrics that follow — paired with the
sibling column's own leading title if it also starts a fresh song at the same page, or rendered
alone (still at that column's own designated width, not full page width) if the sibling column
has no leading title of its own at that point (e.g. it's mid-continuation of a chunked song).
This exactly matches `test.tex`'s title-row-then-lyrics-row structure and the standalone title
box in `Kottar i karburatorn.tex`. A song that stacks into a column *after* another song (not
starting the column itself) has no leading title of its own — its title renders inline with its
own lyrics, as part of the body.

*(Superseded 2026-09-28: this entire addendum describes a design that was implemented, then
reverted by direct instruction — confirmed against the shipped `sb/Leaflets/ColumnBalancer.cs`'s
own doc comment, which states a song's title/melody and lyrics always render together as one
unbroken block, with no separate leading-title row and no cross-column title pairing. Neither
`SongBlock.TitleLineCount` nor `Column.LeadingTitleLineCount` exists on the shipped types. See
research.md §5's superseded note and `contracts/latex-output-contract.md`'s revised "Title/lyrics
rendering" bullet.)*

## PageLayout (internal formatting concept, one per chapter, not part of the public contract)

The result of applying research.md §5's column-fill algorithm to one chapter's ordered songs —
used both to render `LeafletChapterFile.Content` and to hand off the running physical page
counter to the next chapter.

| Field        | Type                                  | Notes |
|--------------|----------------------------------------|-------|
| Pages        | `IReadOnlyList<(Column Left, Column? Right)>` | One entry per physical page this chapter's content occupies, in order; `Right` is `null` only for a page whose column-fill never reached a second column (e.g. the chapter's last page, if its content runs out partway through the left column). |
| PagesConsumed| int                                    | `Pages.Count` — how many physical pages this chapter occupied, so `LeafletBuilder` can advance the running page counter (starting the next chapter, which always begins a fresh page per `\chapter`) to the correct parity for FR-025 (research.md §15). A page is counted as consumed as soon as any content was placed on it, even if only partially filled — there is no "half-page" unit. |

## PageMetrics (internal formatting concept, derived from `DocumentTemplate` — FR-026)

Computed once per build from `sb/template-main.tex`'s own layout geometry (research.md §14),
never from an independently configured value, so pagination decisions stay in sync with whatever
the template actually specifies.

| Field           | Type   | Notes |
|-----------------|--------|-------|
| PaperHeightPt   | double | Fixed at A4's height (297mm converted via `1in = 72.27pt = 25.4mm` ≈ 845.05pt) — `template-main.tex`'s `geometry` package always specifies `a4paper` (confirmed 2026-09-27); not parsed from the template (research.md §14). |
| TopMarginPt     | double | Parsed from the `geometry` package's `tmargin` option (falls back to `margin` if `tmargin` is absent). |
| BottomMarginPt  | double | Parsed from the `geometry` package's `bmargin` option (falls back to `margin` if `bmargin` is absent). |
| BaselineSkipPt  | double | Parsed from the top-level `\fontsize{size}{baselineskip}` command's second argument. |
| UsableHeightPt  | double | `PaperHeightPt - TopMarginPt - BottomMarginPt`. |
| LinesPerPage    | int    | `floor(UsableHeightPt / BaselineSkipPt)` — the per-column capacity research.md §5's column-fill algorithm packs `SongBlock`s against, chunking one that exceeds it on its own (research.md §14/§15). |

**Validation rules**:
- `LeafletBuilder` MUST fail with a descriptive error if `template-main.tex`'s `geometry` options
  or `\fontsize` command are missing or unparseable, rather than silently falling back to a
  hardcoded guess (mirrors `DocumentTemplate`'s existing missing-placeholder failure mode above).
