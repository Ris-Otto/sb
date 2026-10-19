# SB - Sångblad specification

This is the initial document which functions as the source of truth for the project. Subject to change.
SB is built with C#, .NET 10

## 1 Goal

SB should make it easier to create song leaflets (sångblad).
It is an interface between the user and LaTeX. The goal is to seamlessly take
(a list of) songs that are in text-form and transform the into either a multi-column
leaflet or a single-column leaflet of the songs, and format them, order them,
and number them. The encoding is UTF-8.

## 2 Functionality (high-level)

### 2.1 Formatting

SB should have two formatting options; multi-column and single-column.
The multi-column option is achieved by using LaTeX's \minipage-command.
The single-column is default LaTeX behaviour. Multi-column is default SB behaviour

Multi-column formatting is achieved via LaTeX minipages. Columns are filled sequentially with as
many whole songs as fit. When using space-efficient ordering, a later, shorter song is preferred
to fill any remaining column space on a page before moving on to a new page — only a chapter's
final page may be left partially empty, since no further song content exists to fill it (2026-09-28
update). Columns are typeset as top-aligned minipages (LaTeX's `[t]` position), so both already
start at the same line. Whatever small residual gap remains after filling is corrected cosmetically
by adding `\vspace` to the end of the shorter column, so both columns also *end* at the same line —
vertical alignment is a finishing touch for a level-looking page, not the primary mechanism for
using available space.

Single-column formatting leaves the page breaks to LaTeX, which SB cannot see, so it cannot plan pages, add
continuation notices or apply the paragraph rule there. What it does do is keep each song's header together with the
start of its lyrics: the header lines and the first paragraph (at most 10 lyric lines) are joined with LaTeX's
non-breaking line end `\\*`, so a title is never stranded alone at the bottom of a page (A-11).

SB does not try to detect musical phrasing. The user's own line breaks are trusted as-is: one
input line becomes one output line. The only exception is a line too wide for the column
(especially likely in multi-column leaflets, since space is limited), which is broken across
multiple output lines rather than overflowing or being truncated.

### 2.2 Ordering

A few caveats to the ordering:
 - Songs may overflow from the first column to the second column.
 - On an ODD page (left-side page in the leaflet), a song may overflow from the second column to the next page.
 - This should never happen on an EVEN page (right-side page in the leaflet).

These caveats describe the normal case, where a song could otherwise be moved whole to the next page instead of
being split. When a song is long enough that it doesn't fit within a single page's two columns combined, the
even-page restriction is moot: there is no alternative placement, so the song continues onto as many further pages
as it needs, regardless of each page's parity.

Whenever a song's content is split across a page boundary — whether because of the odd-page caveat above or
because the song itself spans multiple pages — a continuation notice must be shown to the reader (e.g. a short
marker where the song breaks off, and another where it resumes), so the break doesn't read as the song simply
ending partway through. A song splitting from column 1 to column 2 within the same page needs no such notice,
since both halves are visible on the same page at once. (Single-column mode, §2.1, has no such notices: LaTeX
decides there where pages break, which SB cannot see.)

Two ways to order the songs:
 - As listed by the user
 - In the most space-efficient way.

#### as-listed

As listed inserts the songs as they are listed. The two caveats still apply.

#### space-efficient

The space-efficient algorithm selects the chapter's longest remaining song and places it first on each new page.
The two caveats apply. It then determines which of the remaining songs should fill out the rest of that page.

Since songs are placed sequentially (filling one column, then overflowing into the next per the two caveats), how
much of the remaining page a set of songs fills depends only on which songs are chosen, not on the order they are
tried in. Picking the best-filling set of remaining songs for a page is therefore a subset-sum / bin-covering
problem, not a permutation search:

- Each song's height is quantized to whole output lines (the same unit used for line-wrapping, §2.1).
- The remaining songs are run through a First-Fit-Decreasing (FFD) greedy pass first: sorted longest-first, added
  to the page while they still fit. If the page this actually produces already fills 95% of its two columns, it is
  used as-is.
- Otherwise, an exact subset-sum/bounded-knapsack search is run over the remaining songs against the page's
  remaining capacity (in lines), to find the subset whose total height best fills that capacity, subject to the
  two caveats.

The best-fit result aims to fill 95% of the page's two columns, measured in lines actually placed. Because this finds the best subset
directly, there is no permutation count to bound or tune, and no separate fallback algorithm is needed for an
exhausted search — if the remaining songs genuinely cannot reach 95% (e.g. too few songs are left in the chapter),
the best achievable fill for that page is used.

The height-based search alone is not enough: a set whose heights sum to the page capacity can still be unplaceable,
since a song may only be started where one whole paragraph fits (§4.3, A-2) and a column can end a few lines short.
Candidate sets (from FFD and from the exact search at the full capacity and at progressively smaller ones, each in a
few orderings) are therefore each evaluated by simulating the real column fill, and the set that places the most lines
on the page is chosen. FFD is still tried first and used as-is if its simulated page reaches 95%.

Essentially, the goal of the space-efficient algorithm is to need to insert as little \vspace{...} as possible.

### 2.3 Numbering

SB should assign a number to each song in a document, either in the order
they were given or in a specified order given by the user.

Numbers stay contiguous automatically: deleting a song shifts every later song's number down by one, and adding or
moving a song to an explicit position shifts every song from that position onward up by one. A song's number is
therefore always its current sequential position, not an independently stable identifier.

The numbering is document-wide: it runs through all chapters of the document in one sequence (a song's chapter does
not affect its number), so renumbering touches songs in every chapter, and a chapter holds a range of the numbers that
need not start at 1. Moving a song to another chapter keeps its number unless a new one is given (A-6).

### 2.4 Storage

#### 2.4.1 Documents and chapters

Each document is stored in its own folder. Chapters are stored in a subfolder, /Chapters/{name}.
Songs are stored in the chapter folder. Documents and chapters are stored as .tex-files.

#### 2.4.2 Songs
The songs are stored as .yml-files, which are then later parsed when `sb documents build` is called.
The .yml for a song looks like this:
```
title: {--title}
writer: {--writer}
melody: {--melody}
number: {--number}
text:
  - {line 1}
  - ...
  - {line N}
```
It is a plain YAML mapping. `writer` and `melody` are left out when the song has none. SB writes every scalar
double-quoted, so that a lyric line that looks like a YAML literal (`null`, `true`, `42`, an empty line) is read back
as text and never reinterpreted; hand-written files may use plain scalars.

The document and chapter the song belongs to is inferred, since the songs is stored inside the chapter-folder
it belongs to, which in turn is stored inside the document-folder the chapter belongs to.

#### 2.4.3 Database
Since files and folders may be saved wherever the user wants, the database should be the source of truth for fast lookup.
If a file or folder isn't where the database says it is, inform user about the mismatch: the `list` commands mark
anything missing with its expected path (`[NOT FOUND on disk: <path>]`, exit code still 0), and the commands that
need a document's or chapter's folder (`build`, `rename`, `chapters add`, `songs add`, `songs modify`) stop with a
Storage error naming it instead of recreating it or failing obscurely (A-10). The database should contain:
"documents": path to the maindocument folder, document filename, display title, display subtitle, internal id.
"chapters": chapter name, display title, document id, internal id.
"songs": song title, song number, chapter id, internal id

Mismatches of this kind are only expected to arise from changes made outside of SB. Any structural operation SB
itself performs (`create`, `add`, `rename`, `delete`, ...) must update the filesystem and the database together as
a single atomic step: if either half fails, the whole operation is rolled back so no orphaned file/folder or
dangling database row is left behind as a direct result of SB's own actions. For songs this includes the renumbering
of other songs: the files involved are rewritten with an undo and all database rows change in one transaction, so a
failure part-way leaves every file and number as it was. The only exception is removing a deleted song's own file,
which happens after the database change has committed and is reported if it fails (A-8).



### 2.5 CLI

Each document will generally
contain only one chapter which houses all the songs, but for the sake of
completeness, songs can be organised into different chapters.

When the CLI parses a file path, that path should be parsed as UTF-8, even if the command line used
doesn't.

All commands' flags/options should have an abbreviated alias

Deleting a document or chapter cascades: it also deletes the chapters/songs (and files) it contains. Since this is
destructive and irreversible, `delete` must ask for confirmation before cascading into a non-empty document/chapter
(or accept a `--force`/`-f` flag to skip the prompt, e.g. for scripting).

Names (document/chapter/song) must be unique within their scope (a document's name among all documents, a
chapter's name within its document, a song's title within its chapter). `create`/`add`/`rename` must reject a name
that already exists in that scope with a clear error, rather than silently disambiguating or allowing the duplicate.

Uniqueness is case-sensitive: "Hymn" and "hymn" are different names to SB (A-7). On a case-insensitive file system
(the Windows default) two such names map to the same file, so SB checks before saving a song: if writing its file
would overwrite an existing file (another song whose title differs only in case, or a stray file), the command fails
with a clear Conflict error naming the file and changes nothing. A song whose title only changes case keeps its own
file. Document and chapter folders are likewise never overwritten.

Any command that references a document/chapter name or song title which does not exist must fail with a clear error
naming what wasn't found, rather than failing silently or no-op'ing.

Since document/chapter names and song titles are used directly as folder/file names on disk, `create`/`add`/`rename`
must validate names against a safe character set and reject (with a clear error) any name containing path
separators, `..` segments, or other filesystem-illegal characters, rather than silently sanitizing them. The check is
a platform-independent superset of what Windows rejects, so that a name accepted on one platform works on all (§5.3).
In addition to the above, a name is rejected if it is exactly `.`, starts or ends with whitespace, ends with a `.`
(document and chapter names only — a song title is a file name stem, where a trailing dot is harmless), or is one of
Windows' reserved device names (CON, PRN, AUX, NUL, COM1–COM9, LPT1–LPT9, in any case and with or without an
extension after the first dot), which cannot be created as files or folders there (A-7).

