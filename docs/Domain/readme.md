# Established Domain Areas

This directory contains domain concepts and cohesive areas whose boundaries have been established through domain discovery. Ongoing discovery remains in [DOMAIN.md](../DOMAIN.md), while the concise canonical business vocabulary remains in [Context.md](../Context.md).

## Domain map

The current bounded-context candidates are:

- [Organization & Access](./organization-access.md) — owns Organization identity, memberships, roles, and organization-scoped authorization concepts.
- [Trading Relationships](./trading-relationships.md) — owns the directed Supplier–Customer relationship used to scope price access and distribution.
- [Agreements](./agreements.md) — owns Supplier-published Price Agreements and Customer agreement acceptance/activation state.
- [Price Publication](./price-publication.md) — owns logical Price Lists, mutable Price Drafts, validation/publication, immutable Price List Versions, and Customer-owned `ExternalSupplierReference` for Customer-managed external prices.
- [Customer Pricing](./customer-pricing.md) — owns deterministic Customer Price Projections and Price Change Sets as rebuildable derived results, one projection per Customer and logical Price List.
- [Customer Approval](./customer-approval.md) — owns explicit Customer Import Approval of an exact projection and the durable identity of the approved price basis.
- [Retrieval & Consumption](./delivery.md) — owns append-only `PriceRetrieval` facts and deterministic export/retrieval behavior for approved price bases. A separate `PriceDelivery` aggregate is not used.
- [Integration Configuration](./integration-configuration.md) — owns reusable Import and Export Profiles at the external-format boundary.

These are domain ownership and consistency seams, not deployment boundaries. PriceSync remains free to implement them together, for example in a modular monolith.

## Current cross-context references

- Trading Relationships reference Organizations by `OrganizationId`; they do not own Organization identity or membership state.
- Agreements reference the applicable Trading Relationship and Organizations rather than duplicating those concepts.
- Price Publication owns General, Customer-Specific Net, and Customer-Managed External Net Price Lists. Customer-managed external lists use `ExternalSupplierReference` without creating a Trading Relationship or making the Customer a Supplier to itself.
- Customer Pricing consumes one exact Price List Version per projection. General Price Lists additionally use the Active accepted Price Agreement Version; already-resolved net price lists do not.
- `CustomerPriceProjection` has no stable domain ID, calculation-rule version, or projection fingerprint. Its natural identity is `CustomerId + PriceListVersionId + optional PriceAgreementVersionId`.
- Customer Approval consumes a specific Customer Price Projection and its Price Change Set, and records one immutable Import Approval for that exact natural projection identity.
- Publication and consumption approval remain separate for all supported price-list forms, including Customer-managed external prices.
- Import Approval, rather than a separate Price Delivery, is the durable identity of the approved price basis available for later export/retrieval.
- Exported representations are deterministic derived results and may be cached rather than permanently stored when they can be regenerated from immutable approval provenance plus versioned export configuration.
- Retrieval & Consumption records each successful handoff as a separate immutable `PriceRetrieval` fact; repeated retrievals therefore form an append-only log rather than mutable retrieval state.
- Integration Configuration transforms external formats to or from the canonical model and does not own pricing facts.

Further questions around publication compatibility across contexts and exact collaboration styles remain under discovery in Issue #7.

## How to use these pages

When a domain area is established, document its ownership, aggregates, invariants, and references here. Keep unresolved design questions in [DOMAIN.md](../DOMAIN.md) until they are decided. Keep purely technical components in the architecture documentation.
