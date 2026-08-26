# PriceSync Context

This file contains the shared business language established for PriceSync. Terms are added only after they have been discovered, discussed, and explicitly defined.

## Language

**Organization**:
A business party participating in PriceSync. An Organization may act as a Supplier in one relationship and a Customer in another.
_Avoid_: Supplier account, customer account

**Legal Organization Identity**:
The combination of registration country and national organization number that identifies a legal Organization in PriceSync. A VAT ID may be recorded as a supporting identifier but does not replace the legal identity.
_Avoid_: Organization name, display name

**Supplier**:
The role of an Organization that provides prices to another Organization within a specific trading relationship. It is a contextual role, not a permanent organization type.

**Customer**:
The role of an Organization that consumes prices from another Organization within a specific trading relationship. It is a contextual role, not a permanent organization type.

**Trading Relationship**:
A mutually accepted, directed connection in which one Organization acts as Supplier and another as Customer for price distribution. Acceptance authorizes the connection, not individual prices or updates.
_Avoid_: Price acceptance, contract

**External Supplier Reference**:
A customer's private identification of the actual supplier behind Customer-Managed External Prices when that supplier is not yet represented by a connected Organization in PriceSync. It may later be linked to the supplier's Organization.
_Avoid_: Self-supplier, placeholder organization

**Agreed Price**:
A commercial price made available through PriceSync by a supplier and assumed by PriceSync to have been agreed with the customer outside the platform. PriceSync neither negotiates the price nor verifies or records its acceptance.
_Avoid_: Approved price, PriceSync-negotiated price

**Customer Price**:
The resolved monetary amount offered by a supplier to a specific customer for an article. It may originate as a directly stated net price or be calculated from a list price and a customer discount.
_Avoid_: Raw price, unresolved price

**Price Basis**:
The amount, Price Quantity, and Unit of Measure that together state what a Customer Price is charged for within the currency of its Customer Price List. For example, 1,200 per 100 metres.
_Avoid_: Unit price

**Price Quantity**:
The number of units covered by the stated price amount, such as 1, 10, or 100.

**Unit of Measure**:
The measurable unit to which the Price Quantity applies, such as piece, metre, kilogram, or litre.

**Net Price**:
A Customer Price stated directly by the supplier rather than calculated by PriceSync from a list price and discount.

**List Price**:
A supplier's general price for an article that can serve as the basis for calculating a Customer Price.

**Customer Discount**:
A customer-specific reduction applied to a List Price to calculate a Customer Price.

**Supplier-Published Prices**:
Customer Prices made available directly by the supplier through PriceSync and treated as the supplier's authoritative price information.

**Customer-Managed External Prices**:
A customer's private record of prices received outside PriceSync. They are not represented as an authoritative publication by the supplier.
_Avoid_: Supplier-published prices

**Price Source**:
The selected origin from which a customer synchronizes prices for a supplier: Supplier-Published Prices or Customer-Managed External Prices.

**Price Snapshot**:
A complete, versioned, and reproducible view of a Customer Price List at the time of publication. A Price Snapshot is published atomically and is never a partial update.
_Avoid_: Partial price list, price patch

**Customer Price List**:
An ongoing, single-currency price publication scope within a supplier-customer relationship, optionally associated with a market. Successive Price Snapshots version the list.

**Market**:
The destination market or commercial context in which a Customer Price List is intended to be used. It is independent of the Customer Organization's registration country, and its relevance may differ by Supplier.
_Avoid_: Customer country

**Publication Time**:
The time at which a Price Snapshot was published through PriceSync. It records when PriceSync knew the published price information, not when each price becomes commercially valid.

**Price Correction**:
A new Price Snapshot that corrects previously published price information without rewriting publication history. Its corrected Price Validity may begin before its own Publication Time.
_Avoid_: Edited snapshot, overwritten snapshot

**Price Validity**:
The period from a required Valid From to an optional Valid To during which a Customer Price applies commercially. Prices within the same Price Snapshot may have different validity periods.

**Article Reference**:
The versioned, minimal article information captured in a Price Snapshot to associate a price with an article: the supplier and the supplier's Article ID. It may also carry external identifiers and a short Article Name but is not a product record.
_Avoid_: Product, catalog item

**Supplier Article ID**:
The supplier-assigned identifier for an article. It is unique only within that supplier and is the required article identifier in PriceSync.

**Article Name**:
A short human-readable designation stored with an Article Reference. It is display information, not a full product description.
_Avoid_: Product description

**Customer Article ID**:
An optional customer-assigned identifier mapped to a Price List Entry. The same Article Reference may therefore have different Customer Article IDs in different Customer Price Lists.

**Price List Entry**:
The occurrence of an Article Reference within a specific Customer Price List. It carries that list's price information and may have a list-specific Customer Article ID without changing the underlying article identity.

## Adding a term

- Use the agreed canonical term as the heading.
- Give it a short, business-focused definition.
- Do not include implementation details, technical design, or unresolved assumptions.
- List alternative terms under `_Avoid_` when the team has agreed not to use them.
- Add deeper rules and relationships to the relevant document under [Domain](./Domain/readme.md) after the concept or module has been defined.
