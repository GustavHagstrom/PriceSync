# Issue #12: Trading Relationships Implementation Plan

> Status: Ready for implementation
>
> Authority: This file records planning decisions and history. Current domain language,
> architecture, tests, and code remain authoritative for implemented behaviour.

Planning base for issue #12, `Implement Trading Relationships`.

## Goal

Implement the directed Supplier → Customer Trading Relationship boundary according to the existing domain model: aggregate lifecycle/status, Organization references by ID, create/end use cases, module-owned persistence, and verification of lifecycle and ownership boundaries.

## Current Baseline

- `TradingRelationship` is the aggregate root for one directed Supplier-to-Customer relationship.
- It owns the parties by Organization ID, relationship lifecycle/status, and the fact/history that the relationship has ended.
- It does not own Organization identity, memberships, permissions, or commercial pricing terms.
- PriceSync represents an externally established relationship; negotiation, requesting, and commercial acceptance are outside this boundary.
- Ending a relationship stops future Supplier-backed distribution without rewriting historical pricing, activation, delivery, or retrieval facts.
- Agreements reference `TradingRelationshipId`; adjacent modules consume relationship identity/status without owning its lifecycle.
- `PriceSync.Modules.TradingRelationships` and its Contracts project already exist; the module is wired into the API but currently contains only registration scaffolding.
- Architecture tests already recognize TradingRelationships as a module boundary.

## Grilling Blocks

All planning blocks are complete. The plan is ready for implementation decomposition through `/to-tickets`.

#### Block 1 — Aggregate model and lifecycle

- Status: Complete
- Completed: 2026-09-17
- Purpose: Define the complete domain semantics of Trading Relationships, including rules that may span multiple relationship instances.
- Decisions:
  - Aggregate identity and required state — Complete. `TradingRelationship` has stable `TradingRelationshipId`; directed `(SupplierOrganizationId, CustomerOrganizationId)` is a domain-significant natural key but not aggregate identity. Required state is ID, Supplier/Customer IDs, lifecycle Status, CreatedAt and nullable EndedAt; no generic UpdatedAt domain state.
  - Supplier/Customer relationship invariants — Complete. Parties must be distinct, direction is significant, reverse relationships may coexist, and party IDs are immutable.
  - Cross-aggregate rules — Complete. At most one active relationship per exact directed pair. Re-establishment after ending creates a new relationship/new ID and preserves the old relationship as history.
  - Lifecycle — Complete. Only Active and Ended; new relationships start Active; Active → Ended is the only transition and Ended is terminal.
  - Temporal/history semantics — Complete. CreatedAt is PriceSync registration time. EndedAt is the PriceSync end-transition time, set once. Ending retains the complete aggregate and historical references.
- Verification:
  - Focused domain tests cover meaningful aggregate invariants/lifecycle. Cross-aggregate uniqueness/concurrency verification is deferred to Block 4.
- Depends on:
  - None
- Completion: Aggregate state, invariants, lifecycle transitions, cross-aggregate domain rules, and historical semantics are precise enough for later blocks to determine enforcement.

#### Block 2 — Organization boundary

- Status: Complete
- Completed: 2026-09-17
- Purpose: Define how TradingRelationships depends on OrganizationAccess while preserving module ownership and contextual Supplier/Customer roles.
- Decisions:
  - Organization validation — Complete. New creation and re-establishment require both Organizations to exist and be Active. Validation is point-in-time with no cross-module transaction/lock. Later Organization archival does not automatically end relationships, and ending does not revalidate Organization status.
  - OrganizationAccess contract — Complete. `OrganizationAccess.Contracts` exposes a purpose-built single-Organization status query, called separately for Supplier and Customer, returning minimal Active/Archived/NotFound semantics. No generic read model, names, metadata, roles or eligibility abstraction. TradingRelationships does not cache/persist Organization status.
  - Ownership — Complete. OrganizationAccess owns Organization identity/lifecycle/memberships/permissions. TradingRelationships owns Supplier/Customer contextual roles and the rule that new relationships require Active Organizations. It stores only Organization IDs.
  - Dependency/failure semantics — Complete. TradingRelationships depends only on OrganizationAccess.Contracts. Validation occurs in the application/use-case layer before aggregate creation. Active/Archived/NotFound are expected results; technical failures fail the operation and are not translated to NotFound. External HTTP/error mapping belongs to Block 3.
