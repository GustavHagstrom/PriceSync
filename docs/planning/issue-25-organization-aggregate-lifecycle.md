# Issue 25: Organization Aggregate Lifecycle

**Status:** Implemented

**Authority:** This document records the delivery decisions for issue #25 so the implementation can proceed without reopening boundary questions. Current authoritative behavior belongs in domain documentation, ADRs, tests, and implemented code.

Planning base for [issue #25](https://github.com/GustavHagstrom/PriceSync/issues/25).

## Goal

Implement the initial `Organization` aggregate root for one legal business party. The aggregate owns its immutable Legal Organization Identity and the Organization-level lifecycle state needed by the first domain foundation.

## Working Decisions

- Issue #25 introduces no new domain-modeling decisions beyond the issue #11 and issue #24 decisions already recorded.
- `Organization` remains internal to `PriceSync.Modules.OrganizationAccess`.
- A new Organization is created with an `OrganizationId`, required display name, `LegalOrganizationIdentity`, and creation timestamp supplied through `TimeProvider`.
- Organization display name is trimmed at the boundary, must be 1-200 characters after trimming, preserves internal characters and casing, and remains mutable presentation data rather than identity data.
- A new Organization starts as `Active`.
- An Organization's Legal Organization Identity is immutable after creation.
- Creation and archiving obtain UTC timestamps through `TimeProvider`.
- Archived is terminal for Organization in this implementation slice.
- Archiving an Organization does not model membership ending or any persistence/API side effects.

## Open Decisions

None for the planned issue #25 implementation scope. New decisions should be opened only if implementation reveals a concrete contradiction or missing requirement.

## Exit Criteria

- A new Organization can be created with an `OrganizationId`, required display name, Legal Organization Identity, and creation timestamp supplied through `TimeProvider`.
- A new Organization starts as `Active`.
- Organization Legal Organization Identity cannot be changed after Organization creation.
- Organization display name is required, trimmed at the boundary, limited to the documented length, and remains presentation data rather than identity data.
- An Active Organization can be archived with a UTC timestamp supplied through `TimeProvider` or a testable equivalent.
- An Archived Organization cannot be restored in this implementation slice.
- Archiving an Organization does not model membership ending or any persistence/API side effects.
- Focused database-free unit tests cover creation, display name validation, immutable identity, Active-to-Archived transition, and terminal Archived behavior.
- The solution builds and all tests pass.

## Session Log

- 2026-09-10: Created implementation branch `codex/issue-25-from-pr-a-spec` from PR #22 branch `codex/issue-11-pr-a-spec`.
- 2026-09-10: Confirmed issue #24 is already present in the PR #22 base through squash commit `1dd7258`.
- 2026-09-10: Completed grilling with no new questions because issue #25 behavior is already decided by issue #11, issue #24, and PR #22.
- 2026-09-10: Implemented the selected issue #25 slice.

## Implementation Outcome

Implemented on 2026-09-10 in branch `codex/issue-25-from-pr-a-spec`.

No deviations from the plan.
