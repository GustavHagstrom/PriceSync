# Customer Pricing

Customer Pricing owns the derived Customer-facing price view produced from immutable published source facts. It does not own Supplier publication, agreement acceptance, approval, or retrieval history.

## Responsibilities

Customer Pricing:

- deterministically produces one Customer Price Projection per Customer and logical Price List
- applies an Active Price Agreement Version only when the source is a General Price List
- directly projects already-resolved Net Prices for Customer-Specific Net and Customer-Managed External Net Price Lists
- invalidates and rebuilds cached projections when relevant immutable inputs change
- calculates the Price Change Set between a current projection and the Customer's previous approved price basis for that logical Price List

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

## Price Change Set

`PriceChangeSet` is also derived. It describes the difference between the current projection and the Customer's previous approved price basis for the same logical Price List. Customer Approval may present that difference during review, but does not own the comparison semantics.

## Context relationships

Customer Pricing consumes immutable Price List Versions from Price Publication and, only for General Price Lists, the currently Active accepted agreement state from Agreements. It exposes projections and change sets to Customer Approval.
