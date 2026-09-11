# PR B: PostgreSQL Persistence and Migrations Spec

## Summary

Implement the Organization & Access persistence foundation for issue #11.

This PR should add the module-owned PostgreSQL schema, EF Core DbContext, aggregate mappings, first migration, and integration-test infrastructure needed before the CreateOrganization API/idempotency slice is built in PR C.

## Background

PR A introduced the Organization & Access contracts, domain model, strongly typed IDs, Legal Organization Identity validation, Organization lifecycle, Organization Membership lifecycle, and domain/unit-test coverage.

PR B is the persistence slice that makes those domain objects durable without adding HTTP endpoints, application feature handlers, authentication adapters, or CreateOrganization request handling. It should prove the database can enforce the invariants that require PostgreSQL guarantees: uniqueness, status/timestamp consistency, role validity, idempotency-record keying, transaction rollback, and module-owned schema boundaries.

## Source Material

This spec is derived from `docs/planning/issue-11-organization-access-implementation-plan.md`, specifically:

- `PR B -- PostgreSQL persistence and migrations`
- `Initial Persistence Schema`
- the persistence, idempotency, migration, and integration-test working constraints for issue #11

## Scope

- Add an internal `OrganizationAccessDbContext` owned by `PriceSync.Modules.OrganizationAccess`.
- Register the DbContext through the Organization & Access module composition surface.
- Configure EF Core/Npgsql persistence for the PR A domain objects without exposing persistence types outside the module.
- Add module-local mapping/configuration classes for:
  - `Organization`
  - `OrganizationMembership`
  - membership roles
  - CreateOrganization idempotency records
- Add the first EF Core migration for the `organization_access` schema.
- Configure a module-local EF migrations history table in the `organization_access` schema.
- Create PostgreSQL tables:
  - `organization_access.organizations`
  - `organization_access.organization_memberships`
  - `organization_access.organization_membership_roles`
  - `organization_access.idempotency_records`
- Map strongly typed IDs to native PostgreSQL `uuid` columns.
- Preserve application-generated UUIDv7 values for new Organization & Access identities.
- Store lifecycle timestamps as `timestamptz`.
- Store status and role values as stable lowercase text.
- Add PostgreSQL Testcontainers infrastructure for Organization & Access integration tests.
- Add integration tests proving mappings, migrations, constraints, normalization persistence, and transaction rollback.
- Keep the complete solution green.

## Out of Scope

- CreateOrganization feature handler, endpoint, request/response DTOs, validation, authentication, or Problem Details behavior.
- Implementing `INSERT ... ON CONFLICT DO NOTHING` idempotency orchestration. PR B creates and constrains the `idempotency_records` table only; PR C uses it.
- Generic idempotency middleware or cross-module idempotency abstractions.
- GetOrganization/ListMyOrganizations query handlers or API endpoints.
- Production deployment automation for applying migrations.
- Generic repository or Unit of Work abstractions.
- A Person table or cross-module foreign key to Person.
- Domain events, integration events, or outbox tables.
- Generic optimistic-concurrency tokens.
- Broad refactors outside Organization & Access persistence and tests.

## Persistence Contract

All objects live in the `organization_access` schema and are owned exclusively by Organization & Access. Other modules must not create cross-schema EF navigations, foreign keys, or direct queries against this schema.

### organizations

- `id uuid` primary key
- `display_name varchar(200)` not null
- `registration_country char(2)` not null
- `national_organization_number varchar(32)` not null
- `status text` not null
- `created_at timestamptz` not null
- `archived_at timestamptz` nullable
- unique index on `registration_country, national_organization_number`
- check constraint limiting `status` to `active` or `archived`
- check constraint requiring:
  - Active organizations have `archived_at is null`
  - Archived organizations have `archived_at is not null`

### organization_memberships

- `id uuid` primary key
- `organization_id uuid` not null, foreign key to `organization_access.organizations(id)`
- `person_id uuid` not null, with no database foreign key outside the module
- `status text` not null
- `started_at timestamptz` not null
- `ended_at timestamptz` nullable
- partial unique index on `organization_id, person_id` where `status = 'active'`
- partial lookup index on `person_id` where `status = 'active'`
- check constraint limiting `status` to `active` or `ended`
- check constraint requiring:
  - Active memberships have `ended_at is null`
  - Ended memberships have `ended_at is not null`

### organization_membership_roles

