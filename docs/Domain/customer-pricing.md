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

`PriceAgreementVersionId` is present for projections derived from a General Price List and absent for already-resolved net price lists. No separate `CalculationRuleVersion`, projection fingerprint, or projection approval identity is part of the domain model.

For a General Price List, Customer Pricing calculates resolved Customer Prices from the exact Price List Version and Price Agreement Version supplied to it. For Customer-Specific Net Price Lists and Customer-Managed External Net Price Lists, the projection is effectively an identity projection of the already-resolved source prices.

The same exact input tuple always represents the same semantic projection regardless of when or how many times it is materialized. Time affects recommendations and Customer selection, not evaluation of an already selected immutable input tuple.

## Current projection

The current projection is defined strictly from Customer-selected Active source state.

For a General Price List:

`Active PriceListVersion + Active PriceAgreementVersion -> current projection basis`

For a Customer-Specific Net or Customer-Managed External Net Price List:

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

Price List activation is scoped per logical Price List. Agreement activation is scoped per Price Agreement, not per Price List. If one Price Agreement applies to multiple General Price Lists, the same Active Price Agreement Version is used for those lists unless the commercial model defines separate Price Agreements.

A thin application/use-case orchestrator obtains the relevant Active selections and asks Customer Pricing to evaluate the resulting exact immutable tuple. The same projection capability may also be invoked with explicitly selected immutable versions for historical or exploratory reconstruction.

Different Price Agreements may cover the same logical General Price List. For current pricing, the Customer explicitly selects which Price Agreement to use for each logical list; that agreement's Active Version supplies the agreement input. Agreement Version activation remains per agreement and applies across all lists where the Customer has selected it. The list's Active Version is selected independently. System recommendations and warnings are advisory; the Customer is responsible for its selections, and the system never silently substitutes another combination. This freedom does not bypass projection compatibility validation or permit invalid projections to be retrieved.

That orchestration does not require a distributed transaction or globally atomic snapshot across contexts. Exact immutable version references remain reproducible even if Customer Active state changes concurrently.

## Materialization

New source versions do not require eager recalculation across all affected Customers. Projections are calculated lazily when requested or otherwise needed. Cache invalidation and background precomputation are implementation optimizations and do not change domain meaning.

## Context relationships

Customer Pricing consumes exact immutable Price List Versions from Price Publication and, for General Price Lists, exact immutable Price Agreement Versions from Agreements. Current-projection requests additionally use the Customer-owned Active selections from those contexts. Customer Pricing exposes valid projections, invalid Projection Issues, Projection Warnings, and Price Change Sets as derived results.
