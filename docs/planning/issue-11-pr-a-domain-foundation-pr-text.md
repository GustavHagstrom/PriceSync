# PR A: Organization & Access Domain Foundation

## Summary

Introduce the first domain foundation for Organization identity and Organization Membership in the Organization & Access module.

This PR establishes the core domain model decisions from issue #11 without adding persistence, API endpoints, authentication-provider integration, or cross-module exposure beyond the contracts needed for boundary-relevant identifiers.

## Background

Issue #11 defines Organization & Access as the area that owns legal business-party identity and the rules for who may act for an Organization.

The current module exists as a modular-monolith shell. This PR fills in the first domain layer so later PRs can add application use cases, persistence, API slices, and contract exposure without widening the initial domain model too early.

## Scope

- Add strongly typed identities for:
  - `OrganizationId`
  - `PersonId`
  - `OrganizationMembershipId`
- Model Legal Organization Identity as the combination of registration country and national organization number.
- Add the `Organization` aggregate root with:
  - immutable Legal Organization Identity after creation
  - initial lifecycle states `Active` and `Archived`
  - terminal archive behavior for the first implementation slice
- Add the `OrganizationMembership` aggregate root with:
  - references to `OrganizationId` and `PersonId`
  - initial lifecycle states `Active` and `Ended`
  - terminal ended behavior for the first implementation slice
  - non-empty role set while active
- Add initial Organization Roles:
  - `Owner`
  - `Member`
- Encode role semantics so Owner includes Member authority.
- Keep Organization Membership as a separate aggregate root, not a child collection on Organization.
- Add focused domain tests for the invariants introduced in this PR.

## Out of Scope

- EF Core mappings, migrations, database schema, repositories, or persistence models.
- HTTP endpoints or application command/query handlers.
- Authentication-provider tokens, sessions, accounts, or identity-provider linking flows.
- Full Person aggregate lifecycle.
- Organization membership reactivation. Re-entry after an ended membership is handled by creating a new membership in a later use case.
- Authorization policies for ordinary business actions in archived Organizations.
- Cross-context query contracts beyond boundary-relevant IDs required by this slice.
- Supplier/Customer interpretation in downstream modules.

## Acceptance Criteria

- Organization identity cannot be created without a registration country and national organization number.
- Organization Legal Organization Identity cannot be changed after Organization creation.
- A new Organization starts as Active.
- An Active Organization can be archived.
- An Archived Organization cannot be restored in this implementation slice.
- A new Organization Membership starts as Active.
- An Active Organization Membership must have at least one role.
- An Active Organization Membership can be ended.
- An Ended Organization Membership cannot be restored in this implementation slice.
- Owner membership authority includes Member authority.
- Organization Membership is represented as its own aggregate root.
- The Shared Kernel remains technical and does not receive Organization-specific concepts.
- Domain objects and persistence concerns remain inside module boundaries.
- Architecture tests still pass.

## Suggested Review Focus

- Whether the aggregate boundaries match `docs/Domain/organization-access.md`.
- Whether the strongly typed IDs are placed at the right boundary:
  - `OrganizationId` and `PersonId` are boundary-relevant.
  - `OrganizationMembershipId` remains internal unless a later use case needs to expose it.
- Whether invariants are enforced by construction and behavior rather than by test setup alone.
- Whether the PR avoids prematurely adding persistence, API, or auth-provider concepts.

## Follow-up PRs

- PR B: Application use cases and API slice for creating Organizations and first Owner memberships.
- PR C: Persistence ownership, schema, repository, and migration strategy.
- PR D: Contracts and cross-context exposure for Organization identity and membership facts.

## Notes

The issue #11 planning branch did not contain explicit PR A-D sections in the checked-out plan file. This PR text treats PR A as the first domain-foundation slice implied by the recorded decisions and working constraints.
