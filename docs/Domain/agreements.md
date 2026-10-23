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

## Commercial scope

### Customer agreement selection per list

Agreements owns the Customer's selected Price Agreement for each logical General Price List. This is a separate Customer-owned aggregate boundary scoped by Customer and logical Price List, with at most one selected Price Agreement. It is independent of `CustomerAgreementState`, which owns acceptance and Active Version selection per agreement. Changing the selected agreement for one list does not change selections for other lists or activate/deactivate any Agreement Version. Customer Pricing consumes these selections through module contracts rather than owning or mutating them.

The Customer may explicitly clear the selected agreement. Each actual selection change, including clearing selection and later returning to a previously selected agreement, is preserved as an immutable Customer business fact recording the selected agreement (or none), decision time and acting Person. Current selection and its durable history are committed atomically within this boundary. Together with Price List and Agreement Version activation history, these facts support reconstruction of previously selected combinations; they do not prove that a projection was retrieved. Clearing selection does not deactivate the agreement or the Price List Version. A General Price List with no selected agreement has no current projection basis; no fallback agreement is chosen automatically.

Selecting an agreement does not require an Active Agreement Version and does not implicitly accept or activate any version. The Customer may prepare the selection before activating a version. Until the selected agreement has an Active Version, the list has no current projection basis; the missing input is reported explicitly without automatic selection.

The Customer may select an agreement even when its Active Version does not cover the list. Lack of coverage produces a warning rather than blocking the selection. The agreement must belong to the applicable Supplier–Customer Trading Relationship and the logical General Price List must belong to that Supplier; Customer choice does not bypass these scope boundaries. Customer Pricing evaluates the exact immutable inputs and returns an invalid projection when the selected Agreement Version has no applicable list terms. It does not invent discount terms, and invalid projections cannot be retrieved as Customer Prices.

Selection changes must be based on the current selection revision. If another Customer decision has changed that selection since it was read, the stale change is rejected as a conflict rather than silently overwriting the other decision. The caller must read the current selection before issuing a new decision. A rejected conflict changes neither current state nor successful selection history.

With a current revision, selecting the already-selected agreement or clearing an already-empty selection returns the existing state without creating a new business-history fact. A stale revision remains a conflict even when its requested agreement matches the current selection. Operational request logging is separate from business selection history.

### Published terms and list coverage

A Price Agreement may cover multiple General Price Lists within its Trading Relationship. Each complete immutable Price Agreement Version records the covered lists and their commercial terms, including Customer Discounts and Rounding Policy. Customer Pricing owns calculation from those terms.

List coverage references logical Price Lists by identity, not fixed Price List Versions. Publishing a new Price List Version does not require republishing the Price Agreement. The Customer selects Price List Versions and Price Agreement Versions independently; Customer Pricing evaluates compatibility of the exact selected immutable versions.

Customer Discounts are specified separately for each covered General Price List. Each list has an explicit general discount, which may be zero, and optional Supplier Price Group discounts. A matching group discount replaces the list's general discount; discounts are never stacked.

The initial model uses a common system rounding rule rather than Supplier-configurable Rounding Policies. The exact decimal precision and rounding method remain to be decided before implementation. Configuration requires a concrete commercial need; if introduced, it is scoped per covered Price List within an immutable Agreement Version.

Different Price Agreements within the same Trading Relationship may reference the same General Price List. List references do not represent exclusive ownership or a transfer between agreements. The Supplier is responsible for publishing the intended terms; the Customer chooses the agreement/list combination to use. Older versions remain usable, with warnings for withdrawal or use outside their validity period rather than a hard block. A newer publication alone does not make an older version expired.

A new Agreement Version may add or remove covered lists and always records the complete coverage. Older versions retain their original coverage and remain usable. Publishing a coverage change does not change Customer Active state; it takes effect for current use only when the Customer activates the new Agreement Version.

For current pricing, the Customer explicitly selects the applicable Price Agreement for each logical General Price List. That selection identifies the agreement, while Agreement Version activation remains scoped per Price Agreement. Changing an agreement's Active version therefore changes the current agreement input for every list where the Customer selected that agreement. The Customer independently selects the Active Price List Version. Recommendations and warnings support these choices but never replace or change them automatically. Customer Pricing still evaluates whether the exact selected combination can produce a valid projection.

## Commands / decisions

This area owns decisions such as:

- publish a new immutable Price Agreement Version
- withdraw a published Agreement Version with reason
- accept an Agreement Version
- activate/reactivate an Agreement Version, implicitly accepting if necessary
- deactivate the currently Active Agreement Version
- select or clear the Customer's applicable Price Agreement for a logical General Price List, independently of version activation

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
