# Customer Pricing

Customer Pricing owns the derived Customer-facing price view produced from immutable published source facts. It does not own Supplier publication, Customer activation decisions, or retrieval history.

## Responsibilities

Customer Pricing:

- deterministically produces one Customer Price Projection for an exact Customer + Price List Version + optional Price Agreement Version tuple
- defines the Customer's current projection from the Customer's currently Active source selections
- allows explicitly requested projections from other compatible immutable source-version combinations, including combinations that were never Active together
- requires Agreement Version coverage for General and Supplier-published Customer-Specific Net Price Lists; only General Price Lists receive calculated discounts
- directly projects already-resolved Net Prices for Customer-Specific Net and Customer-Managed External Net Price Lists
- validates whether the exact source combination can produce a complete Customer-facing projection
- returns invalid projection results explicitly rather than treating expected incompatibility as an exception
- calculates Price Change Sets between a candidate projection and the Customer's current valid projection when requested
- produces non-blocking Projection Warnings from fixed system rules in the initial model
- materializes and caches projections lazily when they are requested or needed

## Projection semantics


`CustomerPriceProjection` is not a classic aggregate with a command-driven mutable lifecycle. It is a derived, immutable Customer-specific view for an exact set of immutable inputs.

The projection may be materialized lazily, cached, evicted, and rebuilt without changing domain meaning. It is not itself a permanent audit record and has no stable domain `CustomerPriceProjectionId`.

Its natural identity is:

- `CustomerId`
- `PriceListVersionId`
- optional `PriceAgreementVersionId`

`PriceAgreementVersionId` is required for General and Supplier-published Customer-Specific Net Price List projections, including explicit historical/exploratory combinations; the Agreement Version must cover the list and match the Supplier and Customer. The combination need not have been Active together. It is absent for Customer-Managed External Net Price Lists. Supplier-published net-list retrieval facts preserve both exact source-version references even though Agreement coverage does not alter the amount. No separate `CalculationRuleVersion`, projection fingerprint, or projection approval identity is part of the domain model.

For a General Price List, Customer Pricing calculates resolved Customer Prices from the exact Price List Version and Price Agreement Version supplied to it. For Customer-Specific Net Price Lists and Customer-Managed External Net Price Lists, the projection is effectively an identity projection of the already-resolved source prices.

The same exact input tuple always represents the same semantic projection regardless of when or how many times it is materialized. Time affects recommendations and Customer selection, not evaluation of an already selected immutable input tuple.

For calculated Customer Prices, use the rounding semantics locked into the supplied Agreement Version at publication. The initial system rule applies the applicable Customer Discount without intermediate rounding, then rounds the result once to four decimal places with midpoints away from zero, preserving the source Price Quantity and Unit of Measure. Later system-rule changes never reinterpret older Agreement Versions. Already-resolved Net Prices remain identity projections and do not receive this calculation rounding. Users may round more coarsely downstream, outside PriceSync.

## Current projection

The current projection is defined strictly from Customer-selected Active source state.

For a General Price List or Supplier-published Customer-Specific Net Price List:

`Active PriceListVersion + relationship-wide Active PriceAgreementVersion -> current projection basis`

For a Customer-Managed External Net Price List:

`Active PriceListVersion -> current projection basis`

The current projection exists semantically from that basis even when it has not yet been materialized. Materialization is lazy and does not create a separate lifecycle state.

If a required Active selection is missing, there is no current projection basis. PriceSync does not silently fall back to a latest, recommended, or previously Active version.

If evaluation of the current basis is invalid, there is no valid current Customer Price Projection. A previous valid projection remains historical and is never silently treated as current.

Recommended source versions are advisory and separate from current state.

## Activation and projection evaluation

Activation and projection evaluation are separate domain actions.

Activating or deactivating a Price List Version or Price Agreement Version changes only the Customer's selected source state. Activation does not synchronously validate the resulting projection combination and does not automatically materialize a projection.

When a projection is requested, Customer Pricing evaluates the exact selected immutable inputs at that time. A source combination may therefore be Active even if evaluation later returns an invalid projection result.

