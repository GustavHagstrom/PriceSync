# Issue #13 delivery design

Planning only. Implementation-ready delivery design for decision map #4, completed on 2026-10-25. No production code or migrations were implemented.
Business decisions are recorded in [the decision map](decision-map-issue-13.md).

## Repository constraints

- Follow ADRs 0001–0005: module-owned endpoints and contracts, EF Core/Npgsql, module schemas/migrations, strongly typed UUIDv7 IDs, canonical Person identity from the host.
- Agreements and Price Publication currently have registration skeletons, not commercial implementations. Trading Relationships supplies examples of internal feature handlers, scoped authorization, named constraints and PostgreSQL integration tests.
- Do not read another module's tables, introduce cross-schema foreign keys or expose persistence entities as contracts.
- No cross-module transaction is needed for immutable list-scope verification. Publication, Customer decisions and Price List activation remain separate workflows.

## Contracts and HTTP shape

Names and routes below define the planned contract shape; they are not implemented API promises. Technical choices complete the agreed behavior without changing the business decisions in the map.

Module contracts:

- Trading Relationships: a purpose-built agreement-scope lookup by TradingRelationshipId, returning SupplierOrganizationId, CustomerOrganizationId and Active/Ended, or NotFound. Technical failures are not domain status values. Agreements applies the authorized acting-party check before exposing the result.
- Price Publication: read-only verification of a complete set of logical PriceListIds against Supplier/Customer identities. Return verified immutable scope, invalid references or Unavailable. No published Price List Version is required. Immutable scope includes owner, type, currency, market and applicable Customer. No permissive production stub.
- OrganizationAccess: separate ReadAgreements, PublishAgreements and ManageCustomerAgreements capabilities; withdrawal uses Supplier publication authority. Members may read, Owners may write, and the acting Organization must be Active.
- Agreements: owned AgreementId/AgreementVersionId identifiers; exact immutable version reader exposing relationship, coverage, discounts, rounding and publication facts; relationship-wide current-selection reader and paged selection-history reader. Consumers receive facts/DTOs, never EF entities or aggregate objects.

Module-owned routes under `/api/v1`:

| Operation | Proposed route/input |
| --- | --- |
| Create stable agreement | `POST /agreements`; body requires `tradingRelationshipId`; required `Idempotency-Key`; Supplier Owner creates an agreement with no published versions |
| List stable agreements | `GET /agreements?tradingRelationshipId={id}`; optional `pageSize` and `cursor`; ordered descending by creation time/identity |
| Publish next version | `POST /agreements/{agreementId}/versions`; expected latest version ID (nullable for first publication), complete validity/coverage/discount payload |
| Read agreement/versions | `GET /agreements/{agreementId}` and `/versions`; scoped to an authorized party |
| Read exact version | `GET /agreements/{agreementId}/versions/{versionId}` |
| Withdraw version | `POST /agreements/{agreementId}/versions/{versionId}/withdraw`; reason |
| Accept version | `POST /trading-relationships/{relationshipId}/agreement-acceptances`; version ID |
| Change Active selection | `PUT /trading-relationships/{relationshipId}/agreement-selection`; required nullable `fromVersionId` and `toVersionId` |
| Read selection | `GET /trading-relationships/{relationshipId}/agreement-selection`; optional `asOf` UTC timestamp or `throughSequence`, mutually exclusive |
| Read transition history | `GET /trading-relationships/{relationshipId}/agreement-selection/history`; sequence cursor |
| Recommendation | `GET /agreements/{agreementId}/recommendation`; evaluated UTC business date and knowledge cutoff |

Both selection properties must be present; JSON null explicitly means no Active version. Both null, or identical non-null IDs, are valid no-op requests only when the current state matches the requested destination. Validate every non-null version reference as belonging to this relationship before comparison. Authorization and Ended restrictions also apply to requests that would be no-ops; retries do not bypass access checks.

