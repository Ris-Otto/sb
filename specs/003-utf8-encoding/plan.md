# Implementation Plan: UTF-8 Encoding Throughout

**Branch**: `003-utf8-encoding` | **Date**: 2026-09-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-utf8-encoding/spec.md`

## Summary

Make UTF-8 correctness explicit end-to-end (command-line/console I/O, file read/write encoding,
and file-existence checks for non-ASCII paths), motivated by — but, per a 2026-09-27 correction
(spec.md Clarifications), not solely explained by — a real, observed defect: a `--text` file path
containing "å" was not recognized as an existing file, and its raw, mis-decoded path string was
used as the song's literal text instead. Empirical testing during planning showed direct
command-line invocation on this platform already delivers non-ASCII arguments to .NET correctly;
the user confirmed the originally-observed instance was actually caused by invoking `sb` through a
`.bat` script whose own encoding mismatched its console's active code page — corruption that
happens upstream of SB's process and that no in-process fix can recover from (research.md §1).
This plan therefore delivers two things with different justifications: (1) explicit UTF-8 file
read/write encoding and a Unicode normalization-form (NFC/NFD) defense for `--text` path
detection — both genuinely in SB's control and independently worthwhile (research.md §2/§3/§4);
and (2) a Windows application-manifest `activeCodePage=UTF-8` setting plus explicit
`Console.OutputEncoding`/`InputEncoding` — kept as the correct, standard fix for console *output*
display correctness (FR-005) and as free defense-in-depth, not as a claimed fix for the
originally-reported argument-corruption instance (research.md §1). No new dependency is required
anywhere. Audited every `File.ReadAllText`/`File.WriteAllText`/`Console.Write*` call site in `sb/`
(research.md §1-§3).

## Technical Context

**Language/Version**: C# targeting .NET 10 (unchanged from `001-song-leaflet-generation`).

**Primary Dependencies**: None new. Uses only `System.Text.Encoding` (BCL) and a Windows
application manifest (`app.manifest`, a plain XML resource — no package dependency) to set the
process's active code page (research.md §1).

**Storage**: Unchanged (SQLite index + per-song `.yml` files + rendered `.tex` files, per
`001-song-leaflet-generation`) — this feature only changes *how* those files' text is
encoded/decoded, never their format or location.

**Testing**: NUnit (existing), extended with round-trip tests using real non-ASCII content
(reusing this project's own Swedish-language fixtures) and a regression test for the specific
`--text` existing-file-detection bug (research.md §4).

**Target Platform**: Windows (the observed bug, and this project's only current target — see
`001-song-leaflet-generation`'s `LocalInstall` publish profile, which builds a Windows-only
self-contained `sb.exe`). The application-manifest fix (research.md §1) is Windows-specific;
Console encoding and file I/O fixes are cross-platform and harmless on any OS.

**Performance Goals**: N/A — this is a correctness fix, not a performance-sensitive change.

**Constraints**: MUST NOT change any existing file format, CLI surface, or stored data shape
(spec.md's Assumptions) — only the encoding used to read/write/display text that was already
being read/written/displayed.

**Scale/Scope**: Applies uniformly to every text value already in scope per
`001-song-leaflet-generation` (titles, lyrics, writer/melody, document/chapter names, file paths,
CLI output) — no new entities or storage.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Readability First**: PASS. The fix is a small, well-named, centralized set of changes
  (one `app.manifest`, one `Console` setup call, explicit `Encoding.UTF8` arguments at existing
  I/O call sites) — no new abstraction obscuring intent.
- **II. Maintainability & Simplicity**: PASS. No new dependency (Principle II: a dependency is
  only justified to replace non-trivial hand-rolled logic — this needs neither, `System.Text.Encoding`
  is already in the BCL). No abstraction layer introduced; each I/O call site is fixed directly.
- **III. Test-First Development (NON-NEGOTIABLE)**: PASS (procedural gate) — every fix below is
  scoped so a failing test (using real non-ASCII content) can be written first.
- **IV. Comprehensive Test Coverage**: PASS. Reuses this project's own real Swedish-language
  fixtures (`sb/Tests/TestFiles/*.txt`, already containing å/ä/ö) as the natural non-ASCII test
  data — no new fixture format needed.
- **V. Consistent Style & Documentation**: PASS. No new public CLI surface; existing commands'
  documented behavior is unchanged, only made correct for non-ASCII input.

No violations requiring justification — Complexity Tracking table is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/003-utf8-encoding/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── quickstart.md         # Phase 1 output (/speckit-plan command)
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

No `data-model.md` or `contracts/` — this feature introduces no new entities and no new external
interface (CLI commands, storage format, and the leaflet-builder API are all unchanged per
`001-song-leaflet-generation`'s own contracts; only their internal encoding handling changes).

### Source Code (repository root)

```text
sb/                             # existing single CLI project (sb.csproj), unchanged structure
├── app.manifest                 # NEW — sets <activeCodePage>UTF-8</activeCodePage> (research.md §1)
├── sb.csproj                    # add <ApplicationManifest>app.manifest</ApplicationManifest>
├── Program.cs                   # add Console.OutputEncoding/InputEncoding = Encoding.UTF8 setup
├── Formatting/
│   └── PathText.cs              # NEW — small helper: Unicode-normalize a path before File.Exists
│                                    (research.md §4), used by SongCommands.ResolveText
├── Cli/
│   └── SongCommands.cs           # ResolveText: normalize the --text value before File.Exists/ReadAllText
├── Leaflets/
│   └── DocumentTemplate.cs       # File.ReadAllText(templatePath, Encoding.UTF8) — explicit
├── Documents/DocumentStore.cs    # File.ReadAllText/WriteAllText(..., Encoding.UTF8) — explicit, at each call site listed in research.md §1
├── Chapters/ChapterStore.cs      # same, for chapter.yml
├── Songs/SongStore.cs            # same, for <song>.yml
├── Index/LocationMismatchDetector.cs # same, for reading a song's .yml to check its number
└── Tests/
    ├── PathTextTests.cs                    # NEW — Unicode-normalization round-trip tests
    ├── SongCommandsTests.cs                # extend: --text existing-file detection with a non-ASCII path (closes the observed bug)
    └── (extend existing Store/DocumentTemplate tests with non-ASCII round-trip cases)
```

**Structure Decision**: Single-project structure is retained (matching
`001-song-leaflet-generation`). No new top-level folder — this feature touches existing
`Leaflets/`, `Documents/`, `Chapters/`, `Songs/`, `Index/`, and `Cli/` files at their existing I/O
call sites, plus one new tiny `Formatting/PathText.cs` helper (Principle II: a single, concrete
static method, not a new abstraction layer) and one new non-C# resource (`app.manifest`).

## Complexity Tracking

*No violations — table omitted.*
