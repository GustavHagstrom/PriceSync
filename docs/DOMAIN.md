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

### Membership and authorization

A person acts for an Organization through an Organization Membership. Role-based access control is scoped to that membership, so the same person may have different roles in different Organizations.

Roles may authorize responsibilities such as organization administration, price publication, price consumption and integration management, or read-only access. An API integration also acts for exactly one Organization with explicitly assigned permissions. The domain model does not prescribe a particular identity or authentication provider.

The initial model provides a small set of fixed, combinable system roles: Organization Admin, Price Publisher, Integration Manager, and Viewer. Permissions remain distinct beneath those roles so that customer-defined roles may be added later without changing the authorization concepts. Custom roles are an initial non-goal until an enterprise need is demonstrated.

### Publication authority

A Price Publisher may validate and publish a Price Draft without approval from a second person. PriceSync records who imported the source, acknowledged each Validation Warning, and published the resulting Price Snapshot.

Mandatory separation between draft author and approver is an initial non-goal. It may later become an optional Supplier-side enterprise control and would not represent Customer acceptance of prices.

## Trading relationships

A Trading Relationship is a directed connection between a Supplier Organization and a Customer Organization. Either party may request the connection, but both Organizations must accept it before the relationship becomes active and supplier-published prices may be distributed through it.

Relationship acceptance authorizes ongoing price distribution. It does not accept an individual Customer Price or Price Snapshot; future publications are synchronized without per-update approval.

Customer-Managed External Prices do not require an active Trading Relationship because they remain private to the Customer that registered them.

### Ending a relationship

Either party may end a Trading Relationship. Ending it stops future distribution but does not delete previously received Price Snapshots or audit history from the Customer's view.

Previously received prices remain accessible and are clearly marked as originating from an ended, no-longer-synchronized relationship. Ending the relationship does not itself change any Customer Price's `Valid To`, because technical disconnection does not establish that the external commercial agreement has ended.

### Organization departure

If an Organization leaves PriceSync, future distribution stops and its private working artifacts such as Price Drafts, Import Profiles, and Source Files follow the applicable deletion and retention policies.

Canonical Price Snapshots already delivered to another Organization remain in that recipient's audit history and are marked as originating from an inactive source Organization. A Supplier cannot retroactively remove delivered business facts from a Customer's history, and a Customer's departure does not erase the Supplier's publication and delivery evidence. Personal actor information may be anonymized without removing the business events.

## Price representation

A supplier may provide a Customer Price in either of two forms:

- a Net Price stated directly for a specific customer and article
- a List Price combined with a Customer Discount

PriceSync resolves both forms to a Customer Price. Customer-facing integrations receive the resolved monetary amount and do not need to interpret the supplier's discount calculation.

The calculation basis is retained so that the resolved Customer Price can be explained and audited.

### Calculation visibility

A Customer always sees the resolved Customer Price in its own list. When PriceSync calculated the price from a List Price and Customer Discount, the Customer also sees that Price Calculation Basis, including the applicable discount and rounding.

A specific Net Price exposes no inferred discount or fictional calculation. Customers never see another Customer's prices, discounts, snapshots, or calculation basis. A shared List Price is exposed only through entries that belong to the receiving Customer's own price list, not as the Supplier's complete internal pricing material.

### Price calculation priority

A Customer Price may be resolved from three levels of pricing input, in descending priority:

1. a Net Price for the specific Price List Entry
2. a Customer Discount for the entry's Supplier Price Group applied to its List Price
3. a general Customer Discount for the Customer Price List applied to its List Price

A Supplier Price Group is commercial pricing metadata owned by the Supplier. It does not make PriceSync responsible for product classification.

Only one pricing input determines a Customer Price. A Supplier Price Group discount replaces the general Customer Discount rather than combining with it, and a specific Net Price replaces either discount calculation. More complex source arrangements must be normalized to a Net Price at the platform boundary.

A Customer Discount is always expressed as a percentage in the core domain. Fixed deductions, factors, and supplier-specific formulas are normalized at the platform boundary to either an unambiguous percentage or a resolved Net Price.

