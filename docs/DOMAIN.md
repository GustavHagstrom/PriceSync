# PriceSync Domain

This document is the working model of the PriceSync domain. It collects discovered concepts, relationships, business rules, concrete scenarios, and unresolved questions before stable module or bounded-context boundaries have emerged.

The terms in [Context](./Context.md) form the concise canonical glossary. This document provides the deeper model behind those terms.

## Domain boundary

PriceSync makes prices that suppliers and customers have agreed outside the platform available for synchronization between their systems. PriceSync does not negotiate those prices or establish the underlying commercial agreement.

### External agreement assumption

PriceSync assumes that the commercial agreement between Supplier and Customer exists outside the platform. The Supplier publishes its representation of that agreement as immutable Price Agreement Versions.

The Customer must accept a Price Agreement Version before it may become active for Customer Price Projections. This Agreement Acceptance means that the Customer has seen the published agreement representation; it is not bilateral contract formation inside PriceSync and does not require Supplier acceptance.

Price changes resulting from an active agreement still require a separate Customer review and Import Approval before a Price Delivery is created.

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

A Trading Relationship establishes access for price distribution but does not itself contain commercial price terms. Those terms belong to a separate Price Agreement associated with the relationship. This separation allows the connection to remain active when an agreement is replaced or expires.

The Supplier owns, maintains, and publishes the Price Agreement as the authoritative representation of the terms it offers. The Customer may report a discrepancy but cannot edit the Supplier's terms.

Each Price Agreement Version is complete and immutable. A Customer does not need to accept superseded intermediate versions before accepting a newer relevant version. Older versions remain available as history even when never accepted.

For a given agreement scope, the Customer uses exactly one accepted Price Agreement Version as Active for projections. Normally the newest accepted version whose `Valid From` has been reached becomes Active. When multiple versions overlap, the applicable accepted version with the latest `Valid From` takes precedence; an older immutable version does not need to be rewritten with a shortened `Valid To`.

A Customer may explicitly activate an accepted future Price Agreement Version before its Supplier-defined `Valid From`. This does not modify that validity metadata; PriceSync records the Customer's activation decision separately. Such early activation is specific to that version and does not imply that future versions should also activate early.

If a newer Price Agreement Version reaches `Valid From` but has not been accepted, the previously Active accepted version remains Active. A Supplier therefore cannot stop the Customer's existing synchronization merely by publishing a new agreement. Once a newer version is accepted, it may become Active according to its validity or the Customer's explicit early activation.

Customer-Managed External Prices do not require an active Trading Relationship because they remain private to the Customer that registered them.

### Ending a relationship

Either party may end a Trading Relationship. Ending it stops future distribution but does not delete previously completed Price Deliveries or audit history from the Customer's view.

Previously received prices remain accessible and are clearly marked as originating from an ended, no-longer-synchronized relationship. Ending the relationship does not itself change any Customer Price's `Valid To`, because technical disconnection does not establish that the external commercial agreement has ended.

### Organization departure

If an Organization leaves PriceSync, future distribution stops and its private working artifacts such as Price Drafts, Import Profiles, and Source Files follow the applicable deletion and retention policies.

Completed Price Deliveries remain in the recipient's audit history and are marked as originating from an inactive source Organization. A Supplier cannot retroactively remove delivered business facts from a Customer's history, and a Customer's departure does not erase publication and delivery evidence. Personal actor information may be anonymized without removing the business events.

## Price representation

A General Price List is a Supplier-owned, reusable collection of List Prices. Multiple Price Agreements may refer to the same General Price List, allowing its List Prices to be maintained and versioned independently of any individual Customer. Customer-specific discounts remain in the Price Agreement rather than being duplicated in the shared list.

A Supplier creates the logical Price List before importing or publishing its first version. The logical list carries stable scope such as Type, Currency and Market. A Customer-Specific Net Price List also identifies exactly one Customer from creation. These scope properties may be edited before first publication, but after the first Price List Version has been published they are fixed; changing the scope requires a new logical Price List. The list Name is display metadata and may be renamed without creating a new Price List or Price List Version.

