# Customer Pricing

Customer Pricing owns the derived Customer-facing price view produced from immutable published source facts. It does not own Supplier publication, agreement acceptance, approval, or delivery history.

## Responsibilities

Customer Pricing:

- deterministically calculates a Customer Price Projection from exact Price List Versions, the Active Price Agreement Version, and a versioned calculation rule
- invalidates and rebuilds cached projections when relevant immutable inputs change
- calculates the Price Change Set between a current projection and a previous delivered price basis

## Projection semantics

`CustomerPriceProjection` is not a classic aggregate with a command-driven mutable lifecycle. It is a derived, immutable result for an exact set of immutable inputs and calculation-rule version.

The projection may be materialized lazily, cached, evicted, and rebuilt without changing domain meaning. It is not itself the permanent audit record.

Customer Pricing therefore owns the calculation semantics while referring to source facts by exact identifiers, including `PriceListVersionId` and `PriceAgreementVersionId`.

## Price Change Set

`PriceChangeSet` is also derived. It describes the difference between the current projection and the Customer's previous delivered price basis. Customer Approval may present that difference during review, but does not own the comparison semantics.

## Context relationships

Customer Pricing consumes immutable published facts from Price Publication and the currently Active accepted agreement state from Agreements. It exposes projections and change sets to Customer Approval.
