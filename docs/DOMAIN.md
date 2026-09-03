# PriceSync Domain

This document is the working model of the PriceSync domain. Stable ownership and aggregate boundaries live under [Domain](./Domain/readme.md); the concise canonical language lives in [Context](./Context.md).

## Domain boundary

PriceSync makes prices that suppliers and customers have agreed outside the platform available for synchronization between their systems. PriceSync does not negotiate those prices or establish the underlying commercial agreement.

## Organizations and relationships

An Organization is a business party. Supplier and Customer are contextual roles, not permanent organization types. A Trading Relationship represents a directed Supplier-to-Customer relationship established outside PriceSync and scopes Supplier-backed distribution without owning commercial terms.

A Customer-Managed External Net Price List does not require a Trading Relationship. The Customer administers the source privately and identifies the actual supplier through an External Supplier Reference.

## Published source facts

PriceSync separates stable logical identities from immutable published versions.

### Price Agreements

A Price Agreement represents Supplier-published commercial terms for one Trading Relationship/scope. Each Price Agreement Version is a complete immutable publication.

Each version has:

- required `ValidFrom`
- optional `ValidTo`
- `PublishedAt`
- immutable commercial terms such as discounts and Rounding Policy

Validity applies to the entire Agreement Version. It expresses Supplier intent and drives recommendation/warnings; it does not hard-control Customer use.

Agreement Versions may overlap. Recommendation for a UTC calendar date selects the eligible non-withdrawn published version with the latest `ValidFrom`; ties use latest `PublishedAt`. If no version covers the evaluated date, there is no recommended version.

Recommendation is independent of Customer acceptance and activation.

### Price Lists

A Price List is the stable logical identity/scope across publications. General Price Lists contain List Prices reusable across Customers. Customer-Specific Net Price Lists contain already-resolved Net Prices for one Customer. Customer-Managed External Net Price Lists follow the same publication/versioning semantics but are administered privately by the consuming Customer.

A Price Draft is mutable working state. At most one active draft exists per logical Price List. Successful publication atomically produces one complete immutable Price List Version.

Each Price List Version has required `ValidFrom`, optional `ValidTo`, and `PublishedAt`. Validity applies to the whole version and follows the same recommendation semantics as Agreement Versions. For Customer-Managed External lists, entered validity is interpreted as the external source's stated validity.

Published version content, including validity metadata, is immutable. Corrections require a new version.

### Withdrawal

A published Price Agreement Version or Price List Version may be permanently withdrawn with a required human-readable reason.

Withdrawal is a separate immutable source-side fact. It:

- removes the version from current recommendation
- produces a strong warning when the version is used
- never rewrites the immutable version
- never changes Customer Active state automatically
- does not prevent Customer activation, reactivation, projection, or retrieval
- cannot be reversed; a new version must be published instead

Historical recommendation is evaluated as-of the relevant point in time, so later withdrawal does not rewrite what was recommended earlier.

## Customer source selection

Supplier/source publication and Customer use are separate business facts.

### Agreement acceptance and activation

Agreement Acceptance records that the Customer has seen a specific Price Agreement Version. It is not bilateral contract formation.

The Customer may explicitly activate an accepted Agreement Version. Activating an unaccepted version implicitly accepts it. At most one Agreement Version is Active per Price Agreement scope, and no Active version is a valid explicit state.

Any accepted published Agreement Version may be activated or reactivated regardless of validity dates or withdrawal status. Supplier publication, validity changes through time, recommendation, and withdrawal never change Customer Active state automatically.

Agreement activation is scoped per Price Agreement, not per Price List.

### Price List activation

There is no separate Price List acceptance step. For each Customer and logical Price List, the Customer may explicitly activate or reactivate any published Price List Version, including a withdrawn version with warning. At most one version is Active per logical Price List, and no Active version is valid.

Publication, recommendation, and withdrawal never change Customer Active state automatically.

Agreement and Price List activation/deactivation decisions are durable Customer business history. Current Active selections may be materialized for efficient access, but historical active state is determined from preserved activation facts.

## Customer Price Projection

A Customer Price Projection is a deterministic, rebuildable Customer-facing view for one exact immutable source tuple.

Natural identity:

- `CustomerId`
- `PriceListVersionId`
- optional `PriceAgreementVersionId`

For a General Price List, Customer Pricing resolves List Prices using the exact supplied Price Agreement Version. For Customer-Specific Net and Customer-Managed External Net Price Lists, the projection exposes already-resolved Net Prices and has no Agreement Version component.

A projection is not an aggregate with an approval lifecycle and has no permanent projection ID. It may be lazily materialized, cached, evicted, and rebuilt without changing domain meaning.

Projection evaluation returns either:

