# Decision map: issue #13 — Agreements

Source: [issue #13](https://github.com/GustavHagstrom/PriceSync/issues/13).
Status: all tickets resolved; planning complete on 2026-10-25. No implementation. The relationship-wide activation decision supersedes the earlier per-list agreement selection design. The GitHub issue body has not yet been updated to reflect the refined scope.

## Established scope

- Include commercial terms as well as publication, withdrawal, acceptance and activation. Customer Pricing owns price calculation.
- One Price Agreement may cover General Price Lists and Supplier-published Customer-Specific Net Price Lists. Different agreements may reference the same list, but the Customer activates only one complete accepted Agreement Version at a time within the Supplier–Customer Trading Relationship. Commercial terms are used together; there is no Customer agreement selection per list.
- Each immutable Agreement Version references logical Price Lists, not fixed list versions. Active Price List Versions remain independently selected per logical list.
- New versions may add/remove lists; older versions retain their coverage. Publication never changes Customer Active state. Missing list terms in the Active Agreement Version produce an invalid projection on evaluation, without fallback to another agreement.
- Issue #13 includes commercial publication and relationship-wide Customer acceptance/activation. Actual price calculation remains outside #13. Per-list agreement selection aggregates, commands and history are removed from scope.
- Each covered General Price List has an explicit general discount (possibly zero). Supplier Price Groups are identified within the Supplier across lists; optional group discounts belong to the complete Agreement Version and apply across all its covered General Price Lists. A matching group discount replaces the list's general discount. Discounts never stack.
- Start with a common system rounding rule. Introduce configuration only for a concrete commercial need, per covered list within the Agreement Version.
- Existing version, recommendation and Customer-selection invariants remain authoritative in [Agreements](Domain/agreements.md) and [Customer Pricing](Domain/customer-pricing.md).

## #1: One Active Agreement Version per Trading Relationship

Blocked by: none
Type: Grilling

### Question

What is the Customer activation boundary, and may different lists use different agreements for current pricing?

### Answer

Resolved and corrected on 2026-10-24. Canonical rules: [Agreements — Relationship-wide Customer activation](Domain/agreements.md#relationship-wide-customer-activation).

- At most one accepted Agreement Version across all Price Agreements is Active within one Supplier–Customer Trading Relationship. Activating another version replaces the previous Active version in the same Customer decision. No Active version remains valid Customer state.
- The Active version supplies its complete coverage and terms together. The Customer cannot mix agreements per list. This deliberately limits the initial model and avoids independently managed agreement/list pairs.
- Agreements owns relationship-wide Customer activation and immutable activation/deactivation history, including switches between agreements. Price Publication independently owns Active Price List Versions and their history.
- Activation remains separate from projection validation. Missing activation means no current projection basis; missing list terms produce an invalid projection on evaluation. No fallback or invalid price retrieval is allowed.
- This supersedes the previous per-list selection answer, including its separate aggregate, commands, revision rules and history. Those mechanisms are removed from scope.
- Concrete acceptance-state storage, authorization, concurrency/retries, atomic replacement, historical ordering and contracts are implementation-design work for #4. Preserve the relationship-wide single-Active invariant without coupling Supplier publication or Price List activation into that transaction.

## #2: Deterministic rounding rule


Blocked by: none
Type: Grilling

### Question

What precision, rounding method and calculation stage define resolved Customer Prices? How will historical calculations remain reproducible if the system rule changes?

### Answer

Resolved on 2026-10-24. A common system rule is the agreed starting point; configurability is not assumed.

- Decided on 2026-10-24: calculated Customer Prices use four decimal places for the stated Price Quantity and Unit of Measure. Users may round more coarsely downstream, outside PriceSync.
- Midpoints round away from zero; for example, `1.23445` becomes `1.2345`.
- Calculate the applicable discount without intermediate rounding, then round the resulting Customer Price once. Preserve the source Price Quantity and Unit of Measure; do not normalize to a price per single unit before rounding.
- Each published Agreement Version locks the system rounding rule in effect at publication, including precision, midpoint method and calculation stage. Later system-rule changes apply to new Agreement Versions only; historical versions retain their original semantics without a user setting or an additional projection-identity component.
- Customer Pricing owns calculation; Agreements publishes the immutable rounding semantics. Concrete representation and reference validation belong to #3 and #4.

## #3: Complete commercial publication contract

Blocked by: #2, #5
Type: Grilling

### Question

Which exact list references, discount values/group identities and rounding semantics constitute a complete publishable Agreement Version? Establish validation and cross-module reference contracts, including handling unavailable Price Publication capabilities, without implementing price calculation in Agreements.

### Answer

Resolved on 2026-10-24. List-scoped general discounts and Agreement-wide non-stacking group overrides are established. Issue #13 has been read; Agreements and Price Publication currently have no implemented commercial contracts.

- General and Supplier Price Group discounts must be between 0 and 100 percent inclusive. Zero leaves the List Price unchanged; 100 percent yields zero. Negative discounts (markups) and discounts above 100 percent are rejected.
- Both discount types allow at most one decimal place in the percentage value (for example, `12.5%`). Values requiring greater precision are rejected rather than rounded silently.
- Supplier group codes are text and match exactly, including case and leading zeros: `0410` differs from `410`, and `a` differs from `A`. Any normalization must be explicit during import before publication; evaluation does not silently normalize published codes.
- Each logical Price List may occur once in an Agreement Version's coverage, and each exact Supplier group code may occur once in its group discounts. Publication rejects duplicate references/codes even when discount values are identical; no silent merging or last-write-wins behavior.
- Each article entry in a Price List Version has at most one Supplier Price Group code. For General Price Lists, an absent group or absent matching group discount uses the list's explicit general discount; no multi-group precedence is needed. Price Publication owns validation and immutable preservation of entry group membership.
- Publication is all-or-nothing: every referenced logical list must be verified through Price Publication's owned contract for existence, allowed type, Supplier and applicable Customer scope. If verification is unavailable (including an unimplemented capability), publish nothing and return an explicitly retryable/unavailable outcome, distinct from an invalid-reference validation result. Never substitute permissive verification or publish partial coverage. Concrete contract/result shapes remain #4 work.
- Supplier Price Group identity is Supplier-scoped, using the Supplier's group code across logical Price Lists. A group discount belongs to the Agreement Version and applies to that group in every covered General Price List. This supersedes list-scoped group discounts; explicit general discounts remain list-scoped. No separately administered group registry is assumed by this decision.
- Support general discounts alone, group discounts with an explicit zero general discount per covered General Price List, or their combination. A matching group discount always replaces the covered list's general discount, even if the group discount is lower or zero. Without a matching group discount, use the list's explicit general discount. No stacking or automatic choice of the highest discount.
- Group discounts may reference Supplier group codes not yet used by articles in the covered Price Lists. Their absence from current list content does not block publication; they take effect when evaluating a selected Price List Version containing matching articles. No mutable group registry lookup may reinterpret historical matching.
- Covered logical Price Lists must already exist, be General Price Lists or Supplier-published Customer-Specific Net Price Lists and belong to the Supplier (and, for net lists, the agreement Customer), but need not have a published version. Publishing prices later in an already-covered logical list does not require a new Agreement Version or renewed acceptance; Customer Price List activation remains separate. Adding a newly created logical list requires a new Agreement Version and Customer activation of that version. This coverage workflow is the current decision, to be evaluated through actual usage; it does not introduce automatic expansion of published coverage.
- A published Agreement Version must cover at least one logical Price List. General and Supplier-published Customer-Specific Net Price Lists both count toward coverage; an Agreement may cover only net lists.
- Revised during #4 on 2026-10-24: owning Organization, type, currency, market and applicable Customer are immutable from logical list creation. Name remains mutable; changing scope requires a new logical list. This supersedes publication-triggered scope locking.
- Each Agreement Version stores its complete rounding semantics directly: decimal precision, midpoint method and calculation stage. Initially these are four decimals, midpoints away from zero, and one final rounding after discount calculation without intermediate rounding. Do not resolve historical behavior through mutable settings or require a separate rounding-rule registry.
- Publication contract: agreement/Trading Relationship identity, required ValidFrom, optional ValidTo, complete unique logical-list coverage (explicit general discount for each General list; explicit net-priced coverage without discounts for Supplier net lists), unique Supplier group-code discounts, and system-supplied immutable rounding semantics. Server-assigned version identity and publication metadata accompany the published fact. Price Publication owns immutable list scope and its verification contract; Agreements owns terms and validation; Customer Pricing consumes exact immutable inputs. Customer acceptance/activation is never changed by publication. Concrete DTOs, endpoint shapes, storage and read-only scope verification belong to #4.

## #4: Implementation-ready vertical slices and verification

Blocked by: #1, #3
Type: Grilling

### Question

How should the agreed behavior be delivered through module-owned contracts/endpoints, PostgreSQL persistence/migrations and meaningful tests? Resolve remaining authorization, retries, concurrency and historical-query decisions against existing repository patterns and every acceptance criterion in issue #13.

### Answer

Resolved on 2026-10-25. Planning only; no production implementation.

Delivery design: [contracts, persistence, concurrency, slices and acceptance checks](agreement-delivery-plan-issue-13.md). This asset completes concrete technical choices against existing repository patterns; the business decisions are recorded below.

- Decided and revised on 2026-10-24: activation requests specify the expected currently Active Agreement Version (From) and desired Agreement Version (To), scoped to the complete relationship-wide `CustomerAgreementState`, including switches between different Price Agreements. This supersedes the earlier caller-supplied revision/concurrency-token design. For A-to-B: if A is Active, atomically replace it with B; if B is already Active, succeed without mutation or additional history, including when another Person performed the same switch; if another version C is Active, return a conflict requiring reload. The state comparison and transition must be concurrency-safe, not a read followed by an unconditional write. Persist a successful replacement, any implicit acceptance and immutable decision history in one transaction. Customer activation requires no idempotency key and does not replay a stored original response.
- Working decision on 2026-10-24 (tentative user agreement): compare the currently Active version identity, not intervening decision history. An A-to-B request may succeed after A-to-C-to-A because A is Active again. Preserve every actual intervening switch in history; no caller-supplied revision is required to detect that sequence.
- Decided on 2026-10-24: use the same From/To comparison for initial activation and deactivation, with an explicit no-Active value. No-Active-to-B activates B only when no version is Active, succeeds without mutation/history if B is already Active, and conflicts if another version is Active. A-to-no-Active deactivates A, succeeds without mutation/history if nothing is Active, and conflicts if another version is Active. The delivery design represents no-Active as an explicit JSON null. Successful deactivation preserves prior acceptances and records the actual transition atomically with current state.
- Decided on 2026-10-24: separate acceptance is idempotent without an idempotency key. Preserve the first acceptance of each Agreement Version within the relationship, including its timestamp and acting Person. Repeated or concurrent acceptance succeeds without duplicates or overwriting that first fact and never changes the Active version. Explicit acceptance and implicit acceptance during activation must share the same uniqueness guarantee; activation must preserve any existing first acceptance.
- Decided on 2026-10-24: Supplier Owners may publish and withdraw Agreement Versions; Customer Owners may accept, activate and deactivate. Active Members (including Owners) of either party may read agreements within that relationship. The acting Organization must be Active. OrganizationAccess owns capability/role evaluation through purpose-built capabilities; Agreements verifies the acting Organization's Supplier/Customer scope through Trading Relationships-owned contracts. Membership in an unrelated Organization grants no agreement access. Ended-relationship behavior is defined below.
- Decided on 2026-10-24: after a Trading Relationship has Ended, either party may still read agreements and history under the established authorization rules. Reject new Agreement Version publication, acceptance and activation/reactivation. Supplier withdrawal and Customer deactivation remain allowed. Ending the relationship never automatically changes the Active Agreement Version or rewrites acceptance/activation history. Future Supplier-backed distribution is stopped by the relationship lifecycle, independently of preserved selections. Concurrent relationship termination is addressed by the point-in-time decision below.
- Decided on 2026-10-24: immutable scope from creation supersedes the #3 scope-lock design. Agreement publication verifies logical list references read-only through Price Publication contracts and writes its own publication facts. No shared transaction, reservation or scope-lock recovery workflow is required for this purpose. Verification remains mandatory and all-or-nothing; unavailable is distinct from invalid. Customer Pricing separately evaluates exact immutable version combinations.
- Confirmed on 2026-10-24: historical recommendation distinguishes the evaluated business date from the knowledge cutoff. Include only versions published by that cutoff and only withdrawals recorded by that cutoff, then apply the established validity/ValidFrom/PublishedAt rules. A later publication with backdated validity or a later withdrawal must not rewrite what was recommended with the information available earlier. The delivery design defines query parameters and deterministic ordering.
- Decided on 2026-10-24: a published Agreement Version cannot be republished or modified; changed terms require a new immutable version within the same Price Agreement. Publication supplies the latest Agreement Version identity the Supplier is starting from (an explicit no-version value for first publication). If that predecessor is still latest, publish the next complete version atomically. If the same request has already created the next version, return that existing version without another publication fact or timestamp; if a different publication has intervened, return conflict. Matching must cover the complete publication request, not merely the predecessor. The delivery design defines request comparison, concurrent arbitration and persisted retry metadata; this is not unrestricted content-based deduplication across historical versions.
- Decided on 2026-10-24: the first withdrawal permanently records its reason, timestamp and acting Person. Repeated or concurrent withdrawal with the same reason succeeds without changing that fact or creating duplicate history. A different reason conflicts rather than overwriting the original. The delivery design defines reason validation and comparison.
- Decided on 2026-10-25: Trading Relationship status is checked point-in-time through its owned contract, without a shared transaction or lock with Agreements. An operation that observed Active may finish after concurrent termination; operations observing Ended obey the restrictions above. This explicitly accepts that bounded race: a late publication/acceptance/activation does not reopen the relationship or authorize continued Supplier-backed distribution. Preserve the resulting facts and history. This resolves the concurrent-termination question above; technical lookup failure must never be interpreted as Active.
- Decided on 2026-10-25: Customer activation and deactivation take effect when the system performs the decision. No caller-supplied backdating or scheduled activation/deactivation is supported. Supplier validity dates remain distinct from actual Customer selection history. Actual transitions record system-assigned decision time; successful no-op retries do not create a new decision time or history fact.
- Decided on 2026-10-25: actual activation/deactivation transitions receive a monotonically increasing sequence number scoped to the Trading Relationship, assigned atomically with current-state replacement and history. Preserve decision timestamp, acting Person and From/To references alongside it. The sequence provides unambiguous transition order when timestamps tie; successful no-ops consume no history sequence. It is internal history ordering, not a caller-supplied concurrency token. Cross-module histories do not acquire a global sequence.
- Decided on 2026-10-25: create the stable PriceAgreement identity within a Trading Relationship as a separate Supplier operation before publishing its first immutable version. A newly created agreement may have no published versions. Multiple agreements may exist within one relationship; creating an agreement does not publish terms or change Customer acceptance/Active state. Creation retry handling is decided below.
- Decided on 2026-10-25: only creation of a new stable agreement requires an idempotency key. Matching retries return the same created agreement instead of creating another. Activation and version publication retain their key-free protocols above. The delivery design scopes and persists creation replay using the repository's planned reservation-first pattern.
- Technical design completed: module-owned routes and contracts, PostgreSQL schema/constraints and local transaction algorithms, creation/publication/withdrawal replay rules, historical queries, and five ordered vertical slices with acceptance-criterion coverage are in the linked asset. Selection uses a locked relationship state row, atomic first acceptance and sequence/history persistence; publication locks only the agreement row. Real immutable list-scope verification is a prerequisite, never a permissive stub. No further investigation tickets are required for the agreed scope. Supplier publication/withdrawal remains independent of Customer decisions.

## #5: Net price lists as part of an Agreement

Blocked by: none
Type: Grilling

### Question

Should Supplier-published Customer-Specific Net Price Lists be referenced by an Agreement Version, explicitly marked as net-priced, to reflect real commercial agreements and provide a recognizable Customer workflow even though no discount calculation is needed? Clarify which net-list types are meant, whether coverage is optional or required, and what acceptance/activation means for availability and current use.

### Answer

Resolved on 2026-10-24. Raised during #3. Real agreements name net price lists and state that they are net-priced; membership has business and usability value independent of calculation.

- Decided: Supplier-published Customer-Specific Net Price Lists require coverage by the relationship-wide Active Agreement Version for current use. Their Price List Versions remain independently selected. Net Prices are used directly without discounts or calculation rounding. This supersedes the General-only coverage and independent Supplier net-list current-use rules elsewhere in this map and the domain docs.
- Customer-Managed External Net Price Lists remain outside this change and independent of Agreements.
- Supplier-published net-list retrieval facts identify both the exact Price List Version and the exact Agreement Version that supplied coverage, even though the Agreement does not alter the Net Price amount. This preserves the commercial context of actual use across later agreement switches.
- Working decision, to validate through usage: switching to an Agreement Version that omits a Supplier-published net list makes evaluation of that selected combination invalid, even if its Price List Version remains selected. Do not deactivate the list automatically or fall back to the previous agreement. Historical source versions and retrieval facts remain preserved. The user's confidence in this workflow is tentative.
- Supplier-published net projections use the exact Customer + Price List Version + Agreement Version tuple. Explicit historical/exploratory views require a selected Agreement Version covering the list; the combination need not have been Active together. General and Supplier-published net lists count toward minimum coverage; covered net lists must match the agreement Supplier and Customer. Net-only Agreements are valid. Net coverage is explicit and carries no discount terms. This reconciles the earlier General-only scope; #3 continues with the remaining publication-contract decisions.

## Beyond the frontier

The agreed scope now has an implementation-ready path in the #4 delivery design. No unresolved investigation tickets remain. Stop here: implementation, GitHub issue updates and creation of implementation issues are separate work. Tentative product choices recorded above remain explicit usage-validation points rather than blockers to planning.