## Explicit and historical projections

Any compatible immutable source versions may be explicitly selected to calculate a projection, even if that exact combination was never Active together.

A projection calculated from old source versions is not automatically a historical Active projection. Historical Active state is established from the preserved Price List and Agreement activation/deactivation facts owned by their respective contexts.

This distinction allows PriceSync both to reproduce what the Customer actually used at a point in time and to explore or reconstruct arbitrary valid historical combinations without mutating current Customer state.

## Projection validation

For General Price Lists, each article entry has at most one Supplier Price Group code in its immutable Price List Version. An exact matching Agreement group discount replaces the list's general discount. If the group code is absent or has no matching discount, use the list's explicit general discount; never combine discounts or choose among multiple groups. Net Prices remain unaffected by discounts.

Projection compatibility belongs to Customer Pricing, not Price Publication or Agreements. A valid Price List Version and a valid Price Agreement Version may still form an invalid combination for a particular Customer projection.

Building/evaluating a projection therefore produces an explicit domain result:

- a valid complete Customer Price Projection, optionally with non-blocking Projection Warnings
- or an invalid result containing structured Projection Issues

Projection validity is atomic. If any required price row cannot be resolved correctly, the complete projection is invalid. PriceSync does not silently omit invalid rows or substitute speculative pricing fallbacks.

Projection Issues and valid/invalid evaluation status are rebuildable results of the immutable inputs rather than permanent business facts. They may be cached or recorded in technical observability, but no permanent `ProjectionFailure` aggregate is introduced.

Warnings represent suspicious but still valid outcomes, such as unusually large price changes. They inform Customer decisions but do not create a separate projection-approval workflow.

Only valid complete Customer Price Projections may flow into Customer Price export/retrieval. Invalid projection results may be inspected for diagnosis but are not consumable Customer Price outputs.

## Price Change Set

`PriceChangeSet` is derived decision support for activation. Its primary meaning is the difference between a candidate projection and the Customer's current valid projection for the same logical Price List.

A Customer UI may present that difference and Projection Warnings before the Customer activates a different Price List Version or Price Agreement Version. Retrieval history does not determine the comparison basis.

If there is no valid current projection, a comparison may instead be unavailable or use an explicitly selected historical basis as a separate user action; PriceSync does not silently substitute the most recently retrieved or previously valid projection as current.

## Source selection and orchestration

Price Publication owns Customer Price List activation state and Agreements owns Customer Agreement acceptance/activation state.

Price List activation is scoped per logical Price List. Agreement activation is scoped to the Supplier–Customer Trading Relationship: at most one version across all Price Agreements is Active. All covered General and Supplier-published Customer-Specific Net Price Lists use that same version. Switching agreements replaces the relationship-wide Active version without changing Active Price List Versions.

A thin application/use-case orchestrator obtains the relevant Active selections and asks Customer Pricing to evaluate the resulting exact immutable tuple. The same projection capability may also be invoked with explicitly selected immutable versions for historical or exploratory reconstruction.

There is no Customer agreement selection per list. Current pricing uses the relationship's Active Agreement Version and the independently Active Price List Version. Missing agreement activation means no current projection basis; missing list coverage produces an invalid projection on evaluation. The system never substitutes another agreement for an uncovered list. Explicit historical or exploratory projections may use other compatible immutable versions without changing current state.

That orchestration does not require a distributed transaction or globally atomic snapshot across contexts. Exact immutable version references remain reproducible even if Customer Active state changes concurrently.

## Materialization

New source versions do not require eager recalculation across all affected Customers. Projections are calculated lazily when requested or otherwise needed. Cache invalidation and background precomputation are implementation optimizations and do not change domain meaning.

## Context relationships

Customer Pricing consumes exact immutable Price List Versions from Price Publication and, for General and Supplier-published Customer-Specific Net Price Lists, exact immutable Price Agreement Versions from Agreements. Current-projection requests additionally use the Customer-owned Active selections from those contexts. Customer Pricing exposes valid projections, invalid Projection Issues, Projection Warnings, and Price Change Sets as derived results.
