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

A Price Publisher may validate and publish a Price Draft without approval from a second person. PriceSync records who imported the source, acknowledged each Validation Warning, and published the resulting Price List Version.

Mandatory separation between draft author and approver is an initial non-goal. It may later become an optional Supplier-side enterprise control and would not represent Customer acceptance of prices.

## Trading relationships

A Trading Relationship represents a directed Supplier–Customer business relationship established outside PriceSync. PriceSync does not provide a workflow for requesting, negotiating, or accepting that relationship. Its representation scopes which Supplier-published prices and terms the Customer may review.

The Customer may verify that the represented Price Agreement matches the external agreement, but this is not acceptance of the relationship or individual commercial prices inside PriceSync. Future source publications update the Customer Price Projection without per-update commercial approval.

A Trading Relationship establishes access for price distribution but does not itself contain commercial price terms. Those terms belong to a separate Price Agreement associated with the relationship. This separation allows the connection to remain active when an agreement is replaced or expires.

The Supplier owns, maintains, and publishes the Price Agreement as the authoritative representation of the terms it offers. The Customer may report a discrepancy but cannot edit the Supplier's terms. Publishing or updating the Price Agreement does not require per-update Customer approval inside PriceSync because the commercial agreement is assumed to exist outside the platform.

Customer-Managed External Prices do not require an active Trading Relationship because they remain private to the Customer that registered them.

### Ending a relationship

Either party may end a Trading Relationship. Ending it stops future distribution but does not delete previously completed Price Deliveries or audit history from the Customer's view.

Previously received prices remain accessible and are clearly marked as originating from an ended, no-longer-synchronized relationship. Ending the relationship does not itself change any Customer Price's `Valid To`, because technical disconnection does not establish that the external commercial agreement has ended.

### Organization departure

If an Organization leaves PriceSync, future distribution stops and its private working artifacts such as Price Drafts, Import Profiles, and Source Files follow the applicable deletion and retention policies.

Completed Price Deliveries remain in the recipient's audit history and are marked as originating from an inactive source Organization. A Supplier cannot retroactively remove delivered business facts from a Customer's history, and a Customer's departure does not erase publication and delivery evidence. Personal actor information may be anonymized without removing the business events.

## Price representation

A General Price List is a Supplier-owned, reusable collection of List Prices. Multiple Price Agreements may refer to the same General Price List, allowing its List Prices to be maintained and versioned independently of any individual Customer. Customer-specific discounts remain in the Price Agreement rather than being duplicated in the shared list.

A Supplier may create, maintain, and publish a General Price List before any Customer or Trading Relationship exists. Publication versions the Supplier's price basis but does not distribute Customer Prices by itself. Distribution begins only when an active Trading Relationship and applicable Price Agreement connect a Customer to that list.

Publishing a new General Price List Version invalidates the affected cached Customer Price Projections. It does not immediately recalculate every Customer's price rows or create permanent Customer copies or Price Deliveries.

A General Price List Version cannot be published unless it can produce valid Customer Price Projections for every active Price Agreement Version that uses it. Incompatibilities such as an unresolved Supplier Price Group, invalid discount, or currency mismatch block publication and must be corrected in the list or agreement first. Once the version is published, recalculation of all affected projections is guaranteed to succeed.

A Supplier may also publish a Customer-Specific Net Price List. Its Net Prices are already resolved for that Customer and receive no additional Customer Discount.

General and customer-specific net lists do not establish an automatic precedence rule. When commercial conditions make two offers distinct sellable forms—for example a net-priced standard white door and a configurable door priced from the General Price List—they are distinct Price List Entries from the Customer's perspective. Their Supplier Article IDs may be identical because the logical Price List is part of entry identity.

A supplier may provide a Customer Price in either of two forms:

- a Net Price stated directly for a specific customer and article
- a List Price combined with a Customer Discount

PriceSync resolves both forms to a Customer Price. Customer-facing integrations receive the resolved monetary amount and do not need to interpret the supplier's discount calculation.

The calculation basis is retained so that the resolved Customer Price can be explained and audited.

### Calculation visibility

A Customer always sees the resolved Customer Price in its own list. When PriceSync calculated the price from a List Price and Customer Discount, the Customer also sees that Price Calculation Basis, including the applicable discount and rounding.

A specific Net Price exposes no inferred discount or fictional calculation. Customers never see another Customer's prices, discounts, projections, deliveries, or calculation basis. A shared List Price is exposed only through the receiving Customer's own projection, not as the Supplier's complete internal pricing material.

### Discount selection

A Customer Price derived from a General Price List uses one of two discount levels, in descending priority:

1. a Customer Discount for the entry's Supplier Price Group applied to its List Price
2. a general Customer Discount for the applicable Price Agreement applied to its List Price

A Supplier Price Group is commercial pricing metadata owned by the Supplier. It does not make PriceSync responsible for product classification.

