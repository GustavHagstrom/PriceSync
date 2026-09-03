# Trading Relationships

## Purpose

Own the directed Supplier–Customer relationship that scopes access and price distribution between two Organizations.

## Aggregate model

### TradingRelationship

`TradingRelationship` is the aggregate root for one directed Supplier-to-Customer relationship.

It owns:

- `SupplierOrganizationId`
- `CustomerOrganizationId`
- relationship lifecycle/status
- the decision and history that the relationship has ended

It does not own Organization identity, memberships, permissions, or commercial pricing terms.

Its transactional boundary exists because the parties and lifecycle state of one directed relationship must remain internally consistent, while Agreements, Price Publication, and retrieval history remain independently owned and must not participate in the same transaction.

## Commands / decisions

This area is responsible for decisions such as:

- represent an externally established Supplier–Customer relationship
- end the relationship

PriceSync does not negotiate, request, or commercially accept the relationship.

## Business facts

The area produces facts that a Trading Relationship exists or has ended. Downstream contexts may use its identity/status to decide whether future Supplier-backed distribution is available, but ending the relationship never rewrites their historical facts.

## Core invariants

- A Trading Relationship connects exactly one Supplier Organization to exactly one Customer Organization.
- Supplier and Customer are contextual roles in this relationship, not permanent Organization types.
- The relationship establishes access for price distribution but contains no Price Agreement terms.
- Ending the relationship stops future Supplier-backed distribution without deleting prior activation or retrieval history.

## References

Agreements reference `TradingRelationshipId`. Customer-specific publication may reference the relationship when required for scope. Those contexts reference it by identity/status and do not own its lifecycle.
