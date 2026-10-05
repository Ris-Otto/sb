# Phase 0 Research: UTF-8 Encoding Throughout

## 1. Command-line argument and console I/O encoding on Windows (FR-001, FR-005)

**Decision (revised 2026-09-27 — see Clarifications)**: Keep two cheap, defense-in-depth fixes,
but on a corrected understanding of what they actually do and don't address:

1. Add `sb/app.manifest` declaring `<application xmlns="urn:schemas-microsoft-com:asm.v3">
<windowsSettings xmlns:ws2="http://schemas.microsoft.com/SMI/2019/WindowsSettings">
<ws2:activeCodePage>UTF-8</ws2:activeCodePage></windowsSettings></application>`, and reference it
from `sb.csproj` via `<ApplicationManifest>app.manifest</ApplicationManifest>`. This makes the
*published `sb.exe`* process use code page 65001 for legacy ANSI/OEM Win32 API calls it makes
directly, as a defensive baseline.
2. In `Program.Main`, before anything else runs, set `Console.OutputEncoding = Encoding.UTF8;` and
`Console.InputEncoding = Encoding.UTF8;`, so the same holds during `dotnet run`/`dotnet test` (the
manifest only applies to the native apphost, not the managed-DLL dev-loop path).

**Rationale — corrected empirical finding**: The original rationale for this section (that the
manifest fixes how command-line *argument bytes reach the process*) does not hold up. Direct
testing — a throwaway console app run via both PowerShell and git-bash, passing `"hon_var_så_liten.txt"`
as an argument, **with none of these fixes applied** — showed .NET on Windows already receives
non-ASCII arguments correctly (`å` arrived as the correct codepoint `U+00E5`, no corruption). This
matches how .NET actually retrieves argv on Windows (via the wide/Unicode `GetCommandLineW`, not a
code-page-dependent ANSI path), so FR-001 was already satisfied by default for direct invocation
before this feature existed.

The user confirmed the originally-observed corruption (`hon_var_s├Ñ_liten.txt`, matching UTF-8's
two-byte encoding of "å" — `0xC3 0xA5` — each byte independently reinterpreted through code page
437) was produced by invoking `sb` through a `.bat` script containing the value as a string
literal. `cmd.exe` parses a batch file's own bytes using the *console's active code page*, not
UTF-8 — if that script was saved as UTF-8 while the console was on a different code page, the
corruption happens when `cmd.exe` reads the script, **before `sb.exe`'s process even starts**. No
manifest setting or in-process encoding configuration inside SB can recover an argument that
already arrived corrupted from its parent process this way — the information about what the
original bytes meant is gone by the time SB's `Main` runs. This is why FR-001/FR-006 are revised
(spec.md Clarifications) to scope SB's responsibility to its own process boundary, and why the
batch-script scenario is recorded as an explicit out-of-scope Assumption rather than something
this feature claims to fix.

The manifest and `Console.OutputEncoding`/`InputEncoding` changes are kept anyway: they are the
correct, standard mechanism for FR-005 (console *output* display correctness, unaffected by this
correction) and are harmless, zero-cost defense-in-depth for argument handling on setups this
environment's empirical test didn't cover.

**Alternatives considered**:
- *Drop the manifest/argv framing entirely now that it's known not to fix the reported bug*:
  Rejected — `Console.OutputEncoding`/`InputEncoding` and the manifest remain the correct fix for
  FR-005 (output display) and are free insurance for argument handling on any Windows
  configuration this session's testing didn't exercise; removing them would trade a real, if
  narrower, benefit for no gain.
- *Try to detect and "un-mangle" already-corrupted mojibake heuristically*: Rejected — lossy and
  unreliable (multiple different original byte sequences can map to the same corrupted string
  under a given code page); explicitly out of scope per spec.md's Assumptions.
- *Document the `chcp 65001` / script-encoding requirement only, no code changes*: Rejected for
  FR-005 specifically — `Console.OutputEncoding` is a real, in-SB's-control fix for output
  display and should not be pushed onto the user when it costs nothing to fix directly.

## 2. Reading text files as UTF-8 (FR-003)

