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
  - Aggregate identity and required state
    - Status: Complete
    - Accepted: `TradingRelationship` has its own stable `TradingRelationshipId`. The directed `(SupplierOrganizationId, CustomerOrganizationId)` pair is also a domain-significant natural key used when defining uniqueness and re-establishment semantics, but is not the aggregate identity.
    - Accepted: Required aggregate state is `TradingRelationshipId`, `SupplierOrganizationId`, `CustomerOrganizationId`, lifecycle `Status`, `CreatedAt`, and nullable `EndedAt`. Generic update metadata such as `UpdatedAt` is not part of the domain state unless a later concrete requirement establishes domain meaning for it.
    - Rationale: Other bounded contexts need a stable relationship reference independent of party-key mechanics, while the Supplier/Customer pair still carries domain meaning for cross-aggregate rules. Creation and ending timestamps are lifecycle facts owned by the relationship rather than generic persistence audit metadata.
  - Supplier/Customer relationship invariants
    - Status: Complete
    - Accepted: Supplier and Customer must be two distinct Organizations within one `TradingRelationship`; `SupplierOrganizationId == CustomerOrganizationId` is invalid.
    - Accepted: Relationship direction is semantically significant. `A → B` and `B → A` are separate Trading Relationships and may coexist.
    - Accepted: `SupplierOrganizationId` and `CustomerOrganizationId` are immutable after creation. Changing either party means a different Trading Relationship and therefore a different aggregate identity.
    - Rationale: Supplier and Customer are contextual roles and one Organization may hold different roles across different relationships. A self-directed `X → X` relationship has no meaningful price-distribution scope, while two Organizations may legitimately supply each other in separate directed relationships. Keeping the parties immutable ensures a stable `TradingRelationshipId` never changes which directed relationship historical downstream references identify.
  - Cross-aggregate relationship rules such as uniqueness or re-establishment semantics
    - Status: Complete
    - Accepted: For one exact directed `(SupplierOrganizationId, CustomerOrganizationId)` pair, at most one Trading Relationship may be active at a time. Historical ended relationships for the same directed pair may coexist with the active relationship.
    - Accepted: Re-establishing a previously ended directed Supplier/Customer relationship creates a new `TradingRelationship` with a new `TradingRelationshipId`; the ended relationship remains unchanged as history and is not reactivated.
    - Rationale: Parallel active relationships for the same directed parties would represent the same access/distribution scope and make downstream ownership ambiguous. Treating each establishment as a distinct aggregate preserves clear historical periods and stable downstream references while allowing the same directed parties to establish a new relationship later.
  - Lifecycle states and allowed transitions
    - Status: Complete
    - Accepted: The lifecycle contains only `Active` and `Ended`. A newly represented externally established relationship starts as `Active`; the only lifecycle transition is `Active → Ended`; `Ended` is terminal.
    - Rationale: PriceSync represents an already established external relationship rather than negotiating or approving it, so `Pending` has no domain meaning here. No current requirement establishes a separate suspended/paused state.
  - Creation, ending, re-establishment, and historical semantics
    - Status: Complete
    - Accepted: Re-establishment after an ended relationship is a new relationship instance rather than a state transition on the ended aggregate.
    - Accepted: Ending is terminal for that aggregate; it cannot return to `Active`.
    - Accepted: Ending never deletes or clears relationship state. The aggregate is retained in full as historical data; ending only changes lifecycle status to `Ended` and records `EndedAt`. Existing historical references continue to identify the same relationship.
    - Rationale: An ended aggregate represents one completed historical relationship period; later commercial re-establishment is a new period with its own identity and lifecycle. Retaining the complete ended aggregate preserves stable historical meaning for downstream references and avoids rewriting past facts.
  - Temporal facts owned by the aggregate
    - Status: Complete
    - Accepted: `CreatedAt` is the timestamp when the Trading Relationship is registered/created in PriceSync, not an externally supplied date for when the real-world commercial relationship originally began.
    - Accepted: `EndedAt` is the timestamp when PriceSync performs the `Active → Ended` transition. It is set once when the relationship ends and never changed afterwards; it is not an externally supplied or backdated commercial end date.
    - Rationale: PriceSync owns and can state its own lifecycle facts reliably. No current requirement establishes separate externally effective relationship start/end dates or responsibility for supplying them.
