# Issue 27: First Owner Domain Creation

**Status:** Implemented

**Authority:** This document records the delivery decisions for issue #27 so the implementation can proceed without reopening boundary questions. Current authoritative behavior belongs in domain documentation, ADRs, tests, and implemented code.

Planning base for [issue #27](https://github.com/GustavHagstrom/PriceSync/issues/27).

## Goal

Connect the completed PR A domain pieces into a small, testable domain-level creation path for the first Organization and its first active Owner Organization Membership.

## Working Decisions

- The first-owner creation path remains internal to `PriceSync.Modules.OrganizationAccess`.
- The creation path takes `ICurrentPerson` directly and reads `currentPerson.PersonId` as the initiating Person.
- The creation path creates an `Organization` and a separate `OrganizationMembership`.
- The creation path returns an `InitialOrganizationState` value that packages the two separate aggregates for the later CreateOrganization application slice.
- The first Organization Membership receives only the explicit `Owner` role.
- The creation path uses `TimeProvider` for lifecycle timestamps.
- The creation path introduces no persistence, API endpoints, authentication-provider concepts, application command/query handlers, or cross-context contracts.
- Issue #27 intentionally supersedes the earlier issue #11 handler-only coordination note for PR A's domain proof. The later CreateOrganization handler will consume the already-tested domain creation path rather than re-deciding the first-owner outcome.

## Open Decisions

None for the planned issue #27 implementation scope. New decisions should be opened only if implementation reveals a concrete contradiction or missing requirement.

## Exit Criteria

- Creating the initial domain state for an Organization produces an Active Organization and a separate Active Organization Membership for the initiating Person.
- The first Organization Membership has the explicit Owner role.
- The creation path obtains or accepts the initiating `PersonId` through the Organization & Access contract shape rather than client-supplied membership data.
- The first-owner behavior coordinates the two aggregates without embedding Organization Membership as a child collection on Organization.
- The implementation does not introduce EF Core mappings, migrations, repositories, database schema, API endpoints, application command/query handlers, authentication-provider tokens, sessions, or accounts.
- Architecture tests still enforce that Organization-specific domain objects and persistence concerns remain inside module boundaries and that the Shared Kernel remains technical.
- Focused database-free tests verify the first-owner cross-aggregate outcome and guard against accidental boundary widening.
- The solution builds and all tests pass.

## Session Log

- 2026-09-10: Created implementation branch `codex/issue-27-from-pr-a-spec` from PR #22 branch `codex/issue-11-pr-a-spec`.
- 2026-09-10: Decided that the first-owner creation path takes `ICurrentPerson` directly and reads the initiating `PersonId` from the Organization & Access contract shape.
- 2026-09-10: Decided that issue #27 supersedes the earlier issue #11 handler-only coordination note for PR A's first-owner domain proof.
- 2026-09-10: Implemented the selected issue #27 slice.

## Implementation Outcome

Implemented on 2026-09-10 in branch `codex/issue-27-from-pr-a-spec`.

No deviations from the plan.
