# Issue #12: Trading Relationships Implementation Plan

> Status: Blocks defined
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

The block map is complete. `/grill-planning-block` should resolve the blocks in dependency order. The map may still be reshaped if later grilling exposes a genuinely new decision tree.

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

- Status: In progress
- Purpose: Define the vertical slices and externally observable behaviour exposed by the TradingRelationships module, including authorization at the use-case boundary.
- Decisions:
  - Create relationship command semantics
    - Status: In progress
    - Accepted: Either the Supplier-side or Customer-side Organization may initiate creation, provided the actor has the required administrative permission in the Organization they are acting as.
    - Accepted: No counterparty approval, invitation, or opt-in is modeled. Successful creation immediately represents the already externally established relationship as Active.
    - Accepted: Repeating create for a directed pair that already has an Active relationship is idempotent success and returns the existing active relationship/identity rather than creating another aggregate or reporting a conflict.
    - Rationale: PriceSync records an externally established relationship rather than negotiating it. The initiating side has no lasting ownership role, and retry-safe creation avoids duplicate aggregates while preserving the one-active-per-directed-pair invariant.
  - End relationship command semantics
    - Status: In progress
    - Accepted: Either Supplier or Customer may end the relationship when the actor has the required administrative permission in the Organization they are acting as.
    - Accepted: Ending an already Ended relationship is idempotent success. The existing EndedAt is preserved and no new lifecycle change occurs.
    - Rationale: Both parties participate in the relationship and neither creator nor Supplier receives permanent unilateral lifecycle ownership. Idempotent End supports safe retries without rewriting historical facts.
  - Required read/query use cases and query semantics
    - Status: In progress
    - Accepted: Expose direction-specific list queries from the acting Organization perspective: relationships where it is Supplier and relationships where it is Customer.
    - Accepted: List queries return Active relationships by default and allow callers to explicitly request Ended/all relationships.
    - Accepted: Expose lookup of a specific Trading Relationship by TradingRelationshipId.
    - Accepted: A specific relationship may be read only when the Organization the actor is currently acting as is either Supplier or Customer in that relationship.
    - Rationale: Direction is core domain semantics and should be explicit in the query surface. Operational reads default to current relationships while retaining access to history. Organization-scoped access prevents memberships in unrelated Organizations from implicitly widening the active request context.
  - Request/response contracts and error semantics
    - Status: Open
  - Actor/organization authorization requirements for each operation
    - Status: In progress
    - Accepted: Create and End require an administrative Organization permission in the Organization the actor is acting as. Ordinary relationship reads may be available to regular members of the acting Organization, provided that Organization is a party to the relationship/query.
    - Rationale: Create and End alter the Organization's external relationship/access scope and therefore require stronger authority than read-only access.
  - Idempotency and repeated-operation behaviour
    - Status: In progress
    - Accepted: Repeated Create against an already-active directed pair and repeated End against an already-ended relationship are idempotent successes. No separate idempotency-key mechanism is required by these cases at this stage.
  - Domain-specific audit/observability requirements, without choosing the application-wide logging strategy
    - Status: Open
- Depends on:
  - Block 1
  - Block 2
- Completion: Required vertical slices, query surface, authorization, contracts, and externally observable success/failure semantics are fully defined.

#### Block 4 — Persistence, consistency, and concurrency

- Status: Ready to grill
- Purpose: Define module-owned storage and how the domain rules from Block 1 are enforced under real database and concurrent execution.
- Decisions:
  - DbContext/schema/entity mapping and migration ownership — Open
  - Database constraints and indexes required to enforce resolved domain rules — Open
  - Transaction boundaries — Open
  - Concurrency/race handling for lifecycle-changing operations — Open
  - Historical retention semantics in persistence — Open
- Depends on:
  - Block 1
  - Block 3
- Completion: Persistence structure, database guarantees, transaction scope, and concurrency behaviour are implementable and preserve the domain semantics resolved in Block 1.

#### Block 5 — Minimal public module boundary

- Status: Ready to grill
- Purpose: Define only the public TradingRelationships module boundary required by issue #12, without proactively designing future consumer integrations.
- Decisions:
  - Minimum contracts, if any, that `PriceSync.Modules.TradingRelationships.Contracts` must expose for issue #12 — Open
  - Minimum relationship identity/status access required outside the module by current scope — Open
  - Dependency-direction and ownership guarantees that the public boundary must preserve — Open
