# Established Domain Areas

This directory contains domain concepts and cohesive areas whose boundaries have been established through domain discovery. Ongoing discovery remains in [DOMAIN.md](../DOMAIN.md), while the concise canonical business vocabulary remains in [Context.md](../Context.md).

## Domain map

The current bounded-context candidates are:

- [Organization & Access](./organization-access.md) — owns Organization identity, memberships, roles, and organization-scoped authorization concepts.
- [Trading Relationships](./trading-relationships.md) — owns the directed Supplier–Customer relationship used to scope price access and distribution.
- [Agreements](./agreements.md) — owns Supplier-published Price Agreements and Customer agreement acceptance/activation state.
- [Price Publication](./price-publication.md) — owns logical Price Lists, mutable Price Drafts, intrinsic list validation/publication, immutable Price List Versions, and Customer-owned `ExternalSupplierReference` for Customer-managed external prices.
- [Customer Pricing](./customer-pricing.md) — owns projection compatibility/validation, deterministic Customer Price Projections, Projection Warnings, and Price Change Sets as rebuildable derived results, one projection per Customer and logical Price List.
- [Customer Approval](./customer-approval.md) — owns explicit Customer Import Approval of an exact valid projection and the durable identity of the approved price basis.
- [Retrieval & Consumption](./delivery.md) — owns append-only `PriceRetrieval` facts and deterministic export/retrieval behavior for approved price bases. A separate `PriceDelivery` aggregate is not used.
- [Integration Configuration](./integration-configuration.md) — owns reusable Import and Export Profiles at the external-format boundary.

These are domain ownership and consistency seams, not deployment boundaries. PriceSync remains free to implement them together, for example in a modular monolith.

## Current cross-context references

- Trading Relationships reference Organizations by `OrganizationId`; they do not own Organization identity or membership state.
- Agreements reference the applicable Trading Relationship and Organizations rather than duplicating those concepts.
- Price Publication owns General, Customer-Specific Net, and Customer-Managed External Net Price Lists. Customer-managed external lists use `ExternalSupplierReference` without creating a Trading Relationship or making the Customer a Supplier to itself.
- Price Publication validates a Price List intrinsically. It does not iterate all Customers and Agreements to guarantee every possible projection before publishing.
- Price Publication owns which `PriceListVersionId` is currently applicable for a logical Price List. Agreements owns which accepted `PriceAgreementVersionId` is Active for a General Price List relationship.
- A thin application/use-case orchestrator combines those context-owned selections with `CustomerId` and asks Customer Pricing to evaluate the exact immutable tuple. No new domain context or distributed transaction is introduced for that orchestration.
- Customer Pricing owns compatibility of those exact inputs. Projection evaluation returns either a complete valid projection (optionally with non-blocking warnings) or structured issues; partial projections are not approvable.
- Projection Issues and valid/invalid evaluation status are rebuildable results rather than permanent domain facts. Technical caching and observability do not change domain meaning.
- `CustomerPriceProjection` has no stable domain ID, calculation-rule version, or projection fingerprint. Its natural identity is `CustomerId + PriceListVersionId + optional PriceAgreementVersionId`.
- The same exact immutable input tuple always denotes the same semantic projection. Time affects source-version selection, not evaluation of an already selected tuple.
- Customer Approval records one immutable Import Approval for that exact natural projection identity. Invalid projections cannot be approved and Projection Warnings never block approval.
- Each Customer has an independently derived current approved basis per logical Price List: the latest relevant Import Approval. New source data does not replace it until another projection for that list is explicitly approved.
- A reviewed projection may still be approved after a newer source basis becomes current; approval is always for the exact projection the Customer actually reviewed.
- Concurrent approvals of different valid projections for the same Customer and logical Price List may both succeed. A deterministic server-defined registration order determines which immutable Import Approval is the current approved basis.
- Publication and consumption approval remain separate for all supported price-list forms, including Customer-managed external prices.
- Import Approval, rather than a separate Price Delivery, is the durable identity of the approved price basis available for later export/retrieval.
- Exported representations are deterministic derived results and may be cached rather than permanently stored when they can be regenerated from immutable approval provenance plus versioned export configuration.
- Retrieval & Consumption records each successful handoff as a separate immutable `PriceRetrieval` fact; repeated retrievals therefore form an append-only log rather than mutable retrieval state.
- Integration Configuration transforms external formats to or from the canonical model and does not own pricing facts.

The remaining Issue #7 discovery can now focus on whether any additional domain seams or collaboration details materially change these ownership rules.

## How to use these pages

When a domain area is established, document its ownership, aggregates, invariants, and references here. Keep unresolved design questions in [DOMAIN.md](../DOMAIN.md) until they are decided. Keep purely technical components in the architecture documentation.
