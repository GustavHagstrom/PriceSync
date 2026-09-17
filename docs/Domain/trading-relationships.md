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

It does not own Organization identity, memberships, permissions, or commercial pricing terms.

Its transactional boundary exists because the parties and lifecycle state of one directed relationship must remain internally consistent, while Agreements, Price Publication, and retrieval history remain independently owned and must not participate in the same transaction.

## Lifecycle

PriceSync represents an externally established Supplier–Customer relationship, so a new `TradingRelationship` starts as `Active`. The only lifecycle transition is `Active → Ended`; `Ended` is terminal. There is no `Pending` state because negotiation or acceptance is outside this boundary, and no separate paused/suspended state is currently part of the domain.

Ending retains the complete aggregate. The transition only changes the lifecycle status to `Ended` and records `EndedAt`; it does not delete or clear relationship state. Existing historical references continue to identify the same relationship.

If the same directed Supplier–Customer relationship is established again after ending, it is represented by a new `TradingRelationship` with a new `TradingRelationshipId`. The ended relationship remains unchanged as historical fact.

## Commands / decisions

This area is responsible for decisions such as:

- represent an externally established Supplier–Customer relationship
- end the relationship

PriceSync does not negotiate, request, or commercially accept the relationship.

## Business facts

The area produces facts that a Trading Relationship exists or has ended. Downstream contexts may use its identity/status to decide whether future Supplier-backed distribution is available, but ending the relationship never rewrites their historical facts.

## Core invariants

- A Trading Relationship connects exactly one Supplier Organization to exactly one distinct Customer Organization; a self-directed relationship is invalid.
- Supplier and Customer are contextual roles in this relationship, not permanent Organization types.
- Supplier and Customer Organization IDs are immutable for the lifetime of the relationship.
- Direction is significant: `A → B` and `B → A` are separate relationships and may coexist.
- At most one active Trading Relationship may exist for the same directed Supplier/Customer pair at a time.
- Ended relationships are retained in full as history; later re-establishment creates a new relationship rather than reactivating an ended one.
- The relationship establishes access for price distribution but contains no Price Agreement terms.
- Ending the relationship stops future Supplier-backed distribution without deleting prior activation or retrieval history.

## References

Agreements reference `TradingRelationshipId`. Customer-specific publication may reference the relationship when required for scope. Those contexts reference it by identity/status and do not own its lifecycle.
