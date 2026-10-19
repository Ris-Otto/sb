# Contract: Leaflet Builder API

This feature exposes no network/web endpoints. Its external interface is the public engine API
called by the `sb documents build` CLI command (see `cli-commands.md`), which loads a `Document`
from storage, groups its songs by chapter into a `LeafletRequest` (one `LeafletChapter` per
chapter — **not** a flattened song list, per spec.md's 2026-09-26 Clarification correcting an
earlier implementation gap), and calls `Build`. This contract fixes that API's observable
behavior so it can be implemented and tested independently of storage/CLI wiring.

## Entry point

```text
LeafletBuilder.Build(LeafletRequest request) -> Leaflet
```

(See `data-model.md` for `LeafletRequest`, `LeafletChapter`, `Leaflet`, `LeafletChapterFile`, and
`PageMetrics` field definitions.) `PageMetrics` (FR-026) is always derived internally from
`sb/template-main.tex`, mirroring how the template path itself is resolved — never supplied by the
caller in production use; tests may substitute a different template path (as the existing
`templatePath` parameter already allows) to exercise a small `LinesPerPage` without needing a
fixture large enough to span real physical pages (research.md §14/§15, `latex-output-contract.md`).

## Preconditions

- `request.Title` MUST be non-empty.
- `request.Chapters` MUST contain at least one `Song` across all chapters combined (FR-012) —
  an individual chapter with zero songs is fine as long as at least one other chapter has songs.
- Each `Song` in any chapter MUST have non-empty `Title` and `Text`.
- `sb/template-main.tex` MUST exist and contain the expected title/subtitle placeholders and
  content insertion point (see `data-model.md`'s `DocumentTemplate`), and its `geometry`
  margin options and `\fontsize` command MUST be present and parseable so `PageMetrics` (FR-026)
  can be derived (see `data-model.md`'s `PageMetrics`).

## Postconditions (on success)

- `result.Songs` contains exactly the songs from every chapter that had at least one song — no
  songs are dropped or duplicated; a chapter with zero songs contributes nothing.
- Within each chapter, that chapter's own songs are ordered according to `request.Ordering`
  (`AsListed` preserves the chapter's own list order; `SpaceEfficient` minimizes that chapter's
  unused column space via page-by-page First Fit Descending — see research.md §4) — ordering never mixes
  songs from different chapters.
- Within each chapter, `PhysicalPosition` restarts at 1 for that chapter's first (ordered) song
  and increments from there (internal layout ordering only, per chapter — never displayed).
- Every song's `SingingOrderNumber` is exactly whatever value it already had in its source
  `LeafletChapter` — `Build` never computes, assigns, or alters it (FR-009); it only reads it for
  display purposes (FR-008).
- `result.ChapterFiles` contains exactly one entry per chapter that had at least one song (empty
  chapters are skipped — no entry, no heading), each a non-empty string starting with
  `\chapter*{<title-or-name>}` (escaped; falls back to the chapter's `Name` when it has no
  `Title`) followed by that chapter's formatted, column-balanced song content (see
  `latex-output-contract.md`).
- `result.Document` is a non-empty string containing the main LaTeX document —
  `sb/template-main.tex` with its title/subtitle placeholders replaced by `request.Title`/
  `request.Subtitle` and its content insertion point replaced by one
  `\input{Chapters/<name>/chapter.tex}` line per `result.ChapterFiles` entry, in chapter order —
  `result.Document` never contains song content directly.
- Calling `Build` twice with an identical `request` (same chapters/songs, format, and ordering)
  always returns identical `result.Songs` order, `result.ChapterFiles` content,
  and `result.Document` (determinism — required by the edge case on tie-breaking).
- In multi-column layout, a song whose content exceeds one page's line capacity is never
  truncated — it is chunked across consecutive columns/pages per FR-025 (see
  `latex-output-contract.md`); this never changes song order (previous bullets) or any
  `SingingOrderNumber`/`PhysicalPosition` value, only where column/page breaks fall.

## Error behavior

- Given a `request.Chapters` where every chapter has zero songs (including an empty list), `Build`
  MUST fail with a descriptive error identifying that at least one song is required (FR-012) — it
  MUST NOT return a `Leaflet` with an empty/blank document.
- Given a `Song` with empty `Title` or `Text`, `Build` MUST fail with a descriptive error
  identifying which song and field is invalid, rather than silently omitting or blanking it.
- Given a missing or malformed `sb/template-main.tex` (missing title/subtitle placeholder or
  content insertion point, or an unparseable/missing `geometry`/`\fontsize` layout declaration),
  `Build` MUST fail with a descriptive error rather than emitting a best-effort/partial document.

## Out of scope for this contract

- Parsing raw/unstructured text into `Song` values (done by `SongStore`, see `cli-commands.md`
  and `storage-format.md`).
- Persisting `Leaflet.Document`/`Leaflet.ChapterFiles` to disk (done by `documents build`'s
  handler after `Build` returns), or invoking any LaTeX compiler/renderer.
- Enforcing `SingingOrderNumber` uniqueness (that is `SongStore`'s job at add/modify time, per
  FR-016 — by the time a `Song` reaches `Build`, its number is assumed already valid).
- Any CLI argument parsing.
- Managing multiple named documents/chapters as stored, editable entities (create, rename,
  list, delete) — this contract covers producing one finished document (plus its chapter
  fragments) from one request; see `cli-commands.md` and `storage-format.md` for those.
