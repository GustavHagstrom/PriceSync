# Trading Relationships

## Purpose

Own the directed Supplier–Customer relationship that scopes access and price distribution between two Organizations.

## Aggregate model

### TradingRelationship

`TradingRelationship` represents one continuous establishment of a directed Supplier-to-Customer relationship and has its own stable `TradingRelationshipId`.

It owns:

- `TradingRelationshipId`
- `SupplierOrganizationId`
- `CustomerOrganizationId`
- lifecycle status: `Active` or `Ended`
- `CreatedAt`
- nullable `EndedAt`

`SupplierOrganizationId` and `CustomerOrganizationId` are immutable after creation. Changing either party means a different Trading Relationship and therefore a different aggregate identity.

`CreatedAt` records when PriceSync registers the relationship. It does not represent an externally supplied date for when the real-world commercial relationship originally began. `EndedAt` records when PriceSync performs the `Active → Ended` transition; it is set once and is not an externally supplied or backdated commercial end date.

The directed `(SupplierOrganizationId, CustomerOrganizationId)` pair is domain-significant for uniqueness and re-establishment rules, but is not the aggregate identity. Generic update metadata such as `UpdatedAt` is not domain state.

It does not own Organization identity, memberships, permissions, or commercial pricing terms. Supplier and Customer are contextual roles owned by TradingRelationships; they are not permanent Organization types owned by OrganizationAccess.

Its transactional boundary exists because the parties and lifecycle state of one directed relationship must remain internally consistent, while Agreements, Price Publication, and retrieval history remain independently owned and must not participate in the same transaction.

## Organization boundary

OrganizationAccess owns Organization identity and lifecycle. TradingRelationships references Organizations only by `OrganizationId` and validates them through the public `OrganizationAccess.Contracts` boundary; it does not access OrganizationAccess persistence directly.

Creating a Trading Relationship requires both the Supplier and Customer Organizations to exist and be `Active`. OrganizationAccess exposes the minimal status semantics required for this validation: `Active`, `Archived`, and `NotFound`. TradingRelationships performs one status query per Organization rather than requiring a relationship-specific batch contract.

`Archived` Organizations remain valid historical references but cannot participate in creation of a new Trading Relationship. Archiving an Organization does not automatically end or otherwise change existing Trading Relationships. Likewise, ending an existing Trading Relationship does not require its referenced Organizations to remain `Active`.

Expected Organization states such as `Active`, `Archived`, and `NotFound` are normal contract results. Technical failures crossing the OrganizationAccess boundary are failures of the operation and must not be interpreted as `NotFound`.

Organization validation is point-in-time, without cross-module transactions or locks. TradingRelationships does not cache or persist Organization status. Although End does not require both referenced Organizations to remain Active, the acting Organization must still be Active and authorized. An Active party may therefore end a relationship whose counterparty is Archived.

## Lifecycle

PriceSync represents an externally established Supplier–Customer relationship, so a new `TradingRelationship` starts as `Active`. The only lifecycle transition is `Active → Ended`; `Ended` is terminal. There is no `Pending` state because negotiation or acceptance is outside this boundary, and no separate paused/suspended state is currently part of the domain.

Ending retains the complete aggregate. The transition only changes the lifecycle status to `Ended` and records `EndedAt`; it does not delete or clear relationship state. Existing historical references continue to identify the same relationship.

If the same directed Supplier–Customer relationship is established again after ending, it is represented by a new `TradingRelationship` with a new `TradingRelationshipId`. The ended relationship remains unchanged as historical fact.

## Commands / decisions

This area is responsible for decisions such as:

- represent an externally established Supplier–Customer relationship
- end the relationship

PriceSync does not negotiate, request, or commercially accept the relationship.

### Authorization and scope

Every operation requires an authenticated Person and an acting Organization. The selected Organization identifies request scope but does not itself grant authority. OrganizationAccess owns membership and role interpretation and exposes `ReadTradingRelationships` and `ManageTradingRelationships` capabilities.

