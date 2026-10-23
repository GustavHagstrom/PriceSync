# Decision map: issue #13 — Agreements

Source: [issue #13](https://github.com/GustavHagstrom/PriceSync/issues/13).
Status: #1 resolved; #2 is the next frontier. Planning only; no implementation. This map captures the agreed scope expansion; the GitHub issue body has not yet been updated.

## Established scope

- Include commercial terms as well as publication, withdrawal, acceptance and activation. Customer Pricing owns price calculation.
- One Price Agreement may cover multiple General Price Lists, and different agreements may reference the same list. References are not ownership or transfers. This supersedes the earlier provisional one-agreement-per-list restriction. The Supplier publishes intended terms; the Customer selects the agreement/list combination. New versions may add/remove lists; older versions retain their coverage and remain usable with applicable validity/withdrawal warnings. Publication never changes Customer Active state.
- Each immutable Agreement Version references logical Price Lists, not fixed list versions. Customer selections remain independent.
- For current pricing, the Customer explicitly selects a Price Agreement per logical General Price List, then uses that agreement's agreement-scoped Active Version and the independently Active Price List Version. Recommendations/warnings are advisory; valid-projection requirements remain in force.
- Issue #13 includes this Customer agreement selection per list, alongside agreement publication and Customer acceptance/activation. Actual price calculation remains outside #13.
- Each covered list has an explicit general discount (possibly zero), with optional Supplier Price Group overrides. Discounts never stack.
- Start with a common system rounding rule. Introduce configuration only for a concrete commercial need, per covered list within the Agreement Version.
- Existing version, recommendation and Customer-selection invariants remain authoritative in [Agreements](Domain/agreements.md) and [Customer Pricing](Domain/customer-pricing.md).

## #1: Ownership and history of Customer agreement selection per list

Blocked by: none
Type: Grilling

### Question

Which module and consistency boundary own the Customer's selected Price Agreement per logical General Price List? What commands, contracts and durable selection history are required for current and historical pricing?

### Answer

Resolved. Canonical rules: [Agreements — Customer agreement selection per list](Domain/agreements.md#customer-agreement-selection-per-list).

- Agreements owns a separate Customer-owned aggregate per Customer + logical General Price List, with one selected Price Agreement or none. Agreement acceptance/activation remains a separate boundary per agreement.
- Select/change/clear never changes other lists or accepts/activates/deactivates versions. Selection is allowed before an Agreement Version is Active and when its Active Version lacks list coverage. Enforce Supplier ownership and the applicable Supplier–Customer relationship; warnings do not bypass authorization or scope.
- Every actual change preserves immutable history: selected agreement (or none), decision time and acting Person. Current state and history commit atomically. Combine with independently preserved version-activation histories for historical selections; this is not retrieval evidence or a globally atomic cross-module snapshot.
- Changes require the current selection revision. Stale requests conflict without state/history mutation, even if the requested value now matches. With the current revision, selecting the same agreement or clearing an empty selection returns existing state without new business history.
- No selected agreement or no Active Agreement Version means no current projection basis, without fallback. Missing list terms produce an invalid projection on evaluation; no invented discounts or retrieval of invalid prices.
- Required capabilities: select/clear using an expected revision; read current selection (including none and revision); read ordered selection history/reconstruct selection as-of. Cross-module consumers use Agreements contracts for current/historical selection and agreement-scoped Active Version, combined with Price Publication's independent version selection. No cross-module transaction or direct schema access is introduced.
- Exact DTO/interface names, HTTP routes/status mapping, initial revision representation, timestamp/order mechanics, authorization capabilities, reference availability, ended-relationship handling and concurrency tests are implementation-design work for #4 (reference contracts also #3). Warning delivery must not couple selection to projection calculation.

## #2: Deterministic rounding rule


Blocked by: none
Type: Grilling

### Question

What precision, rounding method and calculation stage define resolved Customer Prices? How will historical calculations remain reproducible if the system rule changes?

### Answer

Open. A common system rule is the agreed starting point; configurability is not assumed.

## #3: Complete commercial publication contract

Blocked by: #2
Type: Grilling

### Question

Which exact list references, discount values/group identities and rounding semantics constitute a complete publishable Agreement Version? Establish validation and cross-module reference contracts, including handling unavailable Price Publication capabilities, without implementing price calculation in Agreements.

### Answer

Open. List-scoped general discounts and non-stacking group overrides are established. Inspect existing contracts and relevant issue boundaries before asking questions already answered elsewhere. Preserve reproducibility of exact immutable input tuples.

## #4: Implementation-ready vertical slices and verification

Blocked by: #1, #3
Type: Grilling

### Question

How should the agreed behavior be delivered through module-owned contracts/endpoints, PostgreSQL persistence/migrations and meaningful tests? Resolve remaining authorization, retries, concurrency and historical-query decisions against existing repository patterns and every acceptance criterion in issue #13.

### Answer

Open. Keep Supplier publication/withdrawal transactions independent of Customer decisions. Define concrete slices and checks only after the preceding decisions, with no production implementation during decision mapping.

## Beyond the frontier

Continue with #2 at the user's choice; #1 is resolved. Refine #3 and #4 as the frontier advances; split them if investigation exceeds one session. Add tickets only where unresolved decisions remain; do not reopen established domain rules. Stop after resolving the selected ticket in each resume session.
