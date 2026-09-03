# Price Publication

## Purpose

Own the lifecycle from a logical Price List through mutable draft and validation to an immutable published Price List Version.

The area covers General Price Lists, Customer-Specific Net Price Lists, and Customer-Managed External Net Price Lists.

## Aggregate model

### PriceList

`PriceList` is the stable logical identity and scope across publications. Its type, currency, market, and applicable Customer scope are fixed after first publication, while display metadata such as Name may remain mutable according to the domain rules.

### PriceDraft

`PriceDraft` is a separate mutable aggregate referencing `PriceListId`. Draft mutation and validation do not require loading the long-lived Price List or published history as one aggregate.

At most one active Price Draft may exist for a logical Price List. The mechanism enforcing that uniqueness is an implementation concern; the domain invariant is not.

### PriceListVersion

Each `PriceListVersion` is a separate immutable, complete canonical publication referencing `PriceListId`. Published versions are not modeled as a growing child collection of `PriceList`.

### CustomerPriceListState

Customer use of a published Price List is separate from Supplier publication. For each Customer and logical Price List, `CustomerPriceListState` records which published Price List Version is currently Active, if any, together with immutable activation/deactivation history.

There is no separate Price List acceptance step. Activating a published Price List Version is the Customer decision to use that version.

Any published Price List Version may be activated or reactivated. Publishing a newer version does not automatically replace the Customer's Active version. Having no Active Price List Version is a valid explicit Customer state.

The transactional boundary around `CustomerPriceListState` exists to keep the single-Active-version invariant consistent for one Customer and logical Price List without coupling Customer choice to Supplier publication.

### ExternalSupplierReference

`ExternalSupplierReference` belongs to Price Publication and identifies the real external source for Customer-Managed External Net Price Lists. It does not create a Trading Relationship and does not make the Customer Organization a Supplier to itself.

## Core invariants

- At most one active Price Draft exists per logical Price List.
- Publication is atomic: a complete valid draft becomes one complete immutable Price List Version.
- Canonically identical content does not create another version.
- A validation error blocks the entire publication.
- Price Publication validates the Price List intrinsically as a Price List. It does not validate every Customer/Agreement projection combination before publication.
- A General Price List may exist and be published independently of any Customer or Trading Relationship.
- A Customer-Specific Net Price List is scoped to exactly one Customer and contains already resolved Net Prices with no additional Customer Discount.
- A Customer-Managed External Net Price List follows the same publication/versioning flow as a Customer-Specific Net Price List, but is administered by the consuming Customer Organization for its own use and identifies the actual source through an `ExternalSupplierReference`.
- Customer-managed external prices do not require a Trading Relationship or Price Agreement.
- General, Customer-Specific Net, and Customer-Managed External Net Price Lists share the same publication/versioning concepts while differing in source authority, scope, and pricing semantics.
- At most one published Price List Version is Active for a Customer and logical Price List at a time.
- No Active Price List Version is also a valid Customer state.
- Supplier publication never changes Customer Active state automatically.

Publication and Customer activation are separate facts. A newly published Price List Version becomes available for Customer selection but is not used until explicitly activated.

## Context relationship

Compatibility between a published Price List Version and a specific Price Agreement Version belongs to Customer Pricing. A Price List Version may therefore be valid and publishable even if a later Customer projection from that source and a particular agreement is invalid.