A Supplier may create, maintain, and publish a General Price List before any Customer or Trading Relationship exists. Publication versions the Supplier's price basis but does not distribute Customer Prices by itself. Distribution begins only when an active Trading Relationship and an Active accepted Price Agreement Version connect a Customer to that list.

Publishing a new General Price List Version invalidates the affected cached Customer Price Projections. It does not immediately recalculate every Customer's price rows or create permanent Customer copies or Price Deliveries.

A General Price List Version cannot be published unless it can produce valid Customer Price Projections for every Active Price Agreement Version that uses it. Incompatibilities such as an unresolved Supplier Price Group, invalid discount, or currency mismatch block publication and must be corrected in the list or agreement first. Once the version is published, recalculation of all affected projections is guaranteed to succeed.

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

The complete Article Reference is captured in each Price List Version. Completed Price Deliveries retain immutable provenance that identifies the exact source versions used; they do not permanently duplicate every projected article row.

## Price-list versions, projections, approvals, and deliveries

PriceSync separates published source facts, calculated Customer views, Customer authorization, and delivered audit records.

- A Price List Version is an immutable, complete Supplier or Customer-managed source publication.
- A Price Agreement Version is an immutable, complete Supplier representation of externally agreed Customer terms.
- Agreement Acceptance records that the Customer has seen a specific Price Agreement Version.
- An Active Price Agreement Version is the accepted version currently used for Customer Price Projections.
- A Customer Price Projection is the complete calculated Customer view derived from exact source versions. It is rebuildable derived state rather than a permanent audit record.
- Import Approval is the immutable Customer authorization of the exact projection that was reviewed.
- A Price Delivery is the immutable metadata/provenance record created at approval time for that exact approved price basis; it does not permanently contain every projected price row.
- A Delivery Artifact is a specific immutable exported representation of a Price Delivery.
- Successful retrievals are recorded as immutable facts rather than mutable state on the Price Delivery.

This separation avoids permanent Customer copies of every calculated price row while retaining enough immutable source and calculation provenance to identify and deterministically reproduce what the Customer approved.

### Price-list scope and versions

A Supplier or Customer-managed source may maintain multiple logical Price Lists for different currencies, Markets, or commercial purposes. Each logical list has a stable identity through all its versions. Each Price List Version is a complete canonical snapshot and is published atomically: consumers see either the preceding complete version or the new complete version, never an intermediate state or a patch that must be replayed.

Market does not describe where the Customer Organization is registered. Applicability is selected per Supplier and Price Agreement rather than globally for a Customer or destination country.

External integrations may assemble a Price Draft through multiple transport batches, but a batch or patch is never itself a publication. The complete draft must pass validation before it becomes a Price List Version.

Canonical equality is based on all business-relevant fields in the complete Price List Version, not only monetary price values. Changes to fields such as Article Name, Unit of Measure, Price Quantity, validity, Supplier Price Group, identifiers, or price data are therefore version-relevant. Technical input differences such as Excel row order, workbook formatting, file name, or upload time do not change canonical content. Hashing or another deterministic fingerprint may be used to detect equality, but the domain rule is canonical-content equality rather than a specific hashing algorithm.

If the normalized canonical content of a draft is identical to the currently published Price List Version, publishing it does not create another Price List Version. Renaming the logical Price List likewise does not create a new Price List Version because Name is not part of versioned canonical article content.

### Draft and validation

Imported or incrementally assembled data remains a Price Draft until the complete logical list passes validation. A draft may be incomplete or invalid and is not visible to Customers.

At most one active Price Draft exists for a logical Price List. If a Supplier starts a new import while a draft already exists, the Supplier explicitly confirms replacing the existing draft. The replaced draft is discarded as mutable working material rather than preserved as permanent domain history; separate technical audit information may still be retained according to operational needs.

Any Validation Error blocks publication of the entire draft. The preceding Price List Version remains current. Valid rows are never published partially because omitted invalid rows would be indistinguishable from intentional removals.

A Validation Warning identifies suspicious but domain-valid content, such as an unusually large price change or many omitted entries. Manual publication requires explicit acknowledgement; automated publication follows the Supplier's Publication Policy.

