# Agreements

## Purpose

Own Supplier-published Price Agreement facts and Customer-owned agreement acceptance and activation state within one cohesive domain area.

## Aggregate model

### PriceAgreement

`PriceAgreement` is the aggregate root for the stable identity and scope of Supplier-published commercial terms. It references the applicable Trading Relationship by identity and owns the rules for creating new immutable Agreement Versions within that scope.

Its transactional boundary exists because the stable agreement scope and one publication decision must remain consistent without loading historical versions or Customer selection state into the same aggregate.

A Supplier creates this identity separately before publishing the first version. An agreement may initially have no published versions, and multiple agreements may exist within the relationship. Retrying the same creation request must not create another agreement.

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

`CustomerAgreementState` is a separate Customer-owned aggregate root scoped to one Supplier–Customer Trading Relationship that records:

- which Agreement Versions have been accepted
- which accepted Agreement Version is currently Active, if any
- explicit activation, reactivation, and deactivation decisions
- immutable activation/deactivation history as Customer business facts

Agreement activation is scoped to the Trading Relationship, across all its Price Agreements. One Active Agreement Version supplies terms together for its covered Supplier-published Price Lists.

Supplier publication/withdrawal and Customer selection do not share one aggregate or transaction boundary.

## Commercial scope


### Relationship-wide Customer activation

The Customer activates one complete accepted Price Agreement Version for the Supplier–Customer Trading Relationship. At most one version across all Price Agreements in that relationship is Active. Activating another version replaces the previous Active version in the same Customer decision, including switches between different agreements. Having no Active version is valid.

There is no Customer agreement selection per Price List and no separate aggregate, commands or history for agreement/list pairs. Acceptance may be recorded for multiple versions; activation uses their complete commercial terms together.

Current activation state and immutable activation/deactivation history belong to the same consistency boundary. History identifies the exact Agreement Version, decision time and acting Person, including switches between agreements. Together with independent Price List activation history, it supports historical reconstruction; successful retrieval remains a separate business fact.

Activation does not change Active Price List Versions or synchronously validate or materialize projections. Missing agreement activation means no current projection basis for General or Supplier-published Customer-Specific Net Price Lists in the relationship. Missing list terms in the Active Agreement Version produce an invalid projection on evaluation, without fallback to another agreement or invented discounts. Invalid projections cannot be retrieved as Customer Prices.

The Agreement Version must belong to the applicable Trading Relationship and its covered Supplier-published Price Lists must belong to that Supplier. Customer activation never bypasses authorization or scope boundaries.

### Published terms and list coverage

A published Agreement Version must cover at least one logical Price List: a General Price List or Supplier-published Customer-Specific Net Price List. An Agreement may cover only net lists. Net coverage is explicit and carries no discount terms. Covered net lists must match the agreement Supplier and Customer. Customer-Managed External Net Price Lists remain outside Agreements.

General and Supplier Price Group discounts allow at most one decimal place in the percentage value (for example, `12.5%`). Values requiring greater precision are rejected at publication rather than rounded silently.

Supplier Price Group codes are text and match exactly, including case and leading zeros. `0410` and `410` are different codes, as are `a` and `A`. Any normalization must be explicit during import before publication; published codes are not silently normalized during projection evaluation.

An Agreement Version may reference each logical Price List only once and define at most one discount for each exact Supplier Price Group code. Publication rejects duplicates even when the discount values are identical, without silently merging entries or choosing the last entry.

Publication is atomic and requires verification of every referenced logical Price List's existence, allowed type, Supplier and applicable Customer scope through a Price Publication-owned contract. If verification is unavailable, nothing is published; return an unavailable/retryable outcome distinct from an invalid-reference validation result. An unimplemented verification capability must not be replaced by permissive validation or partial publication.

A logical Price List's owning Organization, type, currency, market and applicable Customer are immutable from creation. Agreement publication verifies stable scope through a Price Publication-owned read contract without writing to that module or requiring a shared scope-lock transaction. This verifies logical list references; Customer Pricing separately evaluates whether exact immutable list and agreement versions produce a valid projection.

An Agreement Version may use general discounts alone, group discounts with an explicit zero general discount per covered list, or both together. A matching group discount always takes precedence, even if it is lower than the general discount or zero. If an article has no matching group discount, use its list's explicit general discount. Never stack discounts or automatically choose the highest discount.

Group discounts may reference Supplier group codes not yet used by articles in the covered Price Lists. Publication does not require those codes to occur in current list content. A discount takes effect when the selected immutable Price List Version contains matching articles. Matching uses the code preserved in the immutable source versions, without consulting mutable group metadata to reinterpret historical results.

Each published Agreement Version locks the system rounding rule in effect at publication, including decimal precision, midpoint method and calculation stage. Later changes to the system rule apply only to new Agreement Versions. Existing versions retain their original calculation semantics, preserving reproducibility without a user-configurable setting.

Store those complete rounding semantics directly in the immutable Agreement Version, supplied by the system at publication. The initial values are four decimal places, midpoints away from zero and one final rounding after discount calculation without intermediate rounding. Historical interpretation must not depend on mutable settings or a separate rounding-rule registry. Customer Pricing consumes the preserved semantics with the exact immutable source versions.

A Price Agreement may cover General Price Lists and Supplier-published Customer-Specific Net Price Lists within its Trading Relationship. Each complete immutable Price Agreement Version records the covered lists and their commercial terms, including Customer Discounts and Rounding Policy. Customer Pricing owns calculation from those terms.