- one complete valid Customer Price Projection, optionally with non-blocking warnings
- or an invalid result with structured Projection Issues

Partial projections are not produced.

### Current projection

The current projection basis is defined strictly from the Customer's Active source selections.

For a General Price List:

`Active PriceListVersion + Active PriceAgreementVersion -> current projection basis`

For a Customer-Specific Net or Customer-Managed External Net Price List:

`Active PriceListVersion -> current projection basis`

If a required Active selection is missing, there is no current projection basis. If the current basis evaluates as invalid, there is no valid current Customer Price Projection. PriceSync never falls back silently to an older valid, latest, recommended, or previously retrieved projection.

Activation changes source selection only. It does not synchronously validate or materialize the projection.

### Explicit and historical projections

Any immutable source-version combination may be explicitly evaluated for historical or exploratory reconstruction, even if that exact tuple was never Active together.

A projection built from historical versions does not imply that it was historically Active. Historical Active status is determined from activation history.

## Price Change Set

A Price Change Set is a deterministic difference between two explicitly identified projections. Its primary decision-support use is comparison of a candidate projection against the Customer's current valid projection before changing source activation.

Price Change Sets and Projection Warnings do not create a separate Import Approval workflow.

## Export and retrieval

Only complete valid Customer Price Projections may be exported as Customer Prices.

A projection does not need to be current to be exported or retrieved. The Customer may explicitly retrieve a valid historical/arbitrary projection without changing Active state.

Projection provenance and exported representation provenance are separate. A historical pricing tuple may be rendered using a current or explicitly selected compatible Export Profile/configuration.

Each successful handoff creates one immutable Price Retrieval fact identifying the exact immutable pricing inputs and relevant representation provenance, for example:

- `CustomerId`
- `PriceListVersionId`
- optional `PriceAgreementVersionId`
- Export Profile/configuration version when applicable
- requested format/export-rule version
- retrieval timestamp
- retrieving user or integration identity
- content hash when useful

Repeated successful retrievals create separate facts. Failed attempts before successful handoff are operational telemetry rather than successful retrieval domain facts.

There is no separate `ImportApproval`, `PriceDelivery`, or `DeliveryArtifact` business object in the established model.

## Price representation

A Supplier may provide a Customer Price as either:

- a Net Price stated directly for a Customer and article
- a List Price combined with a Customer Discount from a Price Agreement

PriceSync resolves both forms to a Customer Price. A Supplier Price Group discount takes precedence over a general Customer Discount; discounts are alternatives and are not stacked.

Each Price Agreement carries an explicit Rounding Policy. PriceSync uses decimal arithmetic and retains enough calculation basis to explain resolved prices.

Customer Prices exclude VAT and transaction taxes. Mandatory article-specific surcharges that always apply to the priced article/unit are included in the resolved price; order-, project-, freight-, and delivery-specific costs remain outside the core price domain.

A Price Basis consists of amount, Price Quantity, and Unit of Measure within the Price List Version currency.

Quantity breaks are an initial non-goal. The initial model represents one price basis per Price List Entry within a complete Price List Version.

## Article identity

An Article Reference is identified by Supplier, logical Price List, and Supplier Article ID. The same Supplier Article ID may occur in different logical Price Lists and represent different Price List Entries.

PriceSync assigns a stable opaque PriceSync Article ID to that business identity. Changing Supplier Article ID within the same logical Price List creates a new entry identity; PriceSync does not infer continuity from similar names or prices.

PriceSync does not own a product catalog. Rich descriptions, classification, images, and product-master reconciliation remain outside the domain boundary.

## Import and integration configuration

External Excel/CSV layouts are normalized through reusable Organization-private Import Profiles. Canonical API publication may bypass Import Profiles and supply canonical data directly, but both paths produce the same Price Draft and use the same publication validation rules.

Export Profiles are Customer-private reusable transformations from canonical Customer Price Projections to external representations. They change representation, not pricing semantics.

Source Files are private evidence owned by the uploading Organization. Their retention lifecycle is independent of immutable published versions and Price Retrieval facts.

## Context structure

The established bounded-context/domain-module candidates are documented under [docs/Domain](./Domain/readme.md):

- Organization & Access
- Trading Relationships
- Agreements
- Price Publication
- Customer Pricing
- Retrieval & Consumption
- Integration Configuration

These are ownership and consistency seams, not deployment boundaries. A modular monolith may implement them together without introducing a distributed transaction for the Supplier-to-Customer flow.

## Remaining discovery

Implementation details such as persistence schemas, API contracts, event transport, deployment topology, and UI remain intentionally unresolved. New domain concepts should be introduced only when real business scenarios require them rather than to preserve superseded workflow assumptions.
