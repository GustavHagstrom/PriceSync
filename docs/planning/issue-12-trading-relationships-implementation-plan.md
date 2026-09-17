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

- Status: Complete
- Completed: 2026-09-17
- Purpose: Define the vertical slices and externally observable behaviour exposed by the TradingRelationships module, including authorization at the use-case boundary.
- Decisions:
  - Create relationship command semantics
    - Status: Complete
    - Accepted: Either the Supplier-side or Customer-side Organization may initiate creation. The acting Organization must itself be the Supplier or Customer in the requested relationship.
    - Accepted: Create requires administrative capability in the acting Organization. Under the current OrganizationAccess role model this maps to `Owner`; TradingRelationships does not interpret `Owner`/`Member` directly.
    - Accepted: An Archived acting Organization cannot Create a relationship.
    - Accepted: If the acting Organization is neither requested party, Create returns `403 Forbidden` before Organization existence/status validation, avoiding information disclosure to an unrelated Organization.
    - Accepted: Create request explicitly contains both `SupplierOrganizationId` and `CustomerOrganizationId`; request context supplies the acting Organization separately.
    - Accepted: No counterparty approval, invitation, or opt-in is modeled. Successful creation immediately represents the already externally established relationship as Active.
    - Accepted: Repeating create for a directed pair that already has an Active relationship is idempotent success and returns the existing active relationship/identity rather than creating another aggregate or reporting a conflict.
    - Accepted: Successful Create always returns `200 OK` with the relationship representation, whether a new relationship was created or an existing Active relationship satisfied the request.
    - Rationale: PriceSync records an externally established relationship rather than negotiating it. Either party may register it, but an Organization cannot create relationships on behalf of unrelated Organizations. The command is externally an idempotent ensure operation, so it does not claim resource creation through distinct success semantics.
  - End relationship command semantics
    - Status: Complete
    - Accepted: Either Supplier or Customer may end the relationship when the actor has administrative capability in the Organization they are acting as.
    - Accepted: An Archived acting Organization cannot End a relationship. This is authorization at the use-case boundary, not revalidation of the relationship parties; the other active party can still End it.
    - Accepted: End input requires only `TradingRelationshipId`; acting Organization/person come from request context and party membership is checked against authoritative relationship data.
    - Accepted: When an existing relationship is outside the acting Organization's scope, End returns `404 Not Found`, matching GetById and avoiding disclosure of the relationship.
    - Accepted: Ending an already Ended relationship is idempotent success. The existing EndedAt is preserved and no new lifecycle change occurs.
    - Accepted: Ending an unknown `TradingRelationshipId` returns `404 Not Found`; idempotent success applies only when the identified relationship exists and is already Ended.
    - Accepted: Successful End always returns `200 OK` with the final relationship representation, both for a new Active → Ended transition and an idempotent retry against an already Ended relationship.
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
    - Accepted: Create does not externally distinguish a newly created relationship from an already-existing Active relationship; both return `200 OK` with the same representation. `CreatedAt` still communicates the resource's actual age.
    - Accepted: End returns `200 OK` with the same relationship representation in its Ended state, including `EndedAt`; repeated End returns the same final state.
    - Accepted: Missing Supplier or Customer Organization during Create returns `404 Not Found`.
    - Accepted: An Archived Supplier or Customer Organization during Create returns `409 Conflict`.
    - Accepted: Create errors identify both the reason and the contextual party, allowing distinct `SupplierNotFound`, `CustomerNotFound`, `SupplierArchived`, and `CustomerArchived` semantics.
    - Accepted: A self-relationship request returns `400 Bad Request` with a machine-readable `SupplierAndCustomerMustDiffer` error.
    - Accepted: Expected API failures use stable machine-readable error codes in addition to HTTP status; clients need not parse human-readable error text.
    - Accepted: Missing acting Organization context returns `400 Bad Request` with stable machine-readable `ActingOrganizationRequired` semantics.
    - Accepted: Authorization denials in a supplied acting Organization context return `403 Forbidden` with a single public forbidden semantic; internal causes such as not-member, insufficient role, or archived acting Organization are retained for authorization/audit purposes but are not exposed as distinct public API error codes.
    - Accepted: Unknown relationship identity on lookup or End returns `404 Not Found`. An existing relationship outside the acting Organization's scope is exposed as `404 Not Found`, while an authenticated actor inside the correct Organization scope who lacks the required command capability receives `403 Forbidden`.
    - Accepted: Authentication/`401 Unauthorized` is a cross-cutting API/infrastructure concern and is not modeled as a TradingRelationships use-case error.
    - Rationale: API errors distinguish invalid requests, absent resources, lifecycle conflicts, authorization failures inside a valid scope, and hidden out-of-scope resources. Stable error codes preserve concrete semantics independently of human-readable messages without leaking internal authorization reasons.
  - Actor/organization authorization requirements for each operation
    - Status: Complete
    - Accepted: OrganizationAccess owns membership, roles and permission interpretation. TradingRelationships depends on a purpose-built OrganizationAccess authorization contract rather than reading OrganizationAccess persistence or interpreting its role enum.
    - Accepted: Authorization is capability-based. TradingRelationships asks whether the current person may perform the relevant capability in the acting Organization (for example read relationship scope versus manage Trading Relationships); OrganizationAccess maps its current `Owner`/`Member` model to that answer.
    - Accepted: The authorization result may preserve expected internal denial reasons such as Allowed, NotMember, InsufficientRole and OrganizationArchived for use-case/audit handling, without exposing the underlying membership model or distinct public error codes.
    - Accepted: The same OrganizationAccess authorization boundary is used for reads and commands. Every List/GetById verifies read capability; Create/End verify management capability. `ICurrentOrganization` identifies the selected Organization but does not itself imply that the current person is authorized to act for it.
    - Accepted: Under the current policy, Create and End management capability maps to `Owner`; ordinary relationship reads are available to active `Owner` or `Member` memberships, subject to relationship scope.
    - Accepted: Archived acting Organizations cannot perform Create or End, while reads remain available according to the established OrganizationAccess lifecycle semantics. Denied Create/End due to archived acting Organization maps externally to the same `403 Forbidden` semantic as other authorization denials.
    - Accepted: Acting Organization is security/request context, not ordinary business input. The current-Organization abstraction belongs to `OrganizationAccess.Contracts`; infrastructure supplies it alongside the current person, and individual endpoints do not accept `ActingOrganizationId` as request data.
    - Accepted: The current-Organization abstraction is deliberately thin: it supplies the selected Organization identity, while capability authorization is explicitly performed through OrganizationAccess by each relevant use case.
    - Accepted: Authorization in the acting Organization is checked before TradingRelationships data access/relationship lookup. For Create, acting-party scope is also checked before counterparty existence/status validation to avoid information disclosure.
    - Accepted: Create requires the acting Organization to be one of the requested Supplier/Customer parties; otherwise it returns `403 Forbidden` before counterparty validation.
    - Accepted: When the acting Organization is not a party to an existing specific relationship, GetById/End expose it as `404 Not Found` rather than revealing it through `403 Forbidden`.
    - Accepted: When the acting Organization is a valid party but the authenticated actor lacks the management capability required for Create/End, the API returns `403 Forbidden`.
    - Accepted: Concrete authorization interface/type names and implementation structure are intentionally left to implementation; the planning boundary ends at ownership, capabilities, context and observable semantics.
    - Rationale: OrganizationAccess remains the sole owner of whether a person may act for an Organization and how roles map to capabilities. TradingRelationships consumes only contextual authorization outcomes and owns only relationship-specific scope. Acting Organization is request/security context, while authorization remains explicit rather than hidden in that context abstraction.
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

