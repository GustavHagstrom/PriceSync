# Issue #12: Trading Relationships Implementation Plan

> Status: Structuring
>
> Authority: This file records planning decisions and history. Current domain language,
> architecture, tests, and code remain authoritative for implemented behaviour.

Planning base for issue #12, `Implement Trading Relationships`.

## Goal

Implement the directed Supplier → Customer Trading Relationship boundary: aggregate lifecycle, module-owned persistence, create/end vertical slices, a minimal read surface, and tests, while keeping organization ownership and commercial pricing terms outside the module.

## Current Baseline

- `TradingRelationship` is defined as the aggregate root for one directed Supplier-to-Customer relationship.
- The aggregate owns supplier/customer organization IDs and relationship lifecycle, but not Organization identity, memberships, permissions, or commercial pricing terms.
- Ending a relationship must preserve historical downstream pricing and retrieval facts.
- Agreements reference `TradingRelationshipId`; cross-module interaction must respect module ownership.
- `PriceSync.Modules.TradingRelationships` exists and is wired into the API, but currently contains only module registration scaffolding.

## Grilling Blocks

The block map is still being structured. The first grilling pass has established the following decisions; later questions may reshape these into final blocks.

#### Block 1 — Aggregate identity and lifecycle

- Status: In progress
- Purpose: Define what one Trading Relationship represents and the lifecycle rules it owns.
- Decisions:
  - Relationship creation
    - Status: Resolved
    - Decision: A Trading Relationship is created explicitly through a `CreateTradingRelationship` use case/API; it is not created implicitly by Agreements or another module.
    - Rationale: The relationship is an independent domain fact and aggregate boundary.
  - Self-relationship
    - Status: Resolved
    - Decision: Supplier and Customer must be different Organizations.
    - Rationale: The directed relationship represents trading between two Organizations; a self-relationship has no identified domain meaning.
  - Active relationship uniqueness
    - Status: Resolved
    - Decision: At most one active Trading Relationship may exist for a given Supplier → Customer pair. Historical ended relationships may coexist.
    - Rationale: Current access scope remains unambiguous while history is preserved.
  - Reactivation
    - Status: Resolved
    - Decision: An ended Trading Relationship cannot be reactivated. Resumed trading creates a new Trading Relationship.
    - Rationale: Each aggregate represents one continuous relationship lifecycle and keeps historical identity unambiguous.
  - Lifecycle states
    - Status: Resolved
    - Decision: Initial lifecycle contains only `Active` and `Ended`.
    - Rationale: PriceSync represents an externally established relationship and does not model negotiation, requests, or commercial acceptance.
  - End facts
    - Status: Resolved
    - Decision: Ending records `EndedAtUtc` and the ended lifecycle state. No mandatory reason or actor is modeled initially.
    - Rationale: The end timestamp is a relevant historical fact; additional audit/domain data has no current requirement.
  - Creation timestamp
    - Status: Resolved
    - Decision: The aggregate records `CreatedAtUtc`, meaning when the relationship was registered in PriceSync, not necessarily when the companies began trading externally.
    - Rationale: Lifecycle timestamps remain explicit and technology-independent.
- Depends on:
  - None
- Completion: Aggregate identity, creation, lifecycle transitions, temporal facts, and uniqueness semantics are fully defined.

#### Block 2 — Organization boundary and creation validation

- Status: In progress
- Purpose: Define how TradingRelationships references and validates Organizations without taking ownership of Organization data.
- Decisions:
  - Organization existence
    - Status: Resolved
    - Decision: The create use case verifies that Supplier and Customer Organizations exist through an OrganizationAccess contract/read API before creating the aggregate. The aggregate itself stores only Organization IDs.
    - Rationale: Invalid references are rejected while module boundaries remain explicit and no cross-module database foreign key is introduced.
- Depends on:
  - Block 1
- Completion: Cross-module validation, reference ownership, and failure semantics for Organization references are defined.

#### Block 3 — Command semantics and concurrency

- Status: In progress
- Purpose: Define externally observable semantics for creating and ending relationships, including duplicate and concurrent operations.
- Decisions:
  - Duplicate active relationship creation
    - Status: Resolved
    - Decision: Creating a relationship when an active Supplier → Customer relationship already exists fails with a conflict/domain error rather than returning the existing relationship.
    - Rationale: `CreateTradingRelationship` expresses a domain decision; silently treating a duplicate request as success could hide an incorrect caller assumption.
- Depends on:
  - Block 1
  - Block 2
- Completion: Create/end command outcomes, conflicts, idempotency expectations, and concurrency guarantees are defined.

#### Block 4 — Read surface

- Status: In progress
- Purpose: Define the minimal query surface required from the first implementation.
- Decisions:
  - Initial read capability
    - Status: Resolved
    - Decision: Issue #12 includes a minimal read surface with `GetById` and relationship listing for an Organization.
    - Rationale: The module should expose enough read capability for consumers to inspect relationship identity/status and discover an Organization's relationships without introducing full CRUD.
- Depends on:
  - Block 1
- Completion: Query semantics, direction/role representation, filtering, and returned contract shape are defined.

#### Block 5 — Persistence and module integration

- Status: Ready to grill
- Purpose: Define module-owned persistence, database constraints, transaction boundaries, and external contracts.
- Decisions:
  - Persistence model and constraints
    - Status: Open
  - Transaction boundaries
    - Status: Open
  - Contract exposure to other modules
    - Status: Open
- Depends on:
  - Block 1
  - Block 2
  - Block 3
- Completion: Persistence ownership, schema constraints, transaction scope, and module contracts are explicit and implementable.

#### Block 6 — Verification strategy

- Status: Ready to grill
- Purpose: Define the tests needed to prove lifecycle rules, ownership boundaries, persistence constraints, and main use cases.
- Decisions:
  - Aggregate tests
    - Status: Open
  - Integration tests
    - Status: Open
  - Architecture/boundary tests
    - Status: Open
- Depends on:
  - Blocks 1–5
- Completion: Each material invariant and externally observable use case has an appropriate verification seam without unnecessary test duplication.

## Fog

- Exact semantics of listing relationships for an Organization, especially whether supplier/customer directions are exposed through separate queries or one role-aware query.
- Whether uniqueness of active Supplier → Customer relationships requires a database-level concurrency constraint in addition to application/domain checks.
- Exact behaviour when `End` is repeated or races with another command.
- Exact module contract needed by Agreements and future Price Publication flows.

## Out of Scope

- Negotiating, requesting, inviting, or commercially accepting a Trading Relationship.
- Price Agreement terms or lifecycle.
- Organization identity, membership, and permissions.
- Rewriting historical Agreement, publication, projection, delivery, or retrieval facts when a relationship ends.
- Full CRUD administration for Trading Relationships.

## Session Log

### 2026-09-17 — Initial grilling, questions 1–10

Accepted decisions:

- Explicit relationship creation.
- Supplier and Customer must differ.
- At most one active relationship per directed pair; ended history remains.
- Ended relationships are immutable with respect to activation; resumed trading creates a new identity.
- Lifecycle is initially `Active` / `Ended`.
- End records `EndedAtUtc`; creation records `CreatedAtUtc`.
- Organization existence is validated across the module boundary while only IDs are stored.
- Duplicate active creation is a conflict, not an idempotent success.
- Initial implementation includes `GetById` and relationship listing for an Organization.

The block map remains `Structuring`; subsequent grilling will refine unresolved boundaries and may split or merge blocks.