### Rounding

Each Customer Price List has an explicit Rounding Policy consisting of decimal precision and rounding method. All price calculations use decimal arithmetic.

Currency may provide a suggested default precision, but a list may require greater precision than the currency's smallest payment unit. The resolved and rounded Customer Price is retained together with its calculation basis so that the publication is deterministic and auditable.

### Taxes

Every Customer Price in the canonical domain excludes VAT and other transaction taxes. PriceSync neither stores article tax rates nor calculates taxes; the consuming system applies them using transaction-specific information.

An external format containing tax-inclusive amounts must be normalized before publication. If the tax-exclusive Customer Price cannot be derived safely, the import is rejected rather than published as a comparable canonical price.

### Surcharges and transaction costs

A mandatory surcharge that always applies to the priced article and unit is included in the resolved Customer Price. When an external source represents such a surcharge separately, the adapter normalizes it into a Net Price; the source breakdown may be retained for auditability but does not add another core pricing formula.

Order-, delivery-, and project-specific costs such as freight are outside the PriceSync price domain. Refundable amounts such as deposits are not part of the Customer Price and require a separate model if future evidence justifies supporting them.

### Price basis

Every Customer Price states an amount together with a Price Quantity and Unit of Measure. These three values form the Price Basis. Currency belongs to the Customer Price List containing the price.

For example, an amount of `1,200 / 100 metre` in a SEK Customer Price List means that 1,200 SEK covers 100 metres. Price Quantity is distinct from package size, minimum order quantity, or other commercial conditions.

### Quantity-based prices

The initial domain model allows one Customer Price per Price List Entry and validity period. Ordered quantity does not select between simultaneous price tiers for the same entry.

Price Quantity remains part of the Price Basis; it states what quantity the amount covers and is not a quantity-discount threshold. Quantity breaks are an explicit initial non-goal until evidence from real price lists demonstrates the need.

Within one Customer Price List, a Supplier Article ID has exactly one Price List Entry and one Price Basis. It may have successive Customer Prices with non-overlapping validity periods, but never multiple simultaneously valid prices. Different sellable forms such as piece and pallet must use different Supplier Article IDs rather than parallel entries for the same article number.

## Article identity

A Customer Price applies to an Article Reference identified by the combination of supplier and Supplier Article ID. A Supplier Article ID is required and is unique only within its supplier's namespace.

An Article Reference may include:

- an Article Name for concise human-readable display
- a GTIN or other external identifier

The Supplier Article ID remains the PriceSync identity. Optional identifiers support matching and integration but do not replace it. Currency and Market do not change article identity; the same Article Reference may appear in multiple Customer Price Lists.

An Article Reference appears in a Customer Price List as a Price List Entry. The same supplier article may therefore have separate Swedish and German Price List Entries while remaining one supplier article in PriceSync.

Customer systems own their internal article identifiers. Mapping a Supplier Article ID to a consuming system's identifier is integration configuration and does not belong to the core price domain. PriceSync may support such mappings in an import or export profile when a concrete external format requires them.

PriceSync does not own a product catalog. The Article Name is deliberately limited to a short designation; full descriptions, classifications, images, documents, and other product content remain outside the domain boundary.

The complete Article Reference is versioned as part of each Price Snapshot. Later changes to an Article Name or optional identifier do not rewrite how the article appeared in an earlier snapshot.

## Price snapshots

A Customer Price List uses the same price, snapshot, validation, history, and distribution semantics regardless of Price Source.

- A Supplier-published list belongs to an active Trading Relationship and is owned and published by the Supplier.
- A Customer-managed external list belongs privately to the Customer and identifies its source through an External Supplier Reference; it requires no Trading Relationship.

One Customer and Supplier source may have multiple Customer Price Lists, for example for different currencies or Markets. Each Customer Price List has exactly one currency and may have a Market. Successive Price Snapshots version that list.

Market does not describe where the Customer Organization is registered. A Swedish Customer may simultaneously use a Supplier's Swedish list for domestic projects and German list for exports to Germany. For the same export project, another Supplier's Swedish list may still be the applicable list. Price-list applicability is therefore selected per Supplier rather than globally for the Customer or destination market.

