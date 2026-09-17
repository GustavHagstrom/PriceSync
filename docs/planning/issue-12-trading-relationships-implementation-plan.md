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
    - Accepted: Either the Supplier-side or Customer-side Organization may initiate creation. The acting Organization must itself be the Supplier or Customer in the requested relationship.
    - Accepted: Create requires the actor to hold the existing OrganizationAccess `Owner` role in the acting Organization; `Member` is insufficient for lifecycle-changing commands.
    - Accepted: An Archived acting Organization cannot Create a relationship.
    - Accepted: If the acting Organization is neither requested party, Create returns `403 Forbidden`.
    - Accepted: No counterparty approval, invitation, or opt-in is modeled. Successful creation immediately represents the already externally established relationship as Active.
    - Accepted: Repeating create for a directed pair that already has an Active relationship is idempotent success and returns the existing active relationship/identity rather than creating another aggregate or reporting a conflict.
    - Rationale: PriceSync records an externally established relationship rather than negotiating it. Either party may register it, but an Organization cannot create relationships on behalf of unrelated Organizations. Existing OrganizationAccess roles are reused rather than introducing issue-specific permissions.
  - End relationship command semantics
    - Status: In progress
    - Accepted: Either Supplier or Customer may end the relationship when the actor is `Owner` in the Organization they are acting as.
    - Accepted: An Archived acting Organization cannot End a relationship. This is authorization at the use-case boundary, not revalidation of the relationship parties; the other active party can still End it.
    - Accepted: End input requires only `TradingRelationshipId`; acting Organization/person come from request context and party membership is checked against authoritative relationship data.
    - Accepted: Ending an already Ended relationship is idempotent success. The existing EndedAt is preserved and no new lifecycle change occurs.
    - Accepted: Ending an unknown `TradingRelationshipId` returns `404 Not Found`; idempotent success applies only when the identified relationship exists and is already Ended.
    - Rationale: Both parties participate in the relationship and neither creator nor Supplier receives permanent unilateral lifecycle ownership. Idempotent End supports safe retries without rewriting historical facts. Request context supplies authorization scope rather than duplicating party data in the command.
  - Required read/query use cases and query semantics
    - Status: Complete
    - Accepted: Expose direction-specific list queries from the acting Organization perspective: relationships where it is Supplier and relationships where it is Customer.
    - Accepted: List endpoints do not accept an OrganizationId parameter; Organization scope comes from the current acting Organization context.
    - Accepted: List queries return Active relationships by default and allow callers to explicitly request Ended/all relationships.
    - Accepted: List queries use cursor/keyset pagination and are deterministically ordered newest first by `CreatedAt DESC`, with `TradingRelationshipId` as stable tie-breaker.
    - Accepted: The cursor is opaque to clients. Responses contain `items` plus `nextCursor` and do not include `totalCount`.
    - Accepted: No free-text/Organization-name search is introduced; the list surface remains limited to direction, status and pagination.
    - Accepted: Expose lookup of a specific Trading Relationship by TradingRelationshipId.
    - Accepted: GetById accepts only `TradingRelationshipId`; acting Organization scope comes from request context rather than an OrganizationId request parameter.
    - Accepted: A specific relationship may be read only when the Organization the actor is currently acting as is either Supplier or Customer in that relationship.
    - Accepted: Lookup of an unknown `TradingRelationshipId` returns `404 Not Found`.
    - Accepted: Lookup of a relationship outside the acting Organization's scope also returns `404 Not Found`, avoiding disclosure that another Organization's relationship exists.
    - Rationale: Direction is core domain semantics and should be explicit in the query surface. Acting Organization is the single source of request scope. Cursor pagination provides stable traversal under concurrent inserts without exposing persistence keys or requiring total-count queries.
  - Request/response contracts and error semantics
    - Status: Complete
    - Accepted: Create, End, GetById and list items use the same compact relationship representation containing `TradingRelationshipId`, `SupplierOrganizationId`, `CustomerOrganizationId`, `Status`, `CreatedAt`, and applicable `EndedAt`.
    - Accepted: Create does not externally distinguish a newly created relationship from an already-existing Active relationship; both have the same success semantics and representation. `CreatedAt` still communicates the resource's actual age.
    - Accepted: End returns the same basic relationship representation in its Ended state, including `EndedAt`; repeated End returns the same final state.
    - Accepted: Missing Supplier or Customer Organization during Create returns `404 Not Found`.
    - Accepted: An Archived Supplier or Customer Organization during Create returns `409 Conflict`.
    - Accepted: Create errors identify both the reason and the contextual party, allowing distinct `SupplierNotFound`, `CustomerNotFound`, `SupplierArchived`, and `CustomerArchived` semantics.
    - Accepted: A self-relationship request returns `400 Bad Request` with a machine-readable `SupplierAndCustomerMustDiffer` error.
    - Accepted: Expected API failures use stable machine-readable error codes in addition to HTTP status; clients need not parse human-readable error text.
    - Accepted: Unknown relationship identity on lookup or End returns `404 Not Found`. A relationship outside the acting Organization's scope is exposed as `404 Not Found`, while an authenticated actor inside the correct Organization scope who lacks the required command permission receives `403 Forbidden`.
    - Rationale: API errors distinguish invalid requests, absent resources, lifecycle conflicts, authorization failures inside a valid scope, and hidden out-of-scope resources. Stable error codes preserve concrete semantics independently of human-readable messages.
  - Actor/organization authorization requirements for each operation
    - Status: Complete
    - Accepted: Create and End require the existing OrganizationAccess `Owner` role in the acting Organization. Ordinary relationship reads are available to active `Owner` or `Member` memberships, subject to relationship scope.
    - Accepted: Archived acting Organizations cannot perform Create or End, while reads remain available according to the established OrganizationAccess lifecycle semantics.
    - Accepted: Create requires the acting Organization to be one of the requested Supplier/Customer parties; otherwise it returns `403 Forbidden`.
    - Accepted: When the acting Organization is not a party to an existing specific relationship, GetById/End expose it as `404 Not Found` rather than revealing it through `403 Forbidden`.
    - Accepted: When the acting Organization is a valid party but the authenticated actor lacks the `Owner` role required for Create/End, the API returns `403 Forbidden`.
    - Rationale: Issue #12 reuses the existing Owner/Member authorization model rather than inventing a new permission system. Request scope is derived solely from the acting Organization context, and existing out-of-scope relationships remain undisclosed.
  - Idempotency and repeated-operation behaviour
    - Status: Complete
    - Accepted: Repeated Create against an already-active directed pair and repeated End against an already-ended relationship are idempotent successes. No separate idempotency-key mechanism is required by these cases at this stage.
    - Accepted: Idempotent End does not extend to unknown relationship IDs; those return `404 Not Found`.
  - Domain-specific audit/observability requirements, without choosing the application-wide logging strategy
    - Status: Complete
    - Accepted: Successful and failed/denied Create and End attempts must be auditable with actor, acting Organization, relationship identity/context, operation, timestamp and outcome/error semantics as applicable.
    - Accepted: Read operations do not receive a TradingRelationships-specific audit requirement; read-access auditing remains a cross-cutting observability concern.
    - Accepted: This block defines required auditability only; storage mechanism, logging library, telemetry conventions and application-wide implementation strategy remain separate architecture work.
