# SB - Requirements

Derived from `specification.md`. Subject to change along with the specification.

## 1 Functional requirements

### 1.1 General

- FR-1.1.1: SB shall be an interface between the user and LaTeX, turning songs given in text form into a formatted, 
ordered, and numbered leaflet (sångblad).
- FR-1.1.2: All file paths parsed by the CLI shall be interpreted as UTF-8, regardless of the encoding used by the 
invoking command line.

### 1.2 Formatting

- FR-1.2.1: SB shall support multi-column formatting, implemented via LaTeX `\minipage`.
- FR-1.2.2: SB shall support single-column formatting, using default LaTeX behaviour.
- FR-1.2.3: Multi-column shall be the default format when none is specified.
- FR-1.2.4: In multi-column mode, columns shall be filled sequentially with as many whole songs as fit; songs shall not 
be split across a column boundary except as allowed by the overflow caveats in ordering (§1.3).
- FR-1.2.5: When space-efficient ordering is selected, SB shall prefer a later, shorter song to fill remaining column 
space before starting a new page.
- FR-1.2.6: Only the final page of a chapter may be left partially empty; all other pages must be filled with available 
song content.
- FR-1.2.7: SB shall add `\vspace` to the end of the shorter column so that both columns on a page start at the same 
line, augmenting LaTeX minipages' automatic vertical middle-alignment. This is a cosmetic correction, not the primary 
space-filling mechanism.
- FR-1.2.8: SB shall not attempt to detect or reflow musical phrasing; each input line shall map to exactly one 
output line.
- FR-1.2.9: SB shall break a single output line across multiple output lines only when it is too wide for the column; 
it shall not overflow or truncate the line.

### 1.3 Ordering

- FR-1.3.1: SB shall be able to order songs in the most space-efficient arrangement for the document.
- FR-1.3.2: SB shall be able to order songs in the exact order supplied by the user ("as-listed").
- FR-1.3.3: The ordering mode shall be user-selectable (e.g. via a `build` flag/argument).
- FR-1.3.4: A song may overflow from the first column of a page into the second column.
- FR-1.3.5: On an odd (left-side) page, a song may overflow from the second column into the next page.
- FR-1.3.6: A song shall never overflow out of an even (right-side) page.
- FR-1.3.7: "As-listed" ordering shall insert songs in the exact order given by the user, subject to the overflow 
caveats (FR-1.3.4–1.3.6).
- FR-1.3.8: The space-efficient algorithm shall place the chapter's longest remaining song first on each new page, 
then determine which remaining songs fill out the rest of that page.
- FR-1.3.9: The space-efficient algorithm shall operate on a per-page basis, aiming to fill as much of both columns 
as possible, subject to the overflow caveats (FR-1.3.4–1.3.6).
- FR-1.3.10: The best-fit result shall aim to fill 95% of the remaining space on a page.
- FR-1.3.11: Since songs are placed sequentially (one column overflowing into the next, per FR-1.3.4–1.3.6), the fill 
a set of songs achieves depends only on which songs are chosen, not the order they are evaluated in. SB shall 
therefore select each page's remaining songs via subset-sum / bounded-knapsack optimization over song heights 
(quantized to whole output lines, consistent with FR-1.2.9) against the page's remaining capacity — not via 
permutation enumeration.
- FR-1.3.12: As a fast path, SB shall first try a First-Fit-Decreasing (FFD) greedy fill — remaining songs sorted 
longest-first, added while they still fit — and use that result as-is if it already reaches the 95% fill target 
(FR-1.3.10), skipping the exact search (FR-1.3.11) for that page.
- FR-1.3.13: If the remaining songs genuinely cannot reach the 95% target for a page (e.g. too few songs remain in 
the chapter), SB shall use the best achievable fill for that page rather than erroring or requiring a fallback 
algorithm.
- FR-1.3.14: The underlying goal of the space-efficient algorithm is to minimize the amount of `\vspace` needed to 
cosmetically align columns (FR-1.2.7); the 95%-fill target (FR-1.3.10) is a means to that end, not an end in itself.

### 1.4 Numbering