- `organization_membership_id uuid` foreign key to `organization_access.organization_memberships(id)`
- `role text` not null
- composite primary key on `organization_membership_id, role`
- check constraint limiting `role` to `owner` or `member`
- cascade delete is acceptable as a technical database relationship cleanup; normal domain behavior must not delete memberships
- persisted roles are explicit assignments only; do not persist implied `member` rows for `owner`

### idempotency_records

- `person_id uuid` not null
- `operation varchar(64)` not null
- `idempotency_key varchar(200)` not null
- `request_hash bytea` not null
- `response_status smallint` not null
- `response_body jsonb` not null
- `created_at timestamptz` not null
- composite primary key on `person_id, operation, idempotency_key`
- no `expires_at` column in this issue
- records are retained permanently in the initial model

## Implementation Notes

- Prefer direct EF Core mapping of the existing internal domain aggregates as the initial approach.
- Keep domain types free of public setters and persistence-oriented navigation properties.
- Introduce separate persistence entities only if EF constraints make direct aggregate mapping materially worse.
- Keep mapping code internal to `PriceSync.Modules.OrganizationAccess`.
- Keep `OrganizationMembershipId` internal.
- Keep `OrganizationId`, `PersonId`, and `OrganizationMembershipId` physically stored as `uuid`.
- Store `registration_country` as uppercase `char(2)` and `national_organization_number` as the canonical normalized value.
- Swedish organization numbers must persist as ten normalized digits after domain creation has accepted values with or without a hyphen.
- Use named constraints and indexes where later application code will need reliable conflict translation.
- Do not add a temporary in-memory repository abstraction.
- Do not add a Shared Kernel concept for Organization-specific persistence.
- Development/test startup may apply migrations automatically, but production runtime should not require schema-change privileges.

## Integration Test Scope

Add `PriceSync.Modules.OrganizationAccess.IntegrationTests` if it does not already exist.

The integration tests should use real PostgreSQL through Testcontainers. They should share one PostgreSQL container per non-parallel test collection, apply migrations once, and reset Organization & Access tables between tests. Each test owns its own data and must not depend on execution order.

Required coverage:

- A clean PostgreSQL database can migrate from zero.
- The module-local migrations history table is created in `organization_access`.
- Organizations persist and reload with strongly typed IDs, Legal Organization Identity, status, and timestamps intact.
- Organization Memberships persist and reload with strongly typed IDs, `PersonId`, status, timestamps, and explicit roles intact.
- Legal Organization Identity uniqueness is enforced by the database.
- Active membership uniqueness per Organization and Person is enforced by the partial unique index.
- Multiple ended memberships for the same Organization and Person are allowed.
- Invalid organization statuses are rejected by database constraints.
- Invalid membership statuses are rejected by database constraints.
- Invalid role values are rejected by database constraints.
- Active/archived and active/ended timestamp consistency is enforced by database constraints.
- Duplicate membership-role rows are rejected by the composite primary key.
- Duplicate idempotency records for the same `person_id`, `operation`, and `idempotency_key` are rejected.
- The same idempotency key may be reused by a different Person or operation.
- A transaction rollback leaves no persisted Organization, Membership, role, or idempotency record partial data.
- Persistence infrastructure remains internal to Organization & Access and does not widen module boundaries.

## Acceptance Criteria

- The PR adds the Organization & Access DbContext, mappings, migration, schema, tables, constraints, and indexes described above.
- A clean PostgreSQL database can be migrated from zero.
- Database constraints enforce Legal Organization Identity uniqueness, active-membership uniqueness, lifecycle status/timestamp consistency, valid status values, valid role values, role uniqueness, and idempotency-key uniqueness.
- Strongly typed IDs round-trip through PostgreSQL `uuid` columns.
- Explicit Organization roles round-trip without expanding implied authority into persisted rows.
- Integration tests run against real PostgreSQL and cover the required persistence behavior.
- Persistence types remain internal to Organization & Access.
- The solution builds and all tests pass.

## Suggested Review Focus

- Whether the database schema precisely matches the issue #11 persistence decisions.
- Whether EF mappings keep persistence concerns inside the module without weakening the domain model.
- Whether named constraints/indexes are stable enough for PR C conflict translation.
- Whether Testcontainers setup is reliable, isolated, and not order-dependent.
- Whether idempotency storage supports PR C without introducing generic idempotency middleware.
- Whether no cross-module persistence dependency has leaked into the model.

## Follow-up PRs

- PR C: CreateOrganization API, test authentication adapter, idempotency orchestration, handler/API tests, and concurrency behavior.
- PR D: GetOrganization/ListMyOrganizations read API and remaining HTTP integration tests.
