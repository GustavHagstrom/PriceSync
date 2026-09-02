# Price Publication

## Purpose

Own the lifecycle from a logical Price List through mutable draft and validation to an immutable published Price List Version.

The area covers both General Price Lists and Customer-Specific Net Price Lists.

## Aggregate model

### PriceList

`PriceList` is the stable logical identity and scope across publications. Its type, currency, market, and applicable Customer scope are fixed after first publication, while display metadata such as Name may remain mutable according to the domain rules.

### PriceDraft

`PriceDraft` is a separate mutable aggregate referencing `PriceListId`. Draft mutation and validation do not require loading the long-lived Price List or published history as one aggregate.

At most one active Price Draft may exist for a logical Price List. The mechanism enforcing that uniqueness is an implementation concern; the domain invariant is not.

### PriceListVersion

Each `PriceListVersion` is a separate immutable, complete canonical publication referencing `PriceListId`. Published versions are not modeled as a growing child collection of `PriceList`.

## Core invariants

- At most one active Price Draft exists per logical Price List.
- Publication is atomic: a complete valid draft becomes one complete immutable Price List Version.
- Canonically identical content does not create another version.
- A validation error blocks the entire publication.
- A General Price List may exist and be published independently of any Customer or Trading Relationship.
- A Customer-Specific Net Price List is scoped to exactly one Customer and contains already resolved Net Prices with no additional Customer Discount.
- General and Customer-Specific Net Price Lists share the same publication/versioning concepts while differing in scope and pricing semantics.

## Cross-context collaboration still under discovery

General Price List publication currently has a domain rule requiring compatibility with every Active Price Agreement Version using it. How Price Publication obtains and validates that agreement state without creating an oversized transaction boundary remains intentionally unresolved in Issue #7.