- Verification:
  - Focused application/integration tests verify Active/Archived/NotFound creation paths and re-establishment validation. Architecture verification protects against direct OrganizationAccess implementation/persistence dependencies.
- Depends on:
  - Block 1
- Completion: Organization references, incoming cross-module dependency, validation responsibilities, and ownership boundaries are explicit.

#### Block 3 — Use cases and external API surface

- Status: Complete
- Completed: 2026-09-17
- Purpose: Define the vertical slices and externally observable behaviour exposed by the TradingRelationships module, including authorization at the use-case boundary.
- Decisions:
  - Create relationship command semantics — Complete. Either party may initiate; acting Organization must be a party, requires management capability, archived actors cannot Create, unrelated actors receive 403 before counterparty validation, request contains both party IDs, no counterparty approval is modeled, and repeated Create for an already Active directed pair is idempotent `200 OK` returning the existing representation.
  - End relationship command semantics — Complete. Either party with management capability may End; archived acting Organizations cannot End, input is relationship ID only, out-of-scope/unknown relationships are 404, repeated End is idempotent and preserves EndedAt, and successful End returns `200 OK` with final representation.
  - Required read/query use cases and query semantics — Complete. Direction-specific Supplier/Customer lists derive scope from acting Organization, default to Active with Ended/all options, use opaque cursor/keyset pagination ordered `CreatedAt DESC, TradingRelationshipId DESC`, and expose scoped GetById with unknown/out-of-scope both 404.
  - Request/response contracts and error semantics — Complete. Create/End/Get/List share a compact relationship representation. Expected failures use stable machine-readable codes; missing/archived parties, self-relationship, missing acting Organization, authorization and hidden out-of-scope semantics are defined. Authentication/401 remains cross-cutting.
  - Actor/organization authorization requirements — Complete. OrganizationAccess owns membership/role interpretation and exposes capability-based authorization; reads require read capability, commands require management capability. Acting Organization is trusted request context but does not itself authorize.
  - Idempotency and repeated-operation behaviour — Complete. Repeated Active-pair Create and already-Ended End are idempotent successes; no separate idempotency key is required.
  - Domain-specific audit/observability requirements — Complete. Successful and failed/denied Create/End attempts must be auditable; read auditing and logging/telemetry implementation remain cross-cutting.
- Verification:
  - Focused integration/API tests cover the main externally observable behaviours for Create, End, GetById and directional lists, including authorization/scope, idempotency, status filtering/pagination and important error paths. Avoid a mechanical exhaustive matrix when behaviour is already protected at a more focused layer.
- Depends on:
  - Block 1
  - Block 2
- Completion: Required vertical slices, query surface, authorization, contracts, and externally observable success/failure semantics are fully defined.

#### Block 4 — Persistence, consistency, and concurrency

