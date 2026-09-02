# PriceSync Domain

This document is the working model of the PriceSync domain. It collects discovered concepts, relationships, business rules, concrete scenarios, and unresolved questions before stable module or bounded-context boundaries have emerged.

The terms in [Context](./Context.md) form the concise canonical glossary. This document provides the deeper model behind those terms.

## Domain boundary

PriceSync makes prices that suppliers and customers have agreed outside the platform available for synchronization between their systems. PriceSync does not negotiate those prices or establish the underlying commercial agreement.

### External agreement assumption

PriceSync assumes that the commercial agreement between Supplier and Customer exists outside the platform. The Supplier publishes its representation of that agreement as immutable Price Agreement Versions.

The Customer must accept a Price Agreement Version before it may become active for Customer Price Projections. This Agreement Acceptance means that the Customer has seen the published agreement representation; it is not bilateral contract formation inside PriceSync and does not require Supplier acceptance.

Price changes resulting from an active agreement still require a separate Customer review and Import Approval before they become available for retrieval.

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

Either party may end a Trading Relationship. Ending it stops future distribution but does not delete previously completed approvals or retrieval history from the Customer's view.

Previously received prices remain accessible and are clearly marked as originating from an ended, no-longer-synchronized relationship. Ending the relationship does not itself change any Customer Price's `Valid To`, because technical disconnection does not establish that the external commercial agreement has ended.

### Organization departure

If an Organization leaves PriceSync, future distribution stops and its private working artifacts such as Price Drafts, Import Profiles, and Source Files follow the applicable deletion and retention policies.

Completed Import Approvals and retrieval facts remain in the recipient's audit history and are marked as originating from an inactive source Organization. A Supplier cannot retroactively remove approved business facts from a Customer's history, and a Customer's departure does not erase publication and retrieval evidence. Personal actor information may be anonymized without removing the business events.

## Price representation

A General Price List is a Supplier-owned, reusable collection of List Prices. Multiple Price Agreements may refer to the same General Price List, allowing its List Prices to be maintained and versioned independently of any individual Customer. Customer-specific discounts remain in the Price Agreement rather than being duplicated in the shared list.

A Supplier creates the logical Price List before importing or publishing its first version. The logical list carries stable scope such as Type, Currency and Market. A Customer-Specific Net Price List also identifies exactly one Customer from creation. A Customer-Managed External Net Price List is likewise scoped to exactly one Customer, is administered by that Customer for its own use, and identifies its real external source through an External Supplier Reference. These scope properties may be edited before first publication, but after the first Price List Version has been published they are fixed; changing the scope requires a new logical Price List. The list Name is display metadata and may be renamed without creating a new Price List or Price List Version.

A Supplier may create, maintain, and publish a General Price List before any Customer or Trading Relationship exists. Publication versions the Supplier's price basis but does not distribute Customer Prices by itself. Distribution begins only when an active Trading Relationship and an Active accepted Price Agreement Version connect a Customer to that list.

Publishing a new General Price List Version does not synchronously recalculate every Customer's projection and does not replace any Customer's current approved basis. It only creates a newer source version from which a candidate projection may later be built.

A Supplier may also publish a Customer-Specific Net Price List. Its Net Prices are already resolved for that Customer and receive no additional Customer Discount. A Customer-Managed External Net Price List follows the same publication, versioning, projection, review, approval, and retrieval flow, but the consuming Customer administers the source itself and no Price Agreement or Trading Relationship is required.

General and customer-specific net lists do not establish an automatic precedence rule. When commercial conditions make two offers distinct sellable forms—for example a net-priced standard white door and a configurable door priced from the General Price List—they are distinct Price List Entries from the Customer's perspective. Their Supplier Article IDs may be identical because the logical Price List is part of entry identity.

A supplier may provide a Customer Price in either of two forms:

- a Net Price stated directly for a specific customer and article
- a List Price combined with a Customer Discount

PriceSync resolves both forms to a Customer Price. Customer-facing integrations receive the resolved monetary amount and do not need to interpret the supplier's discount calculation.

The calculation basis is retained so that the resolved Customer Price can be explained and audited.

### Calculation visibility

A Customer always sees the resolved Customer Price in its own list. When PriceSync calculated the price from a List Price and Customer Discount, the Customer also sees that Price Calculation Basis, including the applicable discount and rounding.

A specific Net Price exposes no inferred discount or fictional calculation. Customers never see another Customer's prices, discounts, projections, approvals, retrievals, or calculation basis. A shared List Price is exposed only through the receiving Customer's own projection, not as the Supplier's complete internal pricing material.

### Discount selection

A Customer Price derived from a General Price List uses one of two discount levels, in descending priority:

1. a Customer Discount for the entry's Supplier Price Group applied to its List Price
2. a general Customer Discount for the applicable Price Agreement applied to its List Price

A Supplier Price Group is commercial pricing metadata owned by the Supplier. It does not make PriceSync responsible for product classification.

