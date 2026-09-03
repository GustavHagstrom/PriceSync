# Customer Pricing

Customer Pricing owns the derived Customer-facing price view produced from immutable published source facts. It does not own Supplier publication, Customer activation decisions, or retrieval history.

## Responsibilities

Customer Pricing:

- deterministically produces one Customer Price Projection for an exact Customer + Price List Version + optional Price Agreement Version tuple
- defines the Customer's current projection from the Customer's currently Active source selections
- allows explicitly requested projections from other compatible immutable source-version combinations, including combinations that were never Active together
- applies a Price Agreement Version only when the source is a General Price List
- directly projects already-resolved Net Prices for Customer-Specific Net and Customer-Managed External Net Price Lists
- validates whether the exact source combination can produce a complete Customer-facing projection
- returns invalid projection results explicitly rather than treating expected incompatibility as an exception
- calculates Price Change Sets between projections when requested
- produces non-blocking Projection Warnings from fixed system rules in the initial model
- materializes and caches projections lazily when they are requested or needed

## Projection semantics

`CustomerPriceProjection` is not a classic aggregate with a command-driven mutable lifecycle. It is a derived, immutable Customer-specific view for an exact set of immutable inputs.

The projection may be materialized lazily, cached, evicted, and rebuilt without changing domain meaning. It is not itself a permanent audit record and has no stable domain `CustomerPriceProjectionId`.

Its natural identity is:

- `CustomerId`
- `PriceListVersionId`
- optional `PriceAgreementVersionId`

`PriceAgreementVersionId` is present for projections derived from a General Price List and absent for already-resolved net price lists. No separate `CalculationRuleVersion`, projection fingerprint, or projection approval identity is part of the domain model.

For a General Price List, Customer Pricing calculates resolved Customer Prices from the exact Price List Version and Price Agreement Version supplied to it. For Customer-Specific Net Price Lists and Customer-Managed External Net Price Lists, the projection is effectively an identity projection of the already-resolved source prices.

The same exact input tuple always represents the same semantic projection regardless of when or how many times it is materialized. Time affects recommendations and Customer selection, not evaluation of an already selected immutable input tuple.

## Current projection

The current projection is defined strictly from Customer-selected Active source state.

For a General Price List:

`Active PriceListVersion + Active PriceAgreementVersion -> current CustomerPriceProjection`

For a Customer-Specific Net or Customer-Managed External Net Price List:

`Active PriceListVersion -> current CustomerPriceProjection`

If a required Active selection is missing, there is no current projection. PriceSync does not silently fall back to a latest, recommended, or previously Active version.

Recommended source versions are advisory and separate from current state.

## Explicit and historical projections

Any compatible immutable source versions may be explicitly selected to calculate a projection, even if that exact combination was never Active together.

A projection calculated from old source versions is not automatically a historical Active projection. Historical Active state is established from the preserved Price List and Agreement activation/deactivation facts owned by their respective contexts.

This distinction allows PriceSync both to reproduce what the Customer actually used at a point in time and to explore or reconstruct arbitrary valid historical combinations without mutating current Customer state.

## Projection validation

Projection compatibility belongs to Customer Pricing, not Price Publication or Agreements. A valid Price List Version and a valid Price Agreement Version may still form an invalid combination for a particular Customer projection.

Building/evaluating a projection therefore produces an explicit domain result:

- a valid complete Customer Price Projection, optionally with non-blocking Projection Warnings
- or an invalid result containing structured Projection Issues

Projection validity is atomic. If any required price row cannot be resolved correctly, the complete projection is invalid. PriceSync does not silently omit invalid rows or substitute speculative pricing fallbacks.

Projection Issues and valid/invalid evaluation status are rebuildable results of the immutable inputs rather than permanent business facts. They may be cached or recorded in technical observability, but no permanent `ProjectionFailure` aggregate is introduced.

Warnings represent suspicious but still valid outcomes, such as unusually large price changes. They inform Customer decisions but do not create a separate projection-approval workflow.

## Price Change Set

`PriceChangeSet` is derived. It describes the difference between two explicitly identified projections, commonly the current projection and a recommended or candidate projection.

A Customer UI may present that difference and Projection Warnings before the Customer activates a different Price List Version or Price Agreement Version. Review is therefore decision support for source activation rather than a separate Import Approval business fact.

## Source selection and orchestration

Price Publication owns Customer Price List activation state and Agreements owns Customer Agreement acceptance/activation state.

A thin application/use-case orchestrator obtains the relevant Active selections and asks Customer Pricing to evaluate the resulting exact immutable tuple. The same projection capability may also be invoked with explicitly selected immutable versions for historical or exploratory reconstruction.

That orchestration does not require a distributed transaction or globally atomic snapshot across contexts. Exact immutable version references remain reproducible even if Customer Active state changes concurrently.

## Materialization

New source versions do not require eager recalculation across all affected Customers. Projections are calculated lazily when requested or otherwise needed. Cache invalidation and background precomputation are implementation optimizations and do not change domain meaning.

## Context relationships

Customer Pricing consumes exact immutable Price List Versions from Price Publication and, for General Price Lists, exact immutable Price Agreement Versions from Agreements. Current-projection requests additionally use the Customer-owned Active selections from those contexts. Customer Pricing exposes valid projections, invalid Projection Issues, Projection Warnings, and Price Change Sets as derived results.
