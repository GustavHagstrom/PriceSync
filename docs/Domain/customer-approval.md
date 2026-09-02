# Customer Approval

Customer Approval owns the Customer's explicit authorization of an exact Customer Price Projection and the durable identity of that approved price basis.

## Responsibilities

Customer Approval owns:

- the review decision for an exact valid projection
- `ImportApproval` as an immutable business fact
- the rule that approval applies atomically to the complete projection presented to the Customer
- the rule that an invalid projection cannot be approved
- the rule that a new projection requires a new approval
- the rule that the same exact projection may be approved at most once by the Customer

## Aggregate model

`ImportApproval` is modeled as a small immutable aggregate/fact rather than as mutable state on a long-lived review aggregate.

There is currently no established domain need for a `CustomerReview` aggregate with a workflow such as Pending -> Approved. Review is a user interaction around a projection; the durable business fact is the approval itself.

An approval identifies the exact immutable pricing basis that was reviewed and accepted through explicit references:

- `CustomerId`
- `PriceListVersionId`
- optional `PriceAgreementVersionId`
- approval actor and time

`PriceAgreementVersionId` is absent for Customer-Specific Net and Customer-Managed External Net Price Lists because those prices are already resolved and require no agreement calculation.

No separate projection fingerprint, `CalculationRuleVersion`, or stable `CustomerPriceProjectionId` is required in the domain model. The exact projection is naturally identified by the Customer plus the immutable source-version references above.

The approved projection rows themselves do not need permanent storage. They may be cached and deterministically rebuilt from the immutable approval provenance.

## Current approved basis

A Customer has a current approved basis independently for each logical Price List.

The current approved basis is derived as the latest relevant `ImportApproval` for the Customer and logical Price List. There is no separate mutable `CurrentImportApprovalId` pointer that must be kept in sync.

Publishing a newer Price List Version, activating a newer Agreement Version, or calculating a newer valid projection does not replace the current approved basis. Only an explicit Import Approval of another projection for that same logical Price List does so.

If the Customer never approves the newer candidate projection, the previous Import Approval remains the current approved basis indefinitely unless another explicit domain action is introduced in the future.

Two valid projections for the same Customer and logical Price List may be approved concurrently. Both immutable Import Approval facts may be recorded. The current approved basis is whichever approval is registered last according to a deterministic server-defined ordering; approval does not require serializing a mutable current pointer.

A reviewed projection remains approvable even if a newer source basis becomes current before the Customer confirms approval. Approval always refers to the exact immutable projection the Customer reviewed, not to an implicitly refreshed latest projection.

## Consumption

`ImportApproval` is also the durable identity for the approved price basis that later becomes available for retrieval or export. A separate `PriceDelivery` domain concept is not required.

Exports are derived representations of an Import Approval. Exported bytes do not need permanent storage when they can be regenerated deterministically from the approval plus versioned export configuration and export rules.

Each successful retrieval is recorded separately as an immutable `PriceRetrieval` fact. Retrieval attempts that fail before successful export and handoff remain operational telemetry rather than domain facts.

## Context relationships

Customer Approval consumes only valid `CustomerPriceProjection` results plus their `PriceChangeSet` and non-blocking Projection Warnings from Customer Pricing. Projection Warnings are informational and never require a separate acknowledgement before Import Approval. Integration Configuration may provide versioned Export Profile configuration when an approved price basis is exported. Successful retrievals belong to the retrieval/consumption responsibility and reference the exact `ImportApproval` that was retrieved.