- Depends on:
  - Block 1
  - Block 3
  - Block 4
- Completion: Issue #12 exposes no more public module surface than currently required, while preserving a clean ownership boundary for future consumers.

## Fog

None currently. All visible in-scope decision areas belong to a block.

## Out of Scope

- Negotiating, requesting, inviting, or commercially accepting a Trading Relationship.
- Price Agreement terms or Agreement lifecycle.
- Organization identity, membership, and permission ownership.
- Proactive design of future Agreements, Price Publication, Customer Pricing, Delivery, or Retrieval integrations. Their concrete consumer needs should be designed when those integrations are implemented.
- Hypothetical domain/integration events or contracts not required by issue #12.
- Rewriting historical downstream facts when a relationship ends.
- A repository-wide logging/observability strategy or library choice. This requires separate cross-cutting architecture work; issue #12 only identifies domain/use-case-specific observability needs.
- A separate implementation-structure design. Existing module, DI, and Vertical Slice conventions should be followed unless later grilling discovers a genuinely new architectural decision.
- A standalone verification block. Verification decisions belong with the owning design block unless a cross-cutting test concern emerges.
- Implementation decomposition into tickets; `/to-tickets` owns that after all blocks are grilled.

## Test Strategy

No independent cross-cutting test-strategy decision has been identified at block-mapping time. Each `/grill-planning-block` session should define the verification needed for the decisions it resolves. Add cross-cutting test strategy here only if later grilling discovers one.

## Follow-up Architecture Work

- Define PriceSync-wide logging/observability strategy and library choices separately from issue #12. This should cover cross-cutting concerns such as structured logging, correlation, telemetry, and shared conventions rather than being decided opportunistically inside one module.

## Session Log

### 2026-09-17 — Block 3 grilling, first batch

- Either Supplier or Customer side may create an already externally established relationship; no counterparty approval flow exists.
- Either side may end the relationship.
- Create and End require administrative permission in the Organization the actor is currently acting as; reads may be available to ordinary members.
- Repeated Create for an already-active directed pair and repeated End for an already-ended relationship are idempotent successes.
- Read surface uses direction-specific Supplier/Customer list queries, defaults to Active with explicit historical filtering, and includes lookup by TradingRelationshipId.
- Read authorization is scoped to the Organization the actor is currently acting as, which must be a party to the relationship.

### 2026-09-17 — Block 2 grilling completed

- OrganizationAccess exposes a purpose-built single-Organization status query through `OrganizationAccess.Contracts`; TradingRelationships calls it separately for Supplier and Customer.
- New relationship creation, including re-establishment, requires both Organizations to be currently Active; validation is point-in-time without cross-module locking.
- Organization archival does not automatically end existing relationships, and ending does not revalidate Organization status.
- Supplier/Customer roles and creation eligibility are owned by TradingRelationships; OrganizationAccess owns Organization identity/lifecycle.
- TradingRelationships stores only Organization IDs and neither snapshots, synchronizes, nor caches Organization status.
- Validation occurs in the application/use-case layer before aggregate creation; expected status results remain distinct from technical failures.
- Block 2 completed on 2026-09-17.

### 2026-09-17 — Block 1 grilling completed

- Stable TradingRelationshipId plus directed Supplier/Customer natural key.
- Distinct immutable parties; direction significant; at most one Active relationship per directed pair.
- Re-establishment creates a new aggregate; lifecycle is Active → Ended and Ended is terminal.
- CreatedAt and EndedAt are PriceSync lifecycle facts; ended aggregates are retained as history.
- Focused domain verification defined; concurrency enforcement deferred to Block 4.
- Block 1 completed on 2026-09-17.

### 2026-09-17 — Block-map correction and completion

The initial session incorrectly resolved detailed design questions while running `/grill-to-plan`. Those provisional answers were removed from the current block map. The plan was reset to a breadth-first map of decision areas so each block can later be resolved by `/grill-planning-block`. The map was then refined to keep persistence, Organization boundary, use cases/API, and minimal public contracts as separate owning blocks, with verification attached to each owning block and application-wide observability deferred to separate architecture work.
