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
2. Application use cases and API slice. Completed 2026-09-05.
3. Persistence ownership, schema, and migration strategy.
4. Contracts and cross-context exposure.
5. Test strategy and implementation sequencing.

## Open Decisions

- Whether persistence should be implemented with EF Core mappings now or deferred behind an in-memory/test seam for the first domain pass.
- Which types belong in `.Contracts` versus remaining internal to the module.

## Working Constraints

- Legal Organization Identity must be globally unique across PriceSync Organizations.
- Legal Organization Identity is immutable after Organization creation in issue #11.
- Issue #11 supports Swedish Legal Organization Identities only: registration country SE and a Swedish organization number accepted with or without a hyphen, validated including its check digit, and normalized to ten digits before storage and uniqueness comparison.
- CreateOrganization fails with Conflict and a stable organization_legal_identity_conflict error code when the normalized Legal Organization Identity already exists. It does not disclose the existing Organization or grant membership; access recovery is a separate future use case.
- POST /api/v1/organizations requires an Idempotency-Key scoped to the current Person and CreateOrganization use case. Replaying the same key with the same request returns the original successful result; reusing it with a different request returns Conflict. Different keys remain subject to Legal Organization Identity uniqueness. Persistence and retention are resolved in grilling block 3.
- CreateOrganization accepts displayName, explicit registrationCountry, and organizationNumber. Success returns Created with a Location header and an Organization representation containing OrganizationId, displayName, registrationCountry, the normalized organizationNumber, Active status, and the current Person's Owner role. OrganizationMembershipId remains internal.
- GET /api/v1/organizations implements ListMyOrganizations as a caller-scoped collection, never a global administrative list. It is initially unpaginated, includes Active and Archived Organizations, sorts Active first and then by displayName, and returns each Organization's canonical identity, status, and the current Person's roles.
- API errors use RFC 9457 Problem Details with a stable code extension. Missing authentication maps to Unauthorized; missing or inaccessible Organizations to Not Found; Legal Organization Identity and idempotency payload conflicts to Conflict; missing idempotency keys, invalid fields, and unsupported countries to Bad Request; unexpected failures return Internal Server Error without internal details.
- API role collections expose only roles explicitly assigned to the current Organization Membership. Role implication such as Owner including Member authority is evaluated for authorization and does not expand the returned or persisted role set.
- Membership-based Organization access is enforced inside each application query using the PersonId from ICurrentPerson. API endpoints enforce coarse authentication, while queries scope retrieval to active membership so inaccessible and missing Organizations share the same Not Found result.
- GetOrganization and ListMyOrganizations query module-owned persistence directly and project to response DTOs without rehydrating domain aggregates. Commands use aggregates when enforcing domain behavior; query projections must not leak persistence entities outside the module.
- Organization & Access owns its Minimal API endpoint definitions and exposes MapOrganizationAccessEndpoints as a narrow public composition surface alongside AddOrganizationAccessModule. PriceSync.Api owns host-level HTTP concerns and invokes module endpoint mapping; endpoint handlers and API DTOs remain internal to the module.
- Use cases are organized in feature folders for CreateOrganization, GetOrganization, and ListMyOrganizations. Each feature keeps its internal endpoint, handler, and local request/response types together; handlers are invoked directly through dependency injection without MediatR, generic command/query abstractions, or an internal message bus.
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
- GetOrganization returns Not Found both when the Organization does not exist and when the current Person lacks an active membership, avoiding disclosure of inaccessible Organization identities. Missing authentication/current Person remains Unauthorized.
- Organization Membership must remain a separate aggregate root, not a child collection embedded inside Organization.
- Persistence models, EF navigation properties, and domain objects must not leak across module boundaries.
- Shared kernel must stay technical and must not receive Organization-specific concepts.
- Authentication-provider tokens and sessions are outside the Organization & Access business model.
- Issue #11 introduces ICurrentPerson as the trusted source of PersonId and requires authentication on Organization endpoints, but does not implement an external identity provider, token exchange, internal JWT issuance, Person lifecycle, or identity-account linking. Integration tests supply a fake authenticated Person; production must not fall back to an arbitrary identity when no real adapter is configured.
- Validation is split by responsibility: HTTP/application validation handles request shape and Idempotency-Key requirements; domain value objects and aggregate factories enforce domain validity and normalization; application/persistence mechanisms enforce uniqueness, idempotency consistency, and atomic writes. Issue #11 does not introduce FluentValidation without demonstrated reuse.
- Public HTTP routes use the /api/v1 prefix from the first endpoint. Versioning is expressed through route groups without introducing a versioning library in issue #11.

- CreateOrganizationHandler creates Organization and the first Owner OrganizationMembership as separate aggregates, then persists both in one atomic operation. Each aggregate factory enforces its own invariants; the handler enforces the cross-aggregate use-case outcome without introducing a domain service.

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
- 2026-09-05: Decided that GetOrganization returns Not Found rather than Forbidden when the current Person lacks an active membership.
- 2026-09-05: Limited issue #11 to Swedish Legal Organization Identities, normalized to ten digits and validated including the check digit; other registration countries are rejected as unsupported.
- 2026-09-05: Decided that duplicate Legal Organization Identity returns Conflict with a stable error code, without disclosing the existing Organization or creating a membership.
- 2026-09-05: Decided that CreateOrganization requires an Idempotency-Key scoped to the current Person and use case, with deterministic replay and payload-mismatch conflict semantics.
- 2026-09-05: Defined the CreateOrganization request and Created response, including explicit registration country, canonical legal identity, Location, current-person roles, and no OrganizationMembershipId exposure.
- 2026-09-05: Defined GET /api/v1/organizations as an initially unpaginated, caller-scoped ListMyOrganizations endpoint including Archived Organizations and current-person roles.
- 2026-09-05: Standardized API errors on RFC 9457 Problem Details with stable error codes and the initial HTTP status mapping.
- 2026-09-05: Decided that API role collections expose explicit role assignments only; implied authority is not returned as additional roles.
- 2026-09-05: Decided that membership-based access is enforced in application queries and included in the scoped data lookup rather than existing only at the API endpoint.
- 2026-09-05: Decided that read use cases project directly from module-owned persistence to DTOs rather than rehydrating aggregates.
- 2026-09-05: Decided that modules own and map their Minimal API endpoints through a narrow Map{Module}Endpoints composition surface; documented as ADR 0002.
- 2026-09-05: Decided that CreateOrganizationHandler coordinates creation of the separate Organization and first Owner OrganizationMembership aggregates and persists them atomically without a new domain service.
- 2026-09-05: Limited authentication work in issue #11 to ICurrentPerson, authenticated endpoints, and a test authentication adapter; provider integration, tokens, Person lifecycle, and identity linking remain out of scope.
- 2026-09-05: Split request, domain, and persistence validation by responsibility and deferred FluentValidation until repeated validation needs justify it.
- 2026-09-05: Decided that public API routes use the /api/v1 prefix from the first endpoint without an API-versioning library.
- 2026-09-05: Completed grilling block 2, covering application use cases and the API slice.
- 2026-09-05: Selected feature-local endpoints, handlers, and DTOs with direct dependency-injected handler calls and no MediatR or generic command/query abstractions.
