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
update). Whatever small residual gap remains after that is corrected cosmetically by adding
`\vspace` to the end of the shorter column — vertical alignment is a finishing touch, not the
primary mechanism for using available space. The purpose of the vertical alignment is to have the two 
columns on a page start at the same line - LaTeX minipages automatically middle-align vertically, and this
is one way to augment that.

SB does not try to detect musical phrasing. The user's own line breaks are trusted as-is: one
input line becomes one output line. The only exception is a line too wide for the column
(especially likely in multi-column leaflets, since space is limited), which is broken across
multiple output lines rather than overflowing or being truncated.

### 2.2 Ordering

A few caveats to the ordering:
 - Songs may overflow from the first column to the second column.
 - On an ODD page (left-side page in the leaflet), a song may overflow from the second column to the next page.
 - This should never happen on an EVEN page (right-side page in the leaflet).

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
  to the page while they still fit. If this already fills 95% of the remaining space, it is used as-is.
- Otherwise, an exact subset-sum/bounded-knapsack search is run over the remaining songs against the page's
  remaining capacity (in lines), to find the subset whose total height best fills that capacity, subject to the
  two caveats.

The best-fit result aims to fill 95% of the remaining space on the page. Because this finds the best subset
directly, there is no permutation count to bound or tune, and no separate fallback algorithm is needed for an
exhausted search — if the remaining songs genuinely cannot reach 95% (e.g. too few songs are left in the chapter),
the best achievable fill for that page is used.

Essentially, the goal of the space-efficient algorithm is to need to insert as little \vspace{...} as possible.

### 2.3 Numbering

SB should assign a number to each song in a document, either in the order
they were given or in a specified order given by the user.

Numbers stay contiguous automatically: deleting a song shifts every later song's number down by one, and adding or
moving a song to an explicit position shifts every song from that position onward up by one. A song's number is
therefore always its current sequential position, not an independently stable identifier.

### 2.4 Storage

#### 2.4.1 Documents and chapters

Each document is stored in its own folder. Chapters are stored in a subfolder, /Chapters/{name}.
Songs are stored in the chapter folder. Documents and chapters are stored as .tex-files.

#### 2.4.2 Songs
The songs are stored as .yml-files, which are then later parsed when `sb documents build` is called.
The .yml for a song looks like this:
```
- title: {--title}
- writer: {--writer}
- melody: {--melody}
- number: {--number}
- text:
  - {line 1}
  - ...
  - {line N}
```

The document and chapter the song belongs to is inferred, since the songs is stored inside the chapter-folder
it belongs to, which in turn is stored inside the document-folder the chapter belongs to.

#### 2.4.3
Since files and folders may be saved wherever the user wants, the database should be the source of truth for fast lookup.
If a file or folder isn't where the database says it is, inform user about the mismatch. The database should contain:
"documents": path to the maindocument folder, document filename, internal id.
"chapters": chapter name, document id, internal id.                                                                     
"songs": song title, song number, chapter id, internal id

Mismatches of this kind are only expected to arise from changes made outside of SB. Any structural operation SB
itself performs (`create`, `add`, `rename`, `delete`, ...) must update the filesystem and the database together as
a single atomic step: if either half fails, the whole operation is rolled back so no orphaned file/folder or
dangling database row is left behind as a direct result of SB's own actions.



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

Any command that references a document/chapter name or song title which does not exist must fail with a clear error
naming what wasn't found, rather than failing silently or no-op'ing.

Since document/chapter names and song titles are used directly as folder/file names on disk, `create`/`add`/`rename`
must validate names against a safe character set and reject (with a clear error) any name containing path
separators, `..` segments, or other filesystem-illegal characters, rather than silently sanitizing them.

The following list describes the interface. The fields marked 'Required' should be command arguments, the others
flags

1. Via the CLI, a user can `create` an empty document.

2. The user can `add` chapters and songs to an existing document.

3. A user can `list` chapters and songs contained in a document, and documents created via the tool

4. A user can `modify` some song data, like title, melody and writer, directly through
   the CLI.

5. A user can `delete` a song from a chapter, and a chapter from a document

CLI arguments like 'document-name' or 'chapter name' are inferred if working directory is a
document/chapter directory.

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

- title === the **display** title of the song. Visible in the LaTeX-output. Required
- melody === the melody of the song. Visible in the LaTeX-output
- writer === the name of the writer of the song text. Visible in the LaTeX output
- text === the text of the song. Required. May be given as literal text, or as a path to an
  existing file, whose contents are then used as the song's text.
- number === the song's position/number in the chapter. If omitted, the song is assigned the next
  sequential number. If given an existing position, the songs from that position onward shift up by one (§2.3).

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
are found in PageMetrics.png.

### 4.2 Chapters

Chapters can be defined in two ways in LaTeX: `\chapter{{name}}` or
`\chapter*{{name}}`. We use the latter in SB.

### 4.3 Songs
Songs are contained in minipages, which act like columns.
Each song has atleast a title, and that title is a LaTeX 
`\paragraph*{{name}}`. The text of the song is then inserted under
the title as free text.

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