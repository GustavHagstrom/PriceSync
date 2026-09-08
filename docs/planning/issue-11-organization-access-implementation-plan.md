# Issue 11: Organization & Access Implementation Plan

**Status:** Ready for implementation

**Authority:** This document preserves the planning process, issue-specific delivery decisions, and implementation sequencing. Current authoritative behavior belongs in the domain documentation, ADRs, tests, and implemented code. After implementation, this plan remains as history and records outcomes, deviations, superseded decisions, and implementation PRs.

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
3. Persistence ownership, schema, and migration strategy. Completed 2026-09-05.
4. Contracts and cross-context exposure. Completed 2026-09-05.
5. Test strategy and implementation sequencing. Completed 2026-09-05.

## Open Decisions

None for the planned issue #11 implementation scope. New decisions should be opened only if implementation reveals a concrete contradiction or missing requirement.

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
- Organization & Access is the system of record for the canonical Person identity and PersonId once Person provisioning and external-account linking are implemented. In issue #11, the API host implements ICurrentPerson by reading an already provisioned, trusted internal Person claim; the Organization endpoints do not create or persist a Person record. Token validation and issuance remain host/authentication infrastructure concerns.
- PriceSync.Modules.OrganizationAccess.Contracts initially exposes only OrganizationId, PersonId, and ICurrentPerson. OrganizationMembershipId, domain types, roles/statuses, Legal Organization Identity, feature handlers, API DTOs, and persistence types remain internal until a concrete cross-context consumer requires a purpose-built contract.
- Issue #11 exposes no generic Organization lookup or authorization interface. Purpose-built cross-context queries are added only when a concrete consuming use case defines the required semantics; direct access to Organization & Access persistence remains forbidden.
- Issue #11 publishes no OrganizationCreated or other domain/integration events and introduces no outbox. Event contracts and delivery guarantees are added only for a concrete consumer; CreateOrganization coordinates its two aggregates directly.
- OrganizationId and PersonId are independent Guid-backed record structs in OrganizationAccess.Contracts with no generic strongly typed ID base or Shared Kernel dependency. EF and JSON conversion are configured specifically at their boundaries.
- Tests are split into PriceSync.Modules.OrganizationAccess.UnitTests for database-free domain and pure application logic, and PriceSync.Modules.OrganizationAccess.IntegrationTests for PostgreSQL, migrations, handlers, transactions, concurrency, authentication adapters, and full Minimal API slices through WebApplicationFactory. Existing ArchitectureTests remain separate; no generic API integration-test project is added yet.
- Every documented invariant within issue #11 maps explicitly to at least one test at the lowest level capable of proving it: unit tests for pure domain rules, PostgreSQL integration tests for constraints/transactions, API integration tests for external behavior, and architecture tests for boundaries. Rules are not duplicated mechanically across all layers.
- OrganizationAccess integration tests share one PostgreSQL Testcontainer per non-parallel test collection, apply migrations once, and reset module tables between tests. Each test owns its data and assumes no execution order; explicit concurrency is created inside individual tests. Other test projects may retain parallel execution.
- Required idempotency/concurrency integration coverage includes sequential replay, semantically equivalent normalized replay, same-key payload mismatch, concurrent same-key replay with one persisted result, and concurrent different-key creation for one Legal Organization Identity with exactly one success and no partial data.
- Required HTTP integration coverage verifies authentication and Problem Details; CreateOrganization success, validation, conflict, Owner, Location, and idempotency behavior; GetOrganization active/archived access and indistinguishable Not Found responses; and ListMyOrganizations membership scoping, archived inclusion, explicit roles, deterministic ordering, and an empty collection result.
- Planning PR #20 is merged before implementation. Issue #11 remains the parent delivery scope and is implemented through four sequential PRs: (A) contracts/domain/unit tests, (B) DbContext/mappings/migration/PostgreSQL constraints, (C) CreateOrganization/auth fake/idempotency/API and concurrency tests, and (D) GetOrganization/ListMyOrganizations/remaining API tests. Each PR is based on main after its predecessor merges and keeps the solution green.
- CI is gated by the explicit invariant, HTTP, concurrency, migration, and architecture test requirements rather than an arbitrary code-coverage percentage. Coverage may be reported informationally until a stable baseline justifies a threshold.
- OrganizationMembership references PersonId in issue #11, but the Person aggregate, provisioning, and identity-provider linking flows are out of scope. The missing Person foreign key is intentional and temporary. Integration tests use a synthetic but stable PersonId; production authentication must not expose these endpoints until it can supply a provisioned canonical PersonId.
- PersonId, OrganizationId, and OrganizationMembershipId should be strongly typed IDs rather than raw Guid/string values.
- OrganizationId and PersonId are boundary-relevant IDs; OrganizationMembershipId starts internal unless an external use case needs it.
- A Person may have at most one active Organization Membership per Organization.
- Initial Organization Roles are Owner and Member.
- An active OrganizationMembership has a non-empty set of Organization Roles.
- Owner semantically includes Member authority.
- Every active Organization must have at least one active Owner after the creation use case completes. Issue #11 enforces this through CreateOrganization because it exposes no membership or role mutations. Future use cases that end memberships or remove Owner roles must coordinate the affected aggregates, prevent removal of the last active Owner, and select a database concurrency strategy for that cross-aggregate invariant.
- Archiving an Organization does not automatically end its Organization Memberships.
- An active member may read an Archived Organization's basic details through GetOrganization and see it in ListMyOrganizations. All commands and other Organization-scoped business operations are denied while it remains Archived; administrative recovery or support operations require separately defined authority.
- Archived is terminal for Organization in issue #11.
- CreateOrganization creates the Organization and the first active Owner membership in the same use case.
- CreateOrganization obtains the initiating PersonId through an application abstraction such as ICurrentPerson; clients must not supply the first Owner's PersonId.
- CreateOrganization is the only mutating application use case in issue #11. Other lifecycle operations remain domain behaviors covered by invariant tests until later application slices require them.
- The initial read use cases are GetOrganization and ListMyOrganizations.
- GetOrganization requires the current Person to have an active membership in the requested Organization.
- ListMyOrganizations returns Organizations where the current Person has an active membership, including that Person's Organization Roles for each result.
- Listing all memberships of an Organization is deferred until a membership-administration slice defines its authorization and exposure rules.
- Active members may read an Archived Organization through GetOrganization, and ListMyOrganizations includes Archived Organizations with their status. Historical membership reads may be added later. All commands and other Organization-scoped business operations are denied while the Organization remains Archived.
- GetOrganization returns Not Found both when the Organization does not exist and when the current Person lacks an active membership, avoiding disclosure of inaccessible Organization identities. Missing authentication/current Person remains Unauthorized.
- Organization Membership must remain a separate aggregate root, not a child collection embedded inside Organization.
- Persistence models, EF navigation properties, and domain objects must not leak across module boundaries.
- Shared kernel must stay technical and must not receive Organization-specific concepts.
- Authentication-provider tokens and sessions are outside the Organization & Access business model.
- Issue #11 introduces ICurrentPerson as the trusted source of PersonId and requires authentication on Organization endpoints, but does not implement an external identity provider, token exchange, internal JWT issuance, Person lifecycle, or identity-account linking. Integration tests supply a fake authenticated Person; production must not fall back to an arbitrary identity when no real adapter is configured.
- Validation is split by responsibility: HTTP/application validation handles request shape and Idempotency-Key requirements; domain value objects and aggregate factories enforce domain validity and normalization; application/persistence mechanisms enforce uniqueness, idempotency consistency, and atomic writes. Issue #11 does not introduce FluentValidation without demonstrated reuse.
- Public HTTP routes use the /api/v1 prefix from the first endpoint. Versioning is expressed through route groups without introducing a versioning library in issue #11.
- Issue #11 implements persistence directly with EF Core and Npgsql against PostgreSQL. Organization & Access owns its DbContext and migrations; no temporary in-memory repository abstraction is introduced. Domain tests remain database-free, while persistence and API integration tests use real PostgreSQL through Testcontainers.
- Modules initially share one physical PostgreSQL database but own separate schemas. Organization & Access uses the organization_access schema for its tables, indexes, migrations history, and other persistence artifacts. Other modules must not query it directly or create cross-schema EF navigations or foreign keys.
- EF Core initially maps the Organization and OrganizationMembership domain aggregates directly through separate infrastructure configuration classes. Domain types remain free of public setters and persistence-oriented navigation properties. This is a pragmatic starting point, not a permanent rule; separate persistence entities may be introduced locally if EF constraints or schema/domain divergence create concrete pressure.
- Strongly typed domain IDs wrap Guid values and are stored as native PostgreSQL uuid columns. PriceSync-created OrganizationId and OrganizationMembershipId values use application-generated UUIDv7; referenced PersonId uses the same physical representation but is not created by Organization & Access.
- OrganizationMembership persists its non-empty set of explicitly assigned roles in organization_access.organization_membership_roles, with one row per membership and role and a composite primary key preventing duplicates. Role implication such as Owner including Member authority is evaluated in domain authorization and does not require redundant persisted role rows; future independent roles may be combined.
- A unique index on Organizations registration country plus normalized national organization number enforces global Legal Organization Identity uniqueness.
- A PostgreSQL partial unique index on OrganizationMembership organization_id plus person_id where status is Active enforces at most one active membership while allowing multiple Ended historical memberships. Application checks provide expected errors; the index remains the concurrency-safe guarantee.
- Organization status, OrganizationMembership status, and Organization Role values are stored as stable lowercase text with database check constraints. Role implication and hierarchy remain domain behavior rather than persisted numeric rank.
- Organization & Access owns CreateOrganization-specific idempotency_records keyed uniquely by PersonId, operation, and Idempotency-Key. Each record stores a canonical request hash plus the original HTTP status and serialized jsonb response. The completed record is committed atomically with Organization and first Owner membership; matching replays return it, while payload mismatch returns Conflict. Issue #11 introduces no generic idempotency middleware or cross-module platform abstraction.
- Successful CreateOrganization idempotency records are retained permanently in the initial model and have no expires_at field. The operation is low-volume, so permanent replay semantics avoid cleanup and key-reuse complexity; future high-volume idempotent operations may define separate retention.
- Feature handlers and queries use the internal OrganizationAccessDbContext directly; issue #11 introduces no custom repository or Unit of Work abstractions. A single SaveChangesAsync is the normal atomic unit of work, except CreateOrganization uses the explicit reservation-first idempotency transaction described below. Database constraint names drive concurrency-safe conflict translation.
- Organization & Access owns and generates its EF Core migrations and uses a module-local migrations history table in organization_access. Development startup and isolated Testcontainers tests may apply migrations automatically; production applies them as a separate deployment step and the running API does not require schema-change privileges.
- Issue #11 adds no generic optimistic-concurrency token because its exposed use cases only create and read. Uniqueness constraints and idempotency cover current races; the first updating slice must choose a concurrency strategy appropriate to its concrete row and cross-row invariants.
- Organization persists CreatedAt and nullable ArchivedAt; OrganizationMembership persists StartedAt and nullable EndedAt. Domain creation/mutation obtains UTC instants through TimeProvider and PostgreSQL stores them as timestamptz. Database checks keep Active paired with a null terminal timestamp and Archived/Ended paired with a non-null timestamp. A generic UpdatedAt is not introduced.
- Organization displayName is required, trimmed at its boundaries, and limited to 1-200 Unicode characters. Internal characters and casing are preserved; the value is mutable, non-unique presentation data and is stored as varchar(200), never used for Organization identity matching.
- Legal Organization Identity is persisted as uppercase registration_country char(2) plus canonical national_organization_number varchar(32), with a composite unique index. Country-specific domain validation remains narrower; Sweden requires exactly ten normalized digits.
- CreateOrganization request_hash is SHA-256 over a deterministic serialization of normalized semantic inputs (trimmed displayName, uppercase registration country, canonical organization number), stored as bytea. Idempotency-Key is a separate case-sensitive opaque value of 1-200 characters stored exactly as received.
- CreateOrganization uses an explicit transaction and first attempts to persist the complete precomputed idempotency response with PostgreSQL INSERT ... ON CONFLICT DO NOTHING, reserving its unique PersonId + operation + key scope without aborting the transaction on conflict. The request that inserts the reservation persists Organization, first Owner membership, and role before committing. A concurrent request with the same key waits for the winning transaction, observes that no row was inserted, then loads the committed record and validates its request hash for replay. Rolling back the winning transaction removes both reservation and domain writes.
- The CreateOrganization transaction uses PostgreSQL Read Committed isolation. Named unique constraints provide serialization for idempotency and Legal Organization Identity conflicts; issue #11 does not use Serializable isolation or whole-operation retries.

