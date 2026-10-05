# Feature Specification: Song Leaflet Generation

**Feature Branch**: `001-song-leaflet-generation`

**Created**: 2026-09-11

**Status**: Draft

**Input**: User description: "SB should make it easier to create song leaflets (sångblad). It is an interface between the user and LaTeX. The goal is to seamlessly take (a list of) songs that are in text-form and transform the into either a multi-column leaflet or a single-column leaflet of the songs, and format them, order them, and number them."

## Clarifications

### Session 2026-09-11

- Q: How should this spec reconcile with `002-song-hierarchy-numbering` (document/chapter/song
  containment rules, and singing-order numbers being independent of physical layout)? → A: Merge
  002 into this spec; wherever the two conflict, 002's rules are authoritative.
  This resolved a direct conflict between this spec's original FR-008/FR-009 (a song's "number"
  was defined as its sequential position in the generated leaflet, updating whenever the order
  changed) and 002's rule that a song's number represents its intended singing order, assigned by
  the user and unaffected by physical layout. FR-008/FR-009, the Key Entities, Edge Cases, and
  Success Criteria below have been rewritten accordingly, and the document/chapter/song
  containment rules from 002 have been folded in as FR-013–FR-017. `specs/002-song-hierarchy-numbering/`
  is retained for history but is superseded by this spec — see its spec.md header.
- Q: Should SB maintain a persistent index of what it has created, and is that index or the
  filesystem authoritative for locating documents/chapters/songs? → A: Yes; the index (a SQLite
  database) is authoritative for lookup, since documents/chapters/songs may be saved at
  arbitrary, user-chosen filesystem locations rather than one fixed convention. When the
  filesystem doesn't match what the index records (e.g., a folder/file is missing or has moved),
  the system informs the user of the mismatch rather than silently trusting the index or the
  filesystem, or auto-repairing either. This is captured as FR-018–FR-023 below.
- Q: What must this index record for documents, chapters, and songs? → A: `documents` — internal
  id, path to the document's main folder, filename (derived from that folder's name); `chapters`
  — internal id, name, parent document's internal id; `songs` — internal id, name, singing-order
  number, parent chapter's internal id. The index does not store full song text/lyrics — that
  remains in the song's own file (per the existing Assumptions), keeping the index a
  location/identity lookup rather than a second content store.

### Session 2026-09-26

- Q: `sb/specification.md` was updated to (1) store each song as a single `.yml` file containing
  its singing-order number and lyric text directly (rather than a split `.txt` lyrics + `.yml`
  metadata pair with the number living only in the index), and (2) remove the `config`
  command/`config.yml`/`AppConfig` concept entirely. Should these changes be folded into this
  spec? → A: Yes, both. (1) A song's own file is now the source of truth for all of its data,
  including its singing-order number; the index still records the number too (FR-021 is
  unchanged), but purely as a fast-lookup mirror, with FR-023-style mismatch detection covering
  disagreement between the two. (2) Configuration management is removed from this feature's
  scope — the Assumption that included it (previously citing `sb/specification.md` §2.4) is
  struck, since that section no longer exists in the product spec.
- Q: A document created via `sb documents create` was being stored as a separate `document.yml`
  metadata file (title/subtitle/format) alongside `main.tex`, and `main.tex` itself was only
  generated later, at `build` time. Should this change? → A: Yes. A document has no metadata file
  of its own at all — `main.tex` (rendered from `sb/template-main.tex`, with title/subtitle
  substituted) is written immediately at `create` time, and an empty `Chapters/` folder is
  created alongside it in the same step (matching `sb/specification.md` §2.5.1's "documents and
  chapters are stored as .tex-files," which the prior `document.yml` deviated from). `main.tex`'s
  rendered title/subtitle are also how the title/subtitle are recovered on every later read
  (`list`, `find`, `build`) — there is nothing else to read them from.
- Q: With no `document.yml`, where does a document's `--format` (single-column/multi-column) — a
  `create`-time flag today — get stored, since `main.tex` has no structural placeholder for it and
  the index's `documents` table is fixed to path/filename/id (FR-019)? → A: It doesn't get
  stored — `--format` moves from `sb documents create` to `sb documents build` and is supplied
  fresh on every build invocation instead of defaulting to a value chosen at creation.
  `sb/specification.md` §2.6.1 is updated to match (flag moved from `create`'s list to `build`'s).
- Q: A document's rendered `.tex` file was named with the fixed filename `main.tex`, regardless
  of what `--name` was given at `create` time. Should it instead be named after `--name`? → A:
  Yes — `<name>.tex` (e.g. `sommarvisor.tex`), both when first written at `create` time and when
  overwritten at `build` time. Since a document's filename can change (`sb documents rename`), the
  `.tex` file is renamed alongside `documents.filename` at rename time, keeping the two in sync.
- Q: `sb songs add`'s `--text` flag only accepted literal text, forcing shell workarounds (e.g.
  PowerShell's `Get-Content -Raw`) to supply a song's lyrics from a file. Should `--text` accept a
  file path directly? → A: Yes — when `--text`'s value is an existing file, its contents are read
  and used as the song's text instead of the literal value; when it isn't an existing file, it's
  used as literal text exactly as before (additive, not a breaking change to existing usage).
- Q: The system automatically split each song's text into "musical phrases" (breaking at
  sentence/comma punctuation) so that one phrase occupied one output line. Should this be
  removed, trusting the user's own line breaks instead? → A: Yes — remove phrase-splitting
  entirely. Each of a song's own input lines becomes one output line as-is; the system introduces
  a line break of its own only when a line is too wide for the column (FR-004, unchanged), never
  by inspecting punctuation. FR-003, User Story 1, the punctuation-related Edge Case, SC-002, the
  `Phrase` Key Entity, and the phrase-detection Assumption are all revised/removed accordingly.