- Status: Complete
- Completed: 2026-09-17
- Purpose: Define module-owned storage and how the domain rules from Block 1 are enforced under real database and concurrent execution.
- Decisions:
  - DbContext/schema/entity mapping and migration ownership — Complete.
    - Accepted: `TradingRelationship` is mapped directly by EF Core; no separate persistence entity/mapping layer is introduced.
    - Accepted: TradingRelationships owns PostgreSQL schema `trading_relationships`, its DbContext, entity mappings and migrations, following ADR-0003.
    - Accepted: The aggregate persists as one relationship table with its stable ID, Supplier/Customer Organization IDs, Status, CreatedAt and nullable EndedAt.
    - Accepted: `TradingRelationshipId` is generated by the application before persistence according to PriceSync's established UUID/ID convention; the exact UUID version is an implementation/convention detail rather than a Block 4 architecture decision.
    - Accepted: CreatedAt, party IDs and other immutable aggregate state are protected by domain/application/EF ownership rather than database triggers.
    - Rationale: The persistence shape closely matches the small aggregate, so a separate persistence model adds ceremony without a current isolation benefit. A dedicated schema preserves the physical module boundary, while aggregate identity exists independently of persistence.
  - Database constraints and indexes required to enforce resolved domain rules — Complete.
    - Accepted: At most one Active relationship for a directed `(SupplierOrganizationId, CustomerOrganizationId)` pair is enforced both by the normal application query/idempotent path and by a PostgreSQL partial unique index over the directed pair filtered to Active rows.
    - Accepted: `Status` is stored as readable text (`Active`/`Ended`) rather than an integer or PostgreSQL enum, with a database CHECK constraint restricting allowed values.
    - Accepted: A CHECK constraint enforces state consistency: Active requires `EndedAt IS NULL`; Ended requires `EndedAt IS NOT NULL`. The temporal rule that EndedAt is set once remains a domain/application responsibility.
    - Accepted: A CHECK constraint also enforces `SupplierOrganizationId <> CustomerOrganizationId` as a cheap database-level backstop for the domain invariant.
    - Accepted: Supplier/Customer Organization IDs are external module references and have no foreign keys to `organization_access` persistence. Organization existence/status is validated through the OrganizationAccess contract.
    - Accepted: Directional list access paths get two composite indexes, one Supplier-oriented and one Customer-oriented, shaped around organization ID, status, and the decided `CreatedAt DESC, TradingRelationshipId DESC` keyset ordering.
    - Accepted: These indexes optimize the default/status-filtered list paths. The less common `all` status query may require an additional sort; no duplicate set of status-free indexes is added without observed need.
    - Rationale: Database constraints protect cheap local invariants and close concurrency races while avoiding persistence coupling across module boundaries. Query indexes follow known current access paths without pre-optimizing every filter variant.
  - Transaction boundaries — Complete.
    - Accepted: Create does not introduce an explicit transaction around the pre-insert lookup and insert. OrganizationAccess validation remains outside any cross-module transaction, and EF Core/SaveChanges uses its normal write transaction. The partial unique index is the concurrency guarantee.
    - Accepted: End does not wrap its atomic conditional update and subsequent result/readback query in an explicit transaction; the conditional update itself is the consistency boundary.
    - Rationale: Normal-isolation explicit transactions do not remove the Create race, and Serializable is unnecessary when the expected race is resolved by the unique constraint. Ended is terminal, so the post-update read cannot observe another legitimate lifecycle transition that would require a wider transaction.
  - Concurrency/race handling for lifecycle-changing operations — Complete.
    - Accepted: On concurrent Create, violation of the specific Active-pair partial unique index is treated as the expected losing race; the winner's Active relationship is read back and returned through the already-defined idempotent `200 OK` semantics. Other database failures are not translated into idempotent success.
    - Accepted: End uses an atomic conditional update that transitions only a row currently in Active state. If zero rows are updated, the relationship is read again: an existing Ended relationship is idempotent success preserving its original EndedAt, while an unknown relationship remains `404 Not Found` subject to the already-defined scope semantics.
    - Accepted: The End transition timestamp comes from PriceSync's application clock/`TimeProvider` and the same timestamp is supplied to the atomic update; PostgreSQL does not define the business transition time.
    - Accepted: The End vertical slice may perform the atomic state transition directly in persistence (for example EF Core `ExecuteUpdateAsync`) rather than materializing the aggregate solely to call its lifecycle method. Persistence verification must protect equivalence with the domain lifecycle invariant.
    - Accepted: No generic optimistic concurrency token/`xmin`, pessimistic locking or general retry mechanism is introduced. The known races are handled explicitly by the unique constraint and conditional update.
    - Rationale: Constraints/conditional updates make PostgreSQL the atomic arbiter for the small state transitions without introducing concurrency infrastructure that has no additional current problem to solve.
  - Historical retention semantics in persistence — Complete.
    - Accepted: Ended relationships remain durable historical rows and are never reused when the same directed pair is re-established; re-establishment inserts a new aggregate/new ID.
    - Accepted: Normal TradingRelationships application functionality exposes no physical delete path. Any future legal/retention/administrative purge requirement is separate cross-cutting work and must be designed explicitly if needed.
    - Rationale: Relationship history is domain-significant and downstream historical references must remain stable; deletion semantics should not be invented without a concrete requirement.
