# Specification Quality Checklist: Song Leaflet Generation

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-11
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`
- All items pass on initial generation; no [NEEDS CLARIFICATION] markers were needed because
  reasonable defaults (documented in the Assumptions section) were available for every
  underspecified detail, informed by the project's existing `sb/specification.md` source-of-truth
  document.
- 2026-09-11: `002-song-hierarchy-numbering` was merged into this spec (see `## Clarifications`
  in spec.md). All checklist items were re-validated against the merged content and continue to
  pass — no state changes.
- 2026-09-11: Addendum via `/speckit-clarify` — added a persistent SQLite-backed index for
  documents/chapters/songs (FR-018–FR-023, SC-010–SC-011, new Edge Case, new `Index` Key Entity).
  Re-validated against the updated spec: still 16/16 passing. Note: "No implementation details"
  is satisfied at the FR level (FRs speak of "a persistent index", not SQLite specifically);
  "SQLite" appears only in `## Clarifications` and `## Assumptions` as a recorded, explicit user
  constraint, not an invented technical design choice.
- 2026-09-11: `/speckit-analyze` remediation — added FR-024 (singing-order number survives a
  chapter move; was previously only an Edge Case with no backing FR, and was mis-cited elsewhere
  as FR-006), revised SC-006 to not claim actual LaTeX compilation (out of scope per Assumptions
  — it now claims syntactic validity only), and added an Assumption documenting Config's
  inclusion despite having no dedicated FR. Re-validated: still 16/16 passing.
- 2026-09-26: `sb/specification.md` was updated by the user; folded two resulting changes into
  this spec (see `## Clarifications` Session 2026-09-26): (1) a song's own file, not the index,
  is now the source of truth for its singing-order number — no FR wording changed, since FR-021
  already just requires the index to record the number; (2) the Config Assumption (added
  2026-09-11 above) is struck — Config is now explicitly out of scope, not merely un-cited.
  Re-validated: still 16/16 passing; no new [NEEDS CLARIFICATION] markers.
- 2026-09-26 (`/speckit-clarify`): folded two more changes into this spec (two more bullets under
  the same Session 2026-09-26): (1) a document's `main.tex` (not a separate `document.yml`) is now
  its sole persisted file, written immediately at `create` time alongside an empty `Chapters/`
  folder; (2) `--format` moves from `sb documents create` to `sb documents build`. Neither change
  required any FR wording change — FR-002 already scopes the format choice "to a given generation
  request," not a persisted document default, so plan-level code was actually the thing out of
  step with spec.md, not the reverse. Re-validated: still 16/16 passing; no new [NEEDS
  CLARIFICATION] markers.
- 2026-09-26 (`/speckit-clarify`, third round): folded one more change into this spec (third
  bullet, same Session 2026-09-26): a document's rendered `.tex` file is named `<name>.tex`
  (matching `--name`) rather than a fixed `main.tex`, and is renamed alongside `sb documents
  rename`. No FR wording change needed — this is purely the storage-file-naming detail spec.md's
  Assumptions already defer to planning. Re-validated: still 16/16 passing; no new [NEEDS
  CLARIFICATION] markers.