Only one Customer Discount determines a Customer Price derived from a General Price List. A Supplier Price Group discount replaces the general Customer Discount rather than combining with it. A Customer-Specific Net Price List or Customer-Managed External Net Price List is a separate price source whose prices receive no discount; it does not override entries derived from a General Price List.

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

The complete Article Reference is captured in each Price List Version. Completed Import Approvals retain immutable provenance that identifies the exact source versions used; they do not permanently duplicate every projected article row.

## Price-list versions, projections, approvals, and retrieval

PriceSync separates published source facts, Customer-facing derived views, Customer authorization, and retrieval history.

- A Price List Version is an immutable, complete Supplier or Customer-managed source publication.
- A Price Agreement Version is an immutable, complete Supplier representation of externally agreed Customer terms.
- Agreement Acceptance records that the Customer has seen a specific Price Agreement Version.
- An Active Price Agreement Version is the accepted version currently used for Customer Price Projections derived from General Price Lists.
- A Customer Price Projection is the complete Customer-facing view for exactly one Customer and one logical Price List. It may involve price calculation or simply expose already-resolved Net Prices. It is rebuildable derived state rather than a permanent audit record.
- Import Approval is the immutable Customer authorization of the exact projection that was reviewed and the durable identity of that approved price basis.
- Exported representations are deterministic derived results of an Import Approval plus versioned export configuration; their bytes may be cached rather than permanently stored.
- A Price Retrieval is an immutable fact recording one successful export/retrieval and handoff of an approved price basis.

A separate Price Delivery aggregate is not required. This keeps the model focused on the durable Customer decision and the observable retrieval facts while avoiding a pass-through object that would duplicate Import Approval provenance.

A Customer Price Projection has no stable domain ID. Its natural identity is the exact tuple `CustomerId + PriceListVersionId + optional PriceAgreementVersionId`. The agreement-version component is present only for General Price Lists. No separate projection fingerprint or Calculation Rule Version is required in the domain model.

Each projection concerns exactly one logical Price List. Multiple Price Lists from the same Supplier remain separate projections and may be reviewed and approved independently; PriceSync does not merge all of a Customer's sources into one giant projection or approval unit.

### Price-list scope and versions

A Supplier or Customer-managed source may maintain multiple logical Price Lists for different currencies, Markets, or commercial purposes. Each logical list has a stable identity through all its versions. Each Price List Version is a complete canonical snapshot and is published atomically: consumers see either the preceding complete version or the new complete version, never an intermediate state or a patch that must be replayed.

Market does not describe where the Customer Organization is registered. Applicability is selected per Supplier and Price Agreement rather than globally for a Customer or destination country.

External integrations may assemble a Price Draft through multiple transport batches, but a batch or patch is never itself a publication. The complete draft must pass validation before it becomes a Price List Version.

Canonical equality is based on all business-relevant fields in the complete Price List Version, not only monetary price values. Changes to fields such as Article Name, Unit of Measure, Price Quantity, validity, Supplier Price Group, identifiers, or price data are therefore version-relevant. Technical input differences such as Excel row order, workbook formatting, file name, or upload time do not change canonical content. Hashing or another deterministic fingerprint may be used as an implementation technique to detect equality, but no fingerprint is a domain identity for Customer Price Projection.

If the normalized canonical content of a draft is identical to the currently published Price List Version, publishing it does not create another Price List Version. Renaming the logical Price List likewise does not create a new Price List Version because Name is not part of versioned canonical article content.

### Draft and validation

Imported or incrementally assembled data remains a Price Draft until the complete logical list passes validation. A draft may be incomplete or invalid and is not visible to Customers.

At most one active Price Draft exists for a logical Price List. If an Organization starts a new import while a draft already exists, it explicitly confirms replacing the existing draft. The replaced draft is discarded as mutable working material rather than preserved as permanent domain history; separate technical audit information may still be retained according to operational needs.

Any Validation Error blocks publication of the entire draft. The preceding Price List Version remains current. Valid rows are never published partially because omitted invalid rows would be indistinguishable from intentional removals.

A Validation Warning identifies suspicious but domain-valid content, such as an unusually large price change or many omitted entries. Manual publication requires explicit acknowledgement; automated publication follows the publishing Organization's Publication Policy.

Price Publication validates the Price List itself and does not validate every Customer/Agreement combination before publication. Cross-context compatibility is evaluated later by Customer Pricing when an exact projection is requested or otherwise needed.

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

Source-file retention is independent of published and approved business records. When a Source File expires, PriceSync may delete the file while retaining non-content provenance such as checksum, filename, upload time, actor, Import Profile, and links to the resulting Price List Version. Price List Versions, Price Agreement Versions, completed Import Approvals, and Price Retrieval facts are not deleted as working-file cleanup.

### Customer Price Projection

PriceSync deterministically evaluates a complete Customer Price Projection for one Customer and one logical Price List.

For a General Price List, the projection is calculated from the exact Price List Version and the applicable Active accepted Price Agreement Version, using the domain's pricing rules and the agreement's explicit terms such as discounts and Rounding Policy.

For a Customer-Specific Net Price List or Customer-Managed External Net Price List, the source already contains resolved Customer prices. The projection therefore directly exposes those source prices through the same Customer-facing review model and has no Price Agreement Version.