- Status: In progress
- Purpose: Define module-owned storage and how the domain rules from Block 1 are enforced under real database and concurrent execution.
- Decisions:
  - DbContext/schema/entity mapping and migration ownership — In progress.
    - Accepted: `TradingRelationship` is mapped directly by EF Core; no separate persistence entity/mapping layer is introduced.
    - Accepted: TradingRelationships owns PostgreSQL schema `trading_relationships`, its DbContext, entity mappings and migrations, following ADR-0003.
    - Accepted: The aggregate persists as one relationship table with its stable ID, Supplier/Customer Organization IDs, Status, CreatedAt and nullable EndedAt.
    - Rationale: The persistence shape closely matches the small aggregate, so a separate persistence model adds ceremony without a current isolation benefit. A dedicated schema preserves the physical module boundary.
  - Database constraints and indexes required to enforce resolved domain rules — In progress.
    - Accepted: At most one Active relationship for a directed `(SupplierOrganizationId, CustomerOrganizationId)` pair is enforced both by the normal application query/idempotent path and by a PostgreSQL partial unique index over the directed pair filtered to Active rows.
    - Accepted: `Status` is stored as readable text (`Active`/`Ended`) rather than an integer or PostgreSQL enum, with a database CHECK constraint restricting allowed values.
    - Accepted: A CHECK constraint enforces state consistency: Active requires `EndedAt IS NULL`; Ended requires `EndedAt IS NOT NULL`. The temporal rule that EndedAt is set once remains a domain/application responsibility.
    - Accepted: Supplier/Customer Organization IDs are external module references and have no foreign keys to `organization_access` persistence. Organization existence/status is validated through the OrganizationAccess contract.
    - Rationale: Database constraints protect cheap local invariants and close concurrency races while avoiding persistence coupling across module boundaries.
  - Transaction boundaries — In progress.
    - Accepted: Create does not introduce an explicit transaction around the pre-insert lookup and insert. OrganizationAccess validation remains outside any cross-module transaction, and EF Core/SaveChanges uses its normal write transaction. The partial unique index is the concurrency guarantee.
    - Rationale: A normal-isolation explicit transaction would not remove the Create race, while Serializable is unnecessary when the expected race is already resolved by the unique constraint.
  - Concurrency/race handling for lifecycle-changing operations — In progress.
    - Accepted: On concurrent Create, violation of the specific Active-pair partial unique index is treated as the expected losing race; the winner's Active relationship is read back and returned through the already-defined idempotent `200 OK` semantics. Other database failures are not translated into idempotent success.
    - Accepted: End uses an atomic conditional update that transitions only a row currently in Active state. If zero rows are updated, the relationship is read again: an existing Ended relationship is idempotent success preserving its original EndedAt, while an unknown relationship remains `404 Not Found` subject to the already-defined scope semantics.
    - Accepted: The End transition timestamp comes from PriceSync's application clock/`TimeProvider` and the same timestamp is supplied to the atomic update; PostgreSQL does not define the business transition time.
    - Rationale: Constraints/conditional updates make PostgreSQL the atomic arbiter for the small state transitions without introducing general optimistic-version infrastructure, pessimistic locking or broad retry loops.
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
