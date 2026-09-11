# SB - Sångblad specification

This is the initial document which functions as the source of truth for the project. Subject to change.
SB is built with C#, .NET 10

## 1 Goal

SB should make it easier to create song leaflets (sångblad).
It is an interface between the user and LaTeX. The goal is to seamlessly take 
(a list of) songs that are in text-form and transform the into either a multi-column
leaflet or a single-column leaflet of the songs, and format them, order them,
and number them.

## 2 Functionality (high-level)

### 2.1 Formatting

SB should have two formatting options; multi-column and single-column.
The multi-column option is achieved by using LaTeX's \minipage-command.
The single-column is default LaTeX behaviour.

Multi-column formatting is achieved via LaTeX minipages. Proper vertical alignment between two or more
columns is achieved by adding `\vspace` to the end of the shortest minipage.

Formatting songs unfortunately requires some knowledge of how the songs are phrased. The program
should be able to make a best-effort guess on how the song is phrased
(if the user requests it). Generally, a musical phrase in terms of songs text is as
long as a subsentence (until comma), a sentence or simply a whole line.
The output of SB should generally be one phrase = one line. Sometimes, this is not possible,
especially in multi-column leaflets, since space is limited.



### 2.2 Ordering

SB should, based on user input, order the songs in the most space-efficient
way, or in the order that the user lists them. See section *N*.

### 2.3 Numbering

SB should assign a number to each song in a document, either in the order
they were given or in a specified order given by the user

### 2.4 Config

The application config is a separate .yml-file that the user can modify.
It contains mostly LaTeX constants and commands, or a shorthand for them (preamble).
The program should be able to validate the fields in the config, read them into memory
on start-up, and use them when building the LaTeX-code.

### 2.5 Storage

Each document is stored in its own folder. Chapters are stored in a subfolder, /Chapters/{name}.
Songs are stored in the chapter folder. Documents and chapters are stored as .tex-files.
The songs are stored as .txt-files, which are then later parsed when `sb document build` is called.
Songs are stored in .yml-format, based on the information gathered/inferred when they were added.



### 2.6 CLI

Each document will generally
contain only one chapter which houses all the songs, but for the sake of
completeness, songs can be organised into different chapters.

The following list describes the interface, but it is up to the programmer to decide whether 
something should be a flag or an argument

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

7. A user can `add` images such as scores to the document. These images should be
linked to a particular song, or a song in themselves.

CLI arguments like 'document name' or 'chapter name' are inferred if working directory is a 
document/chapter directory.

#### 2.6.1 `documents`

##### 2.6.1.1 Subcommands

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
- table of contents
- output folder
- format === [Ss]ingle-column | [Mm]ulti-column

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
- max-permutations === default 100?
- other relevant stuff? open to suggestions

#### 2.6.2 `chapters`

##### 2.6.2.1 Subcommands

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

#### 2.6.3 `songs`

- `add`
- `remove`
- `list`
- `modify`

flags or arguments (`add`):

- title === the **display** title of the song. Visible in the LaTeX-output. Required
- melody === the melody of the song. Visible in the LaTeX-output. Required
- writer === the name of the writer of the song text. Visible in the LaTeX output
- text === the text of the song. Required

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

#### `scores`

- `add`
- `list`
- `remove`

flags or arguments (`add`):

- song name. Required
- remove {n} verses from text present in score

flags or arguments (`list`):

- document name. Required
- chapter name

flags or arguments (`remove`):

- document name. Required
- song name. If not set, removes all scores from 

#### 2.6.5 `config`

The config command modifies or adds lines to the global config.yml.

Until further notice the config will contain only LaTeX specific rules.

examples 

`sb config set pt 12`

`sb config set margin 0.4in`

## 3 Testing

Testing happens via XUnit. Files for testing are found in the /Tests folder.