The following list describes the interface. The fields marked 'Required' should be command arguments, the others
flags

1. Via the CLI, a user can `create` an empty document.

2. The user can `add` chapters and songs to an existing document.

3. A user can `list` chapters and songs contained in a document, and documents created via the tool

4. A user can `modify` some song data, like title, melody and writer, directly through
   the CLI.

5. A user can `delete` a song from a chapter, and a chapter from a document

CLI arguments like 'document-name' or 'chapter name' are inferred if working directory is a
document/chapter directory. This holds for every command that takes a document name, and for the chapter name of
`songs add`; leading arguments that are left out are the ones inferred (so `songs add Title Text` run inside a chapter
folder adds to that chapter, and `songs add chapter-1 Title Text` run inside a document folder adds to that
document's `chapter-1`). A name given explicitly always wins (A-8).

Failures that happen outside any one command — for instance an unusable data folder or a locked database — are
reported like other storage errors, with an `Error:` message and the Storage exit code, not as a crash (A-8).

Exit codes: 0 success, 1 validation error (also invalid command-line arguments), 2 conflict (a name already exists,
or a file would be overwritten), 3 not found, 4 storage (file system or database). The database is kept in
`%APPDATA%\sb-cli\sb.db`; the environment variable `SB_DATA_DIR` selects another folder for it, which allows several
independent sets of documents.

#### 2.5.1 `documents`

##### 2.5.1.1 Subcommands

- `create`, creates an 'empty' document and repository
- `list`, lists documents created using this program
- `delete`, deletes the document repository
- `rename`, renames the document and its repository
- `build`, builds the document, calculating the placement of the songs. This is the main function
  of the program.

`build` writes LaTeX source only (the main document and its chapter `.tex` files) to the output folder; it does
not itself invoke a LaTeX engine to compile a PDF. The build pipeline should be structured so that an optional
compilation step (e.g. shelling out to `pdflatex`/`xelatex`) could be added later without becoming a required
dependency now.

flags or arguments (`create`):

- name === the name of the file. Required
- title === the **display** title of the document. Visible in the LaTeX-output. Required
- subtitle
- output folder

flags or arguments (`list`):

- chapters === list chapters
- songs === list songs
- when both -c and -s are true, list songs under chapters
- when only -s is true, list each document's songs directly under the document, in number order (A-8)

flags or args (`delete`):

- document-name. Required
- force === skip the confirmation prompt when cascading into a non-empty document (alias `-f`)

flags or args (`rename`):

- document-name. Required
- new_name. Required

flags or args (`build`):

- document-name. Required
- ordering === song number-wise or by space-efficiency
- [Ss]ingle-column | [Mm]ulti-column

#### 2.5.2 `chapters`

##### 2.5.2.1 Subcommands

- `add`, creates an empty chapter in a document.
- `delete`, deletes a chapter from a document
- `list`, lists chapters in a document

flags or arguments (`add`):

- document-name. Required

- name === the name of the file ( default {document}\_chp\_{n} )

- title === the **display** title of the chapter. Visible in the LaTeX-output

flags or arguments(`delete`):

- document-name. Required

- comma separated list of chapters to delete. Required

- force === skip the confirmation prompt when cascading into a non-empty chapter (alias `-f`)

flags or arguments (`list`):

- document-name. Required

- songs === lists songs under chapters as well

#### 2.5.3 `songs`

- `add`
- `delete`
- `list`
- `modify`

flags or arguments (`add`):

- document-name. Required
- chapter-name. Required
- title === the **display** title of the song. Visible in the LaTeX-output. Required
- melody === the melody of the song. Visible in the LaTeX-output
- writer === the name of the writer of the song text. Visible in the LaTeX output
- text === the text of the song. Required. May be given as literal text, or as a path to an
  existing file, whose contents are then used as the song's text.
- number === the song's number: its position in the whole document (across all chapters, §2.3). If omitted, the song
  is assigned the next sequential number in the document (for `modify`: unchanged, also when moving to another
  chapter). If given an existing position, the songs from that position onward, in any chapter, shift up by one (§2.3).

flags or arguments (`delete`):

- document-name. Required
- song title. Required

flags or arguments (`list`):

- document-name. Required
- chapter name

flags or arguments (`modify`):

- document-name. Required
- song title. Required
- One (or more) of
    - title
    - writer
    - melody
    - chapter
    - number

An empty value for writer or melody (`--writer ""`) removes it from the song; it is then not printed and is left out
of the song's `.yml`. The same empty value given to `add` just means "none" (A-10).

## 3 Testing

Testing happens via NUnit. Files for testing are found in the /Tests folder.
Before implementing a feature, a failing test should already be present for the new feature. After implementing
the feature, create other failing and passing tests for the features.

## 4 LaTeX interface and main file template

The template-main.tex is there for you, the implementer, to base a class, 'DocumentTemplate', on.
The DocumentTemplate-class builds the main file for the final document. Anytime a chapter is added to the document,
insert it into the document. When the document is created, insert the relevant metadata - title, subtitle - into the
document.

### 4.1 Page metrics

The page size is A4. Specific metrics for the page, margins et c., 
are found in PageMetrics.png. The body font is Lexend at 12pt on 14.4pt leading.

SB plans the layout in whole lines and characters, and the numbers come from this geometry (A-12): a column holds 54
lines (53 planned, one line of safety), the first page of a chapter 44 (43 planned) because the `\chapter*` heading
takes 135.7pt, and a line holds 41 characters before SB wraps it. They were measured by compiling the template with
LuaLaTeX (see `LayoutDefaults` in `Sb.Core`), and have to be measured again if the page geometry or the font changes;
the unit tests check that the template still has the geometry they were measured from. A chapter title that takes more
than one line leaves less room on that first page than planned.

### 4.2 Chapters

Chapters can be defined in two ways in LaTeX: `\chapter{{name}}` or
`\chapter*{{name}}`. We use the latter in SB.

### 4.3 Songs
A page's two columns are minipages; the songs (or song fragments) placed in a column are rendered inside its
minipage.
Each song has at least a title, and that title is a LaTeX `\paragraph*{{name}}`, where `name` is the song's number
and title, `N. Title` (A-4). The text of the song is then inserted under the header as free text.

Because `\paragraph*` is a run-in heading (the following text would start on the same line), the title is
emitted as `\paragraph*{{N. Title}}\mbox{}\\` so that it occupies a line of its own. The song's header consists of
separate lines, in this order, all above the lyrics:

1. the title, prefixed with the song's number: `1. Title` (A-4)
2. the melody, prefixed `Mel: ` (only if the song has a melody)
3. the writer, prefixed `Text: ` (only if the song has a writer)

The header lines count towards the song's height when filling columns and pages (§2.1, §2.2), and
only the first fragment of a split song carries the header (see `addendums.md`, A-1). The header also includes the
one line of space SB emits before every title (`\vspace`, see A-13), so a
song's height is title + space + melody/writer lines + lyrics, in whole lines (A-12).

Within a fragment the lines are separated by `\\`, but the last line has no `\\` after it: a line break at the end
of a paragraph makes LaTeX add an empty line, which the planned line counts do not include (A-12).

When a song's content is split across a page boundary (§2.2), a continuation notice is inserted: one marker at the
end of the fragment that breaks off (`(forts.)`), and another at the start of the fragment that resumes
(also `(forts.)`). No notice is needed when a song only splits from column 1 to column 2
within the same page.

A song is only started in a column or page slot if at least one whole paragraph (the lines up to the first empty
line) fits there together with its header; otherwise it moves on to the next slot, so a column never ends with just a
title and a stub (see `addendums.md`, A-2). The empty line ending the paragraph need not fit, and an empty line
falling exactly on a break is dropped. A fresh, empty column 1 is exempt, so a song with an oversized first
paragraph still gets placed.

The `(forts.)` notice lines are rendered lines and count towards heights and line budgets like any other line
(A-5). The even-page rule holds in both ordering modes: a song that would spill out of an even page, but fits on one
page, moves whole to the next page.

### 4.4 Encoding

SB does not assume or hard-code a particular LaTeX engine. The generated `.tex` source must be compiled with a
UTF-8-capable setup (e.g. XeLaTeX/LuaLaTeX, or pdfLaTeX with `inputenc`/`fontenc`) in order to render Swedish
characters (å, ä, ö) correctly — this is a requirement on the user's own LaTeX toolchain, not something SB
detects or enforces.

## 5 Quality attributes

SB has two primary stakeholders: the **developer** building and maintaining it, and the **end user** creating
leaflets with it. The following attributes are prioritized for those two audiences.

### 5.1 Maintainability

SB's core behaviour — ordering, numbering, layout calculation, storage/database access, and LaTeX generation —
must not be architecturally locked to the CLI. It shall be implemented as a CLI-independent core, with the CLI
acting as one interchangeable front-end over it, so that another interface (GUI, web API, etc.) could be added
later by consuming the same core, without rewriting core logic.

The core must report success/failure to whatever front-end is consuming it via a structured result (or typed
exceptions), not via anything CLI-specific (e.g. writing straight to stdout/stderr or calling `Environment.Exit`
directly). The CLI front-end translates that structured result into process exit codes and console output; a
future GUI or API front-end would translate the same result differently.

### 5.2 Usability

End users should not need to retype information already inferable from context (e.g. the working directory), and
common flags should be quick to type (abbreviated aliases, §2.5).

### 5.3 Portability

SB should run wherever .NET 10 runs, and should correctly handle non-ASCII (UTF-8) content and paths regardless of
the host system's own encoding.

### 5.4 Reliability

The database must remain trustworthy even if files/folders are moved by the user outside of SB; any mismatch must
be surfaced to the user, not silently fixed or ignored (§2.4.3). Output must faithfully preserve the user's song
text and line breaks (§2.1).

### 5.5 Performance efficiency

Space-efficient layout calculation (§2.2) must stay tractable (polynomial, not factorial) as song counts grow.