- CreateOrganizationHandler creates Organization and the first Owner OrganizationMembership as separate aggregates, then persists both in one atomic operation. Each aggregate factory enforces its own invariants; the handler enforces the cross-aggregate use-case outcome without introducing a domain service.

## Implementation Slices

Issue #11 is delivered through four sequential pull requests. Each pull request starts from `main` after its predecessor has merged, remains independently reviewable, and leaves the complete solution green.

### PR A — Contracts, domain model, and unit tests

**Scope**

- Add `OrganizationId`, `PersonId`, and `ICurrentPerson` to `PriceSync.Modules.OrganizationAccess.Contracts`.
- Implement the Organization, OrganizationMembership, Legal Organization Identity, lifecycle states, roles, strongly typed internal identifiers, and their domain behavior.
- Use `TimeProvider` for lifecycle timestamps.
- Add database-free unit tests that map the issue #11 domain invariants to the lowest sufficient test seam.
- Extend architecture tests where needed to protect contract and visibility boundaries.

**Exit criteria**

- Domain invariants for creation, lifecycle transitions, role assignment, Swedish Legal Organization Identity normalization/validation, and first-Owner creation are executable and covered.
- OrganizationMembership remains a separate aggregate.
- No persistence or HTTP implementation is introduced.
- The solution builds and all tests pass.