- 2026-09-26 (`/speckit-clarify`, fourth round): folded one more change into this spec (fourth
  bullet, same Session 2026-09-26): `sb songs add`'s `--text` flag now also accepts a file path
  (reads its contents), additively — no FR wording change needed, since FR-001 ("accept a list of
  songs supplied as plain text") is agnostic to how that text reaches the CLI. Re-validated: still
  16/16 passing; no new [NEEDS CLARIFICATION] markers.
- 2026-09-26 (`/speckit-clarify`, fifth round): removed automatic phrase-splitting (fifth bullet,
  same Session 2026-09-26) — a real functional-requirement-level change, not just a planning
  detail. Revised FR-003, User Story 1 (description, Independent Test, Acceptance Scenarios 1/3),
  the punctuation-related Edge Case, and SC-002; removed the `Phrase` Key Entity and the
  phrase-detection Assumption. Re-validated: still 16/16 passing (every item is still about the
  *shape* of the spec, not the specific behavior chosen, so removing a capability doesn't fail any
  of them); no new [NEEDS CLARIFICATION] markers.
- 2026-09-26 (`/speckit-clarify`, sixth round): fixed a real implementation bug identified by the
  user (sixth bullet, same Session 2026-09-26) — the build had flattened all songs directly into
  the main `.tex` file instead of rendering each chapter into its own `chapter.tex` `\input`'d
  from the main document, per `sb/specification.md` §2.5.1/§4 (which this project's own template
  and `data-model.md`/`research.md` had described as the intended design since 2026-09-11). No FR
  wording change — FR-011 already covers a main file plus its `\input`-referenced fragments as
  "complete, self-contained"; the fix corrects `research.md`/`data-model.md`/`plan.md`/both
  `contracts/*.md` files to match spec.md/`sb/specification.md`, which were already right.
  Re-validated: still 16/16 passing; no new [NEEDS CLARIFICATION] markers.
- 2026-09-26 (`/speckit-clarify`, seventh round): required `--flag`s become positional arguments;
  remaining options gain single-letter aliases (seventh bullet, same Session 2026-09-26). No FR
  wording change — `sb/specification.md` §2.6 already left this exact choice to the implementer.
  Confined entirely to `CliRoot.cs`; `Cli/*Commands.cs` untouched. Re-validated: still 16/16
  passing; no new [NEEDS CLARIFICATION] markers.
- 2026-09-26 (`/speckit-clarify`, eighth round): fixed a real bug the user identified (eighth
  bullet, same Session 2026-09-26) — a blank line within a song's text was silently dropped
  instead of rendered as a blank output line; new Edge Case added. No FR wording change — FR-003's
  "each line...as its own output line" already covered blank lines; `LeafletBuilder` just
  under-implemented it. Re-validated: still 16/16 passing; no new [NEEDS CLARIFICATION] markers.
- 2026-09-26 (`/speckit-clarify`, ninth round): `documents build`'s `--ordering` becomes an
  optional positional argument (ninth bullet, same Session 2026-09-26), a deliberate one-off
  exception to the seventh round's general rule, keeping its `as-listed` default. No FR wording
  change — purely `CliRoot.cs` wiring. Re-validated: still 16/16 passing; no new [NEEDS
  CLARIFICATION] markers.
- 2026-09-27 (`/speckit-clarify`, tenth round): informed by `notes` (project working notes on
  multi-column pagination for long songs). Four clarifications, all new Session 2026-09-27: (1)
  `as-listed` ordering never changes song sequence, only page/column break placement — FR-006
  revised; (2) a song too long for one page, on an EVEN page, must be that page's first song and
  fills the left column completely before continuing into the right column as ordinary column
  flow (never a full-width override); on an ODD page it may simply continue to the next page — new
  FR-025, and the "song too long for one page" Edge Case revised to match; (3) page/column
  capacity is computed dynamically from `template-main.tex`'s margins/fontsize/spacing at build
  time, not tracked as a separate configured value — new FR-026; (4) building a single chapter in
  isolation is deferred to a future feature, not added to this spec's scope — new Assumption.
  `template-main.tex`/LaTeX are named because they are existing, given project files/format (like
  the earlier SQLite precedent), not an invented technical design choice. Re-validated: still
  16/16 passing; no new [NEEDS CLARIFICATION] markers.
- 2026-09-28 (`/speckit-clarify`, eleventh round): new Session 2026-09-28. (1) FR-005 tightened
  per a direct user-provided answer — column alignment requires only that columns *start* at
  approximately the same height; ending at approximately the same line is explicitly not required
  (previously conflated via the U1-remediation note's "balanced column heights" phrasing, now
  reworded to remove the contradiction); (2) SC-003, which measured column-ending alignment,
  removed entirely (struck through, not renumbered) since it directly contradicted the FR-005
  change and start-alignment is already structurally guaranteed by the shared template top-margin.
  A separately reported packing bug (a short song deferred to a new page while an adjacent column
  still has room) was evaluated but not turned into a spec change — the Assumptions section
  already defers "the exact algorithm" to planning/implementation, so this is a code-level fix,
  not a spec ambiguity. Re-validated: still 16/16 passing; no new [NEEDS CLARIFICATION] markers.
- 2026-09-28 (`/speckit-clarify`, twelfth round, same Session 2026-09-28): a `PageMetrics.png`
  diagram of `template-main.tex`'s real computed geometry initially appeared to contradict
  research.md §14/FR-026's "paper size is always A4" assumption (showed Letter, 614×794pt). A
  corrected regeneration of the same diagram confirmed true A4 (597×845pt), matching the existing
  assumption exactly — the mismatch was a diagram-generation artifact, not a real drift. No spec
  or research.md change made. Re-validated: still 16/16 passing; no new [NEEDS CLARIFICATION]
  markers.
- 2026-09-28 (`/speckit-analyze` remediation, thirteenth round, same Session 2026-09-28): analysis
  found the eleventh round's FR-005/SC-003 change was applied incompletely (User Story 1's
  Acceptance Scenario 2 still asserted end-alignment; `sb/specification.md` §2.1 and
  research.md/tasks.md/contracts still cited SC-003 as live and load-bearing for the shipped
  `\vspace` mechanism). Investigating with real fixture data (`sb/PageMetrics.png`'s sibling test
  document) surfaced the actual root requirement gap: `space-efficient` ordering's shipped
  `GreedyFallback` heuristic (longest-first sort, used for any chapter beyond `ExactSearch`'s
  4-song reach) structurally strands short songs at the end of a chapter instead of using them to
  fill earlier pages' leftover column space. Resolved: (1) FR-007 revised with an explicit
  front-loading rule (prefer a later, smaller-fitting song over leaving column space unused,
  except on a chapter's final page); (2) new SC-012 added, replacing the intent SC-003 used to
  serve, now measuring page density instead of column end-height; (3) new Edge Case documenting
  the gap-filling rule and its final-page exception; (4) Acceptance Scenario 2 corrected to drop
  the false end-alignment claim; (5) `sb/specification.md` §2.1 revised to describe gap-filling as
  the primary mechanism and `\vspace` as a cosmetic finishing step; (6) stale SC-003 citations in
  research.md §5/§14, tasks.md (T012, T061, T114, T118(e)), and
  `contracts/latex-output-contract.md` annotated as superseded (not rewritten, per this project's
  existing convention). Fixing `GreedyFallback` itself (e.g. to a best-fit/first-fit-decreasing
  packing strategy) remains an open follow-up implementation task, not done as part of this round.
  Re-validated: still 16/16 passing; no new [NEEDS CLARIFICATION] markers.
- 2026-09-28 (further `/speckit-analyze` remediation, fourteenth round, same Session 2026-09-28):
  fixing `contracts/latex-output-contract.md`'s title/lyrics description (per user request)
  surfaced a second, unrelated documentation/code drift of the same shape as the SC-003 one:
  `sb/Leaflets/ColumnBalancer.cs`'s own doc comment confirms the 2026-09-27 "title/lyrics
  separation" feature (`SongBlock.TitleLineCount`/`Column.LeadingTitleLineCount`) was implemented
  and later **reverted** by direct instruction, but the revert was never recorded anywhere —
  plan.md's Complexity Tracking, research.md §5's two addenda, data-model.md's `SongBlock`/`Column`
  field tables, and `contracts/latex-output-contract.md`'s "Title/lyrics separation" bullet all
  still described the pre-revert design as shipped. Annotated all five locations as superseded
  (not rewritten) and rewrote the contract's bullet to describe the actual current behavior (title,
  melody, and lyrics always render as one unbroken block). No code changed. Re-validated: still
  16/16 passing; no new [NEEDS CLARIFICATION] markers.
