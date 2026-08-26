# PriceSync Domain

This document is the working model of the PriceSync domain. It collects discovered concepts, relationships, business rules, concrete scenarios, and unresolved questions before stable module or bounded-context boundaries have emerged.

The terms in [Context](./Context.md) form the concise canonical glossary. This document provides the deeper model behind those terms.

## Domain boundary

PriceSync makes prices that suppliers and customers have agreed outside the platform available for synchronization between their systems. PriceSync does not negotiate or approve those prices.

### External agreement assumption

PriceSync assumes that every Customer Price published by a supplier has already been agreed with the customer outside the platform. Publishing does not initiate an approval workflow, and the customer does not need to accept each price or price update in PriceSync.

PriceSync records the supplier's publication but does not verify or prove bilateral acceptance. There is therefore no separate `Accepted` state in the initial domain model.

## Organizations and roles

An Organization is a business party in PriceSync. Supplier and Customer are contextual roles between Organizations rather than permanent organization types. The same Organization may supply one party while buying from another.

A customer importing Customer-Managed External Prices does not become a Supplier to itself. The customer administers the import, while an External Supplier Reference identifies the actual source of the prices. The reference is private to that customer and may later be linked to the supplier's Organization if the supplier joins PriceSync.

### Organization identity

An Organization's legal identity is the combination of registration country and national organization number. Organization names are display information and may change. VAT IDs and other identifiers may support matching but do not replace the Legal Organization Identity.

An External Supplier Reference may be linked automatically to an Organization only when its legal identifier matches unambiguously. Ambiguous matches require an explicit choice.

## Trading relationships

A Trading Relationship is a directed connection between a Supplier Organization and a Customer Organization. Either party may request the connection, but both Organizations must accept it before the relationship becomes active and supplier-published prices may be distributed through it.

Relationship acceptance authorizes ongoing price distribution. It does not accept an individual Customer Price or Price Snapshot; future publications are synchronized without per-update approval.

Customer-Managed External Prices do not require an active Trading Relationship because they remain private to the Customer that registered them.

### Ending a relationship

Either party may end a Trading Relationship. Ending it stops future distribution but does not delete previously received Price Snapshots or audit history from the Customer's view.

Previously received prices remain accessible and are clearly marked as originating from an ended, no-longer-synchronized relationship. Ending the relationship does not itself change any Customer Price's `Valid To`, because technical disconnection does not establish that the external commercial agreement has ended.

## Price representation

A supplier may provide a Customer Price in either of two forms:

- a Net Price stated directly for a specific customer and article
- a List Price combined with a Customer Discount

PriceSync resolves both forms to a Customer Price. Customer-facing integrations receive the resolved monetary amount and do not need to interpret the supplier's discount calculation.

The calculation basis is retained so that the resolved Customer Price can be explained and audited.

### Price basis

Every Customer Price states an amount together with a Price Quantity and Unit of Measure. These three values form the Price Basis. Currency belongs to the Customer Price List containing the price.

For example, an amount of `1,200 / 100 metre` in a SEK Customer Price List means that 1,200 SEK covers 100 metres. Price Quantity is distinct from package size, minimum order quantity, or other commercial conditions.

## Article identity

A Customer Price applies to an Article Reference identified by the combination of supplier and Supplier Article ID. A Supplier Article ID is required and is unique only within its supplier's namespace.

An Article Reference may include:

- an Article Name for concise human-readable display
- a GTIN or other external identifier

The Supplier Article ID remains the PriceSync identity. Optional identifiers support matching and integration but do not replace it. Currency and Market do not change article identity; the same Article Reference may appear in multiple Customer Price Lists.

An Article Reference appears in a Customer Price List as a Price List Entry. A Customer Article ID maps to that entry rather than directly to the Article Reference. The same supplier article may therefore have different Customer Article IDs in Swedish and German price lists while remaining one supplier article in PriceSync.

PriceSync does not own a product catalog. The Article Name is deliberately limited to a short designation; full descriptions, classifications, images, documents, and other product content remain outside the domain boundary.

The complete Article Reference is versioned as part of each Price Snapshot. Later changes to an Article Name or optional identifier do not rewrite how the article appeared in an earlier snapshot.

## Price snapshots

One supplier-customer relationship may contain multiple Customer Price Lists, for example for different currencies or Markets. Each Customer Price List has exactly one currency and may have a Market. Successive Price Snapshots version that list.

Market does not describe where the Customer Organization is registered. A Swedish Customer may simultaneously use a Supplier's Swedish list for domestic projects and German list for exports to Germany. For the same export project, another Supplier's Swedish list may still be the applicable list. Price-list applicability is therefore selected per Supplier rather than globally for the Customer or destination market.

Every publication produces a complete, versioned Price Snapshot of one Customer Price List. Publication is atomic: consumers see either the previous complete snapshot or the new complete snapshot, never an intermediate state.

External integrations may submit changes incrementally, but those changes must be assembled and validated before they can be published as a new complete Price Snapshot. A partial update is not itself a publication.

A Price Snapshot is a logical, reproducible domain view. It may be reconstructed from versioned components such as a shared List Price, Customer Discounts, and customer-specific Net Prices. The domain model does not require every resolved Customer Price to be physically duplicated or materialized for every customer.

### Publication and validity

The Publication Time of a Price Snapshot and the Price Validity of its Customer Prices are separate facts.

- Publication Time records when the snapshot became available through PriceSync.
- Price Validity records when an individual Customer Price applies commercially.

Customer Prices within the same Price Snapshot may begin to apply at different times. A snapshot may therefore contain both current and future prices.

Each Customer Price has a required `Valid From` and may have a `Valid To`. An explicit end allows a price to expire without being replaced.

When a new Customer Price replaces an open-ended price for the same article and price conditions, the previous price ceases to apply when the new one begins. Prices with identical conditions must not have overlapping Price Validity.

Because every Price Snapshot is complete, omitting an article from a newly published snapshot ends its previously open Customer Price at the new snapshot's Publication Time. If a price should end at a future time, the article remains in the snapshot with an explicit `Valid To`. Earlier snapshots retain the historical price and Article Reference.

### Corrections

A published Price Snapshot is immutable. Incorrect information is corrected by publishing a new Price Snapshot rather than editing or deleting publication history.

A Price Correction may assign corrected Price Validity retroactively. Its Publication Time still records when PriceSync learned about the correction, so consumers can distinguish what was commercially valid from what PriceSync knew at any given time. The incorrect snapshot remains available in the audit history.

## Customer-managed external prices

A customer may gain value from registering price lists received outside PriceSync, even when the supplier does not participate. This would let the customer collect prices in one place and use the same downstream integrations for both connected and unconnected suppliers.

A customer-managed external price must remain distinguishable from a supplier publication:

- its source and registering customer must be explicit
- it is private to the customer unless a later rule says otherwise
- PriceSync cannot present it as an authoritative statement made by the supplier

### Selecting the price source

A customer selects one Price Source for each supplier. Connecting to a supplier does not automatically replace Customer-Managed External Prices with Supplier-Published Prices.

When Supplier-Published Prices become available, the customer may make a one-time source change for that supplier. Once selected, future supplier publications are synchronized without requiring acceptance of each update. The previous customer-managed price information is retained as history rather than remaining an active competing source.

## Unresolved questions

- What may a Customer Discount apply to: the whole customer relationship, a group of articles, an individual article, quantity, packaging, or some combination?
- How are competing discount rules and directly stated Net Prices prioritized?
- Which concepts change together and therefore belong in the same domain module?
- Which domain modules or bounded contexts exist within PriceSync?
