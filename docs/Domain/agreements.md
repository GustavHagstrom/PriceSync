# Agreements

## Purpose

Own Supplier-published Price Agreement facts and Customer-owned agreement acceptance and activation state within one cohesive domain area.

## Aggregate model

### PriceAgreement

`PriceAgreement` is the stable identity and scope for Supplier-published commercial terms. It owns publication rules and the relationship to the applicable Trading Relationship.

### PriceAgreementVersion

Each `PriceAgreementVersion` is a separate immutable, complete published fact referencing `PriceAgreementId`. Published history is not loaded as a growing child collection of `PriceAgreement`.

### CustomerAgreementState

`CustomerAgreementState` is a separate Customer-owned aggregate that records:

- which Agreement Versions have been accepted
- which accepted Agreement Version is currently Active
- explicit early activation of an accepted future version

Supplier publication and Customer selection therefore do not share one aggregate or transaction boundary.

## Core invariants

- A Price Agreement Version is complete and immutable after publication.
- A Customer does not need to accept superseded intermediate versions before accepting a newer version.
- Only an accepted Price Agreement Version may become Active.
- Exactly one accepted version is Active for the relevant agreement scope when an Active version exists.
- Normally the applicable accepted version with the latest reached `Valid From` is Active.
- Explicit early activation may select an accepted future version without changing Supplier-defined validity metadata.
- An unaccepted newer version does not displace the currently Active accepted version.

## References

The area references Organizations and the applicable Trading Relationship by identity. It does not own those concepts.