A General Price List Version must also be compatible with every Active Price Agreement Version that uses it. Publication is blocked if any resulting Customer Price Projection would be invalid.

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

PriceSync deterministically calculates a complete Customer Price Projection from exact Price List Versions, the Active Price Agreement Version, and a versioned calculation rule. The projection shows resolved Customer Prices and their Price Calculation Basis.

Customer Price Projections are calculated lazily. Publishing a relevant Price List Version, accepting or activating a Price Agreement Version, or reaching the `Valid From` of a newer accepted version may invalidate the affected cached projection. PriceSync calculates a projection when a Customer opens its review, then may cache that result for subsequent reads.

A published but unaccepted Price Agreement Version does not become a projection source and does not displace the Customer's current Active accepted agreement. Superseded intermediate Agreement Versions do not have to be accepted in sequence.

Publication performs compatibility validation without materializing complete projections, including checks for valid currencies, discounts, rounding rules, and referenced Supplier Price Groups. Projection materialization is rebuildable cache rather than a business record: it may be evicted, recalculated, or stored incrementally without changing domain meaning.

PriceSync does not create a permanent Customer record merely because a source version changes. Relevant source changes may notify the Customer even when their effect on resolved Customer Prices has not yet been calculated or may ultimately be zero.

### Import approval and deferral

Every new Customer Price Projection requires manual Import Approval in the initial domain model. Automatic projection approval or automatic Price Delivery creation is an explicit future possibility but is not part of the initial model.

Import Approval applies atomically to the complete Customer Price Projection actually presented for approval. The Customer cannot approve selected entries while retaining old values for others. The approved projection is identified by its exact Price List Versions, Active Price Agreement Version, and calculation rule, and approval never silently switches to a newer projection published while the Customer was reviewing.

If a newer relevant source version or Active Price Agreement Version changes while a review is open, that review becomes stale. The Customer may still explicitly approve the exact projection already reviewed, or leave that review and inspect the newest current projection instead. A stale review never changes its contents underneath the Customer.

Repeated or concurrent approval of the same projection for the same Customer channel is idempotent from the domain perspective and creates at most one Price Delivery. Retrieving that Price Delivery multiple times never creates another equivalent business record.

The Customer may defer a pending change, optionally choosing a reminder date. Deferral does not reject or alter Supplier source publications. Any later relevant Price List Version or activation of a different Price Agreement Version cancels the existing deferral and creates a new notification, even if PriceSync has not yet calculated whether the resulting canonical Customer Prices actually differ.

A Customer does not need to review or deliver every intermediate projection. If several source versions are published before the Customer acts, a newly opened review uses the newest current projection and compares it with the Customer's most recent Price Delivery basis; intermediate source versions remain part of publication history without becoming mandatory review steps.

Future prices are available for review immediately. The Customer chooses whether to approve them before or after `Valid From`; PriceSync does not schedule their application inside the Customer system.

### Price Delivery

Manual Import Approval creates one Price Delivery bound to the exact approved Customer Price Projection. Approval is the commit point for that delivery: a later source publication or agreement activation does not change, cancel, or replace a Price Delivery already created.

A completed Price Delivery is immutable and cannot be edited or deleted. Corrections and later prices produce a new delivery rather than rewriting history.

A Price Delivery permanently retains compact immutable metadata and provenance sufficient to identify the exact approved price basis and reproduce it deterministically. This includes the exact Price List Versions and Price Agreement Version used, the calculation-rule version, Customer/channel scope, approval provenance, and a deterministic projection or canonical-content identity/hash.

The complete Customer Price Projection is not required to be stored permanently as rows inside the Price Delivery. It may be materialized and cached for performance and rebuilt from immutable inputs and the retained calculation-rule version when needed.

Exported representations are separate Delivery Artifacts rather than mutable fields of the Price Delivery. The persistence and regeneration policy for the artifact bytes remains under discovery.

A Customer may create a Price Delivery from the newest projection without replaying intermediate source changes. Earlier source versions and prior Price Deliveries remain permanently available as history.