Only one Customer Discount determines a Customer Price derived from a General Price List. A Supplier Price Group discount replaces the general Customer Discount rather than combining with it. A Customer-Specific Net Price List is a separate price source whose prices receive no discount; it does not override entries derived from a General Price List.

A Customer Discount is always expressed as a percentage in the core domain. Fixed deductions, factors, and supplier-specific formulas are normalized at the platform boundary to either an unambiguous percentage or a resolved Net Price.

### Rounding

Each Price Agreement has an explicit Rounding Policy consisting of decimal precision and rounding method. All price calculations use decimal arithmetic.

Currency may provide a suggested default precision, but a list may require greater precision than the currency's smallest payment unit. The resolved and rounded Customer Price is retained together with its calculation basis so that the publication is deterministic and auditable.

### Taxes

Every Customer Price in the canonical domain excludes VAT and other transaction taxes. PriceSync neither stores article tax rates nor calculates taxes; the consuming system applies them using transaction-specific information.

An external format containing tax-inclusive amounts must be normalized before publication. If the tax-exclusive Customer Price cannot be derived safely, the import is rejected rather than published as a comparable canonical price.

### Surcharges and transaction costs

A mandatory surcharge that always applies to the priced article and unit is included in the resolved Customer Price. When an external source represents such a surcharge separately, the adapter normalizes it into a Net Price; the source breakdown may be retained for auditability but does not add another core pricing formula.

Order-, delivery-, and project-specific costs such as freight are outside the PriceSync price domain. Refundable amounts such as deposits are not part of the Customer Price and require a separate model if future evidence justifies supporting them.

### Price basis

Every Customer Price states an amount together with a Price Quantity and Unit of Measure. These three values form the Price Basis. Currency belongs to the applicable Price List Version.

For example, an amount of `1,200 / 100 metre` in a SEK Price List Version means that 1,200 SEK covers 100 metres. Price Quantity is distinct from package size, minimum order quantity, or other commercial conditions.

### Quantity-based prices

The initial domain model allows one Customer Price per Price List Entry and validity period. Ordered quantity does not select between simultaneous price tiers for the same entry.

Price Quantity remains part of the Price Basis; it states what quantity the amount covers and is not a quantity-discount threshold. Quantity breaks are an explicit initial non-goal until evidence from real price lists demonstrates the need.

Within one logical Price List, a Supplier Article ID has exactly one Price List Entry and one Price Basis. It may have successive Customer Prices with non-overlapping validity periods, but never multiple simultaneously valid prices. Different sellable forms in the same list require different Supplier Article IDs; the same identifier in another logical list identifies a separate entry.

## Article identity

A Customer Price applies to an Article Reference identified by the combination of Supplier, logical Price List, and Supplier Article ID. A Supplier Article ID is required and is unique only within that list's namespace.

An Article Reference may include:

- an Article Name for concise human-readable display
- a GTIN or other external identifier

PriceSync assigns a short, opaque, immutable, and globally unique PriceSync Article ID to that combination. The Supplier, logical Price List, and Supplier Article ID remain separately available as readable business fields rather than being encoded into the identifier. The logical Price List identity remains stable through all its versions, so a new version does not create new article identities. Optional identifiers support matching and integration but do not replace the composite business key.

A Supplier Article ID may occur in both a General Price List and a Customer-Specific Net Price List, producing two separate Price List Entries and PriceSync Article IDs. PriceSync does not infer that entries in different lists are the same article merely because their Supplier Article IDs match.

Changing a Supplier Article ID within a logical Price List creates a new Price List Entry and PriceSync Article ID. The previous entry is reported as removed and the new entry as added; PriceSync never infers identity continuity from similar names, prices, or other attributes.

Customer systems may use the PriceSync Article ID as a guaranteed unique external article identifier. Mapping it to an existing internal article, creating a local article, or handling an unmatched entry belongs to the Customer's connector or integration and is outside the PriceSync core domain.

PriceSync mapping is limited to onboarding incoming price data into the canonical model through an Import Profile. It does not provide a general product-master matching engine or AI-based reconciliation for Customer systems.

PriceSync does not own a product catalog. The Article Name is deliberately limited to a short designation; full descriptions, classifications, images, documents, and other product content remain outside the domain boundary.

The complete Article Reference is captured in each Price List Version and completed Price Delivery. Later changes to an Article Name or optional identifier do not rewrite earlier versions or deliveries.

## Price-list versions, projections, and deliveries

PriceSync separates published source facts, calculated Customer views, and delivered audit records.

- A Price List Version is an immutable, complete Supplier or Customer-managed source publication.
- A Price Agreement Version is an immutable Supplier representation of externally agreed Customer terms.
- A Customer Price Projection is the complete calculated Customer view derived from exact source versions.
- A Price Delivery is the immutable result a Customer chose to make available for retrieval or import.

