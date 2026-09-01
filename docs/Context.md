# PriceSync Context

This file contains the shared business language established for PriceSync. Terms are added only after they have been discovered, discussed, and explicitly defined.

## Language

**Organization**:
A business party participating in PriceSync. An Organization may act as a Supplier in one relationship and a Customer in another.
_Avoid_: Supplier account, customer account

**Legal Organization Identity**:
The combination of registration country and national organization number that identifies a legal Organization in PriceSync. A VAT ID may be recorded as a supporting identifier but does not replace the legal identity.
_Avoid_: Organization name, display name

**Organization Membership**:
A person's association with one Organization, carrying the roles that authorize the person to act for that Organization. The same person may have different memberships and roles in different Organizations.

**Organization Role**:
A named set of permissions assigned through an Organization Membership. Roles are scoped to one Organization and never grant global authority over another Organization.
_Avoid_: Global user role

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
The resolved monetary amount, excluding taxes but including mandatory article-specific surcharges, offered by a supplier to a specific customer for an article. It may originate as a directly stated net price or be calculated from a list price and a customer discount.
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
A customer-specific percentage reduction applied to a List Price to calculate a Customer Price. It may apply to an entire Customer Price List or to a Supplier Price Group; applicable discounts are alternatives and are never stacked.

**Supplier Price Group**:
A supplier-defined commercial grouping of articles used to assign pricing rules such as a Customer Discount. It is not a general product classification.
_Avoid_: Product category, product classification

**Rounding Policy**:
The decimal precision and rounding method used by a Customer Price List when resolving a Customer Price from a List Price and Customer Discount.

**Price Calculation Basis**:
The List Price, applicable Customer Discount, and Rounding Policy used to resolve a Customer Price. It exists only when PriceSync performs that calculation; a specific Net Price has no inferred calculation basis.

**Supplier-Published Prices**:
Customer Prices made available directly by the supplier through PriceSync and treated as the supplier's authoritative price information.

**Customer-Managed External Prices**:
A customer's private record of prices received outside PriceSync. They are not represented as an authoritative publication by the supplier.
_Avoid_: Supplier-published prices

**Price Source**:
The selected origin for one price-list context defined by Supplier, currency, and Market or commercial purpose: Supplier-Published Prices or Customer-Managed External Prices.

**Price Snapshot**:
A complete, versioned, and reproducible view of a Customer Price List at the time of publication. A Price Snapshot is published atomically and is never a partial update.
_Avoid_: Partial price list, price patch

**Price Draft**:
Mutable price-list work awaiting successful validation and publication. It is not visible to consumers and is not part of a Customer Price List's published history.
_Avoid_: Price Snapshot

**Import Profile**:
An Organization-private, reusable mapping from an external Excel or CSV layout to PriceSync's canonical price fields. It is used for file imports, not for canonical API payloads.

**Source File**:
The original Excel or CSV file supplied for a manual import. It is private audit evidence owned by the uploading Organization and is not distributed with the canonical Price Snapshot.

**Source Retention Policy**:
An Organization's rule for how long its Source Files are retained. Expiry removes the file but not the canonical Price Snapshot or its provenance metadata.

**Validation Error**:
A violation of a required domain rule that prevents a Price Draft from being published.

**Validation Warning**:
A suspicious but domain-valid condition that requires explicit acknowledgement or an applicable automation policy before publication.

**Publication Policy**:
The Supplier's rules for acknowledging Validation Warnings before a Price Draft may be published.

**Consumption Policy**:
The Customer's rules for notifying, pausing, or allowing downstream processing of a published Price Snapshot. It does not affect the snapshot's publication or commercial validity.

**Transfer Status**:
The observable state of a Price Snapshot's transfer to a Customer channel, such as available, retrieved, or delivered. It does not prove that the receiving system applied the prices.

**Application Status**:
The reported outcome of applying a Price Snapshot in a consuming system: applied, failed, or not reported. A manual confirmation is distinguished from a system-reported outcome.

**Customer Price List**:
An ongoing, single-currency price publication scope for one Customer and Supplier source, optionally associated with a market or commercial purpose. The source may be a Trading Relationship or an External Supplier Reference; successive Price Snapshots version the list.

**Archived Price List**:
A Customer Price List closed to future publication while its snapshots and audit history remain available. Archiving does not itself change Price Validity.
_Avoid_: Deleted price list

**Market**:
The destination market or commercial context in which a Customer Price List is intended to be used. It is independent of the Customer Organization's registration country, and its relevance may differ by Supplier.
_Avoid_: Customer country

**Publication Time**:
The time at which a Price Snapshot was published through PriceSync. It records when PriceSync knew the published price information, not when each price becomes commercially valid.

**Price Correction**:
A new Price Snapshot that corrects previously published price information without rewriting publication history. Its corrected Price Validity may begin before its own Publication Time.
_Avoid_: Edited snapshot, overwritten snapshot

**Price Change Set**:
The immutable canonical difference between a Price Snapshot and the preceding snapshot of the same Customer Price List, classifying entries as added, changed, or removed.

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

**Price List Entry**:
The unique occurrence of an Article Reference within a specific Customer Price List. One Supplier Article ID has one Price Basis and at most one commercially valid Customer Price at a time in that list.

## Adding a term

- Use the agreed canonical term as the heading.
- Give it a short, business-focused definition.
- Do not include implementation details, technical design, or unresolved assumptions.
- List alternative terms under `_Avoid_` when the team has agreed not to use them.
- Add deeper rules and relationships to the relevant document under [Domain](./Domain/readme.md) after the concept or module has been defined.
