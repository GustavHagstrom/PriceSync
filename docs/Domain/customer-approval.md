# Customer Approval

Customer Approval owns the Customer's explicit authorization of an exact Customer Price Projection for delivery.

## Responsibilities

Customer Approval owns:

- the review decision for an exact projection
- `ImportApproval` as an immutable business fact
- the rule that approval applies atomically to the complete projection presented to the Customer
- the rule that a new projection requires a new approval

## Aggregate model

`ImportApproval` is modeled as a small immutable aggregate/fact rather than as mutable state on a long-lived review aggregate.

There is currently no established domain need for a `CustomerReview` aggregate with a workflow such as Pending -> Approved. Review is a user interaction around a projection; the durable business fact is the approval itself.

An approval identifies the exact projection inputs or projection identity that was reviewed and accepted. It never silently follows a newer projection.

## Context relationships

Customer Approval consumes `CustomerPriceProjection` and `PriceChangeSet` from Customer Pricing. A successful approval authorizes creation of one Price Delivery in Delivery for the exact approved projection and Customer channel.