This separation avoids creating permanent Customer copies for changes that are never consumed while retaining exact evidence for every actual delivery.

### Price-list scope and versions

A Supplier or Customer-managed source may maintain multiple logical Price Lists for different currencies, Markets, or commercial purposes. Each logical list has a stable identity through all its versions. A Price List Version is complete and published atomically: consumers see either the preceding complete version or the new complete version, never an intermediate state.

Market does not describe where the Customer Organization is registered. Applicability is selected per Supplier and Price Agreement rather than globally for a Customer or destination country.

External integrations may assemble a Price Draft through multiple transport batches, but a batch or patch is never itself a publication. The complete draft must pass validation before it becomes a Price List Version.

### Draft and validation

Imported or incrementally assembled data remains a Price Draft until the complete logical list passes validation. A draft may be incomplete or invalid and is not visible to Customers.

Any Validation Error blocks publication of the entire draft. The preceding Price List Version remains current. Valid rows are never published partially because omitted invalid rows would be indistinguishable from intentional removals.

A Validation Warning identifies suspicious but domain-valid content, such as an unusually large price change or many omitted entries. Manual publication requires explicit acknowledgement; automated publication follows the Supplier's Publication Policy.

A General Price List Version must also be compatible with every active Price Agreement Version that uses it. Publication is blocked if any resulting Customer Price Projection would be invalid.

### Initial ingestion paths

The initial model has two ingestion paths:

1. Manual File Upload accepts Excel or CSV and uses an Import Profile to map the Organization's external layout into a Price Draft.
2. API Publication accepts canonical JSON and creates a Price Draft without an Import Profile.

Import Profiles are private to their owning Organization and reusable across compatible uploads. Both paths produce the same canonical draft and use the same validation and publication rules. SFTP or other automated file delivery may later reuse the same Import Profiles.

An API client may assemble one complete Price Draft through multiple idempotent batches. Standalone API patches that directly publish individual entries are an initial non-goal.

### Manual file flow and duplicate ingestion

A manual Excel or CSV upload never publishes directly. It is associated with an existing or newly configured Import Profile, parsed, normalized, and validated asynchronously into a reviewable Price Draft. Reusing a compatible profile never bypasses validation.

PriceSync detects canonical content already processed for the same logical Price List. Retrying an upload or API request is idempotent and does not create another draft, version, or notification. Binary differences such as Excel formatting or column order do not create a new Price List Version when normalized canonical content is identical.

### Source-file privacy

The original Source File is private audit evidence for the uploading Organization and is linked to its import attempt, Price Draft, and resulting Price List Version. It is never distributed to a Customer unless the owning Organization takes a separate explicit action.

Source-file retention is independent of published and delivered business records. When a Source File expires, PriceSync may delete the file while retaining non-content provenance such as checksum, filename, upload time, actor, Import Profile, and links to the resulting Price List Version. Price List Versions, Price Agreement Versions, and completed Price Deliveries are not deleted.

### Customer Price Projection

PriceSync deterministically calculates a complete Customer Price Projection from exact Price List Versions, the applicable Price Agreement Version, and a versioned calculation rule. The projection shows resolved Customer Prices and their Price Calculation Basis.

Customer Price Projections are calculated lazily. Publishing a relevant Price List Version or Price Agreement Version marks affected cached projections as stale and notifies Customers without materializing every Customer's price rows. PriceSync calculates a projection when a Customer opens its review or requests delivery, then may cache that result for subsequent reads.

Publication performs compatibility validation without materializing complete projections, including checks for valid currencies, discounts, rounding rules, and referenced Supplier Price Groups. Projection materialization is rebuildable cache rather than a business record: it may be evicted, recalculated, or stored incrementally without changing domain meaning.

PriceSync does not create a permanent Customer record merely because a source version changes. Any newly published source version relevant to the Customer produces a notification, even when its effect on resolved Customer Prices has not yet been calculated or may ultimately be zero.

### Import approval and deferral

The Customer's Consumption Policy determines whether a changed projection requires manual Import Approval or may create a Price Delivery automatically. Manual approval is the default.

Import Approval applies atomically to the complete Customer Price Projection actually presented for approval. The Customer cannot approve selected entries while retaining old values for others. The approved projection is identified by its exact source versions and calculation rule, and approval never silently switches to a newer projection published while the Customer was reviewing.

If a newer relevant source version is published while a review is open, that review becomes stale. The Customer may still explicitly approve the exact projection already reviewed, or leave that review and inspect the newest current projection instead. A stale review never changes its contents underneath the Customer.

Repeated or concurrent approval of the same projection for the same Customer channel is idempotent from the domain perspective and creates at most one Price Delivery. Retrieving that Price Delivery multiple times never creates another equivalent business record.