- Verification:
  - Focused domain tests should cover meaningful aggregate invariants and lifecycle behaviour, including rejecting a self-directed relationship, creation as `Active`, ending as `Ended` with `EndedAt`, and `Ended` being terminal.
  - Do not require a unit test for every documented invariant when the behaviour is structurally trivial.
  - Cross-aggregate active uniqueness and concurrent enforcement are verified in Block 4 after the enforcement mechanism is defined.
- Depends on:
  - None
- Completion: Aggregate state, invariants, lifecycle transitions, cross-aggregate domain rules, and historical semantics are precise enough for later blocks to determine enforcement.

#### Block 2 — Organization boundary

- Status: Complete
- Completed: 2026-09-17
- Purpose: Define how TradingRelationships depends on OrganizationAccess while preserving module ownership and contextual Supplier/Customer roles.
- Decisions:
  - Organization reference and existence-validation semantics
    - Status: Complete
    - Accepted: Creating a Trading Relationship requires both `SupplierOrganizationId` and `CustomerOrganizationId` to reference Organizations that exist in OrganizationAccess and are currently `Active`. Re-establishment is a new creation and performs the same validation again.
    - Accepted: Validation is point-in-time. TradingRelationships does not coordinate a cross-module transaction or lock with OrganizationAccess if an Organization is archived concurrently after validation.
    - Accepted: Archiving an Organization does not automatically end existing Trading Relationships, and current Organization status is not revalidated when an existing relationship is ended.
    - Rationale: Creation is a new Organization-scoped operation and therefore requires currently valid participants. Existing relationship lifecycle remains independently owned by TradingRelationships. Cross-module locking would couple otherwise separate module-owned transactions for an edge race without a demonstrated requirement for stronger consistency.
  - Contract required by TradingRelationships from OrganizationAccess
    - Status: Complete
    - Accepted: `OrganizationAccess.Contracts` exposes a purpose-built single-Organization status query. TradingRelationships calls it separately for Supplier and Customer rather than using a relationship-specific batch contract.
    - Accepted: The minimal result distinguishes `Active`, `Archived`, and `NotFound`; no generic Organization read model, name, metadata, role information, or eligibility abstraction is exposed.
    - Accepted: TradingRelationships does not cache or persist Organization status. Each new relationship creation queries current status through the module contract.
    - Rationale: The consumer needs only identity existence and current lifecycle status. A single-ID contract remains independent of TradingRelationships-specific Supplier/Customer pairing while avoiding unnecessary exposure of the Organization model.
  - Ownership boundary between relationship access scope and Organization identity/membership/permissions
    - Status: Complete
    - Accepted: OrganizationAccess owns Organization identity and lifecycle facts. TradingRelationships owns the rule that a new relationship requires `Active` Organizations and owns Supplier/Customer as contextual roles created by relationship direction.
    - Accepted: OrganizationAccess does not know whether an Organization is being used as Supplier or Customer. TradingRelationships stores only the Organization IDs and does not snapshot or synchronize Organization lifecycle state.
    - Accepted: OrganizationAccess continues to own memberships and permissions; actor authorization for TradingRelationship use cases is resolved in Block 3.
    - Rationale: Supplier/Customer are relationship-specific roles rather than permanent Organization types. Keeping eligibility policy in the consuming use case prevents OrganizationAccess from acquiring TradingRelationships domain language or responsibilities.
  - Dependency direction and failure semantics across the OrganizationAccess boundary
    - Status: Complete
    - Accepted: The status query belongs to `OrganizationAccess.Contracts`; TradingRelationships depends on that public contract only and must not access OrganizationAccess implementation or persistence directly.
    - Accepted: Organization validation is performed in the TradingRelationships application/use-case layer before aggregate creation. The aggregate receives Organization IDs and has no dependency on OrganizationAccess.
    - Accepted: `Active`, `Archived`, and `NotFound` are expected contract results. Technical failures crossing the OrganizationAccess boundary fail the operation and must not be translated into `NotFound`.
    - Accepted: Block 2 defines only internal module-boundary semantics. Exact external error contracts and HTTP mappings for `Archived` and `NotFound` belong to Block 3.
    - Rationale: Provider-owned module contracts preserve the existing dependency direction while keeping domain aggregates free of infrastructure/cross-module dependencies. Expected business outcomes must remain distinguishable from technical dependency failures.