Every publication produces a complete, versioned Price Snapshot of one Customer Price List. Publication is atomic: consumers see either the previous complete snapshot or the new complete snapshot, never an intermediate state.

External integrations may submit a complete draft in multiple transport batches, but those batches must be assembled and validated before they can be published as a new complete Price Snapshot. A batch or partial update is not itself a publication.

### Draft and validation

Imported or incrementally assembled price data remains a Price Draft until the complete list passes validation. A draft may be incomplete or invalid and is not visible to consuming systems.

Any validation error prevents publication of the entire draft. The previous Price Snapshot remains current, and validation reports all detected errors for correction. Valid rows are never published as a partial snapshot because omitted invalid rows would be indistinguishable from intentional price removals.

A Validation Warning identifies a suspicious but domain-valid condition, such as an unusually large price change or many omitted articles. It does not make the draft invalid. Manual publication requires explicit acknowledgement; automated publication follows a configured policy that either permits the warning or pauses the draft for review.

### Initial ingestion paths

The initial model has two ingestion paths:

1. Manual File Upload accepts Excel or CSV and uses an Import Profile to map the Organization's external layout into a Price Draft.
2. API Publication accepts JSON conforming to PriceSync's canonical contract and creates a Price Draft without an Import Profile.

Import Profiles are private to their owning Organization and reusable across compatible file uploads within that Organization. Both ingestion paths produce the same canonical draft and use the same validation and publication rules.

Automated file delivery such as SFTP is a future transport channel rather than an initial ingestion path. It may later reuse file Import Profiles without changing the price domain.

An API client may build one Price Draft through multiple batches and then explicitly declare the draft complete. PriceSync validates and publishes the assembled draft atomically. Standalone API patches that publish changes to individual entries are an initial non-goal.

API batch limits do not apply to Manual File Upload. A file is received and processed through its own import flow before producing a Price Draft.

### Manual file flow

A manual Excel or CSV upload never publishes directly. The complete file is received, associated with an existing or newly configured Import Profile, parsed, and validated asynchronously into a reviewable Price Draft.

Reusing a compatible Import Profile may skip repeated mapping work, but it never bypasses validation. A Price Publisher reviews the result, acknowledges any Validation Warnings, and explicitly publishes the draft atomically.

### Duplicate ingestion

PriceSync detects canonical content that has already been processed for the same Customer Price List. Retrying a file upload or API request is idempotent and does not automatically create another Price Draft, Price Snapshot, or set of notifications.

The user is informed of the existing result and may resume an incomplete attempt. Creating a new draft from identical price content but meaningfully different source metadata requires an explicit choice.

Deduplication is determined after normalization into the canonical Price Draft. Binary differences such as Excel formatting, column order, or file metadata do not create a new price version when the resulting canonical content is identical. A raw-file checksum may provide an earlier shortcut, while distinct source files remain separate audit evidence.

### Source-file privacy

The original Source File is retained as private audit evidence for the uploading Organization and linked to its import attempt and resulting Price Draft or Price Snapshot. Only authorized members of that Organization may access it.

Customers receive the canonical Price Snapshot and never automatically receive the Supplier's Source File. Source-file retention is governed independently from the immutable snapshot history, so expiry of a file does not erase the canonical publication record.

Each Organization has a configurable Source Retention Policy. When a Source File expires, PriceSync retains non-content provenance including its checksum, file name, upload time, actor, Import Profile, and links to resulting drafts or snapshots. Canonical Price Snapshots and their audit metadata have a separate long-term lifecycle.

The Supplier owns the Publication Policy that determines how warnings affect publication. The Customer independently owns a Consumption Policy that may notify, pause, or permit downstream import of an already published snapshot. A Consumption Policy never changes whether the snapshot is published or commercially valid and does not introduce per-update price acceptance.

### Transfer and application status

PriceSync tracks delivery separately for each Customer integration or download channel. Transfer Status records observable facts such as a snapshot becoming available, being retrieved as a file, or being accepted by a destination endpoint.