Expected HTTP outcomes follow existing modules: missing acting Organization/invalid input 400; unauthenticated 401; denied capability 403; unknown or out-of-scope resources 404; stale selection/publication or changed withdrawal reason 409; reference-verification Unavailable 503 with an explicit retryable code. Initial version publication returns 201 and Location; matching retry returns 200 with the existing version. Selection/acceptance/withdrawal success returns 200 with current facts. Domain-invalid references and discounts use field-addressable validation problems, not technical errors. Unexpected failures propagate to host error handling.

Create the stable PriceAgreement identity separately before first publication. The server assigns its identity; creation does not publish terms or alter Customer state. Supplier Owner authority and an Active relationship are required. Multiple agreements are allowed. Initial creation input is the required `tradingRelationshipId` in the JSON request body; no commercial terms or new agreement metadata are required in this slice.

Creation requires a case-sensitive `Idempotency-Key` of 1–200 characters, following the existing planned Organization-creation protocol. Scope the key to Person, acting Organization and operation; the normalized request includes TradingRelationshipId. Persist the original 201 status, Location and JSON representation. Same key/request replays that result; same key with a different relationship returns 409. Missing/invalid key returns 400. Different keys intentionally create distinct agreements. Retain successful replay records without expiry initially. Recheck current authority and relationship access before replay; a key never grants access after authorization is lost.

Version lists use publication-ordinal keyset pagination; relationship agreement lists use CreatedAt/AgreementId keysets, default 50 and maximum 100. Query inputs are explicitly validated. Stable error codes include `agreement_selection_conflict`, `agreement_publication_conflict`, `agreement_creation_key_conflict`, `agreement_withdrawal_reason_conflict`, `agreement_relationship_ended` and `agreement_list_verification_unavailable`; HTTP DTOs and these endpoint details remain internal to Agreements.

## Agreements-owned persistence

Own `AgreementsDbContext`, schema `agreements`, migration history, design-time factory and explicit configuration/registration consistent with the existing modules.

| Stored record | Key and constraints |
| --- | --- |
| PriceAgreement | AgreementId PK; immutable TradingRelationshipId; current latest-version pointer and publication ordinal |
| AgreementVersion | UUIDv7 version PK; AgreementId; predecessor reference; unique agreement/publication ordinal; immutable complete terms and publication actor/time |
| VersionCoverage | unique version/list ID; General with explicit discount or Supplier net with no discount |
| VersionGroupDiscount | unique version/exact group code; exact case-sensitive comparison; no trimming or numeric coercion of group codes |
| VersionWithdrawal | one row per version; immutable reason, PersonId and timestamp |
| CustomerAgreementState | TradingRelationshipId PK; nullable ActiveVersionId; last transition sequence |
| AgreementAcceptance | unique relationship/version; immutable first PersonId and timestamp |
| SelectionTransition | unique relationship/sequence; nullable From/To IDs, PersonId and decision timestamp |
| CreationIdempotencyRecord | unique Person/acting Organization/operation/key; normalized relationship request and original status/Location/JSON |

Use same-module keys/constraints to prevent mismatched version/relationship references. External Organization, Person, Trading Relationship and Price List IDs have no cross-module foreign keys. Store percentages as exact tenths (0–1000) after rejecting excessive input precision, not after rounding. Preserve complete system rounding semantics directly in every version. Coverage/type/discount shape, validity intervals and uniqueness require database constraints as well as domain validation where practical.

Concretely, persist the relationship identity on AgreementVersion and enforce its `(AgreementId, TradingRelationshipId)` link to PriceAgreement. Version has a unique `(TradingRelationshipId, VersionId)` candidate key used by acceptance and transition references. CustomerAgreementState's nullable `(TradingRelationshipId, ActiveVersionId)` references the unique acceptance key, enforcing Active-implies-Accepted without an external foreign key. Acceptance need not reference the state row, so a separate acceptance does not create or lock selection state. Group-code uniqueness and comparison use deterministic exact text semantics (PostgreSQL `C` collation), not a case-insensitive database default. Versions retain their full submitted request representation for publication retry comparison.

