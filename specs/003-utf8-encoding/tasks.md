# Tasks: UTF-8 Encoding Throughout

**Input**: Design documents from `specs/003-utf8-encoding/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md

**Tests**: Included — this project's constitution (Principle III, NON-NEGOTIABLE) requires a
failing test before the corresponding production code.

**Organization**: Tasks are grouped by user story (spec.md's US1/US2/US3) to enable independent
implementation and testing of each.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1/US2/US3)
- File paths are exact, relative to the repository root

---

## Phase 1: Setup

**Purpose**: The Windows application-manifest half of research.md §1's fix — a project-level
artifact, not tied to any single user story.

- [X] T001 [P] Add `sb/app.manifest` (research.md §1): a standard Win32 manifest declaring
  `<application xmlns="urn:schemas-microsoft-com:asm.v3"><windowsSettings
  xmlns:ws2="http://schemas.microsoft.com/SMI/2019/WindowsSettings"><ws2:activeCodePage>UTF-8</ws2:activeCodePage></windowsSettings></application>`
  inside the usual `<assembly>` root (`xmlns="urn:schemas-microsoft-com:asm.v1"`,
  `manifestVersion="1.0"`), so the published `sb.exe` uses code page 65001 for legacy ANSI/OEM
  Win32 calls it makes directly. **Scope note (research.md §1, corrected 2026-09-27)**: this does
  NOT fix command-line argument decoding — empirical testing showed .NET already receives
  non-ASCII arguments correctly on this platform without it. It is kept as free defense-in-depth
  and because it is the correct mechanism for FR-005 (console output display).
- [X] T002 Reference the manifest from `sb/sb.csproj` (depends on T001): add
  `<ApplicationManifest>app.manifest</ApplicationManifest>` to the existing `<PropertyGroup>`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The `dotnet run`/`dotnet test` half of research.md §1's fix — the manifest above only
applies to the published apphost exe, so an explicit, testable code-level fallback is required for
every other way this CLI runs. This delivers FR-005 (console *output* display correctness) and
matching defense-in-depth for input; command-line *argument* decoding itself is already correct by
default on this platform (research.md §1) and does not depend on this phase. US3 (Phase 5) depends
directly on this; US1/US2 depend on it only for the same defense-in-depth reasoning, not because
their own correctness requires it.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 Add `internal static void ConfigureUtf8Console()` to `sb/Program.cs` (research.md §1):
  sets `Console.OutputEncoding = Encoding.UTF8;` and `Console.InputEncoding = Encoding.UTF8;`,
  wrapped in a `try`/`catch` that swallows any exception (redirected stdout/stdin — e.g. when
  piped, or under a test runner — can throw when setting these; the CLI must never crash because
  of it). Call it as the very first statement in `Main`, before anything else runs.
- [X] T004 [P] Add `sb/Tests/ConsoleEncodingTests.cs` (depends on T003): call
  `Program.ConfigureUtf8Console()` and assert `Console.OutputEncoding.CodePage == 65001` and
  `Console.InputEncoding.CodePage == 65001` afterward; save and restore the original
  `Console.OutputEncoding`/`InputEncoding` in a `finally` block so this test never leaks changed
  console state into other tests in the same run.

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Add a song with non-ASCII title, lyrics, or file path (Priority: P1) 🎯 MVP

**Goal**: A `--text` value naming an existing file whose path contains a non-ASCII character
(e.g. å/ä/ö) is correctly recognized as that file and read, instead of being misread as literal
text due to a Unicode normalization-form (NFC/NFD) mismatch, and every file SB reads is decoded as
UTF-8. (The *originally-reported instance* of this symptom was traced to a `.bat` script's own
encoding mismatch — out of scope, see spec.md's Clarifications/Assumptions; this story closes the
independent, in-SB's-control normalization-mismatch case.)

**Independent Test**: Supply `songs add` a `--text` value that is the path to an *existing* file
whose path contains a non-ASCII character, and verify the system reads that file's contents, not
the literal path string.

### Tests for User Story 1 ⚠️

> Write these tests FIRST, ensure they FAIL before implementation

- [X] T005 [P] [US1] Add `sb/Tests/PathTextTests.cs`: `PathText.NormalizeForFileSystem` returns
  its input unchanged when already NFC-normalized (e.g. "å" as the single precomposed code point
  U+00E5); given an NFD-decomposed input representing the same visible character (base letter
  U+0061 + combining ring above U+030A), it returns a string equal to the NFC form — assert the
  two differently-normalized inputs produce equal output after normalization.
- [X] T006 [P] [US1] Extend `sb/Tests/Cli/SongCommandsTests.cs` with
  `Add_WithANonAsciiExistingFilePathAsText_ReadsItsContentsAsTheLyricText`: write a temp file whose
  path contains "å" (e.g. `Path.Combine(_tempRoot, "hon_var_så_liten.txt")`) with known lyric
  content, call `SongCommands.Add` with that path as `--text`, and assert the stored song's text
  matches the file's content. This validates the NFC/NFD normalization defense (research.md §4) —
  it does not, and cannot, reproduce the originally-reported `.bat`-script scenario, which happens
  in a parent process no in-process test can exercise.

### Implementation for User Story 1

- [X] T007 [US1] Add `PathText.NormalizeForFileSystem` (`sb/Formatting/PathText.cs`, research.md
  §4): `public static string NormalizeForFileSystem(string path) => path.Normalize(NormalizationForm.FormC);`
  make T005 pass.
- [X] T008 [US1] Update `SongCommands.ResolveText` (`sb/Cli/SongCommands.cs`, depends on T007):
  normalize the `text` parameter via `PathText.NormalizeForFileSystem` before the
  `File.Exists`/`File.ReadAllText` check; the literal-text fallback (when the value isn't a path)
  MUST still return the original, un-normalized string, never the normalized one (research.md §4);
  make T006 pass.
- [X] T009 [US1] Add explicit `Encoding.UTF8` to `DocumentTemplate.Load`'s `File.ReadAllText` call
  (`sb/Leaflets/DocumentTemplate.cs`, research.md §2).
- [X] T010 [P] [US1] Add explicit `Encoding.UTF8` to every remaining text-file read per research.md
  §2's list: `ChapterStore`'s `chapter.yml` read (`sb/Chapters/ChapterStore.cs`), `SongStore`'s
  `<song>.yml` read (`sb/Songs/SongStore.cs`), `LocationMismatchDetector`'s `.yml` read
  (`sb/Index/LocationMismatchDetector.cs`), and `DocumentStore`'s `<name>.tex` read for
  title/subtitle recovery (`sb/Documents/DocumentStore.cs`).
- [X] T011 [US1] Extend `sb/Tests/DocumentTemplateTests.cs`, `sb/Tests/SongStoreTests.cs`, and
  `sb/Tests/ChapterStoreTests.cs` with a round-trip case each: write, then read back, text
  containing å/ä/ö (reuse this project's own real fixture content — e.g. a line from
  `sb/Tests/TestFiles/hon_var_så_liten.txt`) and assert the read result exactly matches what was
  written.

**Checkpoint**: User Story 1 is fully functional and independently testable — the original defect
no longer reproduces.

---

## Phase 4: User Story 2 - Build a leaflet containing non-ASCII text (Priority: P2)

**Goal**: Every file SB writes is encoded as UTF-8 (no BOM), so a leaflet built from songs whose
text contains non-ASCII characters reproduces them correctly in the generated `.tex` output.

**Independent Test**: Build a leaflet from a chapter containing a real Swedish-language song
fixture and verify every non-ASCII character in the source text appears correctly (not replaced
or garbled) in the generated `.tex` output.

### Tests for User Story 2 ⚠️

- [X] T012 [P] [US2] Extend `sb/Tests/LeafletBuilderTests.cs` with a case that builds a leaflet
  from a chapter containing a song whose text is `sb/Tests/TestFiles/hon_var_så_liten.txt`'s real
  content, and asserts the produced `chapter.tex` content contains every non-ASCII character from
  that fixture (å/ä/ö) exactly, uncorrupted.

### Implementation for User Story 2

- [X] T013 [US2] Add explicit `new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)` to every
  `File.WriteAllText` call site per research.md §3: `ChapterStore`'s `chapter.yml` write
  (`sb/Chapters/ChapterStore.cs`), `DocumentStore`'s `<name>.tex` write
  (`sb/Documents/DocumentStore.cs`), `DocumentCommands`'s `chapter.tex` and main-document writes
  (`sb/Cli/DocumentCommands.cs`), and `SongStore`'s `<song>.yml` write (`sb/Songs/SongStore.cs`);
  make T012 pass.
- [X] T014 [P] [US2] Extend `sb/Tests/DocumentStoreTests.cs`, `sb/Tests/ChapterStoreTests.cs`,
  `sb/Tests/SongStoreTests.cs`, and `sb/Tests/Cli/DocumentCommandsTests.cs` with a write-then-read
  round-trip case for non-ASCII content each, complementing T011's read-side coverage with
  write-side coverage for every file this project persists.

**Checkpoint**: User Stories 1 and 2 both work independently.

---

## Phase 5: User Story 3 - Read CLI output containing non-ASCII text (Priority: P3)

**Goal**: Command output (e.g. `documents list`) displays previously-stored non-ASCII text
correctly.

**Independent Test**: Create a document/song with a non-ASCII title and run a read command (e.g.
`list`), verifying the displayed text matches what was entered.

### Tests for User Story 3 ⚠️

- [X] T015 [P] [US3] Add `sb/Tests/Cli/ConsoleOutputEncodingTests.cs`: create a document and a song
  whose titles contain non-ASCII characters (e.g. "Hon var så liten"), call
  `DocumentCommands.List`/`SongCommands.List`, and assert the returned strings contain those exact
  characters unmodified — confirms the in-memory string is correct end-to-end from storage through
  to the point of display, independent of the actual terminal (byte-level display correctness is
  already covered by T004's `Console.OutputEncoding` check).

### Implementation for User Story 3

- [X] T016 [US3] Audit `sb/Cli/DocumentCommands.cs`, `sb/Cli/ChapterCommands.cs`, and
  `sb/Cli/SongCommands.cs`'s `List` methods: confirm each builds its output purely via C# string
  interpolation/concatenation (already UTF-16-correct in memory) with no separate encoding step of
  its own. If any call site *does* perform its own encoding conversion, fix it there; otherwise
  this task is a verification-only pass (Phase 2's `ConfigureUtf8Console` is what makes the actual
  console byte output correct) — make T015 pass either way.

**Checkpoint**: All three user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final end-to-end validation, plus documenting the one scenario this feature
deliberately does not (and cannot) fix.

- [X] T017 Run `specs/003-utf8-encoding/quickstart.md`'s manual validation steps end-to-end
  (`documents create`/`songs add`/`documents build`/`documents list`) using
  `sb/Tests/TestFiles/hon_var_så_liten.txt`, confirming the song's real lyrics appear in the built
  `.tex`, and its title displays correctly via `documents list`.
- [X] T018 [P] Add a short caveat to `README.md`'s Usage section (or wherever the current, non-
  stale command syntax is documented at implementation time — the README's example commands
  predate several since-changed CLI flags and are due their own separate cleanup, out of scope
  here): invoking `sb` from a `.bat`/`cmd` script with non-ASCII characters as literal text
  requires the script's saved encoding to match the console's active code page (e.g. via
  `chcp 65001`) — SB cannot recover an argument already corrupted by a misconfigured invoking
  script (spec.md's Assumptions, research.md §1).
- [X] T019 `dotnet test` full suite green.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Independent of Phase 1 (different files: `Program.cs` vs.
  `app.manifest`/`sb.csproj`), but BLOCKS all user stories below (research.md §1 explains why both
  halves of the fix are needed).
- **User Story 1 (Phase 3)**: Depends on Foundational only. No dependency on US2/US3.
- **User Story 2 (Phase 4)**: Depends on Foundational only. Independent of US1 (different files —
  US1 touches read call sites, US2 touches write call sites) but naturally follows it since a
  leaflet must first be built from correctly-read song content.
- **User Story 3 (Phase 5)**: Depends on Foundational only (the actual console-encoding fix lives
  there); independent of US1/US2's own files.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### Within Each User Story

- Tests are written first and MUST fail before implementation (Constitution Principle III).
- Helper/entity code (e.g. `PathText`) before the component that consumes it.

### Parallel Opportunities

- T001 (Setup) can run alongside Phase 2's tasks (different files).
- Once Phase 2 is done, US1/US2/US3's test-writing tasks (T005-T006, T012, T015) can all run in
  parallel with each other (different files, no shared dependency), and since no story depends on
  another, their implementation tasks can proceed in parallel too if staffed by different people.
- T010 within US1, and T014 within US2, are each internally parallelizable (independent files).

---

## Parallel Example: User Story 1

```bash
# Launch both failing tests for User Story 1 together:
Task: "Write failing tests for PathText in sb/Tests/PathTextTests.cs"
Task: "Write a failing regression test for the non-ASCII --text bug in sb/Tests/Cli/SongCommandsTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 + Phase 2 (both required before any story).
2. Complete Phase 3 (User Story 1) — this alone closes the originally-reported defect.
3. **STOP and VALIDATE**: run T006's regression test and confirm it passes.
4. Ship if ready — US2/US3 are valuable correctness hardening, not required to close the bug report.

### Incremental Delivery

1. Setup + Foundational → console/argv encoding correct everywhere.
2. Add User Story 1 → the reported defect is closed → validate → ship (MVP).
3. Add User Story 2 → generated leaflets are UTF-8-correct too → validate → ship.
4. Add User Story 3 → console output correctness has explicit test coverage → validate → ship.

---

## Notes

- [P] tasks = different files, no dependencies.
- [Story] label maps task to specific user story for traceability.
- Verify tests fail before implementing.
- This feature changes no CLI surface, storage format, or leaflet-builder contract from
  `001-song-leaflet-generation` — every task above is either a new small helper/manifest or an
  encoding-argument addition to an existing call site.