Application Status records whether a consuming system has reported that the snapshot was applied or failed. When a downloaded file provides no feedback, its Application Status is `Not Reported`; retrieval is never treated as proof of import.

A Customer may optionally mark a manual file import as applied for internal traceability. This is identified as a manual confirmation and is not presented as a system-verified result. No manual status entry is required.

### Change notifications

One snapshot publication produces one summarized notification event per Customer integration or configured notification channel, rather than one notification per changed article.

The summary includes counts of added, changed, and removed entries together with useful aggregate indicators such as the largest price change and range of effective dates. The full Price Change Set remains available for detailed processing. The Customer's Consumption Policy determines whether the summary is logged, notified, or used to pause downstream automation.

A Price Snapshot is a logical, reproducible domain view. It may be reconstructed from versioned components such as a shared List Price, Customer Discounts, and customer-specific Net Prices. The domain model does not require every resolved Customer Price to be physically duplicated or materialized for every customer.

### Archiving a price list

A Customer Price List may be archived but not deleted after publication. An Archived Price List accepts no new snapshots while its complete history remains available and clearly marked as no longer synchronized.

Archiving does not alter the commercial validity of its Customer Prices. If prices should cease to apply, the Supplier publishes the appropriate `Valid To` values before archiving the list.

An Archived Price List may be reactivated only when its business identity remains the same: Supplier, Customer, currency, and Market or commercial purpose. Reactivation does not make an old snapshot current again. The Supplier must publish a new complete Price Snapshot before synchronization resumes, after which version history continues in the same list.

If the business identity has changed, a new Customer Price List is created instead.

### Price-list identity

Before its first publication, a Customer Price List's Supplier source, Customer, currency, and Market or commercial purpose may be corrected. The Supplier source is either a Supplier Organization in a Trading Relationship or an External Supplier Reference. The first publication fixes those identity attributes permanently; later changes require a new list.

The display name remains editable because it does not change business meaning. A replaced list is archived and may link to its successor so readers can follow the history without rewriting earlier snapshots.

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

### Change sets

Publishing a new Price Snapshot creates an immutable Price Change Set against the immediately preceding snapshot of the same Customer Price List. PriceSync derives the difference from canonical content rather than trusting change indicators in a Source File or API client.

The change set classifies Price List Entries as added, changed, or removed. Consumers may retrieve either the complete snapshot or its canonical changes.

For the first Price Snapshot, every Price List Entry is classified as added. A changed entry identifies each canonical field that differs and retains both its previous and new value. A removed entry retains the previous canonical entry and the time at which omission ended it.

A Price Correction is compared with the immediately preceding publication like any other snapshot. Differences limited to source-file formatting, column order, or other non-canonical metadata do not appear in the Price Change Set.

## Customer-managed external prices

A customer may gain value from registering price lists received outside PriceSync, even when the supplier does not participate. This would let the customer collect prices in one place and use the same downstream integrations for both connected and unconnected suppliers.

A customer-managed external price must remain distinguishable from a supplier publication:

- its source and registering customer must be explicit
- it is private to the customer
- PriceSync cannot present it as an authoritative statement made by the supplier

Linking an External Supplier Reference to a registered Supplier Organization, establishing a Trading Relationship, or switching Price Source never reveals the Customer-managed list or its history to the Supplier. The archived external list remains private Customer data. Any future sharing capability would require a separate explicit action and model.

### Selecting the price source

A customer selects a Price Source separately for each price-list context defined by Supplier, currency, and Market or commercial purpose. Different lists from the same Supplier may therefore use different sources simultaneously.

Connecting to a Supplier does not automatically replace Customer-Managed External Prices. When a corresponding Supplier-published list becomes available, the Customer may explicitly switch that one list context to the Supplier source without affecting other lists from the same Supplier.

Once selected, future publications to that list are synchronized without requiring acceptance of each update. The previous customer-managed list is archived, retained as history, and linked to its Supplier-published successor rather than remaining an active competing source.

## Unresolved questions

- Which concepts change together and therefore belong in the same domain module?
- Which domain modules or bounded contexts exist within PriceSync?