Published rows have no ordinary update/delete use case. Withdrawal is a separate insert. Version reads and new publication never load all historical versions into the aggregate. No external group registry lookup is involved.

## Concurrent writes and retry mechanics

Agreement creation uses a local Read Committed transaction and reservation-first insert into CreationIdempotencyRecord (`INSERT ... ON CONFLICT DO NOTHING`). The winner inserts the new Agreement and completed replay result before commit. A concurrent matching request waits, then replays the winner; mismatched normalized request conflicts. Rollback removes both reservation and agreement. Reserve an application-generated UUIDv7 before persisting; there is no committed incomplete replay record. This is operation-specific persistence, not generic middleware or a cross-module transaction.

Customer selection:

1. Authenticate, authorize, verify relationship scope/status and resolve non-null version references through owned contracts.
2. Begin a local Read Committed transaction. Create the relationship state row with `INSERT ... ON CONFLICT DO NOTHING` if absent; lock that row for update. This also arbitrates concurrent first activation.
3. Read current Active value under the lock. If equal to To, return success without new acceptance/history. Otherwise, if unequal to From, return conflict without changes. Otherwise perform the transition.
4. For activation, insert first acceptance with conflict-do-nothing, preserving any prior explicit or implicit acceptance. Increment the relationship transition sequence; save From/To, actor and system decision time with the Active replacement; commit together.
5. Do not automatically retry a conflict against a freshly read From. Retry after an uncertain commit uses the original From/To and the same comparison rule. A-to-C-to-A is deliberately accepted as matching From A.

Acceptance uses insert-on-conflict on relationship/version, with the winning acceptance preserved. Standalone acceptance never needs a selection revision and never changes Active. Concurrent acceptance and activation share that uniqueness rule; rollback of activation must also roll back any acceptance first created by that transaction.

Publication locks the PriceAgreement row in a local transaction after authorization and complete reference validation. Compare expected predecessor with latest; if it matches, append a version and update the latest pointer in the same commit. If latest is the immediate successor created from that predecessor, compare the complete normalized request with its persisted request representation; matching means return the existing version. A different payload or a later intervening publication conflicts. Null predecessor identifies first publication. Do not infer retries merely from matching commercial content anywhere in history.

Retain the normalized submitted publication payload alongside its predecessor/version for comparison; coverage/group collection order is irrelevant, duplicate references remain invalid, and group codes remain exact. Compare submitted terms rather than mutable system defaults: a retry returns the original version's locked rounding semantics even if the system rule has since changed. New genuine publications use the current system rule. Do not use an idempotency key for this version-publication protocol.

Withdrawal uses a version-unique insert and readback of the winning immutable fact. Trim surrounding whitespace, require 1–2000 characters and compare the resulting text exactly. Same reason succeeds, changed reason conflicts. Neither path changes selection. Only named expected uniqueness conflicts are translated; unrelated database errors propagate. Do not add blanket retries that can change the meaning of a Customer decision.

Relationship and Organization status checks remain point-in-time without cross-module locks. The accepted race with relationship termination is deliberate. Unavailable lookups never grant authority or fabricate a valid reference. A crash before local commit leaves no partial publication, selection, implicit acceptance or history.

## History and recommendation reads

- Page Customer transitions by relationship-local sequence, default 50, maximum 100; never use UUID generation order as Customer decision order.
- Record decision time inside the serialized transition, using system time; ensure recorded transition times do not decrease within the relationship if the clock moves backwards. Equal timestamps use sequence order.
- An as-of selection query applies transitions at or before the requested timestamp, taking the greatest sequence among eligible transitions; none means no Active version. A sequence-qualified query disambiguates transitions sharing a timestamp. This is history, not an arbitrary exploratory version combination.
- Recommendation takes an evaluated business date and knowledge cutoff. Defaults are the current UTC date and current system time. Consider only versions published by the cutoff; a withdrawal excludes the version only if its timestamp is also at/before the cutoff. ValidTo is inclusive.
- Recommendation is evaluated within one PriceAgreement. Apply latest ValidFrom, then latest PublishedAt, then publication ordinal as a deterministic final tie-break within that agreement. Preserve the existing two primary ordering rules; do not infer Customer selection among separate agreements from recommendation.
- Recommendations do not require acceptance, never activate anything, and return no recommendation when no version qualifies. Exact-version reads preserve withdrawn/out-of-validity availability, with structured warning codes.
- No pricing calculation, projection materialization or retrieval is implemented by this issue. Current selection and exact immutable terms are contracts for later Customer Pricing/Delivery slices.

