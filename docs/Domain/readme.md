# Established Domain Areas

This directory contains domain concepts and cohesive areas whose boundaries have been established through domain discovery. Ongoing discovery remains in [DOMAIN.md](../DOMAIN.md), while the concise canonical business vocabulary remains in [Context.md](../Context.md).

## Domain map

The current bounded-context candidates are:

- [Organization & Access](./organization-access.md) — owns Organization identity, memberships, roles, and organization-scoped authorization concepts.
- [Trading Relationships](./trading-relationships.md) — owns the directed Supplier–Customer relationship used to scope price access and distribution.
- [Agreements](./agreements.md) — owns Supplier-published Price Agreements and Customer agreement acceptance/activation state.
- [Price Publication](./price-publication.md) — owns logical Price Lists, mutable Price Drafts, validation/publication, and immutable Price List Versions for both General Price Lists and Customer-Specific Net Price Lists.
- [Customer Pricing](./customer-pricing.md) — owns deterministic Customer Price Projection and Price Change Set calculation as rebuildable derived results.
- [Customer Approval](./customer-approval.md) — owns explicit Customer Import Approval of an exact projection.
- [Delivery](./delivery.md) — owns immutable Price Delivery metadata/provenance, Delivery Artifacts, and retrieval facts.
- [Integration Configuration](./integration-configuration.md) — owns reusable Import and Export Profiles at the external-format boundary.

These are domain ownership and consistency seams, not deployment boundaries. PriceSync remains free to implement them together, for example in a modular monolith.

## Current cross-context references

- Trading Relationships reference Organizations by `OrganizationId`; they do not own Organization identity or membership state.
- Agreements reference the applicable Trading Relationship and Organizations rather than duplicating those concepts.
- Price Publication may reference a Customer or Trading Relationship when a Customer-Specific Net Price List requires that scope, without taking ownership of the relationship.
- Customer Pricing consumes exact immutable Price List Versions plus the Active accepted Price Agreement Version and versioned calculation rules.
- Customer Approval consumes a specific Customer Price Projection and its Price Change Set, and records the Customer's immutable approval fact.
- Delivery creates one immutable Price Delivery from an Import Approval. The delivery identifies the exact approved price basis but does not permanently own all projected price rows.
- Delivery Artifacts are representations of a Price Delivery and retrieval is recorded as separate immutable facts rather than mutable state on the Price Delivery.
- Integration Configuration transforms external formats to or from the canonical model and does not own pricing facts.

Further questions around artifact persistence/regeneration, publication compatibility across contexts, Customer-managed external prices, and exact collaboration styles remain under discovery in Issue #7.

## How to use these pages

When a domain area is established, document its ownership, aggregates, invariants, and references here. Keep unresolved design questions in [DOMAIN.md](../DOMAIN.md) until they are decided. Keep purely technical components in the architecture documentation.