- Q: The built document put every song directly into the main `.tex` file, regardless of which
  chapter it belonged to — chapters were never rendered as their own `.tex` files or `\chapter{}`
  sections, contrary to `sb/specification.md` §2.5.1 ("Documents and chapters are stored as
  .tex-files") and §4 (chapters get `\input`'d into the main document). Should this be fixed? →
  A: Yes — this was an implementation gap, not an intended simplification. Each chapter with at
  least one song now renders into its own `chapter.tex` (a `\chapter{<title-or-name>}` heading
  followed by that chapter's own songs), and the main document's content region becomes a
  sequence of `\input{Chapters/<name>/chapter.tex}` lines instead of embedded song content. Song
  ordering and single-/multi-column layout are scoped to within one chapter's own songs only
  (never pairing songs across a chapter boundary, since `\chapter` forces a physical break
  regardless). A chapter with zero songs is skipped entirely (no heading, no `\input` line). No FR
  wording changes — FR-011's "complete, self-contained... document" already covers a main file
  plus the fragments it `\input`s; the specific per-chapter file split remains, as before, an
  implementation/planning detail spec.md's Assumptions already defer.
- Q: Every currently-required CLI flag (e.g. `--name`, `--title`, `--document-name`) is an
  `Option`, and every optional flag has no short form. Should required flags become positional
  arguments, and should the remaining (optional) flags gain single-letter aliases? → A: Yes to
  both — `sb/specification.md` §2.6 already leaves "whether something should be a flag or an
  argument" to the implementer, so this is purely an implementation/CLI-wiring detail (no FR
  wording changes). Every previously-required `--flag` becomes a positional argument (e.g. `sb
  documents create <name> <title>`); every remaining option keeps its long form and gains a short
  alias (e.g. `--path`/`-p`), scoped per-command so the same letter can mean different things in
  different commands with no collision (e.g. `-m` is `--melody` in `songs add` but
  `--max-permutations` in `documents build`).
- Q: A blank line within a song's text (e.g. a stanza separator) was being silently dropped from
  the built leaflet instead of appearing as a blank line in the output. Should it be preserved? →
  A: Yes — this is a bug, not intended behavior; FR-003 already says each of a song's own lines
  becomes its own output line, which a blank line is one of. Fixed: a blank input line now
  renders as a completely blank visual line in the output. Mechanically, this requires emitting
  *two* blank lines in the generated `.tex` source after the preceding line's `\\` — in LaTeX, a
  line break immediately followed by only one blank source line doesn't yet produce a visible
  gap; a second blank line is what actually shows it. No FR wording change — FR-003's "each line...
  as its own output line" already covered this; the fix corrects `LeafletBuilder`'s previous
  under-implementation of it.
- Q: The prior round made every previously-required flag a positional argument and left every
  optional flag as an option. Should `documents build`'s `--ordering` — optional, defaulting to
  `as-listed` — become an optional positional argument instead, as a deliberate exception? → A:
  Yes — `ordering` becomes a positional argument (`sb documents build <document-name>
  [ordering]`), keeping its `as-listed` default and its `as-listed`/`space-efficient` validation
  unchanged. This is a one-off exception the user asked for explicitly, not a reversal of the
  general "required → argument, optional → option" rule from the prior round. No FR wording
  change — purely CLI-wiring, per `sb/specification.md` §2.6 leaving this choice to the
  implementer.

### Session 2026-09-27

- Q: When a song is too long to fit in the space remaining under strict `as-listed` order, should
  the system relax the order to accommodate it, or must it always honor the user's exact order
  even if that means degraded layout (or rejection)? → A: The relative order of songs is never
  changed under `as-listed`; the system may still adjust *where* it inserts a page/column break
  (e.g., forcing a long song to start a new page rather than beginning mid-page) to satisfy
  multi-column layout constraints. `as-listed` is "best effort" only with respect to physical
  packing (break placement), never with respect to song sequence. FR-006 is revised accordingly.
- Q: In multi-column layout, when a song too long for one page starts on an EVEN page (where it
  must be the page's first song), does "split into two columns" mean the song renders as a
  full-width block spanning both columns, or that it fills the left column completely and
  continues into the right column as ordinary column flow? → A: The latter — the song fills the
  left column completely, then continues into the right column as ordinary multi-column flow; no
  full-width override of the two-column grid. New FR-025 captures the full even/odd-page rule.
- Q: To decide whether a song fits in the remaining page/column space (needed for FR-025 and
  FR-004), should the system compute page/line capacity dynamically from `template-main.tex`'s
  own margin/fontsize/spacing values, or track it as an independently configured value? → A:
  Dynamically, from `template-main.tex` at build time — the template is the single source of
  truth for layout geometry; a separately tracked value could drift out of sync with it. New
  FR-026 captures this.
- Q: Should building a single chapter in isolation (rather than the whole document) be added to
  this spec's scope now, or deferred as a separate future feature? → A: Deferred — it is
  independent of the multi-column pagination work just clarified (FR-025/FR-026) and was raised
  only as a tentative "potentially" idea, not a required behavior. Recorded as an out-of-scope
  Assumption for a future feature to pick up.
- Q: *(`/speckit-analyze` finding U1 remediation)* Does FR-025's "first song on an even page"
  placement rule mean a long song monopolizes every page it touches, or does it only constrain
  where the song *starts*? Relatedly, is a column ever hard-capped at holding exactly one song? →
  A: Neither a page nor a column is exclusive to one song. Once a song's own content is
  exhausted — whether on the page it started on or a later continuation page — any remaining
  page/column space MAY be filled by a different, subsequent song; a column has no explicit
  song-count limit, only the physical page-capacity limit (FR-026). Multi-column layout was
  always intended to fill each column sequentially with as many songs as fit top-to-bottom, not
  pair exactly one song per column per row. Minimizing unused space (FR-007) is the actual goal;
  column *start* alignment (FR-005) is a fixed side effect of every column beginning at the same
  template top-margin, not a separate balancing act — not a hard one-song-per-column constraint.
  FR-025's rule only fixes where a long song itself starts and how its own content flows; it does
  not reserve the rest of that page.

### Session 2026-09-28

- Q: Does FR-005's column-alignment requirement include columns *ending* at approximately the
  same line, or only *starting* at the same height? → A: Only starting alignment is required —
  all columns on a page MUST begin at approximately the same height. Ending at approximately the
  same line across columns is explicitly NOT a requirement; a column may end well short of another
  without violating FR-005. FR-005 revised accordingly.
- Q: Now that FR-005 no longer requires columns to end at the same line, what should happen to
  SC-003, which measured exactly that (columns ending within one line's height of each other in
  ≥95% of pages)? → A: Remove SC-003 entirely — column starts are already structurally guaranteed
  by every column sharing the same template top-margin, so a percentage-based success criterion
  for start-alignment would be tautological; no replacement criterion is needed.
- Q: `sb/PageMetrics.png` (a `layout`-package diagram of `template-main.tex`'s actual computed
  page geometry) initially showed `\paperwidth = 614pt` / `\paperheight = 794pt` (US Letter),
  contradicting research.md §14/FR-026's existing "paper size will always be A4" assumption
  (hardcoded as ≈845.05pt). Given that mismatch, how should paper size be resolved for FR-026's
  capacity math? → A: The initial diagram was generated incorrectly and didn't reflect the real
  template; a corrected regeneration confirms `\paperwidth = 597pt` / `\paperheight = 845pt` /
  `\textheight = 776pt` — i.e., true A4, matching the existing assumption exactly. No FR-026
  wording change needed — the apparent drift was a diagram artifact, not a real one.
- Q: *(`/speckit-analyze` finding I1/I2 remediation)* `sb/PageMetrics.png`'s sibling evidence
  (`C:\Test`, a hand-built leaflet using real fixture songs) showed several pages where both
  columns end already roughly balanced, or close to it, yet with roughly half the page left
  unused — while several of the shortest songs in the set were pushed all the way to the end of
  the document, rendered awkwardly rather than filling those earlier gaps. Is FR-005's "no
  end-alignment required" revision (this same session, above) sufficient, or does `space-efficient`
  ordering (FR-007) need a sharper rule? → A: FR-005's revision stands unchanged (start-alignment
  only), but it was incomplete on its own — the real goal was never "columns may simply end
  short," it's that `space-efficient` ordering MUST prefer pulling a later, smaller-fitting song
  forward to fill a page's remaining column space before starting a new page, front-loading
  density; only a chapter's **final** page may be left under-full, since no further song content
  exists to fill it. This applies only to `space-efficient` ordering — `as-listed` (FR-006) is
  unaffected, since it never reorders songs regardless of layout. FR-007 revised; new SC-012 added
  (replacing the intent SC-003 used to serve, now measuring actual page density instead of column
  end-height); new Edge Case added; Acceptance Scenario 2 (User Story 1) corrected — it had
  described the old, incorrect "columns end at the same height" behavior FR-005 already
  disclaimed above, missed by the same-session FR-005 edit.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate a formatted leaflet from a list of songs (Priority: P1)

A user has a set of songs as plain text and wants a finished, well-formatted leaflet
document without hand-writing any LaTeX. They provide the songs and choose either a
single-column or multi-column layout, and the system produces a leaflet where each song's text
keeps exactly the line breaks the user gave it — the system does not merge or re-split lines —
except that a line too wide for the chosen column MUST still be broken so nothing overflows or is
truncated *(revised — see Clarifications)*.

**Why this priority**: This is the core value proposition of SB — turning raw song text into
a usable, properly typeset leaflet. Without this, none of the other capabilities matter.

**Independent Test**: Can be fully tested by supplying a small set of plain-text songs, choosing
a layout, and verifying the output leaflet is a valid, compilable document where each output line
corresponds exactly to one of the input's own lines, wrapped further only where the column width
requires it.

**Acceptance Scenarios**:

1. **Given** a list of songs in text form and a single-column layout choice, **When** the user
   generates the leaflet, **Then** the system produces a single-column document containing all
   songs, with each song's own line breaks preserved exactly.
2. **Given** a list of songs in text form and a multi-column layout choice, **When** the user
   generates the leaflet, **Then** the system produces a document with songs arranged in
   columns, with columns starting at approximately the same height *(revised — see
   Clarifications, 2026-09-28; columns are not required to end at the same height, FR-005)*.
3. **Given** a song whose line does not fit within the available column width, **When** the
   leaflet is generated, **Then** the system breaks that line across multiple output lines rather
   than overflowing or truncating it — this is the only case where the system introduces a line
   break the user didn't write.

---

### User Story 2 - Maintain valid document/chapter/song structure (Priority: P2)

A user builds up a leaflet project by creating a document, then adding chapters to it, and songs
to those chapters, over time. At every step, the system only allows additions that make sense
structurally: a document may exist on its own with no chapters or songs yet, a chapter always
belongs to a document, and a song always belongs to at least one chapter.

**Why this priority**: This is the foundational structural integrity rule underlying everything
else in this spec — formatting, ordering, and numbering all operate on documents/chapters/songs,
and an invalid structure would make those operations unpredictable or produce a broken leaflet.

**Independent Test**: Can be fully tested by: creating a document with no chapters or songs
(succeeds and persists), attempting to add a song before any chapter exists in that document
(rejected), adding a chapter to the document (succeeds), then adding a song to that chapter
(succeeds).

**Acceptance Scenarios**:

1. **Given** no existing document, **When** a user creates a new document, **Then** the document
   exists with zero chapters and zero songs, and this is a valid, stable state (not an error or
   incomplete condition).
2. **Given** an existing document with no chapters, **When** a user attempts to add a song
   directly to the document without specifying or creating a chapter, **Then** the system
   rejects the action with a clear explanation that a song requires a chapter.
3. **Given** an existing document, **When** a user adds a chapter to it, **Then** the chapter is
   created and associated with that document.
4. **Given** an existing chapter within a document, **When** a user adds a song to that chapter,
   **Then** the song is created and associated with that chapter.
5. **Given** no existing document, **When** a user attempts to create a chapter without
   referencing any document, **Then** the system rejects the action with a clear explanation
   that a chapter requires a document.

---

### User Story 3 - Order songs automatically or as specified (Priority: P3)

A user wants the songs in their leaflet arranged either exactly in the order they listed them,
or automatically arranged to use the available space most efficiently (e.g., fewest pages).

**Why this priority**: Ordering affects both the practical usability of the leaflet (users find
songs faster in singer-chosen order) and its physical efficiency (fewer printed pages). It
builds directly on the formatting capability from User Story 1 and assumes a valid structure
from User Story 2.

**Independent Test**: Can be fully tested by supplying the same list of songs twice — once
requesting the user-specified order and once requesting space-efficient ordering — and verifying
the resulting song order differs accordingly and is deterministic for a given input and choice.

**Acceptance Scenarios**:

1. **Given** a list of songs and a request to preserve the user's listed order, **When** the
   leaflet is generated, **Then** songs appear in exactly the order they were provided.
2. **Given** a list of songs and a request for space-efficient ordering, **When** the leaflet is
   generated, **Then** the system arranges the songs to minimize unused space/pages compared to
   the original order.

---

### User Story 4 - Assign and preserve each song's singing-order number (Priority: P4)

A user assigns each song in a document a number representing the order the songs should be sung
in (e.g., at a singing event), separate from wherever that song physically ends up on the page,
in what chapter it's filed under, or how User Story 3's ordering rearranges it for space
efficiency. The leaflet displays this number next to each song, and it never changes as a side
effect of a physical re-layout.

**Why this priority**: Numbering is what makes the leaflet usable for its real purpose — singing
songs in the intended order — but it is meaningful only once songs exist in a valid structure
(User Story 2) and have been formatted (User Story 1); it must also survive whatever the
space-efficient ordering (User Story 3) does to physical placement, which is why it is specified
last but is not merely cosmetic.

**Independent Test**: Can be fully tested by assigning explicit singing-order numbers to a set of
songs, generating the leaflet with the user-specified physical order, then regenerating it with
space-efficient physical ordering instead, and verifying every song displays the exact same
singing-order number in both outputs.

**Acceptance Scenarios**:

1. **Given** a set of songs within a document, **When** a user assigns each one a singing-order
   number, **Then** each song retains that number independently of which chapter it is in or
   where it appears physically in the document.
2. **Given** a generated leaflet, **When** the user inspects the output, **Then** each song
   displays its assigned singing-order number.
3. **Given** songs with assigned singing-order numbers, **When** the leaflet's physical layout is
   rearranged (songs moved between columns/pages for space efficiency, per User Story 3), **Then**
   the singing-order numbers displayed for each song remain unchanged.

### Edge Cases

- What happens when the list of songs to include is empty? The system MUST reject the request
  with a clear message rather than producing an empty or malformed document.
- How does the system handle a song whose text has no internal punctuation at all *(revised —
  see Clarifications)*? Unaffected either way — the system doesn't inspect punctuation; it
  outputs each of the song's own lines as-is (wrapping only if a line is too wide for the
  column), so a punctuation-free line is handled exactly like any other.
