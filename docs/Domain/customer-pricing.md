# Customer Pricing

Customer Pricing owns the derived Customer-facing price view produced from immutable published source facts. It does not own Supplier publication, agreement acceptance, approval, or retrieval history.

## Responsibilities

Customer Pricing:

- deterministically produces one Customer Price Projection per Customer and logical Price List
- applies an Active Price Agreement Version only when the source is a General Price List
- directly projects already-resolved Net Prices for Customer-Specific Net and Customer-Managed External Net Price Lists
- validates whether the exact source combination can produce a complete Customer-facing projection
- returns invalid projection results explicitly rather than treating expected incompatibility as an exception
- calculates the Price Change Set between a candidate projection and the Customer's current approved basis for that logical Price List
- produces non-blocking Projection Warnings from fixed system rules in the initial model
- materializes and caches projections lazily when they are requested or needed

## Projection semantics

`CustomerPriceProjection` is not a classic aggregate with a command-driven mutable lifecycle. It is a derived, immutable Customer-specific view for an exact set of immutable inputs.

The projection may be materialized lazily, cached, evicted, and rebuilt without changing domain meaning. It is not itself the permanent audit record and has no stable domain `CustomerPriceProjectionId`.

Its natural identity is:

- `CustomerId`
- `PriceListVersionId`
- optional `PriceAgreementVersionId`

`PriceAgreementVersionId` is present for projections derived from a General Price List and absent for already-resolved net price lists. No separate `CalculationRuleVersion` or projection fingerprint is part of the domain identity.

A projection always represents exactly one logical Price List for one Customer. Multiple lists from the same Supplier, and lists from different Suppliers, therefore produce separate projections and can be reviewed and approved independently.

For a General Price List, Customer Pricing calculates resolved Customer Prices from the Price List Version and Active accepted Price Agreement Version. For Customer-Specific Net Price Lists and Customer-Managed External Net Price Lists, the projection is effectively an identity projection of the already-resolved source prices into the same Customer-facing review model.

This keeps the downstream review, approval, export, and retrieval flow consistent across all supported price-list forms without inventing a Price Agreement where none exists.

## Projection validation

Projection compatibility belongs to Customer Pricing, not Price Publication. A valid Price List Version and a valid Price Agreement Version may still form an invalid combination for a particular Customer projection.

Building/evaluating a projection therefore produces an explicit domain result:

- a valid complete Customer Price Projection, optionally with non-blocking Projection Warnings
- or an invalid result containing structured Projection Issues

Projection validity is atomic. If any required price row cannot be resolved correctly, the complete projection is invalid and cannot be approved. PriceSync does not silently omit invalid rows or substitute speculative pricing fallbacks.

Warnings represent suspicious but still valid outcomes, such as unusually large price changes. They do not block Import Approval. In the initial model the warning rules and thresholds are fixed system rules rather than Customer-configurable policies.

## Price Change Set

`PriceChangeSet` is derived. It describes the difference between a candidate projection and the Customer's current approved basis for the same logical Price List. Customer Approval may present that difference and Projection Warnings during review, but does not own their calculation semantics.

The current approved basis is the latest relevant Import Approval for `CustomerId + logical PriceListId`; it is derived from immutable approval facts rather than maintained as separate mutable state.

A newer Price List Version or agreement input does not invalidate that approved basis. It merely makes a newer candidate projection possible. The previously approved basis remains current until the Customer approves another valid projection for that logical Price List.

## Materialization

New source versions do not require eager recalculation across all affected Customers. Candidate projections are calculated lazily when requested or otherwise needed. Cache invalidation and background precomputation may be implementation optimizations, but do not change which Import Approval is the current approved basis.

## Context relationships

Customer Pricing consumes immutable Price List Versions from Price Publication and, only for General Price Lists, the currently Active accepted agreement state from Agreements. It exposes valid projections, invalid Projection Issues, Projection Warnings, and Price Change Sets for Customer review and approval.
