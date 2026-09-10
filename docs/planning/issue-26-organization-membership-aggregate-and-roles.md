# Issue 26: Organization Membership Aggregate and Roles

**Status:** Implemented

**Authority:** This document records the delivery decisions for issue #26 so the implementation can proceed without reopening boundary questions. Current authoritative behavior belongs in domain documentation, ADRs, tests, and implemented code.

Planning base for [issue #26](https://github.com/GustavHagstrom/PriceSync/issues/26).

## Goal

Implement the initial `OrganizationMembership` aggregate root for one Person's authority within one Organization. The aggregate references `OrganizationId` and `PersonId`, owns its explicit Organization Role set and lifecycle state, and encodes the initial Owner/Member authority semantics.

## Working Decisions

- Issue #26 introduces no new domain-modeling decisions beyond the issue #11 decisions already recorded.
- `OrganizationMembershipId` remains internal to `PriceSync.Modules.OrganizationAccess` and is not exposed from `PriceSync.Modules.OrganizationAccess.Contracts`.
- `OrganizationMembership` remains internal to `PriceSync.Modules.OrganizationAccess`.
- A new Organization Membership references an `OrganizationId` and `PersonId`.
- A new Organization Membership starts as `Active` with `StartedAt` supplied through `TimeProvider`.
- An active Organization Membership requires at least one explicitly assigned Organization Role.
- Initial Organization Roles are `Owner` and `Member`.
- Owner authority includes Member authority without requiring Member to be redundantly assigned or returned as an explicit role.
- The role model supports multiple explicit roles.
- An Active Organization Membership can be ended with a UTC timestamp supplied through `TimeProvider`.
- Ended is terminal for Organization Membership in this implementation slice.
- Organization Membership is a separate aggregate root and is not modeled as a child collection on Organization.

## Open Decisions

None for the planned issue #26 implementation scope. New decisions should be opened only if implementation reveals a concrete contradiction or missing requirement.

## Exit Criteria

- `OrganizationMembershipId` exists as an internal strongly typed identifier and is not exposed from `PriceSync.Modules.OrganizationAccess.Contracts`.
- A new Organization Membership references an `OrganizationId` and `PersonId` and starts as `Active`.
- A new Active Organization Membership must have at least one explicitly assigned Organization Role.
- Initial Organization Roles are `Owner` and `Member`.
- Owner authority includes Member authority without requiring Member to be redundantly assigned or returned as an explicit role.
- Independent explicit role combinations are supported by the role model.
- An Active Organization Membership can be ended with a UTC timestamp supplied through `TimeProvider` or a testable equivalent.
- An Ended Organization Membership cannot be restored in this implementation slice.
- Organization Membership is represented as its own aggregate root rather than as a child collection on Organization.
- Focused database-free unit tests cover creation, required roles, explicit roles, Owner-includes-Member authority, Active-to-Ended transition, and terminal Ended behavior.
- The solution builds and all tests pass.

## Session Log

- 2026-09-10: Created implementation branch `codex/issue-26-from-pr-a-spec` from PR #22 branch `codex/issue-11-pr-a-spec`.
- 2026-09-10: Completed grilling with no new questions because issue #26 behavior is already decided by issue #11 and PR #22.
- 2026-09-10: Implemented the selected issue #26 slice.

## Implementation Outcome

Implemented on 2026-09-10 in branch `codex/issue-26-from-pr-a-spec`.

No deviations from the plan.
