# Issue #12: Trading Relationships Implementation Plan

> Status: Structuring
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

The map below defines what later `/grill-planning-block` sessions must resolve. It intentionally does not resolve those design questions here.

#### Block 1 — Aggregate model and lifecycle

- Status: Ready to grill
- Purpose: Define the complete domain model and lifecycle semantics of one directed Trading Relationship.
- Decisions:
  - Aggregate identity and required state
    - Status: Open
  - Supplier/Customer relationship invariants
    - Status: Open
  - Lifecycle states and allowed transitions
    - Status: Open
  - Creation, ending, re-establishment, and historical semantics
    - Status: Open
  - Temporal facts owned by the aggregate
    - Status: Open
- Depends on:
  - None
- Completion: The aggregate state, invariants, lifecycle transitions, and historical semantics are precise enough to implement without further domain decisions.

#### Block 2 — Organization boundary and authorization scope

- Status: Ready to grill
- Purpose: Define how TradingRelationships interacts with OrganizationAccess while preserving module ownership and contextual Supplier/Customer roles.
- Decisions:
  - Organization reference and existence-validation semantics
    - Status: Open
  - Cross-module contract needed from OrganizationAccess
    - Status: Open
  - Actor/organization authorization requirements for relationship operations
    - Status: Open
  - Ownership boundary between relationship access scope and Organization memberships/permissions
    - Status: Open
- Depends on:
  - Block 1
- Completion: Organization references, cross-module validation, authorization responsibilities, and ownership boundaries are explicit.

#### Block 3 — Use cases and external API surface

- Status: Ready to grill
- Purpose: Define the vertical slices and externally observable behaviour exposed by the TradingRelationships module.
- Decisions:
  - Create relationship command semantics
    - Status: Open
  - End relationship command semantics
    - Status: Open
  - Required read/query use cases, if any
    - Status: Open
  - Request/response contracts and error semantics
    - Status: Open
  - Idempotency and repeated-operation behaviour
    - Status: Open
- Depends on:
  - Block 1
  - Block 2
- Completion: The required vertical slices, their contracts, and externally observable success/failure semantics are fully defined.

#### Block 4 — Persistence, consistency, and concurrency

- Status: Ready to grill
- Purpose: Define module-owned storage and the guarantees required to preserve Trading Relationship invariants under real database and concurrent execution.
- Decisions:
  - DbContext/schema/entity mapping and migration ownership
    - Status: Open
  - Database constraints and indexes required by domain invariants
    - Status: Open
  - Transaction boundaries
    - Status: Open
  - Concurrency/race handling for lifecycle-changing operations
    - Status: Open
  - Historical retention semantics in persistence
    - Status: Open
- Depends on:
  - Block 1
  - Block 3
- Completion: Persistence structure, database guarantees, transaction scope, and concurrency behaviour are implementable and preserve the resolved domain semantics.

#### Block 5 — Module contracts and downstream integration

- Status: Ready to grill
- Purpose: Define the minimal public contract through which Agreements and future Supplier-backed distribution flows consume Trading Relationship identity/status.
- Decisions:
  - Contracts exposed by `PriceSync.Modules.TradingRelationships.Contracts`
    - Status: Open
  - Status/identity lookup required by Agreements or other modules
    - Status: Open
  - How ending a relationship affects future downstream operations without rewriting historical facts
    - Status: Open
  - Dependency direction and prohibition of shared persistence/transactions
    - Status: Open
- Depends on:
  - Block 1
  - Block 3
  - Block 4
- Completion: Downstream modules can consume exactly the relationship facts they need without taking ownership of lifecycle or crossing persistence boundaries.

#### Block 6 — Verification strategy

- Status: Ready to grill
- Purpose: Define how the implementation proves domain lifecycle, use cases, persistence guarantees, and architectural ownership boundaries.
- Decisions:
  - Domain-level invariant coverage
    - Status: Open
  - Integration coverage for main use cases
    - Status: Open
  - Database/concurrency verification where required
    - Status: Open
  - Architecture/boundary verification
    - Status: Open
  - Test seams for cross-module interactions
    - Status: Open
- Depends on:
  - Blocks 1–5
- Completion: Material domain rules, externally observable use cases, persistence guarantees, and module boundaries each have an appropriate verification seam without unnecessary duplication.

## Fog

- Whether read/query behaviour is substantial enough to remain inside Block 3 or should become its own block. Resolve structurally only if the required query surface proves independently complex.
- Whether authorization scope is substantial enough to remain in Block 2 or needs a separate block after existing OrganizationAccess patterns are inspected during grilling.

## Out of Scope

- Negotiating, requesting, inviting, or commercially accepting a Trading Relationship.
- Price Agreement terms or Agreement lifecycle.
- Organization identity, membership, and permission ownership.
- Price Publication, Customer Pricing, Delivery, or Retrieval implementation except for defining the minimal TradingRelationships contract they may consume.
- Rewriting historical downstream facts when a relationship ends.
- Implementation decomposition into tickets; `/to-tickets` owns that after all blocks are grilled.

## Session Log

### 2026-09-17 — Block-map correction

The initial session incorrectly resolved detailed design questions while running `/grill-to-plan`. Those provisional answers were removed from the current block map. The plan was reset to a breadth-first map of decision areas so each block can later be resolved by `/grill-planning-block`.

The earlier discussion remains conversation history but is not treated as authoritative planning decisions until the owning block is grilled.
