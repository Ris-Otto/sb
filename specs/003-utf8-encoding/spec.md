# Feature Specification: UTF-8 Encoding Throughout

**Feature Branch**: `003-utf8-encoding`

**Created**: 2026-09-27

**Status**: Draft

**Input**: User description: "The program should use UTF-8 encoding"

## Clarifications

### Session 2026-09-27

- Q: `/speckit-analyze` flagged that the app-manifest/console-encoding fix originally proposed for
  FR-001 doesn't hold up empirically — direct testing (PowerShell and git-bash, no fixes applied)
  showed .NET already receives non-ASCII command-line arguments correctly on this platform. What
  actually caused the originally-observed corruption? → A: The reporting user confirmed they
  invoked `sb` via a `.bat` script with the song's title/file path as string literals in the
  script, not typed directly. `cmd.exe` parses a batch file's own bytes using the *console's
  active code page*, not UTF-8 — if the `.bat` file was saved as UTF-8 while the console was on a
  different code page, the two-byte UTF-8 sequence for "å" gets reinterpreted as two unrelated
  characters (matching the exact `├Ñ`-style corruption observed) *before `cmd.exe` ever launches
  `sb.exe`*. This happens upstream of SB's own process boundary — no manifest setting, console
  encoding, or in-process fix can recover a string that already arrived corrupted from a parent
  process's own misreading of a script file. FR-001/FR-002/FR-006 and the Edge Cases below are
  revised to scope this correctly: SB is responsible for correctly handling arguments as they
  actually reach its own process (already true by default on this platform, per the empirical
  test) and for the independent — narrower — case of Unicode normalization-form mismatches; a
  misconfigured invoking script/console is an environment concern outside SB's control, called out
  explicitly as an Assumption rather than left as an implied (and uncloseable) FR.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Add a song whose title, lyrics, or source file path uses non-English characters (Priority: P1)

A user (writing in Swedish, as this project's own song fixtures do) adds a song via the CLI whose
title, lyrics, writer/melody attribution, or the path to a `--text` source file contains
non-ASCII characters (e.g. å, ä, ö, é). The song is created correctly — its file is found and
read with its characters intact — rather than the system silently misinterpreting the argument.

**Why this priority**: This is the core value proposition — SB's own real-world content (the
project's own test fixtures, and its target users' actual songs) is written in Swedish and relies
on these characters throughout. A failure here doesn't just look wrong, it corrupts data or picks
the wrong code path entirely (see Independent Test).

**Independent Test**: Can be fully tested by supplying `songs add` a `--text` value that is the
path to an *existing* file whose path contains a non-ASCII character (e.g.
`hon_var_så_liten.txt`), and verifying the system reads that file's contents (the existing-file
branch of FR from the song-leaflet-generation feature), not the literal path string as if it were
the file-not-found fallback.

**Acceptance Scenarios**:

1. **Given** a song text file whose path contains a non-ASCII character, **When** a user runs
   `sb songs add` with that path as `--text`, **Then** the system recognizes it as an existing
   file and reads its contents, rather than treating the path string itself as literal song text.
2. **Given** a song title, writer, or melody value containing non-ASCII characters, **When** a
   user adds or modifies a song via the CLI, **Then** those characters are stored and later
   retrieved exactly as entered, with no corruption (no replacement characters, no mojibake).
3. **Given** a song's own text file contains non-ASCII characters, **When** the system reads that
   file (whether directly or via `--text`), **Then** every character is read correctly.

---

### User Story 2 - Build a leaflet containing non-English characters (Priority: P2)

A user builds a leaflet from songs whose text contains non-ASCII characters. The generated LaTeX
output reproduces those characters correctly, so the compiled document displays them as written
rather than as corrupted or missing glyphs.

**Why this priority**: Builds directly on User Story 1 — once content is stored correctly, the
leaflet-generation feature's own output must faithfully carry it through to the final document,
or the correctness gained in User Story 1 is lost at the last step.

**Independent Test**: Can be fully tested by building a leaflet from a chapter containing a real
Swedish-language song fixture and verifying every non-ASCII character in the source text appears
correctly (not replaced or garbled) in the generated `.tex` output.

**Acceptance Scenarios**:

1. **Given** a song whose stored text contains non-ASCII characters, **When** a leaflet is built,
   **Then** the generated document's content reproduces those characters exactly, in every song
   block they appear in.

---

### User Story 3 - Read CLI output containing non-English characters (Priority: P3)

A user runs a command (e.g. `documents list`, an error message) whose output includes previously
stored non-ASCII text (a title, a mismatch report naming a song, etc.), and the terminal displays
those characters correctly rather than as garbled symbols.

**Why this priority**: A correctness issue here is cosmetic/diagnostic rather than data-corrupting
(unlike User Stories 1-2), so it's valuable but lower priority — a user can usually infer intent
even from a garbled character in a status message, whereas corrupted stored data or corrupted
build output cannot be recovered without redoing the work.

**Independent Test**: Can be fully tested by creating a document/song with a non-ASCII title and
running a read command (e.g. `list`), verifying the displayed text matches what was entered.

**Acceptance Scenarios**:

1. **Given** a document or song whose name/title contains non-ASCII characters, **When** a user
   runs a command that displays it (e.g. `list`), **Then** the character displays correctly.