- FR-1.4.1: SB shall assign a sequential number to each song in a document.
- FR-1.4.2: Numbering shall follow either the order the songs were given in, or a user-specified order.
- FR-1.4.3: Numbers shall stay contiguous automatically: deleting a song shall shift every later song's number down 
by one, and adding or moving a song to an explicit position shall shift every song from that position onward up by 
one. A song's number is its current sequential position, not an independently stable identifier.

### 1.5 Storage — documents and chapters

- FR-1.5.1: Each document shall be stored in its own folder.
- FR-1.5.2: Chapters shall be stored in a subfolder of the document folder, at `/Chapters/{name}`.
- FR-1.5.3: Songs shall be stored inside the chapter folder they belong to.
- FR-1.5.4: Documents and chapters shall be stored as `.tex` files.
- FR-1.5.5: A song's document and chapter membership shall be inferred from its folder location, not stored redundantly 
in the song file.

### 1.6 Storage — songs

- FR-1.6.1: Songs shall be stored as `.yml` files, containing title, writer, melody, number, and text (ordered lines).
- FR-1.6.2: Song `.yml` files shall be parsed when `sb document build` is invoked.

### 1.7 Storage — database as source of truth

- FR-1.7.1: SB shall maintain a database for fast lookup of documents, chapters, and songs, independent of where 
files/folders physically reside.
- FR-1.7.2: The database's `documents` records shall contain: path to the main document folder, document filename, 
internal id.
- FR-1.7.3: The database's `chapters` records shall contain: chapter name, document id, internal id.
- FR-1.7.4: The database's `songs` records shall contain: song title, song number, chapter id, internal id.
- FR-1.7.5: If a file or folder is not found at the path recorded in the database, SB shall inform the user of the 
mismatch rather than failing silently or guessing.
- FR-1.7.6: Any structural operation SB itself performs (`create`, `add`, `rename`, `delete`) shall update the 
filesystem and the database together as a single atomic step; if either half fails, the whole operation shall be 
rolled back so no orphaned file/folder or dangling database row is left behind as a direct result of SB's own 
actions. (The mismatch-detection in FR-1.7.5 is for drift caused outside of SB, not for SB's own operations.)

### 1.8 CLI — general

- FR-1.8.1: SB shall expose its functionality through a command-line interface.
- FR-1.8.2: Command fields marked 'Required' shall be command arguments; all other fields shall be flags.
- FR-1.8.3: Every command's flags/options shall provide an abbreviated alias (e.g. a short form alongside the long form).
- FR-1.8.4: When the current working directory is a document or chapter directory, SB shall infer the corresponding 
document name/chapter name argument instead of requiring it explicitly.
- FR-1.8.5: Deleting a document or chapter shall cascade — also deleting the chapters/songs (and files) it contains. 
Because this is destructive and irreversible, `delete` shall prompt for confirmation before cascading into a 
non-empty document/chapter, unless a `--force`/`-f` flag is given to skip the prompt (e.g. for scripting).
- FR-1.8.6: Names shall be unique within their scope: a document's name among all documents, a chapter's name within 
its document, and a song's title within its chapter. `create`/`add`/`rename` shall reject a name that already exists 
in that scope with a clear error, rather than silently disambiguating or allowing the duplicate.
- FR-1.8.7: Any command that references a document/chapter name or song title which does not exist shall fail with a 
clear error naming what wasn't found, rather than failing silently or no-op'ing.
- FR-1.8.8: Since document/chapter names and song titles are used directly as folder/file names on disk, 
`create`/`add`/`rename` shall validate names against a safe character set and reject (with a clear error) any name 
containing path separators, `..` segments, or other filesystem-illegal characters, rather than silently sanitizing 
them.

### 1.9 CLI — `documents`

- FR-1.9.1: `documents create` shall create an empty document and its repository, accepting `name` (required), 
`title` (required), `subtitle`, and `output folder`.
- FR-1.9.2: `documents list` shall list documents created with the tool, optionally including chapters and/or songs 
(`-c`, `-s`; both together lists songs grouped under chapters).
- FR-1.9.3: `documents delete` shall delete a document's repository, given a required document name, cascading to its 
chapters and songs (FR-1.8.5), with an optional `--force`/`-f` flag to skip the cascade confirmation prompt.
- FR-1.9.4: `documents rename` shall rename a document and its repository, given a required current name and new name.
- FR-1.9.5: `documents build` shall build the final document by calculating song placement, given a required document 
name, and optional ordering mode (number-wise or space-efficiency) and format (single-column or multi-column). For 
space-efficient ordering, per-page placement follows FR-1.3.8–FR-1.3.14 (FFD fast path, then exact subset-sum search, 
against the 95% fill target); once that target is met for a page, calculation for that page stops and moves on to 
the next page.
- FR-1.9.6: `documents build` shall write LaTeX source only (the main document and its chapter `.tex` files) to the 
output folder. It shall not itself invoke a LaTeX engine (e.g. `pdflatex`/`xelatex`) to produce a compiled PDF.

### 1.10 CLI — `chapters`

- FR-1.10.1: `chapters add` shall create an empty chapter within a document, given a required document name, with 
optional file name (default `{document}_chp_{n}`) and display title.
- FR-1.10.2: `chapters delete` shall delete one or more chapters (comma-separated) from a document, given a required 
document name and chapter list, cascading to each chapter's songs (FR-1.8.5), with an optional `--force`/`-f` flag to 
skip the cascade confirmation prompt.
- FR-1.10.3: `chapters list` shall list chapters in a document, given a required document name, optionally including 
songs under each chapter.

### 1.11 CLI — `songs`

- FR-1.11.1: `songs add` shall add a song, requiring title and text 
(text may be literal or a path to a file whose contents are used), with optional melody, writer, and number 
(`--number`). If number is omitted, the song shall be assigned the next sequential number (FR-1.4.3); if 
given an existing position, later songs shall shift up by one.
- FR-1.11.2: `songs delete` shall delete a song, given a required document name and required song title.
- FR-1.11.3: `songs list` shall list songs, given a required document name and optional chapter name filter.
- FR-1.11.4: `songs modify` shall update one or more of a song's title, writer, melody, chapter, or number, 
given a required document name and song title.

### 1.12 LaTeX document template

- FR-1.12.1: SB shall provide a `DocumentTemplate` class that builds the main LaTeX file for a document, based on a 
`template-main.tex` reference template.
- FR-1.12.2: Adding a chapter to a document shall insert that chapter into the generated main document.
- FR-1.12.3: Creating a document shall insert its metadata (title, subtitle) into the generated main document.
- FR-1.12.4: The generated document's page size shall be A4, with margins and other page metrics as defined in 
`PageMetrics.png`.
- FR-1.12.5: Chapters shall be emitted as unnumbered LaTeX chapters, using `\chapter*{{name}}` rather than `\chapter{{name}}` — the outer braces are LaTeX's own command-argument delimiters, and the inner `{name}` is the substitution placeholder for the chapter's display title.
- FR-1.12.6: Each song shall be rendered inside a minipage acting as a column.
- FR-1.12.7: Every song shall have at least a title, emitted as a LaTeX `\paragraph*{{name}}` command — again, the outer braces are LaTeX's delimiters and `{name}` is the placeholder for the song's title.
- FR-1.12.8: A song's text shall be inserted as free text directly beneath its title.
- FR-1.12.9: SB shall not hard-code or assume a particular LaTeX engine in the generated source. Correct rendering of 
Swedish characters (å, ä, ö) depends on the user compiling with a UTF-8-capable setup (e.g. XeLaTeX/LuaLaTeX, or 
pdfLaTeX with `inputenc`/`fontenc`); this is a documented requirement on the user's toolchain, not something SB 
detects or enforces.

## 2 Non-functional requirements (quality attributes)

### 2.1 Stakeholders

The primary stakeholders are the **developer** building and maintaining SB, and the **end user** creating leaflets
with it. The quality attributes below (§2.2–§2.6, matching spec §5) are selected and prioritized for these two
audiences specifically.

### 2.2 Maintainability (developer)

- NFR-2.2.1 (Architecture independence from the CLI): SB's core behaviour — ordering, numbering, layout calculation, 
storage/database access, and LaTeX generation — shall be implemented as a CLI-independent core (e.g. a separate 
library/assembly), with the CLI consuming that core as one interchangeable front-end. The program shall not be 
architecturally locked to a CLI implementation: another interface (GUI, web API, etc.) shall be addable later by 
consuming the same core, without modifying core logic.
- NFR-2.2.2 (Front-end-agnostic result contract): The core shall report success/failure via a structured result (or 
typed exceptions), not via anything CLI-specific (writing directly to stdout/stderr, calling `Environment.Exit`, 
etc.). The CLI front-end shall translate that structured result into process exit codes and console output; a future 
GUI/API front-end would translate the same result differently without changes to the core.
- NFR-2.2.3 (Testability / TDD): The codebase shall be developed test-first — a failing NUnit test shall exist before a 
feature is implemented, with tests located under `/Tests`.
- NFR-2.2.4 (Test coverage): After implementing a feature, both additional passing and failing (edge-case) tests shall 
be added for it.
- NFR-2.2.5 (Extensibility of build output): Although `build` currently emits LaTeX source only (FR-1.9.6), the build 
pipeline shall be structured so that a PDF-compilation step can be added later (e.g. invoking a LaTeX engine) without 
restructuring the core ordering/numbering/layout/generation logic.

### 2.3 Usability (end user)

- NFR-2.3.1 (CLI consistency): Every CLI flag/option shall expose a short, abbreviated alias in addition to its full 
name, for consistency and faster interactive use.
- NFR-2.3.2 (Argument inference): CLI argument inference (document/chapter name from working directory) shall reduce 
required typing for common in-directory usage without removing the ability to specify names explicitly.

### 2.4 Portability (developer, end user)

- NFR-2.4.1 (Platform): SB shall be implemented in C# targeting .NET 10, and shall run on any platform .NET 10 
supports, consistent with the core/CLI separation in NFR-2.2.1 (a non-CLI front-end should not be blocked by 
platform-specific CLI code).
- NFR-2.4.2 (Encoding): SB shall use UTF-8 as its text encoding throughout, including CLI path parsing, regardless of 
the host command line's own encoding.

### 2.5 Reliability (end user, developer)

- NFR-2.5.1 (Database consistency): The database shall remain the authoritative source of truth for document/chapter/ 
song metadata even when on-disk files or folders are moved or renamed outside of SB.
- NFR-2.5.2 (Transparency): SB shall surface discrepancies between the database and the filesystem to the user rather 
than silently reconciling or ignoring them. This principle extends to user input generally: naming collisions 
(FR-1.8.6) and references to non-existent documents/chapters/songs (FR-1.8.7) shall always produce a clear error, 
never a silent no-op or a guessed correction.
- NFR-2.5.3 (Fidelity to user input): Output formatting shall preserve the user's intended line breaks and content; 
SB shall not silently drop, truncate, or reflow text for musical/semantic reasons.
- NFR-2.5.4 (Layout correctness): The overflow caveats (a song may overflow column 1→2, and on odd pages only column 
2→next page; never on even pages) shall hold for both ordering modes, so that leaflets remain valid for double-sided 
printing/binding.
- NFR-2.5.5 (Portability of storage): Since users may relocate document/chapter/song files and folders freely on disk, 
SB's lookup mechanism (the database) shall not assume a fixed filesystem location beyond what it has recorded.
- NFR-2.5.6 (Atomicity): SB-driven structural operations (FR-1.7.6) shall be atomic with respect to the filesystem and 
database, so that a partial failure never leaves the two sources of truth diverging because of SB's own action.

### 2.6 Performance efficiency (end user)

- NFR-2.6.1 (Space-efficiency algorithmic complexity): The space-efficient layout algorithm shall aim to fill 95% of 
remaining page space per page without altering song content. Per-page song selection shall run in pseudo-polynomial 
time — an `O(n log n)` First-Fit-Decreasing pass followed, if needed, by an `O(songs × capacity)` subset-sum/bounded-
knapsack dynamic program over quantized line-height units — rather than enumerating permutations (`O(n!)`), so that 
build time stays tractable for realistic song counts without needing a tunable search-size limit.