- Verification:
  - Use real PostgreSQL integration tests for database-specific guarantees, including the Active-pair partial unique index, CHECK constraints, concurrent Create and concurrent End behaviour.
  - Keep ordinary mapping/query verification focused and avoid duplicating the same behaviour mechanically across domain, persistence and full API layers.
- Depends on:
  - Block 1
  - Block 3
- Completion: Persistence structure, database guarantees, transaction scope, and concurrency behaviour are implementable and preserve the domain semantics resolved in Block 1.

#### Block 5 — Minimal public module boundary

- Status: Complete
- Completed: 2026-09-17
- Purpose: Define only the public TradingRelationships module boundary required by issue #12, without proactively designing future consumer integrations.
- Decisions:
  - Minimum contracts required by issue #12 — Complete.
    - Accepted: Issue #12 introduces no cross-module query contract for Trading Relationship identity/status because no current consumer requires one.
    - Accepted: `PriceSync.Modules.TradingRelationships.Contracts` may remain empty/minimal; no type is introduced merely because the project exists.
    - Accepted: HTTP request/response models belong to the TradingRelationships Vertical Slices and are not placed in the Contracts project. Contracts is reserved for real module-to-module contracts.
  - Minimum relationship identity/status access outside the module — Complete.
    - Accepted: No such access is required by current issue #12 scope. The concrete shape of a future Agreements or other consumer contract is deliberately deferred until that consumer is implemented.
    - Accepted: Future modules do not consume the TradingRelationships domain model directly; they use explicit contracts designed from their concrete needs.
  - Dependency direction and ownership — Complete.
    - Accepted: The API host is the composition root and may reference `PriceSync.Modules.TradingRelationships` directly for module registration and endpoint mapping; this is not a cross-module domain dependency.
    - Accepted: Other feature modules must not reference TradingRelationships implementation or persistence directly. A future cross-module dependency must go through an explicit TradingRelationships contract.
    - Accepted: `TradingRelationships.Contracts` remains independent of the TradingRelationships implementation.
    - Accepted: As a default design principle, TradingRelationships public contracts own their own contract types. Contracts-to-Contracts dependencies are introduced only when a concrete need justifies them rather than being established speculatively.
    - Accepted: No architecture test is added yet for hypothetical Contracts-to-Contracts dependencies that do not exist.
- Verification:
  - Architecture tests protect the actual current boundary by preventing other feature modules from directly referencing the TradingRelationships implementation.
  - No additional contract-behaviour verification is required because issue #12 introduces no cross-module contract.
- Depends on:
  - Block 1
  - Block 3
  - Block 4
- Completion: Issue #12 exposes no more public module surface than currently required, while preserving a clean ownership boundary for future consumers.

## Fog

None. All in-scope planning decisions are resolved.

## Out of Scope

- Negotiating, requesting, inviting, or commercially accepting a Trading Relationship.
- Price Agreement terms or Agreement lifecycle.
- Organization identity, membership, and permission ownership.
- Proactive design of future Agreements, Price Publication, Customer Pricing, Delivery, or Retrieval integrations. Their concrete consumer needs should be designed when those integrations are implemented.
- Hypothetical domain/integration events or contracts not required by issue #12.
- Rewriting historical downstream facts when a relationship ends.
- A repository-wide logging/observability strategy or library choice. This requires separate cross-cutting architecture work; issue #12 only identifies domain/use-case-specific observability needs.
- A separate implementation-structure design. Existing module, DI, and Vertical Slice conventions should be followed unless implementation exposes a genuinely new architectural decision.
- A standalone verification block. Verification decisions belong with the owning design block unless a cross-cutting test concern emerges.
- Implementation decomposition into tickets; `/to-tickets` owns that after all blocks are grilled.

## Test Strategy

No independent cross-cutting test-strategy decision emerged. Verification requirements are recorded in their owning blocks.

## Follow-up Architecture Work

- Define PriceSync-wide logging/observability strategy and library choices separately from issue #12. This should cover cross-cutting concerns such as structured logging, correlation, telemetry, and shared conventions rather than being decided opportunistically inside one module.

