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

The space-efficient algorithm selects the chapters longest song and places it first. It then searches 
for a best-fit song for every remaining song.
The space-efficient algorithm operates on a per-page basis. It aims to fill as much of both columns as possible. 
The two caveats apply. Every new page starts with the longest remaining song, and then fills in the columns
with remaining songs. 

The best-fit algorithm aims to fill 95% of the remaining space on the page. It goes through `--max-permutations`
permutations for the remaining songs for each page, if `--max-permutations` is set; 
otherwise, it goes through all possible permutations for the remaining songs.

Essentially, the goal of the space-efficient algorithm is to need to insert as little \vspace{...} as possible.

### 2.3 Numbering

SB should assign a number to each song in a document, either in the order
they were given or in a specified order given by the user

### 2.4 Storage

#### 2.4.1 Documents and chapters

Each document is stored in its own folder. Chapters are stored in a subfolder, /Chapters/{name}.
Songs are stored in the chapter folder. Documents and chapters are stored as .tex-files.

#### 2.4.2 Songs
The songs are stored as .yml-files, which are then later parsed when `sb document build` is called.
The .yml for a song looks like this:
```
- title: {--title}
- writer: {--writer}
- melody: {--melody}
- number: {--singing-number}
- text:
  - {line 1}
  - ...
  - {line N}
- score: #Optional
  - {score_filename}
```

The document and chapter the song belongs to is inferred, since the songs is stored inside the chapter-folder
it belongs to, which in turn is stored inside the document-folder the chapter belongs to.

#### 2.4.3
Since files and folders may be saved wherever the user wants, the database should be the source of truth for fast lookup.
If a file or folder isn't where the database says it is, inform user about the mismatch. The database should contain:
"documents": path to the maindocument folder, document filename, internal id.
"chapters": chapter name, document id, internal id.                                                                     
"songs": song name, song number, chapter id, internal id



### 2.5 CLI

Each document will generally
contain only one chapter which houses all the songs, but for the sake of
completeness, songs can be organised into different chapters.

When the CLI parses a file path, that path should be parsed as UTF-8, even if the command line used
doesn't.

All commands' flags/options should have an abbreviated alias

The following list describes the interface. The fields marked 'Required' should be command arguments, the others
flags

1. Via the CLI, a user can `create` an empty document.

2. The user can `add` chapters and songs to an existing document.

3. A user can `list` chapters and songs contained in a document, and documents created via the tool

4. A user can `modify` some song data, like title, melody and writer, directly through
   the CLI.

5. A user can `remove` a song from a chapter, and a chapter from a document

6. A user can `replace` a song in a chapter. When replacing a song, the new song
   will have the same cardinal number as the song replaced, meaning it will either
   be in the same place in the document and/or have the same number in the index and before
   its title

CLI arguments like 'document name' or 'chapter name' are inferred if working directory is a
document/chapter directory.

#### 2.5.1 `documents`

##### 2.5.1.1 Subcommands

- `create`, creates an 'empty' document and repository
- `list`, lists documents created using this program
- `delete`, deletes the document repository
- `rename`, renames the document and its repository
- `build`, builds the document, calculating the placement of the songs. This is the main function
  of the program.

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

- document name. Required

flags or args (`rename`):

- document_name. Required
- new_name. Required

flags or args (`build`):

- document-name. Required
- ordering === song number-wise or by space-efficiency
- max-permutations === default `count(songs) factorial`
 - [Ss]ingle-column | [Mm]ulti-column

#### 2.5.2 `chapters`

##### 2.5.2.1 Subcommands

- `add`, creates an empty chapter in a document.
- `remove`, removes a chapter from a document
- `list`, lists chapters in a document

flags or arguments (`add`):

- document_name. Required

- name === the name of the file ( default {document}\_chp\_{n} )

- title === the **display** title of the chapter. Visible in the LaTeX-output

flags or arguments(`remove`):

- document_name. Required

- comma separated list of chapters to remove. Required

flags or arguments (`list`):

- document_name. Required

- songs === lists songs under chapters as well

#### 2.5.3 `songs`

- `add`
- `remove`
- `list`
- `modify`

flags or arguments (`add`):

- title === the **display** title of the song. Visible in the LaTeX-output. Required
- melody === the melody of the song. Visible in the LaTeX-output
- writer === the name of the writer of the song text. Visible in the LaTeX output
- text === the text of the song. Required. May be given as literal text, or as a path to an
  existing file, whose contents are then used as the song's text.

flags or arguments (`remove`):

- document name
- song name

flags or arguments (`list`):

- document name. Required
- chapter name

flags or arguments (`modify`):

- document name. Required
- song name. Required
- One (or more) of
    - title
    - writer
    - melody
    - chapter
    - ordinal number

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