- Verification:
  - Focused integration/API tests cover the main externally observable behaviours for Create, End, GetById and directional lists, including authorization/scope, idempotency, status filtering/pagination and important error paths. Avoid a mechanical exhaustive matrix when behaviour is already protected at a more focused layer.
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

### 2026-09-17 — Block 3 grilling, fourth batch

- Existing OrganizationAccess roles are used directly: `Owner` is required for Create/End; ordinary reads are available to `Owner`/`Member` according to established membership semantics.
- Archived acting Organizations cannot Create or End, but can retain read access; this is authorization policy and does not change Block 2's decision that End does not revalidate party Organization status.
- Create requires the acting Organization to be Supplier or Customer and returns `403 Forbidden` otherwise.
- List/GetById/End derive Organization scope from request context rather than accepting redundant OrganizationId parameters; End takes only TradingRelationshipId as command input.
- Directional lists use opaque cursor/keyset pagination, stable newest-first ordering, `items` + `nextCursor`, and no `totalCount`.

### 2026-09-17 — Block 3 grilling, third batch

- Create and End require explicit auditability for both successful and failed/denied attempts, including actor, acting Organization, operation, relationship context, timestamp and outcome; the technical observability mechanism remains separate architecture work.
- Reads do not receive a module-specific audit requirement.
- Directional list queries are paginated, ordered by `CreatedAt DESC` plus `TradingRelationshipId` tie-breaker, and do not add free-text/Organization-name search.
- Create, End, GetById and list items use the same compact relationship representation.
- Self-relationship is `400 Bad Request` with a machine-readable error, and expected API failures generally expose stable machine-readable error codes.
- Focused integration/API tests verify the main external behaviours without requiring an exhaustive combinatorial matrix.

### 2026-09-17 — Block 3 grilling, second batch

- Create returns the basic Trading Relationship representation and uses the same externally observable success semantics whether it created a new aggregate or returned the already-active relationship.
- End returns the relationship in its final Ended state; repeated End preserves the original `EndedAt` and returns the same final representation.
- Unknown relationship IDs return `404 Not Found` for lookup and End. Out-of-scope relationship lookup also returns `404` to avoid disclosing another Organization's relationship.
- An authenticated actor in the correct Organization scope who lacks the required administrative command permission receives `403 Forbidden`.
- Create returns `404` when Supplier or Customer Organization does not exist and `409 Conflict` when either Organization is Archived. Error semantics identify the affected Supplier/Customer role.

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
