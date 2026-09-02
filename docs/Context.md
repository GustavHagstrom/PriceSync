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
A directed Supplier–Customer business relationship established outside PriceSync and represented in the platform to scope price access. PriceSync does not create, negotiate, or accept the relationship and it does not contain the parties' commercial price terms.
_Avoid_: Price acceptance, contract

**Price Agreement**:
The Supplier-published commercial price terms that apply between a Supplier and Customer within a Trading Relationship. The Supplier controls the terms; the Customer may report discrepancies but cannot change them.
_Avoid_: Trading Relationship, connection

**Price Agreement Version**:
An immutable, published version of a Price Agreement used together with Price List Versions to calculate a Customer Price Projection.

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
The amount, Price Quantity, and Unit of Measure that together state what a Customer Price is charged for within its source currency. For example, 1,200 per 100 metres.
_Avoid_: Unit price

**Price Quantity**:
The number of units covered by the stated price amount, such as 1, 10, or 100.

**Unit of Measure**:
The measurable unit to which the Price Quantity applies, such as piece, metre, kilogram, or litre.

**Net Price**:
A Customer Price stated directly by the supplier rather than calculated by PriceSync from a list price and discount.

**List Price**:
A supplier's general price for an article that can serve as the basis for calculating a Customer Price.

**General Price List**:
A Supplier-owned, reusable collection of List Prices that exists independently of Customers and Trading Relationships. A Price Agreement supplies the Customer-specific discounts needed to derive Customer Prices from it.
_Avoid_: Customer Price List, net price list

**Customer-Specific Net Price List**:
A Supplier-published list of Net Prices prepared for one Customer. Its prices are already resolved and receive no additional Customer Discount.
_Avoid_: General Price List, discounted price list

**Customer Discount**:
A customer-specific percentage reduction in a Price Agreement applied to a List Price. It may apply generally to the referenced General Price List or specifically to a Supplier Price Group; applicable discounts are alternatives and are never stacked.

**Supplier Price Group**:
A supplier-defined commercial grouping of articles used to assign pricing rules such as a Customer Discount. It is not a general product classification.
_Avoid_: Product category, product classification

**Rounding Policy**:
The decimal precision and rounding method used when a Price Agreement resolves a Customer Price from a List Price and Customer Discount.

**Price Calculation Basis**:
The List Price, applicable Customer Discount, and Rounding Policy used to resolve a Customer Price. It exists only when PriceSync performs that calculation; a specific Net Price has no inferred calculation basis.

**Supplier-Published Prices**:
Customer Prices made available directly by the supplier through PriceSync and treated as the supplier's authoritative price information.

**Customer-Managed External Prices**:
A customer's private record of prices received outside PriceSync. They are not represented as an authoritative publication by the supplier.
_Avoid_: Supplier-published prices

**Price Source**:
The selected origin for one price-list context defined by Supplier, currency, and Market or commercial purpose: Supplier-Published Prices or Customer-Managed External Prices.

**Price List Version**:
An immutable, complete version of one logical General Price List or Customer-Specific Net Price List. It is published atomically and remains permanently available as a calculation source.
_Avoid_: Partial price list, price patch, Customer Price Snapshot

**Customer Price Projection**:
The complete Customer Price view calculated from exact Price List Versions and a Price Agreement Version. It may be materialized for performance but is rebuildable cache rather than an audit record.
_Avoid_: Customer Price Snapshot, published customer list

**Price Delivery**:
The permanent, immutable record of the exact Customer Price Projection a Customer authorized for download or import. It retains its source versions, calculation-rule version, canonical result, content hash, exported representation, and delivery history. A later Price Delivery may supersede it as the delivery PriceSync actively tries to synchronize without rewriting or deleting the earlier record.
_Avoid_: Cache, mutable export

**Price Draft**:
Mutable price-list work awaiting successful validation and publication as a Price List Version. It is not visible to consumers and is not part of published history.
_Avoid_: Price List Version

**Import Profile**:
An Organization-private, reusable onboarding mapping from an external Excel or CSV layout, field values, and identifiers to PriceSync's canonical price model. It is used for file imports, not for canonical API payloads.

**Export Profile**:
A Customer-private, reusable transformation from a canonical Customer Price Projection to the file or API layout required by a Customer system. It changes delivery format without changing the canonical prices.

