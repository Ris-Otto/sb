# Quickstart: Validating Song Leaflet Generation

This feature now includes the CLI/storage layer (`sb documents/chapters/songs`) backed by
a SQLite index (documents may live anywhere on disk — see `plan.md` Structure Decision,
`contracts/cli-commands.md`, and `contracts/storage-format.md`), not just the internal engine.
(`sb config` was removed 2026-09-26 — see spec.md's 2026-09-26 Clarification.)

## Prerequisites

- .NET 10 SDK installed (see `research.md` §1 for the target-framework alignment this feature
  requires).
- Repository cloned, working directory at the repo root.

## Run the automated test suite

```powershell
dotnet test sb.sln
```

Expected outcome: all tests pass, including:
- Unit tests for `LineWrapper`, `LatexEscaper`, `SongOrderer`, and `ColumnBalancer`.
- `PageMetricsReaderTests` covering `geometry`/`\fontsize` parsing and `LinesPerPage` computation
  (FR-026), and `ColumnBalancerTests` covering long-song chunking and the even/odd-page placement
  rule against a small test-supplied `LinesPerPage` (FR-025).
- `DocumentStoreTests`/`SongStoreTests` covering the containment rules (FR-013/014/015) and
  singing-order-number uniqueness (FR-016), now enforced via the SQLite index.
- `LocationMismatchDetectorTests` covering FR-022/FR-023 (index-vs-filesystem drift detection).
- `Cli/*CommandsTests` covering each command in `contracts/cli-commands.md`.
- `LeafletBuilderTests`, an end-to-end test building a leaflet from real fixture songs and
  asserting the result satisfies `contracts/leaflet-builder-api.md` and
  `contracts/latex-output-contract.md`.

## Manually exercise the CLI (developer sanity check)

`--path`/`-p` may point anywhere on disk (documents are no longer confined to one fixed output
root). As of 2026-09-26, previously-required flags (`name`, `title`, `document-name`,
`chapter-name`, `text`, `singing-number`, ...) are positional arguments, and every remaining
option has a single-letter alias:

```powershell
dotnet run --project sb -- documents create sommarvisor "Sommarvisor" `
  -p C:\Users\me\Desktop\sommarvisor
dotnet run --project sb -- chapters add sommarvisor -n main
dotnet run --project sb -- songs add sommarvisor main "Idas sommarvisa" `
  sb/Tests/TestFiles/idas_sommar.txt 1
dotnet run --project sb -- songs add sommarvisor main "Barfotavisan" `
  sb/Tests/TestFiles/barfotavisan.txt 2
dotnet run --project sb -- documents build sommarvisor space-efficient -f multi-column
```

(`--format`/`-f` is a `documents build` option, not `documents create` — moved there 2026-09-26
since a document has no persisted layout default to override. `ordering` (`space-efficient`
above) is an optional positional argument, not a flag — a further 2026-09-26 Clarification;
omitting it defaults to `as-listed`. The `text` argument above passes a file path directly — sb
reads it since the path exists — rather than the earlier `Get-Content -Raw` shell workaround, per
a further 2026-09-26 Clarification; literal text still works exactly as before.)

**Expected outcome**:
- Each command succeeds per `contracts/cli-commands.md`.
- After `build`, `C:\Users\me\Desktop\sommarvisor\sommarvisor.tex` exists (named after `--name`,
  not a fixed `main.tex` — see 2026-09-26 Clarification) and `\input`s
  `...\sommarvisor\Chapters\main\chapter.tex` — found via the SQLite index, not a fixed
  convention path.
- Both songs display singing-order numbers **1** and **2** respectively, regardless of which
  physical column/page `space-efficient` ordering placed them in.
- Re-running `build` with the same inputs produces byte-for-byte identical output (determinism).

## Validate index/filesystem mismatch detection (FR-022/FR-023)

```powershell
Rename-Item C:\Users\me\Desktop\sommarvisor C:\Users\me\Desktop\sommarvisor-moved
dotnet run --project sb -- documents list
```

**Expected outcome**: the index still records the original path, so `documents list` reports a
`MismatchReport` for `sommarvisor` (expected path no longer exists) instead of silently omitting
it, silently pointing at the new location, or crashing.

## Validate the containment rules (FR-013/014/015)

```powershell
dotnet run --project sb -- documents create empty-doc "Empty"
# Succeeds — a document may exist with zero chapters/songs (FR-013).

dotnet run --project sb -- chapters add does-not-exist -n x
# Fails — a chapter requires an existing document (FR-014).

dotnet run --project sb -- songs add empty-doc does-not-exist "X" "..." 1
# Fails — a song requires an existing chapter (FR-015).
```

## Validate singing-order number independence (FR-008/FR-009)

Build `sommarvisor` twice — once with `as-listed`, once with `space-efficient` — and diff
the two `main.tex` outputs' displayed numbers only. **Expected
outcome**: singing-order numbers 1 and 2 stay attached to the same songs in both outputs, even
though physical column placement may differ.

## Validate long-song pagination (FR-025/FR-026)

Add a song whose text has clearly more lines than fit on one page (e.g., a long song like
"Kottar i karburatorn", per the project's working notes) to a document, then build it with
`-f multi-column`:

```powershell
dotnet run --project sb -- songs add sommarvisor main "Kottar i karburatorn" `
  sb/Tests/TestFiles/kottar_i_karburatorn.txt 3
dotnet run --project sb -- documents build sommarvisor -f multi-column
```

**Expected outcome**: the song's text is never truncated/clipped — it continues across the left
and then right column, and onto subsequent pages if still too long, per `latex-output-contract.md`
("Long songs"). Whether it must be the page's only song (forced first-song placement) or may
share a page with a neighboring song depends on whether it starts on an even or odd physical
page (research.md §15) — inspect the generated `chapter.tex` to confirm the rule was applied
correctly for the page it actually landed on.

## Where to look next

- `data-model.md` — field-level shapes for `Document`, `Chapter`, `Song`, `LeafletRequest`,
  `Leaflet`, `PageMetrics`, etc. (`AppConfig` was removed 2026-09-26.)
- `contracts/cli-commands.md` — every command's flags, success/error behavior.
- `contracts/storage-format.md` — the SQLite index schema, on-disk file layout, and `.yml` schema.
- `contracts/leaflet-builder-api.md` / `contracts/latex-output-contract.md` — the underlying
  engine's contract.
- `tasks.md` (generated by `/speckit-tasks`) — the concrete implementation task breakdown.
