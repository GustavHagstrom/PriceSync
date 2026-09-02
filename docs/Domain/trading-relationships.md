# Trading Relationships

## Purpose

Own the directed Supplier–Customer relationship that scopes access and price distribution between two Organizations.

## Ownership

This area owns the Trading Relationship lifecycle and its status. It references:

- `SupplierOrganizationId`
- `CustomerOrganizationId`

It does not own Organization identity, memberships, permissions, or commercial pricing terms.

## Core invariants

- A Trading Relationship connects exactly one Supplier Organization to exactly one Customer Organization.
- Supplier and Customer are contextual roles in this relationship, not permanent Organization types.
- The relationship establishes access for price distribution but contains no Price Agreement terms.
- Ending the relationship stops future distribution without deleting previously completed deliveries or audit history.

## References

Agreements may reference `TradingRelationshipId`. Customer-specific publication may reference the relationship when required for scope, without owning its lifecycle.