List coverage references logical Price Lists by identity, not fixed Price List Versions. Each referenced list must already exist, be a General Price List or Supplier-published Customer-Specific Net Price List and belong to the Supplier, but need not have a published version. A net list must also match the agreement Customer. Publishing a new Price List Version does not require republishing the Price Agreement or renewed Agreement Acceptance, including the first publication in an already-covered logical list. The Customer selects Price List Versions and Price Agreement Versions independently; Customer Pricing evaluates compatibility of the exact selected immutable versions. A logical list created later is not automatically covered: including it requires a new Agreement Version and Customer activation of that version. This workflow is the current product decision and should be evaluated through actual usage.

Each covered General Price List has an explicit general Customer Discount, which may be zero. Optional Supplier Price Group discounts belong to the complete Agreement Version, keyed by the Supplier's group code, and apply to matching articles in every covered General Price List. Net Prices receive no discount or calculation rounding. Group identity is shared across the Supplier's logical Price Lists; group discounts are not repeated or overridden per list. A matching group discount replaces the list's general discount; discounts are never stacked. Both general and group discounts must be between 0 and 100 percent inclusive: zero leaves the List Price unchanged and 100 percent yields zero. Negative discounts (markups) and values above 100 percent are rejected.

The initial model uses a common system rounding rule rather than Supplier-configurable Rounding Policies. Calculated Customer Prices use four decimal places for their stated Price Quantity and Unit of Measure, with midpoints rounded away from zero (for example, `1.23445` becomes `1.2345`). Calculate the applicable discount without intermediate rounding, then round the resulting Customer Price once. Preserve the source Price Quantity and Unit of Measure rather than normalizing to a price per single unit before rounding. Users may round more coarsely downstream, outside PriceSync. Configuration requires a concrete commercial need; if introduced, it is scoped per covered Price List within an immutable Agreement Version.

Different Price Agreements within the same Trading Relationship may reference the same General Price List. List references do not represent exclusive ownership or a transfer between agreements. The Supplier is responsible for publishing the intended terms; the Customer activates one complete Agreement Version for the relationship. Older versions remain usable, with warnings for withdrawal or use outside their validity period rather than a hard block. A newer publication alone does not make an older version expired.

A new Agreement Version may add or remove covered lists and always records the complete coverage. Older versions retain their original coverage and remain usable. Publishing a coverage change does not change Customer Active state; it takes effect for current use only when the Customer activates the new Agreement Version.

For current pricing, all General and Supplier-published Customer-Specific Net Price Lists in the Trading Relationship use the same Active Agreement Version. Activating another version changes the agreement input for all those lists together, including changes in coverage. The Customer independently selects the Active Price List Version for each logical list. Recommendations and warnings never change Customer state automatically. Customer Pricing validates the exact immutable combination for each list.

## Commands / decisions

### Authorization and relationship lifecycle

Active Members (including Owners) of either party may read agreements and history. Supplier Owners may create agreements, publish versions and withdraw versions. Customer Owners may accept versions and activate/deactivate the relationship-wide selection. The acting Organization must be Active; authority in an unrelated Organization grants no access.

After the Trading Relationship has Ended, reading history, Supplier withdrawal and Customer deactivation remain allowed. New agreements, publications, acceptances and activations/reactivations are rejected. Ending the relationship does not automatically clear Customer selections or rewrite history, but stops future Supplier-backed distribution. Status is checked point-in-time: an operation that observed Active may complete after concurrent termination, without reopening the relationship or authorizing continued distribution.

### Repeated and concurrent decisions

Customer selection requests identify the expected From version and desired To version, either of which may explicitly mean no Active version. If To is already current, succeed without another transition. Otherwise, apply the change only if From is current; a different current version conflicts and requires reload. Comparison and transition are atomic. Intervening changes that return to From do not themselves prevent the request. No idempotency key or caller revision is required for selection.

The first acceptance of a version preserves its Person and timestamp. Repeated explicit acceptance, or implicit acceptance during activation, never overwrites that first fact or creates duplicates. Actual selection transitions preserve From/To, Person and system decision time in an unambiguous relationship-wide order.

Version publication identifies the latest version the Supplier is starting from, or explicitly no version for first publication. A matching retry that already created the immediate next version returns that version; a different publication conflicts. Published versions cannot be republished or changed. Withdrawal similarly preserves its first reason, Person and timestamp: a repeated matching reason succeeds, while a different reason conflicts.

This area owns decisions such as:

- create a stable Price Agreement identity
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
- At most one Agreement Version across all Price Agreements is Active within a Trading Relationship at a time.
- Having no Active Agreement Version is a valid explicit Customer state.
- Any published Agreement Version, including a withdrawn one, may be accepted, activated, or reactivated regardless of `ValidFrom` / `ValidTo`; withdrawal and out-of-validity use produce warnings rather than hard blocks.
- Publishing or withdrawing an Agreement Version never changes Customer Active state automatically.
- Withdrawal requires a reason and cannot be reversed for that same immutable version.

The transactional boundary around `CustomerAgreementState` exists because acceptance and the single-Active-version invariant must change consistently for one Supplier–Customer Trading Relationship, including switches between different Price Agreements. Supplier publication/withdrawal remains independent and does not participate in that transaction.

## Business facts

Agreement activation and deactivation are durable Customer decisions. Their history is preserved so PriceSync can distinguish current state from versions that were actually active at a previous point in time.

Activation and deactivation take effect when PriceSync performs the decision. Customers cannot backdate or schedule those decisions. Their system-assigned decision times describe actual Customer selection changes, independently of Supplier-defined version validity. A successful repeated request that changes nothing creates no additional decision history.

A current Active version may be represented as derived/current state for efficient access, but that does not erase the underlying activation history.

Publication and withdrawal are durable Supplier facts. Historical recommendation is reconstructed from facts that existed at the evaluated point in time rather than from today's state.

## References

The area references Organizations and the applicable Trading Relationship by identity. It does not own those concepts. Customer Pricing consumes exact immutable Agreement Version references but does not mutate Agreements state.