- How does the system handle a song containing characters with special meaning in LaTeX (e.g.,
  `%`, `&`, `#`, `_`)? The system MUST render them as literal characters in the output rather than
  breaking the document or altering meaning.
- What happens when a song's text contains a blank line (e.g. a stanza separator)? *(2026-09-26
  Clarification)* The system MUST render it as a completely blank visual line in the output,
  rather than silently dropping it.
- What happens when a single song is too long to fit on one page in the chosen layout?
  *(revised — see Clarifications, 2026-09-27)* In single-column layout, the system MUST continue
  the song onto the next page. In multi-column layout, the rule depends on which page the song
  starts on: if it starts on an EVEN page, the song MUST be positioned as the first song on that
  page, filling the left column completely before continuing into the right column as ordinary
  column flow (never a full-width override of the column grid) — see FR-025; if it starts on an
  ODD page, the song MAY simply continue onto the next page. In all cases, if the song still
  doesn't fit after this, it continues onto subsequent pages rather than failing or being clipped.
- What happens when two songs are otherwise identical in space usage during space-efficient
  ordering? The system MUST produce a consistent, deterministic ordering rather than an
  arbitrary one that changes between runs.
- What happens when a page's remaining column space, after placing the current sequence of
  songs, is large enough to fit a smaller song that appears later in the chapter? *(2026-09-28
  Clarification)* Under space-efficient ordering, the system MUST prefer moving that later,
  smaller song earlier in the physical placement sequence to fill the gap, rather than leaving
  the space unused and starting a new page — except on a chapter's **final** page, where
  remaining unused space is acceptable since no further content exists to fill it. This does not
  apply to `as-listed` ordering, which never reorders songs (FR-006).