### PR B — PostgreSQL persistence and migrations

**Depends on:** PR A.

**Scope**

- Add the module-owned `OrganizationAccessDbContext`, EF Core mappings, and migrations.
- Create the `organization_access` schema and module-local migrations history.
- Add the organizations, organization_memberships, organization_membership_roles, and CreateOrganization-specific idempotency_records tables with the documented constraints and indexes.
- Map strongly typed IDs and UUIDv7 values to PostgreSQL `uuid`.
- Add PostgreSQL Testcontainers infrastructure and integration tests for mappings, migrations, constraints, normalization, and transaction rollback.

**Exit criteria**

- A clean PostgreSQL database can be migrated from zero.
- Database constraints enforce Legal Organization Identity uniqueness, active-membership uniqueness, status/timestamp consistency, valid role/status values, and idempotency-key uniqueness.
- Persistence types remain internal to Organization & Access.
- The solution builds and all tests pass.

### PR C — CreateOrganization API and idempotency

**Depends on:** PR B.

**Scope**

- Add the test authentication adapter and resolve the initiating `PersonId` through `ICurrentPerson`.
- Implement the CreateOrganization feature folder, handler, request/response DTOs, endpoint mapping, validation, and RFC 9457 Problem Details responses.
- Create Organization and its first active Owner membership atomically.
- Implement operation-scoped idempotency using `INSERT ... ON CONFLICT DO NOTHING`, canonical request hashing, exact successful-response replay, and payload-mismatch conflict handling.
- Add handler, PostgreSQL concurrency, and full Minimal API tests for CreateOrganization.

