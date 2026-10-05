# SB - Requirements

Derived from `specification.md`. Subject to change along with the specification.

## 1 Functional requirements

### 1.1 General

- FR-1.1.1: SB shall be an interface between the user and LaTeX, turning songs given in text form into a formatted, ordered, and numbered leaflet (sångblad).
- FR-1.1.2: All file paths parsed by the CLI shall be interpreted as UTF-8, regardless of the encoding used by the invoking command line.

### 1.2 Formatting

- FR-1.2.1: SB shall support multi-column formatting, implemented via LaTeX `\minipage`.
- FR-1.2.2: SB shall support single-column formatting, using default LaTeX behaviour.
- FR-1.2.3: Multi-column shall be the default format when none is specified.
- FR-1.2.4: In multi-column mode, columns shall be filled sequentially with as many whole songs as fit; songs shall not be split across a column boundary except as allowed by the overflow caveats in ordering (§1.3).
- FR-1.2.5: When space-efficient ordering is selected, SB shall prefer a later, shorter song to fill remaining column space before starting a new page.
- FR-1.2.6: Only the final page of a chapter may be left partially empty; all other pages must be filled with available song content.
- FR-1.2.7: SB shall add `\vspace` to the end of the shorter column so that both columns on a page start at the same line, augmenting LaTeX minipages' automatic vertical middle-alignment. This is a cosmetic correction, not the primary space-filling mechanism.
- FR-1.2.8: SB shall not attempt to detect or reflow musical phrasing; each input line shall map to exactly one output line.
- FR-1.2.9: SB shall break a single output line across multiple output lines only when it is too wide for the column; it shall not overflow or truncate the line.

### 1.3 Ordering

- FR-1.3.1: SB shall be able to order songs in the most space-efficient arrangement for the document.
- FR-1.3.2: SB shall be able to order songs in the exact order supplied by the user ("as-listed").
- FR-1.3.3: The ordering mode shall be user-selectable (e.g. via a `build` flag/argument).
- FR-1.3.4: A song may overflow from the first column of a page into the second column.
- FR-1.3.5: On an odd (left-side) page, a song may overflow from the second column into the next page.
- FR-1.3.6: A song shall never overflow out of an even (right-side) page.
- FR-1.3.7: "As-listed" ordering shall insert songs in the exact order given by the user, subject to the overflow caveats (FR-1.3.4–1.3.6).
- FR-1.3.8: The space-efficient algorithm shall place the chapter's longest remaining song first on each new page, then search for a best-fit song for each remaining slot.
- FR-1.3.9: The space-efficient algorithm shall operate on a per-page basis, aiming to fill as much of both columns as possible, subject to the overflow caveats (FR-1.3.4–1.3.6).
- FR-1.3.10: The best-fit algorithm shall aim to fill 95% of the remaining space on a page.
- FR-1.3.11: If `--max-permutations` is set, the best-fit search shall evaluate at most that many permutations of the remaining songs per page; if unset, it defaults to `count(songs)!`, i.e. it evaluates all possible permutations of the remaining songs for that page.
- FR-1.3.12: The underlying goal of the space-efficient algorithm is to minimize the amount of `\vspace` needed to cosmetically align columns (FR-1.2.7); the 95%-fill target (FR-1.3.10) is a means to that end, not an end in itself.

### 1.4 Numbering

- FR-1.4.1: SB shall assign a sequential number to each song in a document.
- FR-1.4.2: Numbering shall follow either the order the songs were given in, or a user-specified order.

### 1.5 Storage — documents and chapters

- FR-1.5.1: Each document shall be stored in its own folder.
- FR-1.5.2: Chapters shall be stored in a subfolder of the document folder, at `/Chapters/{name}`.
- FR-1.5.3: Songs shall be stored inside the chapter folder they belong to.
- FR-1.5.4: Documents and chapters shall be stored as `.tex` files.
- FR-1.5.5: A song's document and chapter membership shall be inferred from its folder location, not stored redundantly in the song file.

### 1.6 Storage — songs

- FR-1.6.1: Songs shall be stored as `.yml` files, containing title, writer, melody, number, text (ordered lines), and an optional list of score filenames. (No CLI is currently specified for managing scores — see §3.)
- FR-1.6.2: Song `.yml` files shall be parsed when `sb document build` is invoked.

### 1.7 Storage — database as source of truth

- FR-1.7.1: SB shall maintain a database for fast lookup of documents, chapters, and songs, independent of where files/folders physically reside.
- FR-1.7.2: The database's `documents` records shall contain: path to the main document folder, document filename, internal id.
- FR-1.7.3: The database's `chapters` records shall contain: chapter name, document id, internal id.
- FR-1.7.4: The database's `songs` records shall contain: song name, song number, chapter id, internal id.
- FR-1.7.5: If a file or folder is not found at the path recorded in the database, SB shall inform the user of the mismatch rather than failing silently or guessing.

### 1.8 CLI — general

- FR-1.8.1: SB shall expose its functionality through a command-line interface.
- FR-1.8.2: Command fields marked 'Required' shall be command arguments; all other fields shall be flags.
- FR-1.8.3: Every command's flags/options shall provide an abbreviated alias (e.g. a short form alongside the long form).
- FR-1.8.4: When the current working directory is a document or chapter directory, SB shall infer the corresponding document name/chapter name argument instead of requiring it explicitly.