- What happens when a user tries to remove a chapter that still contains songs? The system MUST
  either remove the contained songs along with the chapter, or reject the removal — either way,
  it MUST NOT leave a song existing without any chapter (see Assumptions for the chosen default).
- What happens when a user tries to assign a singing-order number that is already used by
  another song in the same document? The system MUST reject the assignment and explain the
  conflict, rather than silently allowing two songs to share a singing-order number.
- What happens when a document has chapters but none of those chapters contain any songs? The
  document remains a valid stored state; it is simply not yet ready to be built into a leaflet
  (FR-012 still requires at least one song to generate).
- What happens when a song is moved from one chapter to another within the same document? Its
  singing-order number MUST remain unchanged by the move (FR-024).
- What happens when the index says a document/chapter/song is at a given location, but the
  actual filesystem doesn't match (folder/file missing, moved, or altered outside SB)? The
  system MUST inform the user of the mismatch rather than silently trusting the index, silently
  trusting the filesystem, or silently repairing either.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST accept a list of songs supplied as plain text.
- **FR-002**: System MUST allow the user to choose between a single-column and a multi-column
  leaflet layout for a given generation request.
- **FR-003** *(revised — see Clarifications)*: System MUST preserve each line of a song's input
  text as its own output line — trusting the user's own line breaks — rather than merging lines
  together or splitting a line at internal punctuation, whenever the column width allows it.