The Customer may defer a pending change, optionally choosing a reminder date. Deferral does not reject or alter Supplier source publications. Any later relevant Price List Version or Price Agreement Version cancels the existing deferral and creates a new notification, even if PriceSync has not yet calculated whether the resulting canonical Customer Prices actually differ. The initial model deliberately favors this simple publication-based rule over calculating projection equality before deciding whether to interrupt deferral.

A Customer does not need to review or deliver every intermediate projection. If several source versions are published before the Customer acts, a newly opened review uses the newest current projection and compares it with the Customer's most recent Price Delivery; intermediate source versions remain part of publication history without becoming mandatory review steps.

Future prices are available for review immediately. The Customer chooses whether to approve them before or after `Valid From`; PriceSync does not schedule their application inside the Customer system.

### Price Delivery

Import Approval or an applicable automatic Consumption Policy creates one complete Price Delivery bound to the exact approved Customer Price Projection. Approval is the commit point for that delivery: a later source publication does not change, cancel, or replace the contents of a Price Delivery already created.

A completed Price Delivery is immutable and cannot be edited or deleted. Corrections and later prices produce a new delivery rather than rewriting history.

Every Price Delivery permanently retains:

- the exact Price List Versions and Price Agreement Version used
- the calculation-rule version
- the complete canonical Customer Price result
- a canonical content hash
- each generated export representation
- Retrieval Status
- actor and timing provenance, subject to personal-data anonymization

PriceSync may store the canonical result using compression, deduplication, or content-addressed components, but these implementation choices may not compromise permanent exact retrieval. Calculation cache and previews are not Price Deliveries and may be removed.

A Customer may create a Price Delivery from the newest projection without replaying intermediate source changes. Earlier source versions and prior Price Deliveries remain permanently available as history.

Creation of a newer Price Delivery does not require an earlier Price Delivery to have been retrieved. Because every Price Delivery contains a complete canonical projection rather than a patch, the newer delivery can be consumed directly without first consuming older deliveries.

### Retrieval

PriceSync makes Price Deliveries available for retrieval; it does not push them into Customer systems. Customer-owned integrations or users retrieve deliveries through the available PriceSync interface and remain responsible for any downstream import, retry, or application behavior inside the Customer environment.

A new Price Delivery starts with `Retrieval Status = Available`. The first successful retrieval changes that status to `Retrieved`. Subsequent retrievals of the same immutable Price Delivery remain allowed and do not change its business meaning or create another delivery.

When multiple Price Deliveries exist for a Customer channel, the newest Price Delivery is the current one returned when an integration requests the current delivery. Older deliveries remain available through explicit historical access and do not need to be retrieved in sequence.

PriceSync does not model whether a Customer system later applied the retrieved prices and does not expose an initial Synchronization Status. The platform records only facts observable at its own boundary.

### Change review and notifications

When a relevant Price List Version or Price Agreement Version is published, PriceSync produces one notification per configured Customer channel rather than one notification per article. Notification does not require PriceSync to prove that resolved Customer Prices changed.

When the Customer opens the review, PriceSync calculates or loads the current projection and compares it with the canonical result of the Customer's most recent Price Delivery. It presents:

1. a summary with counts of added, changed, and removed Price List Entries and aggregate percentage indicators for comparable price changes
2. a list of added entries
3. a list of removed entries
4. a list of changed entries showing the complete previous and new row, including Article Name and Customer Price, together with percentage price change

The primary aggregate indicator is the median percentage change among comparable entries. The review also shows arithmetic mean, minimum, and maximum percentage change. Added and removed entries are excluded from these indicators.

For a Customer with no previous Price Delivery, every entry is classified as added. A changed entry identifies every canonical field that differs and shows both previous and new values. The exact Price Change Set reviewed is captured permanently when the resulting Price Delivery is created.

If the review finds no canonical Customer Price changes, no new Price Delivery or import is needed. The Customer can still see which source versions triggered the notification.

### Archiving, validity, and corrections

A logical Price List may be archived but not deleted after publication. It accepts no new Price List Versions while its versions and related Price Deliveries remain available. Reactivation is allowed only when its stable business identity is unchanged.

Publication Time and Price Validity are separate facts. Publication Time records when a Price List Version or Price Agreement Version became available through PriceSync. Price Validity records when an individual resulting Customer Price applies commercially. One version or projection may contain both current and future prices.

Each Customer Price has a required `Valid From` and may have a `Valid To`. Prices with identical conditions must not have overlapping validity. Omitting an entry from a new complete Price List Version ends its prior open representation according to the publication and validity rules captured by that source model.

Published Price List Versions and Price Agreement Versions are immutable. Incorrect source information is corrected by publishing a new version, possibly with retroactive Price Validity. Earlier versions and every completed Price Delivery remain available so readers can distinguish what was commercially valid, what PriceSync knew, and what a Customer actually received.

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