**Decision**: Make every `File.ReadAllText` call site that reads user-authored or SB-authored text
(`DocumentTemplate.Load`, `SongCommands.ResolveText`'s existing-file branch, `ChapterStore`'s
`chapter.yml`, `SongStore`'s `<song>.yml`, `LocationMismatchDetector`'s `.yml` read,
`DocumentStore`'s `<name>.tex` read for title/subtitle recovery) pass `Encoding.UTF8` explicitly,
rather than relying on the parameterless overload's implicit behavior.

**Rationale**: `File.ReadAllText(path)` without an explicit encoding already auto-detects a BOM
and otherwise assumes UTF-8 — for files SB itself writes (§3, always UTF-8), this already works
correctly today, so this change makes *existing, correct* behavior explicit rather than fixing a
defect in it (Constitution Principle I: readability — a future reader shouldn't have to know the
BCL's implicit default to know this is UTF-8). For a `--text` path naming a file *authored outside
SB* (e.g. in a plain text editor), this remains a real edge: a file saved in a legacy single-byte
encoding (e.g. Windows-1252) with no BOM will still be misread as UTF-8. Per spec.md's Assumptions
("this feature governs how SB reads/writes going forward... not migrating already-existing
files" and the general framing around UTF-8, not arbitrary-encoding auto-detection), SB's
contract is that it correctly handles UTF-8 input — a non-UTF-8 external file is out of scope,
matching how the rest of this project's tooling (and this feature's own Success Criteria, framed
around UTF-8 characters specifically) is scoped.

**Alternatives considered**:
- *Encoding auto-detection/sniffing library (e.g. heuristic charset detection) for external
  `--text` files*: Rejected — real dependency weight and heuristic-detection false positives for
  a problem the spec explicitly scopes to UTF-8 correctness, not universal-encoding support
  (Constitution Principle II).

## 3. Writing text files as UTF-8, no BOM (FR-004)

**Decision**: Make every `File.WriteAllText` call site (`ChapterStore`'s `chapter.yml`,
`DocumentStore`'s `<name>.tex`, `DocumentCommands`' `chapter.tex`/main document write, `SongStore`'s
`<song>.yml`) pass `new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)` explicitly.

**Rationale**: Same reasoning as §2 — `File.WriteAllText(path, contents)`'s parameterless overload
already defaults to UTF-8 without a byte-order mark in modern .NET, which is exactly the format a
LaTeX compiler and a YAML parser both expect (a leading BOM in a `.tex` file can trip up some
LaTeX engines' first line, e.g. a `%` comment/magic-comment expectation). Making it explicit
removes any ambiguity for a future reader or a future .NET version's default, and pairs with §2's
explicit reads so every text file this project touches states its encoding contract at the point
of I/O rather than relying on an implicit BCL default (Constitution Principle I).

**Alternatives considered**:
- *Writing a UTF-8 BOM*: Rejected — no reader in this pipeline (LaTeX compiler, YAML parser, this
  project's own `File.ReadAllText` calls) requires one, and a BOM in generated `.tex` source is a
  known source of LaTeX compilation quirks.

## 4. Correctly detecting an existing file for a non-ASCII `--text` path (FR-002, FR-006)

**Decision**: Add a small `PathText.NormalizeForFileSystem(string path)` helper
(`sb/Formatting/PathText.cs`) that returns `path.Normalize(NormalizationForm.FormC)`, and call it
on `SongCommands.ResolveText`'s `text` parameter before the existing `File.Exists`/
`File.ReadAllText` calls.

**Rationale (corrected scope, 2026-09-27 — see Clarifications)**: This is the *primary* in-SB's-
control mechanism FR-002/FR-006 now target — §1's correction established that the originally-
reported instance of this symptom was actually caused upstream (a `.bat` script's encoding
mismatch, out of scope), not by anything fixable here. The independent, genuinely in-scope failure
mode this section closes: Unicode allows the same visible character (e.g. "å") to be represented
as either one precomposed code point (NFC, `U+00E5`) or a base letter plus a combining ring-above
(NFD, `U+0061 U+030A`). If a `--text` value's normalization form differs from however the actual
file's name is stored on disk (e.g. because it was typed via a different input method, or pasted
from a source that uses NFD — some non-Windows editors, XML processors, and file systems do),
two visually-identical strings compare unequal and `File.Exists` can genuinely return `false` for
a file that is, to a human, obviously right there. Normalizing to FormC (the form precomposed
Latin text — including this project's own Swedish fixture filenames — already uses) before the
existence check is a one-line, dependency-free defense against this real, if narrower than
originally assumed, failure mode. It is applied only to the *path used for the filesystem
check/read*, never to the literal-text fallback value itself (which must stay exactly as the user
typed it, per `contracts/leaflet-builder-api.md`'s existing determinism guarantees in
`001-song-leaflet-generation` — this feature changes no behavior there).

**Alternatives considered**:
- *Normalizing every string SB touches (titles, lyrics, etc.), not just filesystem-check paths*:
  Rejected — out of scope; this feature's FR-002/FR-006 are specifically about file-existence
  detection, and normalizing arbitrary user content (song lyrics, titles) would risk silently
  altering text the user typed a specific way for stylistic reasons, which none of this feature's
  user stories ask for.
- *Drop this fix now that it's known not to be the originally-reported bug's actual cause*:
  Rejected — it remains a real, independent failure mode (NFC/NFD mismatch) worth closing on its
  own merits, and the fix is essentially free (one line, no dependency).

## 5. Testing strategy (Constitution Principle IV)

**Decision**: Reuse this project's own real fixtures (`sb/Tests/TestFiles/*.txt`, already
containing å/ä/ö) for round-trip read/write assertions, rather than inventing synthetic non-ASCII
fixtures. Add a regression test for a `--text` value naming an existing file whose path contains a
non-ASCII character, asserting it is read as that file's contents, not treated as literal text.
Add unit tests for `PathText.NormalizeForFileSystem` covering an NFC input (no-op) and an NFD
input (normalized to match).

**Rationale (scope note, 2026-09-27 — see Clarifications)**: This regression test exercises
`ResolveText` directly with an already-correctly-encoded C# string (as SB actually receives
arguments per §1's empirical finding) — it validates the in-scope NFC/NFD defense (§4), not a
reproduction of the originally-reported `.bat`-script scenario, which happens at a layer (a parent
process's own script parsing) no in-process unit test can exercise or fix. Real fixtures already
exercise realistic non-ASCII content without inventing new test data.