Creation of a newer Price Delivery does not require an earlier Price Delivery to have been retrieved. A newer delivery identifies a complete approved price basis rather than a patch, so it can be consumed directly without first consuming older deliveries.

### Retrieval

PriceSync makes Price Deliveries and their Delivery Artifacts available for retrieval; it does not push them into Customer systems. Customer-owned integrations or users retrieve them through the available PriceSync interface and remain responsible for any downstream import, retry, or application behavior inside the Customer environment.

Retrieval Status is not mutable state on Price Delivery. Each successful retrieval is recorded as an immutable retrieval fact associated with the relevant delivery or artifact. `Available` means no successful retrieval fact exists; `Retrieved` means one or more exist.

Subsequent retrievals remain allowed and do not change the Price Delivery's business meaning or create another delivery. When multiple Price Deliveries exist for a Customer channel, the newest Price Delivery is the current one returned when an integration requests the current delivery. Older deliveries remain available through explicit historical access and do not need to be retrieved in sequence.

PriceSync does not model whether a Customer system later applied the retrieved prices and does not expose an initial Synchronization Status. The platform records only facts observable at its own boundary.

### Change review and notifications

When a relevant Price List Version is published, or a different Price Agreement Version becomes Active for the Customer, PriceSync produces one notification per configured Customer channel rather than one notification per article. Notification does not require PriceSync to prove that resolved Customer Prices changed.

When the Customer opens the review, PriceSync calculates or loads the current projection and compares it with the reproducible price basis identified by the Customer's most recent Price Delivery. It presents:

1. a summary with counts of added, changed, and removed Price List Entries and aggregate percentage indicators for comparable price changes
2. a list of added entries
3. a list of removed entries
4. a list of changed entries showing the complete previous and new row, including Article Name and Customer Price, together with percentage price change

The primary aggregate indicator is the median percentage change among comparable entries. The review also shows arithmetic mean, minimum, and maximum percentage change. Added and removed entries are excluded from these indicators.

For a Customer with no previous Price Delivery, every entry is classified as added. A changed entry identifies every canonical field that differs and shows both previous and new values. The exact projection identity and calculation basis approved by the Customer are retained through Import Approval and Price Delivery provenance.

If the review finds no canonical Customer Price changes, no new Price Delivery or import is needed. The Customer can still see which source versions triggered the notification.

### Archiving, validity, and corrections

A logical Price List may be archived but not deleted after publication. It accepts no new Price List Versions while its versions and related Price Deliveries remain available. Reactivation is allowed only when its stable business identity is unchanged.

Publication Time and Price Validity are separate facts. Publication Time records when a Price List Version or Price Agreement Version became available through PriceSync. Price Validity records when an individual resulting Customer Price applies commercially. One version or projection may contain both current and future prices.

Each Customer Price has a required `Valid From` and may have a `Valid To`. Prices with identical conditions must not have overlapping validity. Omitting an entry from a new complete Price List Version ends its prior open representation according to the publication and validity rules captured by that source model.

Published Price List Versions and Price Agreement Versions are immutable. Incorrect source information is corrected by publishing a new version, possibly with retroactive Price Validity. Earlier versions and every completed Price Delivery remain available so readers can distinguish what was commercially valid, what PriceSync knew, which agreement the Customer had accepted and activated, and what the Customer approved for delivery.

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

Once selected, future publications to that list participate in the normal Customer review and manual Import Approval flow. The previous customer-managed list is archived, retained as history, and linked to its Supplier-published successor rather than remaining an active competing source.

## Established domain areas

The current bounded-context candidates and aggregate ownership are documented under [docs/Domain](./Domain/readme.md).

## Unresolved questions

- How should Price Publication validate compatibility against Active Agreement state across contexts without creating a giant cross-context transaction?
- Should Delivery Artifact bytes be retained permanently or regenerated deterministically from immutable delivery inputs and versioned export configuration?
- Which context owns Customer-Managed External Price source selection and successor linking?
- Which cross-context relationships should use immutable snapshots, ID/query references, emitted facts, or synchronous validation?
