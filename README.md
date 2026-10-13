# sb

`sb` is a command-line tool that turns songs into LaTeX source for song leaflets (*sångblad*). You keep songs in
chapters inside a document; `sb documents build` calculates where every song goes on the page and writes the
`.tex` files, which you then compile with your own LaTeX setup.

## Documentation

| File | What it holds |
| --- | --- |
| `specification.md` | What the program does and why |
| `requirements.md` | Numbered functional (FR) and non-functional (NFR) requirements |
| `addendums.md` | Changes and bug fixes made after the original design, newest last; where it disagrees with an older section, the addendum wins |
| `TASKS.md` | The milestone-by-milestone implementation plan and its status |

## Projects

- `Sb.Core` — all behaviour: songs, layout/pagination, LaTeX emission, storage (SQLite index plus the files on disk).
  Knows nothing about the command line.
- `Sb.Cli` — the `sb-cli` executable: argument parsing and turning results into console output and exit codes.
- `Sb.Tests` — NUnit tests for both.

## Build, test, install

```
dotnet build
dotnet test
dotnet publish Sb.Cli\Sb.Cli.csproj -p:PublishProfile=LocalInstall
```

The last command (Windows) publishes a self-contained `sb-cli.exe` into `%APPDATA%\sb-cli\` and adds that folder to
your user `PATH`, so `sb-cli` works from any shell. Re-running it updates the executable in place.

## Where things live

- A document is a folder: `<output folder>/<document>/` with the main `.tex`, and `Chapters/<chapter>/` folders
  holding each chapter's `.tex` and one `<song title>.yml` per song.
- A SQLite index at `%APPDATA%\sb-cli\sb.db` records which documents, chapters and songs exist. Set the environment
  variable `SB_DATA_DIR` to use a different folder for it (for example to keep separate sets of documents).

## Usage

```
sb-cli documents create <name> <title> [-s subtitle] [-o output-folder]
sb-cli documents list [-c] [-s]
sb-cli documents rename [document-name] <new-name>
sb-cli documents delete [document-name] [-f]
sb-cli documents build [document-name] [-o as-listed|space-efficient] [-f multi-column|single-column]

sb-cli chapters add [document-name] [-n name] [-t title]
sb-cli chapters list [document-name] [-s]
sb-cli chapters delete [document-name] <chapter[,chapter...]> [-f]

sb-cli songs add [document-name] [chapter-name] <title> <text or path to a text file> [-m melody] [-w writer] [-n number]
sb-cli songs list [document-name] [-c chapter-name]
sb-cli songs modify [document-name] <song-title> [-t title] [-w writer] [-m melody] [-c chapter] [-n number]   (-w "" / -m "" remove the writer / melody)
sb-cli songs delete [document-name] <song-title>
```

Arguments in `[square brackets]` that name a document or chapter are inferred when you run the command from inside
that document's (or chapter's) folder. Song numbers run through the whole document, across its chapters.

Exit codes: `0` success, `1` validation error (also bad arguments), `2` conflict (the name already exists, or a file
would be overwritten), `3` not found, `4` storage / file system / database error.

## Compiling the output

`build` only writes LaTeX source. Compile the generated main `.tex` with a UTF-8-capable engine (XeLaTeX or LuaLaTeX,
or pdfLaTeX with `inputenc`/`fontenc`) so that å, ä and ö come out right.
