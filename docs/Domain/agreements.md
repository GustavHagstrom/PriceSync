# Agreements

## Purpose

Own Supplier-published Price Agreement facts and Customer-owned agreement acceptance and activation state within one cohesive domain area.

## Aggregate model

### PriceAgreement

`PriceAgreement` is the stable identity and scope for Supplier-published commercial terms. It owns publication rules and the relationship to the applicable Trading Relationship.

### PriceAgreementVersion

Each `PriceAgreementVersion` is a separate immutable, complete published fact referencing `PriceAgreementId`. Published history is not loaded as a growing child collection of `PriceAgreement`.

`ValidFrom` and `ValidTo` describe the Supplier's intended validity period. They drive recommendations and warnings, but do not hard-block Customer use before or after those dates.

### CustomerAgreementState

`CustomerAgreementState` is a separate Customer-owned aggregate that records:

- which Agreement Versions have been accepted
- which accepted Agreement Version is currently Active, if any
- explicit activation, reactivation, and deactivation decisions
- immutable activation/deactivation history as Customer business facts

Supplier publication and Customer selection therefore do not share one aggregate or transaction boundary.

## Core invariants

- A Price Agreement Version is complete and immutable after publication.
- A Customer does not need to accept superseded intermediate versions before accepting a newer version.
- An Active Agreement Version is always accepted.
- Explicit activation may implicitly accept a previously unaccepted Agreement Version in the same Customer decision.
- At most one Agreement Version is Active for the relevant scope at a time.
- Having no Active Agreement Version is a valid explicit Customer state.
- Any accepted published Agreement Version may be activated or reactivated regardless of `ValidFrom` / `ValidTo`.
- `ValidFrom` / `ValidTo` support system recommendations and warnings rather than hard activation constraints.
- Publishing a newer Agreement Version never changes Customer Active state automatically.

The transactional boundary around `CustomerAgreementState` exists because acceptance and the single-Active-version invariant must change consistently for one Customer/agreement scope. Supplier publication remains independent and does not participate in that transaction.

## Business facts

Agreement activation and deactivation are durable Customer decisions. Their history is preserved so PriceSync can distinguish current state from versions that were actually active at a previous point in time.

A current Active version may be represented as derived/current state for efficient access, but that does not erase the underlying activation history.

## References

The area references Organizations and the applicable Trading Relationship by identity. It does not own those concepts.