- **FR-004**: System MUST break a line across multiple output lines when it does not fit within
  the available column width, instead of truncating or overflowing it — the only case where the
  system introduces a line break the user didn't write.
- **FR-005** *(revised by user, 2026-09-28 — see Clarifications)*: System MUST vertically align
  columns in multi-column layouts so that all columns on a page start at approximately the same
  height. Columns ending at approximately the same line is explicitly NOT required — a column may
  end well short of another without violating this requirement.
- **FR-006** *(revised — see Clarifications, 2026-09-27)*: System MUST support ordering songs so
  their relative sequence exactly matches the order the user listed them in; the system MAY still
  choose where to insert a page/column break to satisfy multi-column layout constraints (e.g.,
  starting a long song at the top of a new page rather than mid-page), but MUST NOT change which
  song comes before which as a result.
- **FR-007** *(revised — see Clarifications, 2026-09-28)*: System MUST support automatically
  ordering songs to minimize unused space across the generated leaflet, as an alternative to the
  user-specified order. Concretely: when space remains in a page's column(s) after placing songs
  in sequence, the system MUST prefer pulling a later, smaller-fitting song forward to fill that
  space before starting a new page — front-loading page density — rather than leaving a page
  under-full while shorter songs are pushed later in the document. The one exception is a
  chapter's **final** page, which MAY be left with unused space, since no further song content is
  available to fill it.
