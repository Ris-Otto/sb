# Feature Specification: Song Hierarchy & Singing-Order Numbering

**Feature Branch**: `002-song-hierarchy-numbering`

**Created**: 2026-09-11

**Status**: Merged into [`001-song-leaflet-generation`](../001-song-leaflet-generation/spec.md)
on 2026-09-11 — this spec is retained for history only; do not plan or implement against it
directly. All content below was folded into `001-song-leaflet-generation/spec.md` (as FR-013
through FR-017, User Story 2, and related Edge Cases/Success Criteria/Assumptions), with this
spec's rules treated as authoritative wherever it conflicted with 001's original text.

**Input**: User description: "A main document may exists without songs or chapters. A chapter
cannot exists without a document, nor a song without atleast one chapter. Song numbering is
completely separate from song order; song numbers represent the order in which they should be
sung, not necessarily in which order they are physically."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Build up a document's structure safely (Priority: P1)

A user starts a new leaflet project by creating an empty document, then adds chapters to it, and
songs to those chapters, over time. At every step, the system only allows additions that make
sense structurally: a chapter always belongs to a document, and a song always belongs to at
least one chapter.

**Why this priority**: This is the foundational structural integrity rule for all of SB's data.
Every other capability (formatting, ordering, numbering) operates on documents/chapters/songs
and depends on these containment rules always holding — an invalid structure would make later
operations unpredictable or produce a broken leaflet.

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

### User Story 2 - Assign each song a singing-order number, independent of layout (Priority: P2)

A user assigns each song in a document a number representing the order the songs should be sung
in (e.g., at a singing event), separate from wherever that song physically ends up on the page
or in what chapter it's filed under. When the leaflet's physical layout is later rearranged (for
example, to use space more efficiently), the singing-order numbers the user assigned do not
change.

**Why this priority**: This directly protects the usability of the leaflet for its real purpose
— singing songs in a specific order — from being disrupted by unrelated layout/space
optimizations. It builds on User Story 1's structure (numbers are assigned to songs that already
exist within chapters/documents).

**Independent Test**: Can be fully tested by assigning explicit singing-order numbers to a set of
songs in a document, then triggering a physical re-layout of that document (e.g., a
space-efficient rearrangement), and verifying every song still reports the exact same
singing-order number it was assigned, regardless of its new physical position.

**Acceptance Scenarios**:

1. **Given** a set of songs within a document, **When** a user assigns each one a singing-order
   number, **Then** each song retains that number independently of which chapter it is in or
   where it appears physically in the document.
2. **Given** songs with assigned singing-order numbers, **When** the document's physical layout
   is rearranged (songs moved between columns/pages/chapters for space efficiency), **Then** the
   singing-order numbers displayed for each song remain unchanged.
3. **Given** a document with songs that already have singing-order numbers assigned, **When** a
   user asks to see a song's singing-order number, **Then** the system reports it independently
   of the song's current physical position.

### Edge Cases

- What happens when a user tries to remove a chapter that still contains songs? The system MUST
  either remove the contained songs along with the chapter, or reject the removal — either way,
  it MUST NOT leave a song existing without any chapter (see Assumptions for the chosen default).
- What happens when a user tries to assign a singing-order number that is already used by
  another song in the same document? The system MUST reject the assignment and explain the
  conflict, rather than silently allowing two songs to share a singing-order number.
- What happens when a document has chapters but none of those chapters contain any songs? The
  document remains a valid stored state; it is simply not yet ready to be built into a leaflet
  (per the existing leaflet-generation feature's requirement of at least one song).
- What happens when a song is moved from one chapter to another within the same document? Its
  singing-order number MUST remain unchanged by the move.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow a document to exist and persist with zero chapters and zero
  songs.
- **FR-002**: System MUST reject creating a chapter that is not associated with an existing
  document.
- **FR-003**: System MUST reject creating a song that is not associated with at least one
  existing chapter.
- **FR-004**: System MUST allow assigning a singing-order number to a song, representing the
  order in which songs should be performed.
- **FR-005**: System MUST store and report a song's singing-order number independently of that
  song's physical position within the document (which chapter it's in, or where it lands on the
  page/column layout).
- **FR-006**: System MUST NOT alter a song's singing-order number as a side effect of any
  physical re-layout or re-ordering operation performed on the document.
- **FR-007**: System MUST reject assigning a singing-order number to a song when another song in
  the same document already holds that number.
- **FR-008**: System MUST allow a user to retrieve a song's singing-order number at any time,
  independent of its current physical position.

### Key Entities

- **Document**: The top-level container for a leaflet project. May contain zero or more
  Chapters. Exists and is valid on its own, even with no chapters or songs yet.
- **Chapter**: A subdivision of exactly one Document, used to organize songs. Cannot exist
  without being associated with a Document.
- **Song**: A single piece of song content, belonging to at least one Chapter (and transitively,
  one Document). Carries a singing-order number, distinct from its physical layout position.
- **Singing-Order Number**: An attribute of a Song representing the order it should be performed
  in, unique among songs within the same Document, and unaffected by that song's physical
  position in the generated leaflet layout.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can create an empty document and return to add chapters/songs later, with
  zero data loss or forced premature content in 100% of cases.
- **SC-002**: 100% of attempts to create a chapter without a document, or a song without a
  chapter, are rejected with an explanation the user can act on.
- **SC-003**: After any physical re-layout of a document's songs, 100% of songs report the exact
  same singing-order number they had before the re-layout.
- **SC-004**: 100% of attempts to assign a singing-order number already used elsewhere in the
  same document are stopped before being saved, with a clear conflict explanation.

## Assumptions

- Removing a chapter that still contains songs removes those songs along with it (cascading
  removal), since a song cannot exist without a chapter — the system will not leave orphaned
  songs behind. A user who wants to keep the songs is expected to move them to another chapter
  first.
- Singing-order numbers are assigned by the user (not automatically computed from physical
  layout or input order); this feature's responsibility is to store, protect, and report them
  correctly, not to decide what number a song should have.
- Singing-order number uniqueness is scoped to a single Document (matching that a leaflet is
  built from one document's full content at a time).
- This specification governs the structural containment rules (document/chapter/song) and the
  singing-order numbering rule; it does not cover the specific commands, storage file formats,
  or CLI surface used to implement them — those are implementation concerns for planning.
- This feature directly constrains the existing song-leaflet-generation feature (see
  `specs/001-song-leaflet-generation/spec.md`): that feature's numbering behavior must be
  understood as displaying/using each song's singing-order number, not deriving a new number
  from physical layout position.