**Exit criteria**

- The required authentication, validation, Created/Location, conflict, first-Owner, idempotency, normalization, rollback, and concurrency scenarios pass against real PostgreSQL.
- Concurrent same-key requests converge on one committed result without an aborted transaction.
- Concurrent different-key requests for the same Legal Organization Identity yield exactly one success and no partial data.
- No generic idempotency middleware or cross-module abstraction is introduced.
- The solution builds and all tests pass.

### PR D — Organization read API

**Depends on:** PR C.

**Scope**

- Implement GetOrganization and ListMyOrganizations as caller-scoped persistence projections.
- Enforce membership access inside each application query.
- Include archived Organizations according to the documented read policy.
- Return only explicitly assigned roles and preserve deterministic list ordering.
- Add the remaining Minimal API integration tests.

**Exit criteria**

- Missing and inaccessible Organizations both return Not Found.
- Active members can read archived Organization basics, while no additional archived-Organization authority is introduced.
- ListMyOrganizations covers membership scoping, archived inclusion, explicit roles, deterministic ordering, and the empty result.
- Persistence entities do not escape the module.
- All issue #11 acceptance criteria are covered and the complete solution is green.

## Initial Persistence Schema

All objects below are owned by Organization & Access and live in the `organization_access` PostgreSQL schema.