- 2026-09-28 (implementation fix, fifteenth round, same Session 2026-09-28): the open follow-up
  from the thirteenth round — `GreedyFallback`'s longest-first heuristic couldn't satisfy FR-007's
  front-loading rule — was fixed per a direct user requirement: `space-efficient` mode should
  still track singing order as closely as possible, deviating only to use column space that would
  otherwise go to waste. `sb/Ordering/SongOrderer.cs`'s `GreedyFallback` rewritten as an
  order-preserving, gap-filling walk (Phase 18, T123-T125); `sb/Tests/SongOrdererTests.cs`'s
  greedy-fallback case rewritten to match. research.md §4 updated with a 2026-09-28 correction.
  `dotnet test`: 142/142 passing — no regressions. This is a code change, not a spec/requirements
  change — FR-007/SC-012 (added in the thirteenth round) already described this behavior; this
  round makes the implementation match them.
- 2026-09-28 (`/speckit-analyze` remediation, sixteenth round, same Session 2026-09-28): found
  three process/documentation gaps from the fifteenth round: (F1, CRITICAL per Constitution
  Principle III) Phase 18 listed the `GreedyFallback` implementation task before its test-rewrite
  task, contradicting this file's own "tests first" rule and the fifteenth round's own (inaccurate)
  claim of test-first execution — corrected by reordering the tasks into "Tests for Phase 18" /
  "Implementation for Phase 18" blocks (matching every other phase's convention) with an honest
  process note about actual chronology; (F2, MEDIUM) Phase 18 was missing from the "Phase
  Dependencies" list — added; (F3, MEDIUM) neither the thirteenth round's FR-007/SC-012 spec
  change nor the fifteenth round's `GreedyFallback` rewrite had a matching plan.md Complexity
  Tracking entry, breaking the convention every prior phase followed — two entries added. No
  functional/behavioral change; `dotnet test` unaffected (142/142, docs-only round). Re-validated:
  still 16/16 passing; no new [NEEDS CLARIFICATION] markers.
- 2026-09-28 (`/speckit-tasks` regeneration, seventeenth round, same Session 2026-09-28): tasks.md
  regenerated from current spec.md/plan.md/research.md/data-model.md/contracts/ into a clean
  60-task, 7-phase current-state snapshot (the prior 125-task/18-phase incremental history is
  preserved at `tasks.md.bak`, at the user's explicit choice after being warned this would
  overwrite the file). No functional change. `dotnet test`: 142/142 unaffected.
- 2026-09-28 (`/speckit-clarify` + implementation fix, eighteenth round, same Session 2026-09-28):
  real-world testing against a 16-song fixture (`C:\Test`) showed `GreedyFallback` (fifteenth
  round) still left a whole column empty next to a long song — its hand-rolled capacity model
  didn't know about FR-025's even-page forced-break rule for over-long songs, so it couldn't
  foresee that placing the next song there would trigger a forced break, and never considered
  filling that space with a smaller later song first. No spec change needed (FR-007 already
  requires this); fixed `GreedyFallback` to be simulation-driven — it now builds every candidate
  next-placement's full hypothetical ordering and picks whichever minimizes `ComputeWastedSpace`
  (i.e., actually calls `ColumnBalancer.Fill`), exactly like `ExactSearch` already does, closing
  the FR-025 blind spot for good. `sb/Tests/SongOrdererTests.cs`'s greedy-fallback case rewritten
  again — the previous demonstration case turned out to be a waste-neutral tie under real
  simulation, not a genuine improvement, so it no longer distinguished the fix from a no-op.
  research.md §4 and tasks.md (T045/T048) updated. Verified against the real `C:\Test` fixture:
  the previously-stranded song now shares a page with its neighbors, and the chapter's content
  page count drops from 6 to 5. `dotnet test`: 142/142 passing.
