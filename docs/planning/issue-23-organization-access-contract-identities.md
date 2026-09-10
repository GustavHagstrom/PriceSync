# Issue 23: Organization & Access Contract Identities

**Status:** Implemented

**Authority:** This document records the delivery decisions for issue #23 so the implementation can proceed without reopening boundary questions. Current authoritative behavior belongs in domain documentation, ADRs, tests, and implemented code.

Planning base for [issue #23](https://github.com/GustavHagstrom/PriceSync/issues/23).

## Goal

Expose the first Organization & Access contracts needed by later implementation slices while keeping Organization domain internals, membership internals, persistence concerns, HTTP DTOs, and authentication-provider details outside the public contracts project.

## Working Decisions

- `OrganizationId` and `PersonId` are public, independent, Guid-backed value wrappers in `PriceSync.Modules.OrganizationAccess.Contracts`.
- The contract identifiers expose an existing `Guid` value; ID generation remains outside the contracts project.
- `OrganizationId` and `PersonId` reject `Guid.Empty` at construction.
- Issue #23 does not add parsing helpers, JSON converters, EF converters, or serializer-specific attributes to the contract identifiers.
- `ICurrentPerson` exposes a non-null `PersonId PersonId` property. Missing authentication is handled before application code consumes the contract.
- The issue #23 public contract types share the single namespace `PriceSync.Modules.OrganizationAccess.Contracts`.
- Architecture tests protect the initial `OrganizationAccess.Contracts` public surface with an explicit exported-type allowlist: `OrganizationId`, `PersonId`, and `ICurrentPerson`.
- Architecture tests verify that `OrganizationAccess.Contracts` has no project references in issue #23.
- Existing architecture tests also cover the small contract-identifier behavior that `OrganizationId(Guid.Empty)` and `PersonId(Guid.Empty)` throw; issue #23 adds no dedicated contracts test project.
- The contracts project does not introduce a generic strongly typed ID base and does not move Organization-specific concepts into `PriceSync.SharedKernel`.

## Open Decisions

None for the planned issue #23 implementation scope. New decisions should be opened only if implementation reveals a concrete contradiction or missing requirement.

## Exit Criteria

- `OrganizationId`, `PersonId`, and `ICurrentPerson` exist in `PriceSync.Modules.OrganizationAccess.Contracts`.
- `OrganizationId` and `PersonId` are independent Guid-backed value wrappers that reject `Guid.Empty`.
- `ICurrentPerson` exposes the authenticated PriceSync `PersonId` through a non-null `PersonId` property.
- No ID generation, generic ID base, Shared Kernel dependency, serializer-specific behavior, EF behavior, HTTP DTO, domain type, membership identity, or persistence type is introduced in the contracts project.
- Architecture tests protect the public contract allowlist, project-reference independence, and empty-Guid behavior.
- The solution builds and all tests pass.

## Session Log

- 2026-09-10: Decided that `OrganizationId` and `PersonId` should be pure Guid-backed contract value wrappers without ID-generation factories in the contracts project.
- 2026-09-10: Decided that `ICurrentPerson` exposes the authenticated PriceSync `PersonId` directly; unauthenticated access is handled outside the contract.
- 2026-09-10: Decided to keep the initial Organization & Access contracts in one public namespace rather than introducing sub-namespaces before there is enough contract surface to justify them.
- 2026-09-10: Decided to add an architecture test that allowlists the public exported types from `OrganizationAccess.Contracts`.
- 2026-09-10: Decided to add an architecture test that keeps `OrganizationAccess.Contracts` free of project references for issue #23.
- 2026-09-10: Decided that `OrganizationId` and `PersonId` reject `Guid.Empty` at construction.
- 2026-09-10: Decided not to add parsing helpers, JSON converters, EF converters, or serializer-specific attributes to the issue #23 contract identifiers.
- 2026-09-10: Decided not to add a dedicated contracts test project for issue #23; the existing architecture test project covers boundary tests plus the empty-Guid behavior.
- 2026-09-10: Completed grilling and selected a single implementation slice: contracts first, then architecture tests, then build/test.

## Implementation Outcome

Implemented on 2026-09-10 in branch `codex/issue-23-organization-access-contract-identities`.

No deviations from the plan.