### organizations

- `id uuid` primary key
- `display_name varchar(200)` not null
- `registration_country char(2)` not null
- `national_organization_number varchar(32)` not null
- `status text` not null, constrained to `active | archived`
- `created_at timestamptz` not null
- `archived_at timestamptz` nullable, constrained consistently with status
- unique index on `registration_country + national_organization_number`

### organization_memberships

- `id uuid` primary key
- `organization_id uuid` not null, foreign key to module-owned `organizations`
- `person_id uuid` not null, without a cross-module database foreign key
- `status text` not null, constrained to `active | ended`
- `started_at timestamptz` not null
- `ended_at timestamptz` nullable, constrained consistently with status
- partial unique index on `organization_id + person_id` where status is `active`
- partial lookup index on `person_id` where status is `active`

### organization_membership_roles

- `organization_membership_id uuid` foreign key to `organization_memberships`
- `role text` constrained initially to `owner | member`
- composite primary key on `organization_membership_id + role`
- cascading technical delete with the membership; normal domain behavior does not delete memberships

### idempotency_records

- `person_id uuid` not null
- `operation varchar(64)` not null
- `idempotency_key varchar(200)` not null
- `request_hash bytea` not null
- `response_status smallint` not null
- `response_body jsonb` not null
- `created_at timestamptz` not null
- composite primary key on `person_id + operation + idempotency_key`
- successful CreateOrganization records are retained permanently

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
- 2026-09-05: Selected feature-local endpoints, handlers, and DTOs with direct dependency-injected handler calls and no MediatR or generic command/query abstractions.
- 2026-09-05: Decided that CreateOrganizationHandler coordinates creation of the separate Organization and first Owner OrganizationMembership aggregates and persists them atomically without a new domain service.
- 2026-09-05: Limited authentication work in issue #11 to ICurrentPerson, authenticated endpoints, and a test authentication adapter; provider integration, tokens, Person lifecycle, and identity linking remain out of scope.
- 2026-09-05: Split request, domain, and persistence validation by responsibility and deferred FluentValidation until repeated validation needs justify it.
- 2026-09-05: Decided that public API routes use the /api/v1 prefix from the first endpoint without an API-versioning library.
- 2026-09-05: Completed grilling block 2, covering application use cases and the API slice.
- 2026-09-05: Started grilling block 3, covering persistence ownership, schema, and migrations.
- 2026-09-05: Selected EF Core with Npgsql/PostgreSQL, a module-owned DbContext and migrations, and PostgreSQL integration tests through Testcontainers; documented as ADR 0003.
- 2026-09-05: Selected one shared PostgreSQL database with an organization_access schema owned exclusively by Organization & Access, including its own migrations history.
- 2026-09-05: Selected direct EF Core mapping of module-internal aggregates as the starting point, while allowing separate persistence entities later when justified by concrete divergence or EF pressure.
- 2026-09-05: Selected strongly typed Guid-backed IDs, application-generated UUIDv7 for new module-owned identities, and native PostgreSQL uuid storage; documented as ADR 0004.
- 2026-09-05: Confirmed that an OrganizationMembership may have multiple explicitly assigned, potentially independent roles; selected a separate membership-role table with no redundant rows for implied roles.
- 2026-09-05: Selected a partial unique PostgreSQL index for active membership per Organization and Person, alongside the ordinary unique index for normalized Legal Organization Identity.
- 2026-09-05: Selected stable lowercase text storage with check constraints for Organization status, membership status, and roles; role hierarchy remains in domain code.
- 2026-09-05: Defined module-owned idempotency records containing request hash and original response, committed atomically with CreateOrganization domain data.
- 2026-09-05: Chose permanent retention for successful CreateOrganization idempotency records and removed expiry/cleanup from issue #11.
- 2026-09-05: Chose direct DbContext use in feature handlers and queries, relying on SaveChangesAsync as the atomic unit of work without custom repository or Unit of Work abstractions.
- 2026-09-05: Chose automatic migration application for development and Testcontainers tests, with module-owned migrations applied through a separate production deployment step.
- 2026-09-05: Deferred a generic optimistic-concurrency token until the first updating use case can select a strategy against concrete invariants.
- 2026-09-05: Added UTC lifecycle timestamps for Organization creation/archive and membership start/end, with status/timestamp consistency constraints and no generic UpdatedAt.
- 2026-09-05: Defined Organization displayName as trimmed, 1-200-character, case-preserving, non-unique mutable presentation data stored as varchar(200).
- 2026-09-05: Chose char(2) registration country and varchar(32) canonical national organization number columns to support future countries without weakening country-specific validation.
- 2026-09-05: Defined request_hash as SHA-256 over deterministically ordered normalized request values, with an exact case-sensitive 1-200-character Idempotency-Key.
- 2026-09-05: Selected an explicit reservation-first transaction for CreateOrganization so concurrent requests with the same idempotency key deterministically replay the committed winner.
- 2026-09-05: Selected PostgreSQL Read Committed isolation with named unique constraints rather than Serializable isolation and transaction retries.
- 2026-09-05: Confirmed the initial four-table organization_access schema, its foreign keys, checks, uniqueness guarantees, and query indexes.
- 2026-09-05: Completed grilling block 3, covering persistence ownership, schema, idempotency, transactions, and migration strategy.
- 2026-09-05: Started grilling block 4, covering contracts and cross-context exposure.
- 2026-09-05: Assigned ownership of stable Person identity and external-account links to Organization & Access, with protocol authentication in the API host; documented as ADR 0005.
- 2026-09-05: Limited OrganizationAccess.Contracts in issue #11 to OrganizationId, PersonId, and ICurrentPerson; all other module and HTTP types remain internal.
- 2026-09-05: Deferred cross-context Organization lookup and authorization ports until a concrete consumer can define a precise contract.
- 2026-09-05: Deferred OrganizationCreated and other integration/domain events, outbox, and event versioning until a real cross-context consumer exists.
- 2026-09-05: Kept OrganizationId and PersonId as independent Guid-backed record structs without a generic Shared Kernel ID abstraction.
- 2026-09-05: Completed grilling block 4, covering Person ownership, public contracts, and cross-context exposure.
- 2026-09-05: Started grilling block 5, covering test strategy and implementation sequencing.
- 2026-09-05: Selected separate Organization & Access unit and integration test projects, with full HTTP slices tested through the real API host from the module integration project.
- 2026-09-05: Required explicit traceability from every issue #11 invariant to at least one test at the lowest sufficient test level, without routine duplication across layers.
- 2026-09-05: Selected one shared PostgreSQL Testcontainer per non-parallel integration-test collection with migration-once and database reset between tests.
- 2026-09-05: Required five real-PostgreSQL idempotency/concurrency scenarios covering replay, normalization, payload mismatch, same-key races, and Legal Organization Identity races.
- 2026-09-05: Required the initial HTTP contract matrix for authentication, Problem Details, CreateOrganization, GetOrganization, and ListMyOrganizations.
- 2026-09-05: Split issue #11 implementation into four sequential PRs after planning PR #20: domain/contracts, persistence, CreateOrganization, and read slices.
- 2026-09-05: Chose explicit behavior and invariant coverage without a percentage-based CI coverage gate.
- 2026-09-05: Completed grilling block 5 and the full issue #11 planning grilling session.

## Implementation Outcome

Not implemented yet. After issue #11 is delivered, record completion date, implementation PRs, deviations from this plan, and superseded decisions here without rewriting the historical plan.
