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

## Grilling Blocks

The implementation plan will be sharpened through five focused grilling blocks:

1. Domain invariants and aggregate boundaries. Completed 2026-09-04.
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

- Legal Organization Identity must be globally unique across PriceSync Organizations.
- Legal Organization Identity is immutable after Organization creation in issue #11.
- Organization lifecycle state starts with Active and Archived only.
- Organization Membership lifecycle state starts with Active and Ended only.
- Ended is terminal for OrganizationMembership in issue #11; re-entry creates a new membership.
- PriceSync has a stable Person identity, and external identity-provider accounts may be linked to that Person over time.
- OrganizationMembership references PersonId in issue #11, but the Person aggregate and identity-provider linking flows are out of scope.
- PersonId, OrganizationId, and OrganizationMembershipId should be strongly typed IDs rather than raw Guid/string values.
- OrganizationId and PersonId are boundary-relevant IDs; OrganizationMembershipId starts internal unless an external use case needs it.
- A Person may have at most one active Organization Membership per Organization.
- Initial Organization Roles are Owner and Member.
- An active OrganizationMembership has a non-empty set of Organization Roles.
- Owner semantically includes Member authority.
- Every active Organization must have at least one active Owner after the creation use case completes.
- Archiving an Organization does not automatically end its Organization Memberships.
- Active memberships in an Archived Organization do not grant ordinary business authority while the Organization remains Archived.
- Archived is terminal for Organization in issue #11.
- CreateOrganization creates the Organization and the first active Owner membership in the same use case.
- CreateOrganization obtains the initiating PersonId through an application abstraction such as ICurrentPerson; clients must not supply the first Owner's PersonId.
- CreateOrganization is the only mutating application use case in issue #11. Other lifecycle operations remain domain behaviors covered by invariant tests until later application slices require them.
- The initial read use cases are GetOrganization and ListMyOrganizations.
- GetOrganization requires the current Person to have an active membership in the requested Organization.
- ListMyOrganizations returns Organizations where the current Person has an active membership, including that Person's Organization Roles for each result.
- Listing all memberships of an Organization is deferred until a membership-administration slice defines its authorization and exposure rules.
- Active members may read an Archived Organization and ListMyOrganizations includes Archived Organizations with their status; archival blocks ordinary business authority rather than historical visibility.
- Organization Membership must remain a separate aggregate root, not a child collection embedded inside Organization.
- Persistence models, EF navigation properties, and domain objects must not leak across module boundaries.
- Shared kernel must stay technical and must not receive Organization-specific concepts.
- Authentication-provider tokens and sessions are outside the Organization & Access business model.

## Session Log

- 2026-09-04: Created planning branch and initial PR base for issue #11.
- 2026-09-04: Accepted five grilling blocks as the planning structure for issue #11.
- 2026-09-04: Decided that Legal Organization Identity is globally unique across PriceSync Organizations.
- 2026-09-04: Decided that Legal Organization Identity is immutable after Organization creation for the first implementation slice.
- 2026-09-04: Decided that Organization lifecycle state starts with Active and Archived only.
- 2026-09-04: Decided that Organization Membership lifecycle state starts with Active and Ended only.
- 2026-09-04: Decided that Ended is terminal for OrganizationMembership in issue #11, and re-entry creates a new membership.
- 2026-09-04: Decided that PriceSync has a stable Person identity, with external identity-provider accounts linked to it over time.
- 2026-09-04: Decided that issue #11 uses PersonId from OrganizationMembership while leaving the Person aggregate and identity-provider linking flows out of scope.
- 2026-09-04: Decided to use strongly typed IDs for PersonId, OrganizationId, and OrganizationMembershipId.
- 2026-09-04: Decided that OrganizationId and PersonId are boundary-relevant, while OrganizationMembershipId starts internal unless needed externally.
- 2026-09-04: Decided that a Person may have at most one active Organization Membership per Organization.
- 2026-09-04: Decided that initial Organization Roles are Owner and Member.
- 2026-09-04: Decided that an active OrganizationMembership has a non-empty set of Organization Roles.
- 2026-09-04: Decided that Owner semantically includes Member authority.
- 2026-09-04: Decided that every active Organization must have at least one active Owner after the creation use case completes.
- 2026-09-04: Decided that archiving an Organization does not automatically end its Organization Memberships, but blocks ordinary business authority while archived.
- 2026-09-04: Decided that Archived is terminal for Organization in issue #11.
- 2026-09-04: Decided that CreateOrganization creates the Organization and first active Owner membership in one use case.
- 2026-09-04: Completed grilling block 1, covering domain invariants and aggregate boundaries.
- 2026-09-05: Decided that CreateOrganization resolves the initiating PersonId through ICurrentPerson rather than accepting the first Owner's PersonId from the client.
- 2026-09-05: Limited issue #11 to CreateOrganization as its only mutating application use case; archive, membership-management, and role-management endpoints are deferred.
- 2026-09-05: Selected GetOrganization and ListMyOrganizations as the initial read use cases; listing all Organization Memberships is deferred.
- 2026-09-05: Decided that active members retain read access to Archived Organizations and that ListMyOrganizations includes them with Archived status.
