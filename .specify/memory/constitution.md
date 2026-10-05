<!--
Sync Impact Report
==================
Version change: [TEMPLATE] → 1.0.0 (initial ratification)
Rationale: First concrete adoption of the constitution for this repository. The prior file
contained only unfilled template placeholders, so this is treated as the initial ratification
(MAJOR) rather than an amendment.

Modified principles: n/a (no prior ratified principles existed)

Added sections:
- Core Principles: I. Readability First, II. Maintainability & Simplicity,
  III. Test-First Development (NON-NEGOTIABLE), IV. Comprehensive Test Coverage,
  V. Consistent Style & Documentation
- Quality Gates (SECTION_2)
- Development Workflow (SECTION_3)
- Governance

Removed sections: none

Deferred / TODO items:
- RATIFICATION_DATE set to the date of this ratification (2026-09-11) since no earlier
  ratification date could be found in repository history or docs. If an earlier date is known,
  amend this value.

Templates checked for alignment (no other template files were modified by this command):
- .specify/templates/plan-template.md — generic placeholders, no direct principle references; OK
- .specify/templates/spec-template.md — no direct principle references; OK
- .specify/templates/tasks-template.md — task categories already align with test-first/testing
  emphasis (tests precede implementation tasks); OK
- .specify/templates/checklist-template.md — generic; OK
-->

# SB (Sångblad) Constitution
<!-- Sångblad: a C#/.NET CLI tool that transforms song text into formatted LaTeX leaflets. -->

## Core Principles

### I. Readability First
Code MUST be written for the next human reader before it is written for the compiler.
Names (types, methods, variables) MUST describe intent, not implementation detail. Methods
MUST do one thing and stay short enough to be understood without scrolling; if a method needs
a comment to explain "what" it does, it MUST be split or renamed instead. Control flow MUST
prefer early returns and guard clauses over deep nesting. Comments are permitted only to
capture non-obvious "why" (a workaround, a subtle invariant, a constraint from LaTeX output
requirements) — never to restate "what" the code already says.
**Rationale**: SB's core logic (phrase-splitting, column balancing, LaTeX generation) is
inherently fiddly; unreadable code in this area compounds into unmaintainable output-formatting
bugs that are hard to diagnose from LaTeX symptoms alone.

### II. Maintainability & Simplicity
Every change MUST favor the simplest design that satisfies the current requirement (YAGNI).
Abstractions (interfaces, base classes, configuration layers) MUST NOT be introduced ahead of a
second concrete use case. Duplication of three or fewer similar lines MUST be tolerated rather
than papered over with a premature shared helper. Nullable reference types MUST remain enabled
project-wide, and new code MUST NOT suppress nullability warnings without a documented reason.
Dependencies (NuGet packages) MUST be added only when they replace non-trivial hand-rolled logic.
**Rationale**: This is a small, single-maintainer CLI; unnecessary layers and speculative
generality cost more in review and onboarding time than they ever save.

### III. Test-First Development (NON-NEGOTIABLE)
For any new behavior or bug fix, a failing NUnit test MUST exist before the corresponding
production code is written or changed. The Red-Green-Refactor cycle MUST be followed: write the
test, confirm it fails for the expected reason, implement the minimal code to pass it, then
refactor with tests green. Bug fixes MUST include a regression test that fails on the pre-fix
code path.
**Rationale**: SB's output (formatted LaTeX leaflets) is easy to visually eyeball as "looks fine"
while silently regressing edge cases (phrase splitting, column balancing); test-first is the only
reliable guard against that.

### IV. Comprehensive Test Coverage
Every public method and every branch of non-trivial conditional logic MUST have at least one
corresponding test. Song-parsing and formatting logic MUST be exercised against the fixture
files under `Tests/TestFiles/` (or equivalent golden files) to catch regressions in real-world
input. Tests MUST assert on behavior and output, not on internal implementation details, so that
refactors under Principle II do not require rewriting unrelated tests.
**Rationale**: Coverage on the parsing/formatting core is what makes Principle II's simplifying
refactors safe to perform without manual re-verification of every song fixture.

### V. Consistent Style & Documentation
Code MUST follow standard C#/.NET naming and formatting conventions consistently across the
codebase. Public types and methods that form the CLI's external surface (commands, options
exposed via `System.CommandLine`) MUST have their behavior documented at the point of definition.
Formatting and style MUST NOT vary by author or be left to ad hoc judgment call at review time;
when a style question recurs, it MUST be resolved once and recorded rather than re-litigated per
pull request.
**Rationale**: Consistency reduces the cognitive load of reading unfamiliar parts of a codebase
maintained by a small team, and keeps code review focused on behavior rather than formatting.

## Quality Gates

All changes MUST build cleanly (no compiler warnings introduced) and MUST pass the full NUnit
test suite before merge. A pull request that adds or changes behavior without an accompanying
test change MUST be rejected in review. Test fixtures under `Tests/TestFiles/` MUST be treated as
part of the test contract — changing expected output for an existing fixture MUST be a deliberate,
reviewed decision, not an incidental side effect of an unrelated change.

## Development Workflow

Changes MUST be reviewed against these principles before merge: readability (Principle I),
absence of unnecessary abstraction (Principle II), presence of tests written before or alongside
the change (Principle III), coverage of new/changed branches (Principle IV), and adherence to
existing style conventions (Principle V). Reviewers MUST request simplification when a change
introduces complexity not justified by the current requirement.

## Governance

This constitution supersedes ad hoc style and process preferences for this repository. Any
amendment MUST be recorded in this file with an updated Sync Impact Report, a version bump
following semantic versioning (MAJOR for incompatible principle removal/redefinition, MINOR for
a new or materially expanded principle/section, PATCH for clarifications and wording fixes), and
an updated Last Amended date. All pull requests and code reviews MUST verify compliance with the
principles above; unresolved complexity or missing tests MUST be justified in the pull request
description or resolved before merge.

**Version**: 1.0.0 | **Ratified**: 2026-09-11 | **Last Amended**: 2026-09-11