## Delivery sequence and meaningful checks

Each slice includes its own migrations, endpoint/contract wiring and relevant tests; do not implement a temporary in-memory commercial store.

1. **Scope and authorization contracts:** implement the real Trading Relationships lookup and OrganizationAccess capabilities. Verify wrong party, unrelated membership, Owner/Member distinction, Archived actor, Ended behavior and technical failure handling. Add minimal logical Price List creation/read-only verification in Price Publication, with immutable scope and its own PostgreSQL migration; no import pipeline or Price List versioning is required for this dependency. Until the real verifier is available, publication returns Unavailable.
2. **Stable agreement creation and first/next publication:** deliver separate idempotent creation and immutable complete terms, list coverage, group overrides, preserved rounding and publication read APIs. PostgreSQL tests cover creation replay/mismatched keys/concurrent creation/rollback, multi-list validation failure with no publication, wrong Supplier/Customer/type, zero and 100%, excessive precision, exact codes, duplicates, net-only coverage and publication retry races (matching, different payload, later intervening version and changed system rounding defaults). Verify withdrawal/current selection cannot be changed by publication.
3. **Withdrawal and recommendation:** permanent withdrawal and current/historical recommendation. Test required reason, repeated/concurrent same and different reasons, overlapping/inclusive validity, missing candidate, equal primary sort fields, retroactive publication and withdrawal cutoff. Verify immutable version content remains unchanged.
4. **Customer acceptance and selection:** relationship state, acceptance and transition history in the same module. Real PostgreSQL concurrency tests cover first activation, A-to-B twice, A-to-B versus A-to-C (including versions from different agreements), stale conflict, deactivation, no-op retries and A-to-C-to-A. Inject failure before commit to prove Active, implicit acceptance and transition history roll back together. Verify first acceptance survives explicit/implicit races.
5. **Historical queries and consumer contracts:** exact-version and current/as-of selection readers, paged history and warning representations. Test timestamps tied/backwards, no-op sequence preservation, ended history access and no automatic change from publication/withdrawal/termination. Architecture tests enforce internal handlers/DTOs and contract-only cross-module dependencies. Test real contract wiring as well as isolated domain decisions.

Use the repository's xUnit/FluentAssertions conventions and Testcontainers PostgreSQL fixture pattern; do not replace concurrency/constraint checks with EF InMemory or timing-only sleeps. Coordinate races with test barriers. Apply fresh migrations in integration tests; production migrations remain a separate deployment action. No tests are run during this planning session.

## Issue #13 acceptance coverage

| Acceptance criterion | Planned evidence |
| --- | --- |
| Supplier publication/withdrawal cannot mutate Customer Active | separate local transactions and tables; tests with existing selection |
| Published versions immutable | insert-only version API, separate withdrawal fact; stored-content checks after subsequent actions |
| Recommendation follows validity/PublishedAt/withdrawal | deterministic current/as-of query tests in slice 3 |
| Withdrawn versions usable with warnings | exact-version reads and Active selection tests; warning contract; Ended access restrictions remain separate |
| Active implies Accepted | atomic implicit acceptance and same-module consistency constraints; race/rollback tests |
| Durable activation/deactivation history | same-transaction sequence/state writes; new DbContext reads after commit; rollback/no-op tests |
| Publication and Customer state independent | separate aggregates/local workflows, contract-only dependencies and integration tests |

The refined commercial scope in the map supersedes the older issue wording; updating the GitHub issue is separate from this local planning artifact.
