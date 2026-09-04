# Issue 11: Organization & Access Implementation Plan

Planning base for [issue #11](https://github.com/GustavHagstrom/PriceSync/issues/11).

## Goal

Implement the first domain foundation for Organization identity and Organization Membership according to `docs/Domain/organization-access.md`, while preserving the module boundaries established by issue #10.

## Current Baseline

- `PriceSync.Modules.OrganizationAccess` exists as the implementation module.
- `PriceSync.Modules.OrganizationAccess.Contracts` exists as the sibling contracts project.
- `PriceSync.Api` composes the module through `AddOrganizationAccessModule`.
- Architecture tests currently verify project structure and dependency rules.
- `docs/Context.md` already defines Organization, Legal Organization Identity, Organization Membership, and Organization Role.

## Proposed Grilling Blocks

The implementation plan should be sharpened through focused grilling blocks. The working hypothesis is that issue #11 needs five blocks:

1. Domain invariants and aggregate boundaries.
2. Application use cases and API slice.
3. Persistence ownership, schema, and migration strategy.
4. Contracts and cross-context exposure.
5. Test strategy and implementation sequencing.

## Open Decisions

- Which legal identity formats and uniqueness rules are required in the first implementation slice?
- Which Organization Membership states are needed initially?
- Which Organization Roles and permissions are required now, and which should stay outside the first slice?
- Which commands and queries are necessary to satisfy the vertical-slice acceptance criteria without widening the module too early?
- Whether persistence should be implemented with EF Core mappings now or deferred behind an in-memory/test seam for the first domain pass.
- Which types belong in `.Contracts` versus remaining internal to the module.

## Working Constraints

- Organization Membership must remain a separate aggregate root, not a child collection embedded inside Organization.
- Persistence models, EF navigation properties, and domain objects must not leak across module boundaries.
- Shared kernel must stay technical and must not receive Organization-specific concepts.
- Authentication-provider tokens and sessions are outside the Organization & Access business model.

## Session Log

- 2026-09-04: Created planning branch and initial PR base for issue #11.