## Session Log

### 2026-09-17 — Block 5 grilling completed

- Issue #12 adds no speculative cross-module TradingRelationships query/status contract; the Contracts project may remain empty/minimal until a concrete consumer exists.
- HTTP DTOs remain owned by their Vertical Slices rather than being placed in the cross-module Contracts project.
- The API host may reference the implementation as composition root, while other feature modules may not reference TradingRelationships implementation/persistence directly.
- Future cross-module dependencies must use explicit contracts designed from concrete consumer needs; no future Agreements contract or shared domain type is designed now.
- Contracts-to-Contracts dependencies are not established or architecture-tested speculatively.
- Existing/current architecture verification is extended to protect the actual direct-implementation dependency rule; no additional contract tests are needed while no cross-module contract exists.
- Block 5 completed on 2026-09-17. With Blocks 1–5 complete, Fog empty, verification owned by the relevant blocks, and no unresolved in-scope decisions, the plan is Ready for implementation.

### 2026-09-17 — Block 4 grilling completed

- Block 4 completion criterion was rechecked after the second decision batch. Persistence structure, database guarantees, transaction boundaries, concurrency behaviour, retention semantics and PostgreSQL verification are all implementable with no remaining in-block design question.
- Block 4 completed on 2026-09-17.

### 2026-09-17 — Block 4 grilling, second batch

- Ended relationships have no normal physical-delete path; historical rows are retained and never reused on re-establishment.
- Supplier- and Customer-direction list queries receive composite indexes optimized for organization + status + keyset ordering; `all` is not given duplicate status-free indexes without measured need.
- Database also enforces Supplier and Customer differ through a CHECK constraint.
- No generic concurrency token is introduced; known Create/End races remain handled by their specific database mechanisms.
- TradingRelationship IDs are application-generated before persistence according to the project's ID convention; immutable state does not use database triggers.
- End may use a direct atomic persistence update, with no wider explicit transaction around update/readback.
- Real PostgreSQL integration tests verify PostgreSQL-specific constraints and concurrent Create/End guarantees.

### 2026-09-17 — Block 4 grilling, first batch

- Direct EF Core mapping of `TradingRelationship` selected; TradingRelationships owns PostgreSQL schema `trading_relationships`, DbContext, mappings and migrations.
- Active directed-pair uniqueness is protected by both the normal application idempotency lookup and a partial unique index; concurrent losing Create reads back the winner after the specific expected constraint violation.
- Status is persisted as constrained text; database CHECK constraints protect allowed statuses and Status/EndedAt consistency.
- Organization IDs remain persistence-independent cross-module references with no foreign keys into OrganizationAccess.
- Create relies on normal EF write transaction semantics rather than explicit/Serializable transactions; the unique index owns race arbitration.
- End is an atomic conditional Active-to-Ended update. A zero-row update is resolved by rereading for idempotent Ended versus Not Found, and EndedAt comes from PriceSync's application clock.

### 2026-09-17 — Block 4 grilling started

- Block 4 selected as the next `Ready to grill` block whose dependencies are complete.
- Existing architecture already fixes PostgreSQL + EF Core/Npgsql and module-owned DbContext, mappings, migrations, and schema ownership through ADR-0003; Block 4 should refine TradingRelationships-specific persistence/concurrency decisions rather than reopen that architecture choice.

### 2026-09-17 — Block 3 grilling completed, sixth batch

- `ICurrentOrganization`/current-Organization context belongs to `OrganizationAccess.Contracts`; it supplies selected Organization identity but does not itself authorize the current person.
- All TradingRelationships reads and commands explicitly authorize the relevant capability through OrganizationAccess. Authorization precedes relationship lookup/data access.
- Archived acting Organization and other authorization denials collapse to the same public `403 Forbidden` semantic; internal denial reasons remain available for audit/authorization handling.
- Missing acting Organization is `400 Bad Request` with stable `ActingOrganizationRequired` error semantics. Authentication/`401` remains a cross-cutting API concern.
- Authorization design stops at ownership, capabilities, context and observable semantics; concrete interface/type design is intentionally deferred to implementation.
- Block 3 completed on 2026-09-17.
