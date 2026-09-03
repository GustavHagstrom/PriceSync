# Agreements

## Purpose

Own Supplier-published Price Agreement facts and Customer-owned agreement acceptance and activation state within one cohesive domain area.

## Aggregate model

### PriceAgreement

`PriceAgreement` is the aggregate root for the stable identity and scope of Supplier-published commercial terms. It references the applicable Trading Relationship by identity and owns the rules for creating new immutable Agreement Versions within that scope.

Its transactional boundary exists because the stable agreement scope and one publication decision must remain consistent without loading historical versions or Customer selection state into the same aggregate.

### PriceAgreementVersion

Each `PriceAgreementVersion` is a separate immutable, complete published fact referencing `PriceAgreementId`. Published history is not loaded as a growing child collection of `PriceAgreement`.

Each version has:

- `ValidFrom` as a required business date
- optional `ValidTo`, where null means valid until further notice
- `PublishedAt` as the publication timestamp

Validity applies to the complete immutable Agreement Version. When `ValidTo` exists it must be on or after `ValidFrom`.

`ValidFrom` / `ValidTo` represent Supplier intent. They drive recommendations and warnings, but do not hard-block Customer use before or after those dates. The current calendar date for recommendation evaluation is UTC.

Overlapping validity intervals are allowed. Among non-withdrawn published versions whose validity interval contains the evaluated UTC date, the recommended version is the one with the latest `ValidFrom`; if several share that `ValidFrom`, the one with the latest `PublishedAt` wins. Recommendation is independent of Customer acceptance and activation state. If no published non-withdrawn version covers the evaluated date, there is no recommended Agreement Version.

Recommendation can also be evaluated historically as-of a point in time. Later publication or withdrawal does not rewrite what was recommended before those facts existed.

A published Agreement Version is immutable including its validity dates. Correcting validity or commercial terms therefore requires a new version.

A Supplier may permanently withdraw a published Agreement Version with a required human-readable `WithdrawalReason`. Withdrawal is a separate immutable Supplier fact; it does not mutate the published version. A withdrawn version is excluded from current recommendations and should produce a strong warning when used, but it remains available for Customer acceptance, activation/reactivation, projection, and retrieval. Withdrawal is permanent; restoring the same version to Published is not supported.

Publication/withdrawal commands operate on the Supplier-owned agreement scope and produce immutable version/withdrawal facts. They do not participate in the Customer selection transaction.

### CustomerAgreementState

`CustomerAgreementState` is a separate Customer-owned aggregate root that records:

- which Agreement Versions have been accepted
- which accepted Agreement Version is currently Active, if any
- explicit activation, reactivation, and deactivation decisions
- immutable activation/deactivation history as Customer business facts

Agreement activation is scoped per `PriceAgreement`, not per Price List. One Active Agreement Version therefore applies consistently wherever that Price Agreement is used.

Supplier publication/withdrawal and Customer selection do not share one aggregate or transaction boundary.

## Commands / decisions

This area owns decisions such as:

- publish a new immutable Price Agreement Version
- withdraw a published Agreement Version with reason
- accept an Agreement Version
- activate/reactivate an Agreement Version, implicitly accepting if necessary
- deactivate the currently Active Agreement Version

## Core invariants

- A Price Agreement Version is complete and immutable after publication, including validity metadata.
- `ValidFrom` is required.
- `ValidTo` is optional and, when present, must be on or after `ValidFrom`.
- Overlapping validity intervals are allowed.
- A Customer does not need to accept superseded intermediate versions before accepting a newer version.
- An Active Agreement Version is always accepted.
- Explicit activation may implicitly accept a previously unaccepted Agreement Version in the same Customer decision.
- At most one Agreement Version is Active for the relevant Price Agreement scope at a time.
- Having no Active Agreement Version is a valid explicit Customer state.
- Any published Agreement Version, including a withdrawn one, may be accepted, activated, or reactivated regardless of `ValidFrom` / `ValidTo`; withdrawal and out-of-validity use produce warnings rather than hard blocks.
- Publishing or withdrawing an Agreement Version never changes Customer Active state automatically.
- Withdrawal requires a reason and cannot be reversed for that same immutable version.

The transactional boundary around `CustomerAgreementState` exists because acceptance and the single-Active-version invariant must change consistently for one Customer/agreement scope. Supplier publication/withdrawal remains independent and does not participate in that transaction.

## Business facts

Agreement activation and deactivation are durable Customer decisions. Their history is preserved so PriceSync can distinguish current state from versions that were actually active at a previous point in time.

A current Active version may be represented as derived/current state for efficient access, but that does not erase the underlying activation history.

Publication and withdrawal are durable Supplier facts. Historical recommendation is reconstructed from facts that existed at the evaluated point in time rather than from today's state.

## References

The area references Organizations and the applicable Trading Relationship by identity. It does not own those concepts. Customer Pricing consumes exact immutable Agreement Version references but does not mutate Agreements state.
