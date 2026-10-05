# SB (Sångblad)

SB turns song lyrics into formatted, print-ready song leaflets — an interface between the user
and LaTeX. Give it a list of songs and it handles the layout (single- or multi-column), the
physical ordering (as listed, or automatically for space efficiency), and numbering (the order
songs should be sung in, independent of where they land on the page).

See [`sb/specification.md`](sb/specification.md) for the full product specification.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build & test

```powershell
dotnet build sb.sln
dotnet test sb.sln
```

## Usage

The CLI is organized around documents, which contain chapters, which contain songs.

```powershell
# Create a document (--path is optional; defaults under the current directory)
dotnet run --project sb -- documents create --name sommarvisor --title "Sommarvisor" --format multi-column

# Add a chapter
dotnet run --project sb -- chapters add --document-name sommarvisor --name main

# Add songs (--singing-number is the order they should be sung in, not their page position)
dotnet run --project sb -- songs add --document-name sommarvisor --chapter-name main `
  --title "Idas sommarvisa" --text "..." --singing-number 1
dotnet run --project sb -- songs add --document-name sommarvisor --chapter-name main `
  --title "Barfotavisan" --text "..." --melody "Trad." --singing-number 2

# Build the leaflet (--ordering: as-listed | space-efficient)
dotnet run --project sb -- documents build --document-name sommarvisor --ordering space-efficient

# List everything, including any detected location mismatches
dotnet run --project sb -- documents list --chapters --songs

# Configure LaTeX constants (known keys: pt, margin)
dotnet run --project sb -- config set pt 12
```

Run `dotnet run --project sb -- <command> --help` for full flag details on any command
(`documents`, `chapters`, `songs`, `config`).

> **Non-ASCII arguments from a `.bat`/`cmd` script**: if you invoke `sb` from a batch script with
> literal non-ASCII text (e.g. å/ä/ö) as an argument, the script's own saved encoding must match
> the console's active code page (e.g. via `chcp 65001`) — SB cannot recover an argument that a
> misconfigured invoking script already corrupted before `sb.exe` even started.

Documents may live anywhere on disk — SB tracks their location in a local SQLite index (under
`%APPDATA%/sb/`) rather than assuming a fixed folder layout, and will tell you if a document's
files have gone missing or moved.

## Project structure

```text
sb/
├── Formatting/    phrase splitting, LaTeX escaping, line wrapping
├── Ordering/      physical song ordering (as-listed / space-efficient)
├── Leaflets/      the LeafletBuilder engine + LaTeX template handling
├── Documents/     Chapters/  Songs/    entities + filesystem stores
├── Index/         the SQLite index and location-mismatch detection
├── Config/        config.yml loading/validation
├── Cli/           System.CommandLine wiring
├── Tests/         NUnit tests (incl. Tests/TestFiles/ real-song fixtures)
├── template-main.tex   master LaTeX document template
└── config.yml     global LaTeX configuration
```

## Spec-driven development

This project is developed with [GitHub Spec Kit](https://github.com/github/spec-kit):

- [`.specify/memory/constitution.md`](.specify/memory/constitution.md) — governing principles
  (readability, maintainability, test-first development).
- [`specs/001-song-leaflet-generation/`](specs/001-song-leaflet-generation/) — the feature
  specification, implementation plan, and task breakdown this codebase implements.

## Testing philosophy

Per the project constitution, all new behavior is developed test-first (NUnit) and real fixture
songs under `sb/Tests/TestFiles/` are used as golden-style input alongside synthetic edge cases.