### Edge Cases

- What happens when a `--text` argument's value contains non-ASCII characters and does *not*
  name an existing file? It MUST be treated as literal text, exactly as an all-ASCII value would
  be (FR-006) — the existing-file check must never itself be the reason a valid path is missed.
- What happens when an input song text file was saved with a leading UTF-8 byte-order mark (BOM)?
  The system MUST read the file's actual text content correctly, without a stray BOM character
  corrupting the first line.
- What happens when a value entered by the user contains a character outside the common
  Scandinavian/Latin set this feature was motivated by (e.g. emoji, other non-Latin scripts)? It
  MUST be handled the same way as any other UTF-8 character — this feature is about encoding
  correctness in general, not a fixed allow-list of specific characters.
- What happens when `sb` is invoked from a `.bat`/`cmd` script containing non-ASCII characters as
  literal text, where the script's own saved encoding doesn't match the invoking console's active
  code page? *(2026-09-27 Clarification)* This is explicitly **out of scope** — `cmd.exe` parses
  the script's bytes using the console's active code page before `sb.exe` ever starts, so the
  argument SB receives can already be corrupted beyond recovery by the time its process begins.
  SB is responsible only for correctly handling arguments as they actually arrive at its own
  process boundary (see Assumptions).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** *(revised — see Clarifications, 2026-09-27)*: System MUST correctly receive
  command-line argument values containing UTF-8 characters (song titles, writer/melody names,
  subtitles, file paths, etc.) *as they actually reach SB's own process* — this does not extend to
  recovering a value already corrupted by an invoking script or shell before SB starts (see
  Assumptions).
- **FR-002**: System MUST correctly determine whether a `--text` argument naming a path with
  non-ASCII characters refers to an existing file, using the same existing-file check that
  already applies to all-ASCII paths — a non-ASCII path MUST NOT be more likely to be
  misclassified as literal text than an equivalent ASCII path.
- **FR-003**: System MUST read the contents of a text file (a song's own stored text, or a file
  named by `--text`) using UTF-8 decoding, preserving every character exactly.
- **FR-004**: System MUST write every file it produces or stores (leaflet `.tex`/chapter `.tex`
  output, song `.yml` files, any other persisted content file) using UTF-8 encoding, so
  non-ASCII characters are preserved for any later reader (this system, a text editor, or a LaTeX
  compiler).
- **FR-005**: System MUST display non-ASCII characters correctly in its own command-line output
  (status messages, `list` output, mismatch reports).
- **FR-006** *(revised — see Clarifications, 2026-09-27)*: System MUST NOT let a Unicode
  normalization-form mismatch (e.g. precomposed vs. decomposed "å") cause a `--text` value that
  names an existing file to be treated as literal text instead — this is the specific,
  in-SB's-control mechanism this feature closes. (The originally-reported instance of this symptom
  was traced to a `.bat` script's own encoding mismatch with its console's active code page,
  corrupting the argument before it ever reached SB — see Assumptions; that upstream scenario is
  out of scope, since no fix inside SB can recover a value already corrupted by its caller.)
- **FR-007**: System MUST correctly reproduce non-ASCII characters from stored song text in the
  LaTeX content it generates when building a leaflet.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of song titles, writer/melody names, subtitles, document/chapter names, and
  file paths containing Scandinavian characters (å, ä, ö, Å, Ä, Ö) or other common Latin-alphabet
  accented characters are stored, retrieved, and displayed with zero corrupted characters.
- **SC-002**: A `--text` argument naming an existing file whose path contains non-ASCII
  characters is recognized as an existing file in 100% of cases, exactly as an equivalent ASCII
  path would be.
- **SC-003**: 100% of non-ASCII characters present in a song's stored text appear correctly,
  uncorrupted, in that song's rendered content within a generated leaflet.

## Assumptions

- This feature governs how SB reads, stores, and writes data going forward; it does not include
  migrating or repairing any already-existing files that may have been saved with a different
  encoding before this feature existed.
- The user's terminal is itself capable of displaying UTF-8 text correctly (e.g. a modern
  PowerShell/Windows Terminal session); this feature is responsible for SB *producing* correct
  UTF-8 output, not for compensating for a terminal that cannot render it.
- "Non-ASCII characters" in this spec is illustrated with the Scandinavian letters relevant to
  this project's own real-world use (Swedish song lyrics), but the requirements apply to UTF-8
  text in general, not only that character set.
- This feature is about correctness of encoding handling; it does not change any existing
  functional behavior (commands, file formats, or business rules) described in
  `001-song-leaflet-generation`.
- *(2026-09-27 Clarification)* SB's responsibility for command-line arguments begins at its own
  process boundary. On this platform, direct invocation (typed or via PowerShell/git-bash) already
  delivers non-ASCII arguments to a .NET process correctly — verified empirically, with no fix
  applied. If `sb` is instead invoked through an intermediate script or shell (e.g. a `.bat` file)
  whose own text encoding doesn't match the console's active code page at the time it runs, that
  intermediate process can corrupt the argument before SB starts; recovering from that is outside
  SB's control and out of this feature's scope. Users invoking SB from scripts containing
  non-ASCII characters are responsible for keeping the script's encoding and the console's active
  code page consistent (e.g. via `chcp 65001`), the same way any other Windows console tool
  requires.