**Source File**:
The original Excel or CSV file supplied for a manual import. It is private audit evidence owned by the uploading Organization and is not distributed with a Price Delivery.

**Source Retention Policy**:
An Organization's rule for how long its Source Files are retained. Expiry removes the file but not published Price List Versions, Price Deliveries, or their provenance metadata.

**Validation Error**:
A violation of a required domain rule that prevents a Price Draft from being published.

**Validation Warning**:
A suspicious but domain-valid condition that requires explicit acknowledgement or an applicable automation policy before publication.

**Publication Policy**:
The Supplier's rules for acknowledging Validation Warnings before a Price Draft may be published.

**Consumption Policy**:
The Customer's rules for notifying, reviewing, approving, or automatically delivering a changed Customer Price Projection. Manual Import Approval is the default; automatic delivery requires an explicit Customer choice.

**Import Approval**:
The Customer's atomic authorization to create and transfer a Price Delivery from the exact Customer Price Projection being approved. Approval is bound to that projection, cannot select individual entries, and is an internal processing decision rather than acceptance of commercial prices. Repeated or concurrent approval of the same projection for the same Customer channel does not create duplicate Price Deliveries.
_Avoid_: Price acceptance, publication approval

**Transfer Status**:
The observable state and history of a Price Delivery's transfer to a Customer channel, such as prepared, attempted, retrieved, or delivered. Transfer attempts do not alter the immutable Price Delivery and do not prove that the receiving system applied the prices.

**Application Status**:
The reported outcome of applying a Price Delivery in a consuming system: applied, failed, or not reported. A manual confirmation is distinguished from a system-reported outcome.

**Synchronization Status**:
The relationship between the current Customer Price Projection and the most recent Price Delivery reported as applied in a Customer system. A channel is Out of Sync when those canonical results differ.
_Avoid_: Price validity, commercial acceptance

**Archived Price List**:
A logical Price List closed to future publication while its Price List Versions and related Price Deliveries remain available. Archiving does not itself change Price Validity.
_Avoid_: Deleted price list

**Market**:
The destination market or commercial context in which a logical Price List is intended to be used. It is independent of the Customer Organization's registration country, and its relevance may differ by Supplier.
_Avoid_: Customer country

**Publication Time**:
The time at which a Price List Version or Price Agreement Version was published through PriceSync. It records when PriceSync knew the source information, not when each resulting price becomes commercially valid.

**Price Correction**:
A new Price List Version or Price Agreement Version that corrects published source information without rewriting publication history. Its corrected Price Validity may begin before its own Publication Time.
_Avoid_: Edited snapshot, overwritten snapshot

**Price Change Set**:
The canonical difference between the current Customer Price Projection and the Customer's most recent Price Delivery, classifying entries as added, changed, or removed. The comparison is captured immutably when a new Price Delivery is created.

**Price Validity**:
The period from a required Valid From to an optional Valid To during which a Customer Price applies commercially. Prices within the same Price List Version or Customer Price Projection may have different validity periods.

**Article Reference**:
The versioned, minimal article information captured in a Price List Version to associate a price with an entry: the Supplier, logical Price List, and Supplier Article ID. It may also carry external identifiers and a short Article Name but is not a product record.
_Avoid_: Product, catalog item

**Supplier Article ID**:
The Supplier-assigned identifier for an article within one logical Price List. The same identifier may represent separate Price List Entries in different lists.

**PriceSync Article ID**:
The short, opaque, globally unique, and immutable identifier PriceSync assigns to the combination of Supplier, logical Price List, and Supplier Article ID. It remains stable through successive versions of that list and may be used by Customer systems without manual article-number mapping.

**Article Name**:
A short human-readable designation stored with an Article Reference. It is display information, not a full product description.
_Avoid_: Product description

**Price List Entry**:
The uniquely identified occurrence of an Article Reference within one logical Price List. Its identity persists through successive list versions.

## Adding a term

- Use the agreed canonical term as the heading.
- Give it a short, business-focused definition.
- Do not include implementation details, technical design, or unresolved assumptions.
- List alternative terms under `_Avoid_` when the team has agreed not to use them.
- Add deeper rules and relationships to the relevant document under [Domain](./Domain/readme.md) after the concept or module has been defined.