- **FR-008** *(revised — see Clarifications)*: System MUST display each song's singing-order
  number in the generated leaflet — a number representing the order songs should be performed
  in, assigned independently of the song's physical position in the document.
- **FR-009** *(revised — see Clarifications)*: System MUST NOT alter a song's singing-order
  number as a side effect of any physical re-layout or re-ordering operation (including User
  Story 3's space-efficient ordering).
- **FR-010**: System MUST safely render song text containing characters with special meaning in
  the output format without corrupting the document or altering the visible text.
- **FR-011**: System MUST produce a complete, self-contained leaflet document as output, ready to
  be rendered into its final visual form without further manual editing.
- **FR-012**: System MUST reject a generation request that includes no songs, with a message
  explaining that at least one song is required.
- **FR-013** *(from 002)*: System MUST allow a document to exist and persist with zero chapters
  and zero songs.
- **FR-014** *(from 002)*: System MUST reject creating a chapter that is not associated with an
  existing document.
- **FR-015** *(from 002)*: System MUST reject creating a song that is not associated with at
  least one existing chapter.
- **FR-016** *(from 002)*: System MUST reject assigning a singing-order number to a song when
  another song in the same document already holds that number.
- **FR-017** *(from 002)*: System MUST allow a user to retrieve a song's singing-order number at
  any time, independent of its current physical position.
- **FR-018** *(addendum — see Clarifications)*: System MUST maintain a persistent index
  recording every document, chapter, and song it has created, each keyed by an internal
  identifier.
- **FR-019** *(addendum)*: For each document, the index MUST record the document's internal id,
  the filesystem path to its main folder, and its filename (derived from that folder's name).
- **FR-020** *(addendum)*: For each chapter, the index MUST record the chapter's internal id, its
  name, and its parent document's internal id.
- **FR-021** *(addendum)*: For each song, the index MUST record the song's internal id, its name,
  its singing-order number, and its parent chapter's internal id.
- **FR-022** *(addendum)*: System MUST treat the index as authoritative for locating a document,
  chapter, or song, since these may be stored at arbitrary, user-chosen filesystem locations
  rather than one fixed convention.
- **FR-023** *(addendum)*: System MUST detect when an indexed document/chapter/song's recorded
  location does not match the actual filesystem (e.g., a missing or relocated folder/file) and
  inform the user of the mismatch rather than silently proceeding.
- **FR-024** *(added — `/speckit-analyze` remediation, closes finding C1)*: System MUST NOT
  alter a song's singing-order number when the song is moved to a different chapter within the
  same document.
- **FR-025** *(added — see Clarifications, 2026-09-27; scope clarified per finding U1)*: In
  multi-column layout, when a song is too long to fit within the remaining space of the page it
  starts on, the system MUST apply the following rule based on that page's parity: (a) if the
  page is EVEN-numbered, the song MUST be positioned as the first song on that page, with its
  text filling the left column completely before continuing into the right column as ordinary
  column flow; (b) if the page is ODD-numbered, the song MAY continue onto the next page without
  requiring first-song placement. This rule constrains only where the long song itself starts and
  how its own content flows — once its content is exhausted (on that page or a later continuation
  page), any remaining page/column space MAY be filled by a subsequent song; it does not reserve
  an entire page exclusively for one song.
- **FR-026** *(added — see Clarifications, 2026-09-27; corrected 2026-09-27 per `/speckit-analyze`
  finding I1)*: System MUST determine available page and column capacity (usable height,
  accounting for margins, font size, and line spacing) by reading those values from the
  document's LaTeX template (`template-main.tex`) at build time, rather than from an
  independently maintained configuration value, so pagination decisions (FR-025) stay consistent
  with the template actually used to render the output. (FR-004's line-*width* wrapping is
  unaffected — it depends on column width, not this vertical/height capacity.)

### Key Entities

- **Document**: The top-level container for a leaflet project. May contain zero or more
  Chapters. Exists and is valid on its own, even with no chapters or songs yet. Key attributes
  (per the index, FR-018/FR-019): an internal id, the filesystem path to its main folder, and a
  filename derived from that folder's name.
- **Chapter**: A subdivision of exactly one Document, used to organize songs. Cannot exist
  without being associated with a Document. Key attributes (per the index, FR-020): an internal
  id, its name, and its parent Document's internal id.
- **Song**: A single piece of text-form content, belonging to at least one Chapter (and
  transitively, one Document). Key attributes: title, body text (lyrics), its singing-order
  number (see below), and its physical position once placed in a leaflet — these two numbers are
  independent of each other. May optionally carry attribution details (e.g., writer, melody
  reference) that appear alongside it in the output. Per the index (FR-021), also identified by
  an internal id and its parent Chapter's internal id.
- **Singing-Order Number**: An attribute of a Song representing the order it should be performed
  in, unique among songs within the same Document, assigned by the user, and unaffected by that
  song's physical position in the generated leaflet layout.
- **Index**: A persistent record of every Document/Chapter/Song's identity and location
  (FR-018–FR-021), authoritative for locating them (FR-022) since they may reside at arbitrary,
  user-chosen filesystem locations rather than one fixed convention. Detects and surfaces
  mismatches between its recorded locations and the actual filesystem rather than resolving them
  silently (FR-023). Does not store song lyrics/content — only identity, location, and the
  singing-order number.
- **Leaflet**: The finished output document produced from a set of songs. Key attributes: chosen
  layout (single-column or multi-column) and the physically ordered list of songs it contains,
  each displaying its singing-order number.
- ~~**Phrase**~~ *(removed — see Clarifications)*: This feature no longer detects sub-line
  musical phrases; a song's own input line is the unit of line-formatting.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can go from a set of plain-text songs to a finished, ready-to-render
  leaflet without writing or editing any layout markup themselves.
- **SC-002** *(revised — see Clarifications)*: For real-world song text, at least 95% of the
  user's own input lines fit within one output line without needing to be wrapped, even at the
  narrower multi-column width — confirming that trusting the user's line breaks (rather than
  algorithmic phrase-splitting) still produces a readable layout in practice.
- ~~**SC-003**~~ *(removed — see Clarifications, 2026-09-28)*: Previously required columns to end
  within one text line's height of each other in at least 95% of generated pages. Dropped because
  FR-005 no longer requires end-of-column alignment, and start alignment is already structurally
  guaranteed by the shared template top-margin, making a percentage-based criterion unnecessary.
- **SC-004** *(revised — see Clarifications)*: After any physical re-layout of a document's songs
  (e.g., switching between user-specified and space-efficient ordering), 100% of songs display
  the exact same singing-order number they had before the re-layout.
- **SC-005**: Producing a leaflet for a typical song list (10–20 songs) takes a user under one
  minute of interaction, compared to the significantly longer time required to hand-format the
  equivalent document.
- **SC-006** *(revised — `/speckit-analyze` remediation, closes finding F2)*: Generated leaflets
  are syntactically well-formed LaTeX (balanced environments, no unescaped reserved characters)
  in 100% of cases where valid song text was supplied — ready for compilation without manual
  correction. (Actually invoking a LaTeX compiler is out of this feature's scope, per the
  Assumptions below; this criterion covers producing output that would compile, not compiling
  it.)
- **SC-007** *(from 002)*: Users can create an empty document and return to add chapters/songs
  later, with zero data loss or forced premature content in 100% of cases.
- **SC-008** *(from 002)*: 100% of attempts to create a chapter without a document, or a song
  without a chapter, are rejected with an explanation the user can act on.
- **SC-009** *(from 002)*: 100% of attempts to assign a singing-order number already used
  elsewhere in the same document are stopped before being saved, with a clear conflict
  explanation.
- **SC-010** *(addendum)*: SB can locate any document/chapter/song it has created via the index
  in 100% of cases, regardless of where the underlying folder was placed on disk.
- **SC-011** *(addendum)*: 100% of detected mismatches between the index and the actual
  filesystem are surfaced to the user with enough detail to act on, rather than silently ignored
  or silently auto-corrected.
- **SC-012** *(added — see Clarifications, 2026-09-28; replaces the intent SC-003 used to serve)*:
  Under space-efficient ordering, at least 95% of a chapter's **non-final** pages have no
  remaining column space large enough to fit a song appearing later in that chapter's build —
  confirming shorter songs are pulled forward to fill gaps rather than deferred to a later page.
  A chapter's final page is excluded from this measure, since it may legitimately be under-full.

## Assumptions

- Songs are supplied as plain text (one song's lyrics per song), consistent with how songs are
  authored and stored elsewhere in the project; parsing song text into structured data is treated
  as a given input to this feature, not something it must reverse-engineer from arbitrary formats.
- "Multi-column" defaults to a two-column layout unless a future feature explicitly introduces
  configurable column counts; this specification only requires that multi-column output exists
  and is vertically balanced.
- Automatic phrase detection is **removed** (2026-09-26 Clarification): the user's own line breaks
  are trusted as-is, and the system only introduces a line break of its own when a line is too
  wide for the column (FR-004). The system does not infer musical phrasing from punctuation.
- "Space-efficient" ordering means minimizing total unused vertical space / page count across the
  leaflet; the exact algorithm is an implementation concern for planning, not this specification.
- Rendering the final document into a viewable/printable form (e.g., compiling it to a viewable
  file) is a downstream step outside this feature's scope; this feature is responsible for
  producing a correct, complete source document.
- The specific CLI commands, storage file formats, and naming/renaming/listing/deletion mechanics
  for documents/chapters/songs remain implementation concerns for planning, not this
  specification — only the containment rules themselves (FR-013–FR-015) and their consequences
  are in scope here.
- Removing a chapter that still contains songs removes those songs along with it (cascading
  removal), since a song cannot exist without a chapter — the system will not leave orphaned
  songs behind. A user who wants to keep the songs is expected to move them to another chapter
  first.
- Singing-order numbers are assigned by the user (not automatically computed from physical
  layout or input order); this feature's responsibility is to store, protect, and report them
  correctly, not to decide what number a song should have.
- Singing-order number uniqueness is scoped to a single Document (matching that a leaflet is
  built from one document's full content at a time).
- Documents/chapters/songs may be stored at arbitrary, user-chosen filesystem locations rather
  than uniformly under one fixed output root — this is the reason an index is needed at all
  (FR-018–FR-023) rather than relying on directory scanning under a known convention. Any
  previously-assumed fixed `<output>/` folder convention (from earlier planning) needs to be
  revisited alongside this index in the next planning pass.
- The user has specified SQLite as the concrete index technology; this specification records
  that as a given constraint rather than an open technical choice, while keeping the functional
  requirements themselves (FR-018–FR-023) phrased in technology-agnostic terms.
- The index records identity, location, and the singing-order number only — it is not a mirror
  of song lyrics/content, which remain in the song's own file (existing Assumption above). Per
  the 2026-09-26 Clarification, the song's own file (not the index) is the source of truth for
  the singing-order number; the index's copy is a fast-lookup mirror, and reconciling the two
  when they disagree is an implementation concern for planning (see FR-023's existing mismatch
  pattern).
- Configuration management (`sb config set`, `config.yml`) is explicitly out of scope for this
  feature, per the 2026-09-26 Clarification and the removal of that section from
  `sb/specification.md`. Any previously-planned config support (loading/validating `config.yml`,
  LaTeX preamble shorthand) is to be removed, not merely left unbuilt.
- Building a single chapter in isolation (rather than the whole document) is explicitly out of
  scope for this feature, per the 2026-09-27 Clarification — it is deferred to a separate future
  feature and MUST NOT be designed or built as part of this spec's planning.
