# Contract: CLI Commands

This fixes the observable behavior of the `sb` CLI surface built for this feature, scoped
against `sb/specification.md` §2.6 to what spec.md's functional requirements actually require
(see research.md §8 for what's deliberately deferred/excluded). As of 2026-09-26 (spec.md's
Clarification), every previously-required `--flag` is a **positional argument** instead
(`sb/specification.md` §2.6 explicitly leaves "whether something should be a flag or an
argument" to the implementer); every remaining (optional) flag is an **option** with a
single-letter **alias** (e.g. `--path`/`-p`) — both listed explicitly below per command.

## `sb documents create`

**Arguments** (in order): `name` (required), `title` (required).
**Options**: `--path`/`-p` (optional — the folder to create the document at; defaults to
`./<name>` under the current working directory when omitted, per `sb/specification.md` §2.6.1's
"output folder" flag), `--subtitle`/`-s` (optional). (`--format` moved to `documents build`
2026-09-26 — see below; there is no persisted per-document default anymore.)

- **Success**: creates the folder at `--path` (or the default) with zero chapters (FR-013);
  writes `<name>.tex` immediately (rendered from `sb/template-main.tex` with `--title`/`--subtitle`
  substituted, empty chapter-content region — named after `--name`, not a fixed `main.tex`, per a
  further 2026-09-26 Clarification) and an empty `Chapters/` subfolder alongside it — a
  document has no other file (no `document.yml`, spec.md's 2026-09-26 Clarification); and writes
  a `documents` index row recording the path and the derived filename (FR-018/FR-019). The
  document is immediately valid and listable, and its title/subtitle are always read back from
  `<name>.tex` thereafter (`list`, `find`, `build`), never from a separate store.
- **Errors**: `--path` already recorded for another document → descriptive conflict error,
  nothing written. Missing `name`/`title` → `System.CommandLine`'s own parse error (a required
  positional argument), nothing written; the CLI never reaches `DocumentCommands.Create`.

## `sb documents list`

**Options**: `--chapters`/`-c` (list chapters per document), `--songs`/`-s` (list songs; combined
with `--chapters` per `sb/specification.md` §2.6.1, lists songs under their chapters).

- **Success**: prints every document (and, per flags, their chapters/songs) with zero side
  effects. Before printing, checks each index entry's computed path against the filesystem
  (FR-023) and appends any `MismatchReport`s to the output so the user sees them alongside the
  listing, rather than in a separate, easy-to-miss step.

## `sb documents delete <name>` / `sb documents rename <name> <new-name>`

- **Success**: `delete` removes the document's folder and everything under it. `rename` updates
  `documents.filename` and renames `<name>.tex` to `<new-name>.tex` in place, keeping the file and
  the index in sync (a further 2026-09-26 Clarification).
- **Errors**: unknown `<name>` → descriptive not-found error, nothing changed.

## `sb documents build`

**Arguments**: `document-name` (required), `ordering` (optional — `as-listed`|`space-efficient`,
default `as-listed`; a positional argument despite being optional, per a further 2026-09-26
Clarification — the only exception to "required flags become arguments, optional flags stay
options" made so far).
**Options**: `--format`/`-f` (`single-column`|`multi-column`, default `single-column` — moved here from `documents create`
2026-09-26; supplied fresh on every build, not a stored document default). *(2026-10-19: the former
`--max-permutations`/`-m` option is removed — superseded by First Fit Descending, FR-007.)*

- **Success**: loads the named `Document` (resolving its folder via the `documents` index row,
  and its `Title`/`Subtitle` by parsing them back out of its current `<name>.tex`), groups its
  songs by chapter into a `LeafletRequest` (one `LeafletChapter` per chapter — 2026-09-26,
  correcting an earlier flattening bug) carrying that `Title`/`Subtitle` plus this invocation's
  `--format` (see `leaflet-builder-api.md`), calls `LeafletBuilder.Build`, writes each non-empty
  chapter's `chapter.tex` under its own `Chapters/<name>/` folder, and writes the main document to
  `<document's indexed path>/<name>.tex` (overwriting any previous build for both, but
  re-substituting the same title/subtitle in the main document so a later `list`/`find`/`build`
  still recovers them) — the main document only `\input`s each chapter file, per
  `sb/specification.md` §2.5.1/§4; it never contains song content directly.
- **Errors**:
  - Unknown `document-name` → descriptive not-found error.
  - Document has zero songs across all its chapters → descriptive error citing FR-012 ("at least
    one song is required"), nothing written.
  - The document's indexed path (or a chapter's/song's computed path) doesn't match the
    filesystem → descriptive `MismatchReport`-based error (FR-023); `build` refuses to write a
    possibly-wrong output rather than guessing.

## `sb chapters add`

**Arguments**: `document-name` (required).
**Options**: `--name`/`-n` (optional, defaulted per `sb/specification.md` §2.6.2's
`{document}_chp_{n}` convention), `--title`/`-t` (optional).

- **Success**: creates `Chapters/<name>/` under the named document with zero songs (chapters may
  be empty).
- **Errors**: unknown `document-name` → descriptive error citing FR-014 ("a chapter requires a
  document"), nothing written. `--name` already used in this document → conflict error.

## `sb chapters remove`

**Arguments**: `document-name` (required), `names` (required, one or more — a variadic positional
argument, so it must come last).

- **Success**: removes each named chapter **and all songs within it** (cascading removal, per
  spec.md Assumptions) from the document.
- **Errors**: unknown document or any unknown chapter name in the list → descriptive error,
  nothing removed (all-or-nothing for the whole list).

## `sb chapters list`

**Arguments**: `document-name` (required).
**Options**: `--songs`/`-s` (also list songs under each chapter).

- **Success**: prints the document's chapters (and, per flag, their songs) with zero side
  effects.

## `sb songs add`

**Arguments** (in order): `document-name` (required), `chapter-name` (required), `title`
(required), `text` (required — literal text, **or** a path to an existing file whose contents are
used instead; a 2026-09-26 Clarification, resolved in `SongCommands.Add`), `singing-number`
(required, positive integer).
**Options**: `--writer`/`-w` (optional), `--melody`/`-m` (optional).

- **Success**: writes a single `<song-name>.yml` (title, writer?, melody?, number, text lines,
  score? — research.md §9) under the named chapter, and a `songs` index row mirroring its
  identity and singing-order number for fast lookup (FR-015, FR-021, research.md §12). If
  `--text` names an existing file, its contents (not the path string) become the song's text.
- **Errors**:
  - Unknown `document-name`/`chapter-name` → descriptive error citing FR-015 ("a song
    requires a chapter"), nothing written.
  - `singing-number` already used by another song in the same document → descriptive conflict
    error naming the existing song (FR-016), nothing written.

## `sb songs remove`

**Arguments**: `document-name` (required), `song-name` (required).

- **Success**: deletes the song's `.yml` file from wherever it lives in the document.
- **Errors**: unknown document or song name → descriptive error, nothing removed.

## `sb songs list`

**Arguments**: `document-name` (required).
**Options**: `--chapter-name`/`-c` (optional, filters to one chapter).

- **Success**: prints matching songs (including their singing-order numbers) with zero side
  effects.

## `sb songs modify`

**Arguments**: `document-name` (required), `song-name` (required).
**Options**: one or more of `--title`/`-t`, `--writer`/`-w`, `--melody`/`-m`, `--chapter`/`-c`
(moves the song to another chapter in the same document), `--singing-number`/`-s`.

- **Success**: updates only the specified field(s); moving `--chapter` does not change
  `SingingOrderNumber` (FR-024); changing `--singing-number` still goes through the same
  uniqueness check as `songs add` (FR-016).
- **Errors**: unknown document/song/target chapter → descriptive error. New `--singing-number`
  conflicts with another song in the document → descriptive conflict error, nothing changed.

## Explicitly out of scope for this contract

- CWD-based inference of `document-name`/`chapter-name` (research.md §8) — every command above
  requires it explicitly (as a positional argument, 2026-09-26).
- `sb scores ...` (attaching images/scores to songs) — no functional requirement covers a
  dedicated command for it; `songs add`/`modify` may accept/round-trip the `.yml` `score` field
  (research.md §9) as plain data, but no CLI verb manages it.
- `sb config ...` — removed 2026-09-26; Config is out of scope for this feature entirely (see
  spec.md's 2026-09-26 Clarification, research.md §11).