Projection evaluation is explicit domain behavior rather than an exception-driven technical operation. The result is either:

- a complete valid Customer Price Projection, optionally carrying non-blocking Projection Warnings
- or an invalid result containing structured Projection Issues

The projection is atomic. If any required row cannot be resolved correctly, the whole candidate projection is invalid and cannot be approved. PriceSync does not silently drop unresolved rows or substitute speculative fallback pricing.

Projection Warnings identify suspicious but still valid outcomes, for example unusually large changes compared with the Customer's current approved basis. Warnings do not block Import Approval. Customer Pricing owns the warning and Price Change Set semantics. The initial model uses fixed system warning rules rather than Customer-configurable warning policies.

Customer Price Projections are calculated lazily. Publishing a newer Price List Version, accepting or activating a Price Agreement Version, or reaching the `Valid From` of a newer accepted version may make a newer candidate projection available, but does not invalidate or replace the Customer's current approved basis. PriceSync calculates a candidate projection when a Customer opens its review or when another use case needs it, and may cache the result for subsequent reads.

A published but unaccepted Price Agreement Version does not become a projection source and does not displace the Customer's current Active accepted agreement. Superseded intermediate Agreement Versions do not have to be accepted in sequence.

PriceSync does not create a permanent Customer record merely because a source version changes. Relevant source changes may notify the Customer even when their effect on resolved Customer Prices has not yet been calculated or may ultimately be zero.

### Import approval and deferral

Every new Customer Price Projection requires manual Import Approval in the initial domain model, including projections from Customer-Specific Net Price Lists and Customer-Managed External Net Price Lists. Publication and consumption approval are separate decisions. Automatic projection approval is an explicit future possibility but is not part of the initial model.

Import Approval applies atomically to the complete valid Customer Price Projection actually presented for approval. Invalid projections cannot be approved. The Customer cannot approve selected entries while retaining old values for others. The approval stores explicit immutable references to `CustomerId`, `PriceListVersionId`, and optional `PriceAgreementVersionId`, plus approval actor and time. No separate projection fingerprint, Calculation Rule Version, or Customer Price Projection ID is required.

The same exact Customer Price Projection may be approved at most once by the Customer. Repeated or concurrent attempts to approve the same natural projection identity are idempotent and resolve to the existing Import Approval rather than creating duplicate approval facts.

A Customer has one current approved basis per logical Price List, not one global basis across all price sources. The current basis is derived from the latest relevant Import Approval for `CustomerId + logical PriceListId`; PriceSync does not maintain a separate mutable pointer for it.

A newer source version, a newer active agreement, or a newly calculated candidate projection does not move the current approved basis. If the Customer never approves the candidate, the previous Import Approval remains current. Only a new explicit Import Approval for that logical Price List replaces it.

If a newer relevant source version or Active Price Agreement Version changes while a review is open, that review becomes stale. The Customer may still explicitly approve the exact projection already reviewed, or leave that review and inspect the newest candidate projection instead. A stale review never changes its contents underneath the Customer.

The Customer may defer a pending change, optionally choosing a reminder date. Deferral does not reject or alter source publications. Any later relevant Price List Version or activation of a different Price Agreement Version cancels the existing deferral and creates a new notification, even if PriceSync has not yet calculated whether the resulting canonical Customer Prices actually differ.

A Customer does not need to review or approve every intermediate projection. If several source versions are published before the Customer acts, a newly opened review uses the newest current candidate projection and compares it with the Customer's current approved basis for that logical Price List; intermediate source versions remain part of publication history without becoming mandatory review steps.

Future prices are available for review immediately. The Customer chooses whether to approve them before or after `Valid From`; PriceSync does not schedule their application inside the Customer system.

### Export and retrieval

An Import Approval is the durable identity of an approved price basis that is available for retrieval. Export does not create another pricing business object.

When a Customer requests an external representation, PriceSync deterministically rebuilds the approved Customer Price Projection from the immutable inputs captured by the Import Approval, applies the selected versioned Export Profile/configuration and export rules, and generates the requested representation.

Generated export bytes do not need permanent storage when the representation can be reproduced deterministically. They may be cached for performance. Historical reproducibility requires PriceSync to retain enough versioned export configuration and export-rule provenance to regenerate a semantically, and where required bitwise, equivalent representation.

Each successful export/retrieval and handoff creates one immutable Price Retrieval fact. A Price Retrieval may identify the Import Approval, export configuration/profile version, format, retrieving actor/integration, retrieval time, and a deterministic content hash.

A Price Retrieval fact is created only after generation and handoff succeed. Failed attempts and retries before successful handoff are operational telemetry, not successful retrieval domain facts.

Repeated retrieval is allowed and creates additional Price Retrieval facts, producing an append-only retrieval log. The derived state is therefore:

- `Available`: no successful Price Retrieval exists for the Import Approval
- `Retrieved`: one or more successful Price Retrieval facts exist

PriceSync does not model whether a Customer system later applied the retrieved prices. The platform records only facts observable at its own boundary.