### 1.9 CLI — `documents`

- FR-1.9.1: `documents create` shall create an empty document and its repository, accepting `name` (required), `title` (required), `subtitle`, and `output folder`.
- FR-1.9.2: `documents list` shall list documents created with the tool, optionally including chapters and/or songs (`-c`, `-s`; both together lists songs grouped under chapters).
- FR-1.9.3: `documents delete` shall delete a document's repository, given a required document name.
- FR-1.9.4: `documents rename` shall rename a document and its repository, given a required current name and new name.
- FR-1.9.5: `documents build` shall build the final document by calculating song placement, given a required document name, and optional ordering mode (number-wise or space-efficiency), max-permutations (default `count(songs)!`, i.e. all permutations), and format (single-column or multi-column).

### 1.10 CLI — `chapters`

- FR-1.10.1: `chapters add` shall create an empty chapter within a document, given a required document name, with optional file name (default `{document}_chp_{n}`) and display title.
- FR-1.10.2: `chapters remove` shall remove one or more chapters (comma-separated) from a document, given a required document name and chapter list.
- FR-1.10.3: `chapters list` shall list chapters in a document, given a required document name, optionally including songs under each chapter.

### 1.11 CLI — `songs`

- FR-1.11.1: `songs add` shall add a song, requiring title and text (text may be literal or a path to a file whose contents are used), with optional melody and writer.
- FR-1.11.2: `songs remove` shall remove a song given a document name and song name.
- FR-1.11.3: `songs list` shall list songs, given a required document name and optional chapter name filter.
- FR-1.11.4: `songs modify` shall update one or more of a song's title, writer, melody, chapter, or ordinal number, given a required document name and song name.
- FR-1.11.5: `songs replace` shall replace a song's content while preserving its cardinal number (its position/index number in the document).

### 1.12 LaTeX document template

- FR-1.12.1: SB shall provide a `DocumentTemplate` class that builds the main LaTeX file for a document, based on a `template-main.tex` reference template.
- FR-1.12.2: Adding a chapter to a document shall insert that chapter into the generated main document.
- FR-1.12.3: Creating a document shall insert its metadata (title, subtitle) into the generated main document.
- FR-1.12.4: The generated document's page size shall be A4, with margins and other page metrics as defined in `PageMetrics.png`.
- FR-1.12.5: Chapters shall be emitted as unnumbered LaTeX chapters, using `\chapter*{name}` rather than `\chapter{name}`.
- FR-1.12.6: Each song shall be rendered inside a minipage acting as a column.
- FR-1.12.7: Every song shall have at least a title, emitted as a LaTeX `\paragraph*{name}` command.
- FR-1.12.8: A song's text shall be inserted as free text directly beneath its title.

## 2 Non-functional requirements

- NFR-2.1 (Platform): SB shall be implemented in C# targeting .NET 10.
- NFR-2.2 (Encoding): SB shall use UTF-8 as its text encoding throughout, including CLI path parsing, regardless of the host command line's own encoding.
- NFR-2.3 (CLI consistency): Every CLI flag/option shall expose a short, abbreviated alias in addition to its full name, for consistency and faster interactive use.
- NFR-2.4 (Consistency): The database shall remain the authoritative source of truth for document/chapter/song metadata even when on-disk files or folders are moved or renamed outside of SB.
- NFR-2.5 (Transparency): SB shall surface discrepancies between the database and the filesystem to the user rather than silently reconciling or ignoring them.
- NFR-2.6 (Testability / TDD): The codebase shall be developed test-first — a failing NUnit test shall exist before a feature is implemented, with tests located under `/Tests`.
- NFR-2.7 (Test coverage): After implementing a feature, both additional passing and failing (edge-case) tests shall be added for it.
- NFR-2.8 (Fidelity to user input): Output formatting shall preserve the user's intended line breaks and content; SB shall not silently drop, truncate, or reflow text for musical/semantic reasons.
- NFR-2.9 (Space efficiency): The space-efficient layout algorithm shall aim to fill 95% of remaining page space per page without altering song content, bounded by a configurable `--max-permutations` search limit (default `count(songs)!`, i.e. exhaustive by default) to keep build time predictable when overridden with a smaller value.
- NFR-2.10 (Layout correctness): The overflow caveats (a song may overflow column 1→2, and on odd pages only column 2→next page; never on even pages) shall hold for both ordering modes, so that leaflets remain valid for double-sided printing/binding.
- NFR-2.11 (Portability of storage): Since users may relocate document/chapter/song files and folders freely on disk, SB's lookup mechanism (the database) shall not assume a fixed filesystem location beyond what it has recorded.
- NFR-2.12 (Usability): CLI argument inference (document/chapter name from working directory) shall reduce required typing for common in-directory usage without removing the ability to specify names explicitly.

## 3 Open issues / ambiguities to clarify

- The spec no longer describes any CLI for attaching images/scores to a song (the `scores` subcommand section and the general "add images" bullet have both been removed), yet the song `.yml` format (§1.6.1) still reserves an optional score filename list. It is unclear whether score attachment is deferred/out of scope for now, or still needed but not yet (re)specified.
- `documents build`'s `max-permutations` default of `count(songs)!` is, for any non-trivial song count, astronomically large and not a practical default for an exhaustive search; confirm whether this is intentional (i.e. "unlimited/exhaustive unless overridden") or a placeholder.