- Verification:
  - Focused application/integration tests should verify relationship creation with `Active`, `Archived`, and `NotFound` Organization results and re-establishment applying the same validation.
  - Architecture verification should protect against TradingRelationships depending directly on OrganizationAccess implementation/persistence.
  - Aggregate unit tests do not need to duplicate OrganizationAccess validation because that rule is enforced at the application/module boundary.
- Depends on:
  - Block 1
- Completion: Organization references, incoming cross-module dependency, validation responsibilities, and ownership boundaries are explicit.

#### Block 3 — Use cases and external API surface

- Status: Ready to grill
- Purpose: Define the vertical slices and externally observable behaviour exposed by the TradingRelationships module, including authorization at the use-case boundary.
- Decisions:
  - Create relationship command semantics
    - Status: Open
  - End relationship command semantics
    - Status: Open
  - Required read/query use cases and query semantics
    - Status: Open
  - Request/response contracts and error semantics
    - Status: Open
  - Actor/organization authorization requirements for each operation
    - Status: Open
  - Idempotency and repeated-operation behaviour
    - Status: Open
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
  - DbContext/schema/entity mapping and migration ownership
    - Status: Open
  - Database constraints and indexes required to enforce resolved domain rules
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
- Completion: Persistence structure, database guarantees, transaction scope, and concurrency behaviour are implementable and preserve the domain semantics resolved in Block 1.

#### Block 5 — Minimal public module boundary

- Status: Ready to grill
- Purpose: Define only the public TradingRelationships module boundary required by issue #12, without proactively designing future consumer integrations.
- Decisions:
  - Minimum contracts, if any, that `PriceSync.Modules.TradingRelationships.Contracts` must expose for issue #12
    - Status: Open
  - Minimum relationship identity/status access required outside the module by current scope
    - Status: Open
  - Dependency-direction and ownership guarantees that the public boundary must preserve
    - Status: Open
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

### 2026-09-17 — Block 2 grilling completed

- OrganizationAccess exposes a purpose-built single-Organization status query through `OrganizationAccess.Contracts`; TradingRelationships calls it separately for Supplier and Customer.
- The result exposes only `Active`, `Archived`, and `NotFound`; no generic Organization read model or TradingRelationships-specific role/eligibility concept is added.
- New relationship creation, including re-establishment, requires both Organizations to be currently `Active`.
- Validation is point-in-time; no cross-module transaction or locking is introduced for concurrent Organization archival.
- Organization archival does not automatically end existing relationships, and ending an existing relationship does not revalidate Organization status.
- Supplier/Customer roles and the rule requiring Active participants are owned by TradingRelationships; OrganizationAccess owns Organization identity/lifecycle and does not know relationship roles.
- TradingRelationships stores only Organization IDs and neither snapshots, synchronizes, nor caches Organization status.
- Validation occurs in the application/use-case layer before aggregate creation; the aggregate does not depend on OrganizationAccess.
- Expected `Active`/`Archived`/`NotFound` results remain distinct from technical failures. External HTTP/error mapping is deferred to Block 3.
- Verification uses focused application/integration tests plus architecture protection against direct OrganizationAccess implementation/persistence dependencies.
- Block 2 completed on 2026-09-17.

### 2026-09-17 — Block 2 grilling started