Create and End require management capability; Get and directional lists require read capability. In the current role model, active Owners may manage relationships and active Members (including Owners) may read them. The acting Organization must be Active for either capability. Archived Organizations remain historical references but cannot act through these operations.

Either party may Create or End without counterparty approval. Create rejects an unrelated actor before counterparty validation. Once authorized, Get and End return the same Not Found result for unknown relationships and relationships outside the acting Organization's scope.

### Create and End

Create takes both party IDs. New creation returns `201 Created` with the relationship representation and Location. Repeating Create for an already Active pair returns `200 OK` with the existing representation, after the same authorization and Organization validation. No separate idempotency key is required.

Concurrent Create obeys the same one-Active-per-pair invariant. The losing request returns `200 OK` rather than a uniqueness error. If End completes before conflict readback, the current implementation prefers an Active relationship for the pair, otherwise its latest ended history. The response may therefore be Ended; resolving the conflict does not create another relationship. This is a pair-level resolution rule, not a guarantee to return the exact relationship that caused the conflict if further Create/End operations intervene.

End takes only the relationship ID and returns `200 OK` with the final representation. Repeated or concurrent End preserves the original EndedAt. Timestamps come from PriceSync's application clock. There is no ordinary physical-delete operation.

Missing acting Organization is `400 Bad Request`, authentication failure is `401 Unauthorized`, and capability denials are `403 Forbidden`. Invalid input, including self-relationships, is rejected. After authorization, missing counterparties return Not Found and Archived counterparties prevent creation. Expected failures expose stable machine-readable Problem Details codes; technical failures remain operation failures.

### Reads

Get returns a scoped relationship by identity, including ended history. Directional lists derive scope from the acting Organization: the Supplier list contains relationships where it is Supplier, and the Customer list contains relationships where it is Customer.

Lists default to Active and support Ended or all statuses. Opaque cursor/keyset pagination orders by `CreatedAt DESC, TradingRelationshipId DESC`, with up to 50 results per page in the current API. Clients retain the same status filter while following a cursor. The compact representation contains relationship identity, party IDs, status, CreatedAt and EndedAt.

### Audit

Successful, denied, cancelled and failed Create/End attempts must be auditable with actor and acting-Organization context, the requested pair or relationship identity, and outcome. Create validation failures are included. Correlation/logging mechanisms, a repository-wide telemetry strategy and read-audit policy remain separate technical concerns.

## Business facts

The area produces facts that a Trading Relationship exists or has ended. Downstream contexts may use its identity/status to decide whether future Supplier-backed distribution is available, but ending the relationship never rewrites their historical facts.

## Core invariants

- A Trading Relationship connects exactly one Supplier Organization to exactly one distinct Customer Organization; a self-directed relationship is invalid.
- Supplier and Customer are contextual roles in this relationship, not permanent Organization types.
- Both referenced Organizations must exist and be `Active` when a new Trading Relationship is created.
- Supplier and Customer Organization IDs are immutable for the lifetime of the relationship.
- Direction is significant: `A → B` and `B → A` are separate relationships and may coexist.
- At most one active Trading Relationship may exist for the same directed Supplier/Customer pair at a time.
- Ended relationships are retained in full as history; later re-establishment creates a new relationship rather than reactivating an ended one.
- Organization archival does not implicitly transition an existing Trading Relationship.
- Ending a Trading Relationship does not require its Organizations to remain `Active`.
- The relationship establishes access for price distribution but contains no Price Agreement terms.
- Ending the relationship stops future Supplier-backed distribution without deleting prior activation or retrieval history.

## Public module boundary

The current cross-module contract exposes `TradingRelationshipId`, without exposing the aggregate or a generic identity/status reader. Future consumers must define purpose-built contracts from concrete needs. HTTP representations remain internal to the module. No domain/integration event or outbox contract is required by the current implementation.

## References

Agreements reference `TradingRelationshipId`. Customer-specific publication may reference the relationship when required for scope. Those contexts reference it by identity/status and do not own its lifecycle.
