# Price Publication

## Purpose

Own the lifecycle from a logical Price List through mutable draft and validation to an immutable published Price List Version.

The area covers General Price Lists, Customer-Specific Net Price Lists, and Customer-Managed External Net Price Lists.

## Aggregate model

### PriceList

`PriceList` is the aggregate root for the stable logical identity and scope across publications. Its type, currency, market, and applicable Customer scope are fixed after first publication, while display metadata such as Name may remain mutable according to the domain rules.

Its transactional boundary exists because stable list scope and one publication decision must remain internally consistent without loading all drafts, published versions, or Customer activation state into the same aggregate.

### PriceDraft

`PriceDraft` is a separate mutable aggregate root referencing `PriceListId`. Draft mutation and validation do not require loading the long-lived Price List or published history as one aggregate.

At most one active Price Draft may exist for a logical Price List. The mechanism enforcing that uniqueness is an implementation concern; the domain invariant is not.

### PriceListVersion

Each `PriceListVersion` is a separate immutable, complete canonical publication referencing `PriceListId`. Published versions are not modeled as a growing child collection of `PriceList`.

Each version has:

- `ValidFrom` as a required business date
- optional `ValidTo`, where null means valid until further notice
- `PublishedAt` as the publication timestamp

Validity applies to the complete immutable Price List Version, not to individual price rows. Row-level validity is not part of the established model. When `ValidTo` exists it must be on or after `ValidFrom`.

`ValidFrom` / `ValidTo` represent source intent and drive recommendations/warnings rather than hard Customer-use constraints. The current calendar date for recommendation evaluation is UTC.

Overlapping validity intervals are allowed. Among non-withdrawn published versions whose validity interval contains the evaluated UTC date, the recommended version is the one with the latest `ValidFrom`; if several share that `ValidFrom`, the one with the latest `PublishedAt` wins. If no published non-withdrawn version covers the evaluated date, there is no recommended Price List Version.

Recommendation can also be evaluated historically as-of a point in time. Later publication or withdrawal does not rewrite what was recommended before those facts existed.

A published Price List Version is immutable including its validity dates. Correcting validity or prices therefore requires a new version.

A source authority may permanently withdraw a published Price List Version with a required human-readable `WithdrawalReason`. Withdrawal is a separate immutable source fact; it does not mutate the published version. A withdrawn version is excluded from current recommendations and should produce a strong warning when used, but it remains available for Customer activation/reactivation, projection, and retrieval. Withdrawal is permanent; restoring the same version to Published is not supported.

For Supplier-managed Price Lists, the source authority is the Supplier. For Customer-Managed External Net Price Lists, the Customer administers the publication on behalf of an external source; `ValidFrom` / `ValidTo` are still interpreted as the external source's stated validity rather than the Customer's activation decision. PriceSync does not attempt to verify that intent beyond treating the entered values according to this domain meaning.

Publication/withdrawal commands operate on source-owned publication state and produce immutable version/withdrawal facts. They do not participate in Customer activation transactions.

### CustomerPriceListState

`CustomerPriceListState` is a separate Customer-owned aggregate root. For each Customer and logical Price List, it records which published Price List Version is currently Active, if any, together with immutable activation/deactivation history.

There is no separate Price List acceptance step. Activating a published Price List Version is the Customer decision to use that version.

Any published Price List Version, including a withdrawn one, may be activated or reactivated. Publishing or withdrawing a version does not automatically replace or deactivate the Customer's Active version. Having no Active Price List Version is a valid explicit Customer state.

The transactional boundary around `CustomerPriceListState` exists to keep the single-Active-version invariant consistent for one Customer and logical Price List without coupling Customer choice to source publication.

### ExternalSupplierReference

`ExternalSupplierReference` belongs to Price Publication and identifies the real external source for Customer-Managed External Net Price Lists. It does not create a Trading Relationship and does not make the Customer Organization a Supplier to itself.

## Commands / decisions

This area owns decisions such as:

- create/update a logical Price List within its allowed lifecycle
- create/replace/mutate a Price Draft
- validate and atomically publish a complete Price List Version
- withdraw a published version with reason
- activate/reactivate/deactivate a Customer's Price List Version selection
- maintain the Customer-private External Supplier Reference for external lists

## Core invariants

- At most one active Price Draft exists per logical Price List.
- Publication is atomic: a complete valid draft becomes one complete immutable Price List Version.
- Canonically identical content does not create another version.
- A validation error blocks the entire publication.
- Price Publication validates the Price List intrinsically as a Price List. It does not validate every Customer/Agreement projection combination before publication.
- `ValidFrom` is required for every Price List Version.
- `ValidTo` is optional and, when present, must be on or after `ValidFrom`.
- Validity applies to the complete Price List Version.
- Overlapping validity intervals are allowed.
- A General Price List may exist and be published independently of any Customer or Trading Relationship.
- A Customer-Specific Net Price List is scoped to exactly one Customer and contains already resolved Net Prices with no additional Customer Discount.
- A Customer-Managed External Net Price List follows the same publication/versioning and validity semantics as a Customer-Specific Net Price List, but is administered by the consuming Customer Organization for its own use and identifies the actual source through an `ExternalSupplierReference`.
- Customer-managed external prices do not require a Trading Relationship or Price Agreement.
- General, Customer-Specific Net, and Customer-Managed External Net Price Lists share the same publication/versioning concepts while differing in source authority, scope, and pricing semantics.
- At most one published Price List Version is Active for a Customer and logical Price List at a time.
- No Active Price List Version is also a valid Customer state.
- Any published version, including a withdrawn version, may be activated/reactivated; withdrawal and out-of-validity use produce warnings rather than hard blocks.
- Publication or withdrawal never changes Customer Active state automatically.
- Withdrawal requires a reason and cannot be reversed for that same immutable version.

Publication/withdrawal and Customer activation are separate facts. A newly published Price List Version becomes available for Customer selection but is not used until explicitly activated.

## Context relationship

Compatibility between a published Price List Version and a specific Price Agreement Version belongs to Customer Pricing. A Price List Version may therefore be valid and publishable even if a later Customer projection from that source and a particular agreement is invalid.

Agreements, Trading Relationships, Organizations, and Integration Configuration are referenced by identity/configuration where needed; Price Publication does not own their mutable state.