- Block 2 marked `In progress` after Block 1 completion.
- Existing OrganizationAccess planning establishes that `OrganizationId` is already a boundary-relevant strongly typed ID in `OrganizationAccess.Contracts`.
- Existing OrganizationAccess planning deliberately exposes no generic Organization lookup/authorization interface; purpose-built cross-context queries should be added only for a concrete consuming use case such as TradingRelationships.
- Direct access from TradingRelationships to OrganizationAccess persistence remains forbidden.
- Organization existence: creating a Trading Relationship requires both Supplier and Customer Organization IDs to exist in OrganizationAccess, validated through its module boundary.
- Organization lifecycle: both Organizations must be `Active` at relationship creation; archived Organizations remain valid only for historical references/reads.

### 2026-09-17 — Block 1 grilling started

- Block 1 marked `In progress`.
- Aggregate identity: accepted hybrid identity semantics — stable `TradingRelationshipId` plus domain-significant directed Supplier/Customer natural key for uniqueness/re-establishment rules.
- Required state: accepted `TradingRelationshipId`, Supplier/Customer Organization IDs, lifecycle status, `CreatedAt`, and nullable `EndedAt`; generic `UpdatedAt` is excluded from domain state.
- Party invariant: Supplier and Customer must be distinct Organizations; self-directed relationships are invalid.
- Direction invariant: `A → B` and `B → A` are distinct relationships and may coexist.
- Party immutability: Supplier and Customer Organization IDs cannot change after creation; changing either party means a different Trading Relationship.
- Active uniqueness: at most one active relationship may exist for an exact directed Supplier/Customer pair; ended historical instances may coexist.
- Re-establishment: a new relationship with a new ID is created; the previous ended relationship remains immutable history.
- Lifecycle: only `Active` and `Ended`; creation starts `Active`, `Active → Ended` is the only transition, and `Ended` is terminal.
- Creation time: `CreatedAt` records when PriceSync registers the relationship, not when the external commercial relationship originally began.
- Ending time: `EndedAt` records the PriceSync end transition, is set once, and is not externally supplied/backdated.
- Historical retention: ending retains the complete aggregate and only records `Status = Ended` plus `EndedAt`; historical references remain valid.
- Verification: focused domain tests cover meaningful aggregate invariants/lifecycle; cross-aggregate uniqueness/concurrency verification is deferred to Block 4 where enforcement is designed.
- Block 1 completed on 2026-09-17.

### 2026-09-17 — Block-map correction

The initial session incorrectly resolved detailed design questions while running `/grill-to-plan`. Those provisional answers were removed from the current block map. The plan was reset to a breadth-first map of decision areas so each block can later be resolved by `/grill-planning-block`.

The earlier discussion remains conversation history but is not treated as authoritative planning decisions until the owning block is grilled.

### 2026-09-17 — Structural refinement

Block-map decisions:

- Keep downstream module contracts/integration separate from the use-case/API block.
- Remove the standalone verification block; verification belongs to the owning design block unless a cross-cutting test concern emerges.
- Keep persistence/consistency/concurrency separate from aggregate modelling: Block 1 defines domain rules and Block 4 determines enforcement.
- Keep Organization boundary as its own block, but move operation authorization into the use-case/API block.
- Block 1 owns domain semantics even when a rule spans multiple TradingRelationship instances.
- Keep read/query surface within the use-case/API block unless later grilling exposes independently substantial complexity.
- Keep domain/integration-event decisions within downstream integration rather than creating a separate event block.
- Do not create an observability block for issue #12; domain-specific needs stay with their owning use cases while application-wide logging strategy becomes separate architecture work.
- Split contracts by dependency direction: OrganizationAccess contracts consumed by TradingRelationships belong to Block 2; contracts exposed by TradingRelationships belong to Block 5.
- Do not create an implementation-structure block; follow established module and Vertical Slice conventions unless a new architectural problem emerges.

### 2026-09-17 — Block map completed

- Narrowed Block 5 from proactive downstream integration design to the minimal public module boundary actually required by issue #12.
- Future consumer-specific contracts, events, and integration behaviour are deferred until those consumers are implemented.
- Confirmed that no remaining visible fog prevents starting Block 1.
- Plan status advanced from `Structuring` to `Blocks defined`.
