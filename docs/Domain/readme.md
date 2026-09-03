# Established Domain Areas

This directory contains domain concepts and cohesive areas whose boundaries have been established through domain discovery. Ongoing discovery remains in [DOMAIN.md](../DOMAIN.md), while the concise canonical business vocabulary remains in [Context.md](../Context.md).

## Domain map

The current bounded-context candidates are:

- [Organization & Access](./organization-access.md) — owns Organization identity, memberships, roles, and organization-scoped authorization concepts.
- [Trading Relationships](./trading-relationships.md) — owns the directed Supplier–Customer relationship used to scope price access and distribution.
- [Agreements](./agreements.md) — owns Supplier-published Price Agreements plus Customer acceptance and activation state/history.
- [Price Publication](./price-publication.md) — owns logical Price Lists, mutable Price Drafts, intrinsic validation/publication, immutable Price List Versions, Customer Price List activation state/history, and Customer-owned `ExternalSupplierReference` for Customer-managed external prices.
- [Customer Pricing](./customer-pricing.md) — owns projection compatibility/validation, deterministic Customer Price Projections, Projection Warnings, and Price Change Sets as rebuildable derived results.
- [Retrieval & Consumption](./delivery.md) — owns deterministic export/retrieval behavior and append-only `PriceRetrieval` facts for exact immutable projection inputs. Separate `ImportApproval` and `PriceDelivery` concepts are not used.
- [Integration Configuration](./integration-configuration.md) — owns reusable Import and Export Profiles at the external-format boundary.

These are domain ownership and consistency seams, not deployment boundaries. PriceSync remains free to implement them together, for example in a modular monolith.

## Current cross-context references

- Trading Relationships reference Organizations by `OrganizationId`; they do not own Organization identity or membership state.
- Agreements reference the applicable Trading Relationship and Organizations rather than duplicating those concepts.
- Price Agreement `ValidFrom` / `ValidTo` express Supplier intent and drive recommendations/warnings; Customer activation is authoritative for current use and may occur before or after those dates.
- Agreement activation implies acceptance if necessary. Accepted versions may be activated, reactivated, or deactivated; Supplier publication never changes Customer Active state automatically.
- Agreement activation is scoped per Price Agreement, not per Price List. One Active Agreement Version therefore applies consistently wherever that Price Agreement is used.
- Price Publication owns General, Customer-Specific Net, and Customer-Managed External Net Price Lists. Customer-managed external lists use `ExternalSupplierReference` without creating a Trading Relationship or making the Customer a Supplier to itself.
- Price Publication validates a Price List intrinsically. It does not iterate all Customers and Agreements to guarantee every possible projection before publishing.
- A Price List has no separate Customer acceptance step. Any published Price List Version may be explicitly activated or reactivated, and no Active version is a valid state. Supplier publication never changes the Customer's Active Price List Version automatically.
- Price List activation is scoped per logical Price List.
- Agreement and Price List activation/deactivation decisions are preserved as immutable Customer business history; current Active selections may be derived/materialized from that history.
- Activation changes only Customer source selection. It does not synchronously validate or materialize a Customer Price Projection.
- A thin application/use-case orchestrator obtains Customer Active selections and asks Customer Pricing to evaluate the exact immutable tuple. No new domain context or distributed transaction is introduced for that orchestration.
- The current projection basis is strictly derived from the Customer's Active selections and exists semantically even before materialization. Missing required Active input means there is no current projection basis.
- If the current projection basis evaluates as invalid, there is no valid current Customer Price Projection. PriceSync never silently falls back to an older valid, latest, recommended, or retrieved projection.
- `CustomerPriceProjection` has no stable domain ID, calculation-rule version, projection fingerprint, or approval identity. Its natural identity is `CustomerId + PriceListVersionId + optional PriceAgreementVersionId`.
- The same exact immutable input tuple always denotes the same semantic projection.
- Any immutable versions may be explicitly combined and evaluated for historical or exploratory projection. Whether a projection was actually Active historically is determined from activation history, not from projection existence.
- Customer Pricing owns compatibility of exact inputs. Projection evaluation returns either a complete valid projection with optional warnings or structured issues; partial projections are not produced.
- `PriceChangeSet` primarily compares a candidate projection against the Customer's current valid projection and serves as decision support for activation. Retrieval history does not define the comparison basis.
- Only complete valid projections may be exported as Customer Prices.
- A valid projection does not need to be current to be exported or retrieved. Explicit historical retrieval does not mutate Customer Active state.
- Projection provenance and export-representation provenance are separate. A historical projection may be rendered through a current or explicitly selected compatible Export Profile, with the chosen export configuration captured in retrieval provenance.
- Exported representations are deterministic derived results and may be cached rather than permanently stored when they can be regenerated from immutable source references plus versioned export configuration.
- Retrieval & Consumption records each successful handoff as a separate immutable `PriceRetrieval` fact carrying the exact immutable source-version and export-configuration provenance used for that retrieval.
- Integration Configuration transforms external formats to or from the canonical model and does not own pricing facts.

The remaining Issue #7 discovery can now focus on whether any additional domain seams or collaboration details materially change these ownership rules.

## How to use these pages

When a domain area is established, document its ownership, aggregates, invariants, and references here. Keep unresolved design questions in [DOMAIN.md](../DOMAIN.md) until they are decided. Keep purely technical components in the architecture documentation.
