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
- relationship lifecycle/status
- `CreatedAt`
- nullable `EndedAt`

The directed `(SupplierOrganizationId, CustomerOrganizationId)` pair is domain-significant for uniqueness and re-establishment rules, but is not the aggregate identity. Generic update metadata such as `UpdatedAt` is not domain state.

It does not own Organization identity, memberships, permissions, or commercial pricing terms.

Its transactional boundary exists because the parties and lifecycle state of one directed relationship must remain internally consistent, while Agreements, Price Publication, and retrieval history remain independently owned and must not participate in the same transaction.

## Commands / decisions

This area is responsible for decisions such as:

- represent an externally established Supplier–Customer relationship
- end the relationship

PriceSync does not negotiate, request, or commercially accept the relationship.

If the same directed Supplier–Customer relationship is established again after ending, it is represented by a new `TradingRelationship` with a new `TradingRelationshipId`. The ended relationship remains unchanged as historical fact.

## Business facts

The area produces facts that a Trading Relationship exists or has ended. Downstream contexts may use its identity/status to decide whether future Supplier-backed distribution is available, but ending the relationship never rewrites their historical facts.

## Core invariants

- A Trading Relationship connects exactly one Supplier Organization to exactly one distinct Customer Organization; a self-directed relationship is invalid.
- Supplier and Customer are contextual roles in this relationship, not permanent Organization types.
- Direction is significant: `A → B` and `B → A` are separate relationships and may coexist.
- At most one active Trading Relationship may exist for the same directed Supplier/Customer pair at a time.
- Ended relationships are retained as history; later re-establishment creates a new relationship rather than reactivating an ended one.
- The relationship establishes access for price distribution but contains no Price Agreement terms.
- Ending the relationship stops future Supplier-backed distribution without deleting prior activation or retrieval history.

## References

Agreements reference `TradingRelationshipId`. Customer-specific publication may reference the relationship when required for scope. Those contexts reference it by identity/status and do not own its lifecycle.
