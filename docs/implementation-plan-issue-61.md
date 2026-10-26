# Implementation plan for issue #61

Status: stage 1 approved and completed on 2026-10-26. Stages 2–8 remain. The user requested stage 1 with `/implement` and `/tdd` under this plan.

Starting point: `issue-61`, created from `Issue-13`, with draft PR #68. Execute the stages sequentially on the same branch. Intermediate results are reviewable commits; keep the PR in draft until all of #61 is complete.

## Goal and references

[Issue #61](https://github.com/GustavHagstrom/PriceSync/issues/61) enables the Supplier to publish complete, immutable Price Agreement Versions and authorized parties to read them. Exact versions must also be available through Agreements-owned consumer contracts.

Read the requirements in this order:

1. Issue #61, including any new comments.
2. [Context](Context.md), [Agreements](Domain/agreements.md) and [Price Publication](Domain/price-publication.md).
3. [Issue #13 delivery design](agreement-delivery-plan-issue-13.md), particularly publication, persistence and retries.
4. ADRs 0001–0005 and existing code patterns.

Use `/implement` for execution and `/tdd` for behavior-driven red–green–refactor. Use `codebase-design` to keep public interfaces small and their implementation cohesive. This plan does not replace the referenced domain rules.

## Existing implementation

- #59 provides logical Price Lists and a real `IPriceListScopeVerifier` with Verified, Invalid and Unavailable results.
- #60 provides stable Agreement identity, create/get/list, authorization and persistence in `agreements`.
- `CreateHandler` demonstrates authorization and a local Read Committed transaction. Do **not** reuse its idempotency-key protocol for version publication.
- Agreements has no version model, latest pointer, publication or version reads. Its production project has no reference to Price Publication Contracts.
- The Agreements test host and PostgreSQL fixture do not yet configure Price Publication. Extend them to test real scope verification.
- `AgreementsBoundaryTests` currently allows only AgreementId in the contracts project; update it for the intentional new contract surface.

## Proposed interfaces for approval

Follow the HTTP shape in the delivery design:

| Operation | Interface | Observable result |
| --- | --- | --- |
| Publish | `POST /api/v1/agreements/{agreementId}/versions` | 201 + Location for a new version; 200 for a matching retry; 409 for a conflict |
| Read exact version | `GET /api/v1/agreements/{agreementId}/versions/{versionId}` | Complete immutable version or a scope-safe 404 |
| List versions | `GET /api/v1/agreements/{agreementId}/versions?pageSize=...&cursor=...` | Complete versions, newest ordinal first, with `items` and `nextCursor` |
| Internal consumer | Agreements-owned exact-version reader, proposed `IAgreementVersionReader.GetAsync(AgreementVersionId, CancellationToken)` | Complete typed facts or NotFound; technical failures must not be disguised as NotFound |

The publication request includes a required but nullable `expectedLatestVersionId`, required `validFrom`, optional `validTo`, complete list coverage with expected types and applicable general discounts, and version-wide group discounts. Explicit null means first publication; an omitted predecessor produces 400. The system supplies the rounding rule.

Version responses and consumer facts include version/agreement/relationship identities, predecessor, ordinal, validity, complete coverage and discounts, complete rounding semantics, PublishedAt and PublishedByPersonId. Use typed IDs and facts, not EF entities. HTTP DTOs remain internal.

The reader is a trusted module contract, not a shortcut around HTTP authorization. HTTP reads check an Active acting Organization and Member authority for either party, including when the relationship has Ended. Distribution and selection of compatible versions belong to the consumer and are outside this issue.

Prioritize tests through HTTP against real PostgreSQL and the real scope verifier, followed by tests through the exact-version contract. Complex pure validation may be tested through a cohesive domain interface if HTTP tests would otherwise become unnecessarily repetitive. Avoid mocking internal handlers or repositories.

## Workflow for each agent session

1. Read this plan, the latest handoff, the current issue and relevant files. Check the branch and working tree; preserve the user's changes.
2. Implement only the next unfinished stage after the user has approved implementation under this plan. A request to revise the plan is not itself implementation approval. Once given, approval remains valid without repeated questions except for actual requirements conflicts or scope changes.
3. Work one behavior at a time: write one test, demonstrate that it fails for the intended reason, implement the smallest solution, run it green, then refactor. Do not write all of a stage's tests first.
4. Run the relevant test class regularly and `dotnet build PriceSync.slnx` after contract/model changes. Run the full test suite once at the end of the stage, after the last code change.
5. Use `/review` against the stage's starting commit, checking both repository standards and the current issue/stage. Address findings and rerun relevant checks.
6. Commit on `issue-61`. Update the handoff below with the commit, completed behaviors, exact test commands/results and next stage. Do not report unexecuted tests as passing.

Update architecture-test contract allowlists in the same stage that introduces a public ID or contract. Test basic rollback and concurrency behavior when a transaction is introduced or changed, rather than deferring those guarantees to a later testing stage.

A Docker-compatible runtime is required for Testcontainers/PostgreSQL 16. If unavailable, document the verification blocker; the stage is not verified complete until its tests have run. Early stages intentionally cover only part of the issue and must not be presented as ready to ship.

## Stage 1 — A narrow first publication

**Scope: one bounded agent session.** Publish the first version with exactly one General Price List and an explicit general discount, without groups. Exact HTTP reads follow in stage 2.

- Extend the test host/fixture with real Price Publication configuration, migrations and isolated reset. Create lists through existing public functionality.
- Add `AgreementVersionId` and immediately update `AgreementsBoundaryTests`. Introduce only version storage, coverage and the latest pointer/ordinal needed by this publication path. Do not add group tables or normalized retry requests before their behaviors in stages 3 and 4 respectively.
- Preserve the complete rounding rule from the start: four decimal places, midpoints away from zero, one final rounding after discount without intermediate rounding, and preserved Price Quantity/Unit of Measure. Introduce no price calculation.
- Implement POST with real Supplier Owner authority, an Active acting Organization, an Active relationship and real verification of the single list. Persist the complete supported terms atomically in a local Read Committed transaction, locking only the Agreement row.
- Validate the supported path immediately: required ValidFrom, inclusive ValidTo not before ValidFrom, an explicit 0–100 discount with at most one decimal place, and correct list scope. Net coverage, multiple lists and nonempty groups are not implemented yet and must be explicitly rejected with field-addressable 400 responses in this draft intermediate state; never ignore or partially publish them. Extend the request shape as the corresponding behaviors are introduced.
- Require an explicit null predecessor for the first version. Until the retry protocol is complete, a repeated first-publication attempt on an already published agreement returns a conflict.
- Add an Agreements-owned version-storage migration. Use same-module scope keys to bind a version to the correct Agreement and relationship, unique ordinals per Agreement and a consistent latest pointer. No external foreign keys or growing history navigations.

**First red–green behavior:** a Supplier Owner publishes terms for an existing General list with an explicit zero discount and receives 201/Location plus the version's complete supported facts in the response.

**Other priority behaviors:** server-generated UUIDv7, ordinal 1 and publication actor/time; unauthorized or unrelated parties cannot publish; an Ended relationship blocks publication; explicit null differs from an omitted predecessor; Invalid and Unavailable produce 400 and explicit retryable 503 respectively, without publication.

**Transaction tests in this session:** two concurrent first attempts produce exactly one 201 and one 409 in this intermediate state; failure before commit rolls back both version and latest. The same first publication can succeed after rollback. Use separate DbContexts and deterministic gates. Narrow persistence assertions may verify the transaction guarantee until exact GET is available.

**Done when:** the narrow POST path, its failures and transaction guarantees work against PostgreSQL with real list verification; architecture tests and existing create/get/list tests are green. Introduce no permissive verification stub or speculative storage. Retry responses with 200 explicitly remain for stage 4.

## Stage 2 — Exact HTTP reads of a published version

**Scope: one smaller agent session.** Introduce `GET /agreements/{agreementId}/versions/{versionId}` over the storage from stage 1.

- Read one version and its coverage without loading the entire history. Return all stored inputs, rounding and publication facts unchanged.
- Check the Active acting Organization and ReadAgreements capability for either Supplier or Customer. Historical reads remain allowed after the relationship has Ended.
- Test Members of both parties, an Archived acting Organization, unauthorized and unrelated parties, an unknown version and an incorrect Agreement/version combination. Follow existing Problem Details patterns and scope-safe 404 behavior.
- Prove POST → new request/DbContext → GET, including that failure before commit leaves no readable version. Retain the earlier persistence-focused transaction tests for their specific guarantee.

**Done when:** exact HTTP reads provide authorized access to durable, complete facts for the publication flow supported so far. Do not introduce a public module reader contract or paged list yet.

## Stage 3 — Commercial rules and complete list verification

**Scope: one normal agent session.** Extend working publication one behavior at a time.

- Retain and extend validity tests from stage 1: required ValidFrom; ValidTo on the same day is allowed, an earlier date is rejected. Testing two overlapping versions belongs in stage 4, where next publication is introduced.
- Require at least one unique logical list. General coverage requires an explicit discount, including zero; net coverage carries no discount and must match the Customer. Net-only and mixed coverage work without published list versions.
- Verify existence, type, Supplier and Customer through the real contract. One invalid reference in a multi-list request rejects the entire publication and leaves latest unchanged.
- Accept exact percentages from 0 to 100 with at most one decimal place. Test 0, 0.1, 99.9 and 100, negatives, values above 100 and 0.01. Reject excessive precision rather than rounding it away.
- Introduce the group-discount model, storage and migration with the first group behavior test; extend exact GET with the new facts. Reject duplicate list IDs and exact duplicate group codes even when discounts match. Preserve case and leading zeros: `A`, `a`, `01` and `1` are distinct. Do not trim codes or convert them to numbers. Codes need not occur in list content.
- Group discounts belong to the complete version, apply across General coverage and represent replacement of the general discount. Build no calculator or group-registry lookup.
- Unavailable returns 503 with stable code `agreement_list_verification_unavailable` and explicit retryable facts, without publication. Use controlled fault injection at the contract for this failure test; other scope tests use the real verifier.
- Add practical DB checks for dates, type/discount shape and percentage scale, plus unique coverage/group keys with exact PostgreSQL `C` collation. Add a migration as needed.

**Done when:** commercial boundaries and all-or-nothing failures are tested through publication/exact GET with field-addressable 400 Problem Details. Persistence tests may directly exercise DB constraints when the constraint itself is the guarantee under test.

## Stage 4 — Next version and deterministic retries

**Scope: one normal agent session; add no new read functionality here.**

- Publish the next complete version from the expected latest version. Update latest and ordinal atomically; the previous version remains unchanged and readable.
- Test two overlapping validity intervals through two publications; both versions remain valid published facts.
- Under the Agreement lock: if the predecessor matches latest, create a successor. Otherwise, only if latest is the immediate successor created from the submitted predecessor may the complete normalized request be compared and replayed with 200. All other cases produce `agreement_publication_conflict`/409.
- Normalization ignores coverage/group collection order and makes percentages numerically unambiguous while preserving exact group codes. Validate duplicates before normalization. Compare all submitted commercial fields and the predecessor; do not deduplicate historical content.
- Introduce normalized request storage and its migration with retry behavior. The saved request must survive process restarts. Separate request comparison from the current system rounding rule.
- Test a changed system rule through a narrow internal source of current system semantics. A retry returns the original rule/ID/time/ordinal; a genuine new publication uses the current rule. Introduce no public Supplier setting or rule registry.
- Recheck authority, relationship and scope on retry. No idempotency keys and no automatic retries against a new predecessor.

**Priority red–green behaviors:** a next version can change/remove coverage without changing older versions; an identical retry after restart returns the same facts; reordered collections match; changed validity/discount/list/group code conflicts; a retry after a later publication conflicts; identical commercial content with the current predecessor creates a genuine new version.

**Transaction tests in this session:** update the first-publication race to produce one 201/one 200 for matching requests; different requests produce 201/409. Test equivalent next-publication races with the same predecessor, and failure before next commit leaving the previous version/latest unchanged. Add tests one behavior at a time as the protocol is built.

**Done when:** sequential first/next/retry cases and changed rounding defaults are covered, including that a conflict neither appends a version nor moves latest.

## Stage 5 — Additional concurrency and transaction guarantees

**Scope: one bounded agent session for high-risk behaviors.**

- Use separate requests/DbContexts and deterministic gates/interceptors following existing transaction tests. Avoid sleep-based race tests.
- Build on first/next races and rollback tests that must already be green from stages 1 and 4. Add rollback of multiple coverage/group rows, a waiting request after the winner rolls back, and a retry after a later intervening publication. Reuse existing tests where they already prove the same behavior.
- Verify that different Agreements are not serialized through a shared lock and that the transaction does not lock Price Publication or Trading Relationships. Use PostgreSQL diagnostics only to verify the locking guarantee.
- Verify the point-in-time race: an operation that observed Active may finish after concurrent termination; a later publication/retry observing Ended is rejected. Historical reads remain available.
- Expected conflicts have stable Problem Details; unrelated DB failures reach host error handling rather than being hidden by blanket catch/retry logic.

**Done when:** concurrency and fault injection prove atomic append/latest updates without partial publication. Any production fixes follow the same one-test-at-a-time loop.

## Stage 6 — Exact-version consumer contract

**Scope: one normal agent session.** Build on stable publication and exact HTTP reads.

- Introduce the Agreements-owned reader and typed immutable results from the interface proposal. Read one exact version with complete commercial inputs and metadata without loading all history.
- Update architecture-test contract allowlists as the reader and result types are introduced; the ID change must already have been handled in stage 1.
- Test real DI wiring, an unknown version, and a consumer's ability to reconstruct every unchanged input for an old version after a new publication. Technical failures must not become NotFound.

**Done when:** HTTP and the module reader expose the same complete version facts without external implementation or persistence dependencies. Pagination belongs in the next session.

## Stage 7 — Paged version reads and list-contract decision

**Scope: one bounded agent session.**

**Open requirement interpretation before implementing this stage:** #61 says “Exact-version and paged version-list endpoints/consumer contracts”, while its introduction explicitly mentions only an exact-version consumer contract. Check the current issue and any clarifications; if unresolved, the user needs to decide whether paged module reads are included. Other stages can proceed independently. Record the outcome in the handoff before implementing the list interface; neither exclude nor introduce it as an already settled requirement.

- Implement the version-list HTTP endpoint with descending ordinal keyset pagination, default 50 and maximum 100. Fetch at most pageSize+1 versions and only their child rows; never fetch the entire history.
- Make the cursor opaque, validated and bound to the Agreement; invalid cursor/pageSize produces field-addressable 400. A later publication must not duplicate or shift already traversed versions on continuation pages.
- Version lists and exact HTTP GET follow existing agreement-read authority. An incorrect Agreement/version combination and another party's resources produce scope-safe 404 responses.
- Update architecture-test contract allowlists as needed and preserve contract-only cross-module dependencies.
- If the decision includes a paged module contract: introduce its typed query/results, DI wiring and tests in this session, using the same complete facts, ordinal ordering and page limits as HTTP. Expose no EF entities or HTTP DTOs in the contract.

**Priority red–green behaviors:** a consumer can reconstruct unchanged inputs for an old version after a new publication; an unknown version returns NotFound; Members of both parties can list after Ended; empty list; 50/100 limits and invalid values; last page; continuation during a new publication; cursor from the wrong Agreement; no missing or repeated versions.

**Done when:** the list-contract scope has a documented decision, the HTTP list and any module contract follow that decision, all pages use bounded queries and modules remain isolated.

## Stage 8 — Final requirement verification, documentation and review

**Scope: one shorter separate agent session reserved for integration and completion.**

- Map every acceptance criterion in #61 to named tests. Add a test only for an identified gap.
- Verify there are no edit/delete/republish commands, older versions remain unchanged and complete versions cannot acquire mixed child rows during rollback.
- Verify no Customer selections/acceptances, Customer Price Projections, calculations, Price Publication writes or external transactions have been introduced.
- Verify that the migration chain from #60 applies alongside all module migrations and that same-module constraints hold. Retain migration history from earlier stages.
- Update README with requests/responses, error codes, first/next/retry behavior, dates, pagination and consumer contracts. Change domain documents/ADRs only for an actual new decision requiring documentation.
- Run final `/review` across the whole diff against `Issue-13`, address findings and document remaining limitations. PR #68 must describe the finished functionality and verification.

**Final checks:**

```powershell
dotnet build PriceSync.slnx
dotnet test PriceSync.slnx
```

For focused checks during stages:

```powershell
dotnet test tests/PriceSync.Modules.Agreements.IntegrationTests --filter FullyQualifiedName~<TestClass>
dotnet test tests/PriceSync.ArchitectureTests
dotnet test tests/PriceSync.Modules.PricePublication.IntegrationTests
```

**Done when:** all criteria have test evidence, the build and full suite are green, review findings are addressed and documentation/PR match the final result. Marking the PR ready or merging remains a separate decision.

## Requirement coverage and dependencies

| Requirement area in #61 | Primary stage | Final evidence |
| --- | --- | --- |
| Supplier Owner, Active Organization/relationship, Member reads | 1–2 | 5, 8 |
| Identity, ordinal, publication facts, complete immutable versions | 1, 3–4 | 6, 8 |
| Validity, General/net, precision, duplicates, exact codes | 1, 3–4 | 8 |
| Real all-or-nothing scope verification and retryable 503 | 1, 3 | 8 |
| Complete preserved rounding, no price calculation | 1, 4 | 6, 8 |
| Expected predecessor, locking, atomic append/latest, retries | 1, 4–5 | 8 |
| Changed system defaults and durable request comparison | 4 | 8 |
| Owned migrations and same-module constraints | 1, 3–5 | 8 |
| Exact GET, consumer contract, complete paged list | 2, 6–7 | 8 |
| Concurrency, rollback and point-in-time status | 1, 4–5 | 8 |
| No acceptance/activation/projection or external writes | All | 8 |

Order: **1 → 2 → 3 → 4 → 5 → 6 → 7 → 8**. Start each stage from the previous green commit. If one session is insufficient, stop at a green vertical slice, commit and document the exact remaining behaviors; the next session finishes that stage before advancing. Do not hand off a failing test suite. Stages are scope budgets, not guarantees of session duration.

## Handoff between sessions

Update this section at the end of each stage:

- Last completed stage: **stage 1 — A narrow first publication**.
- Implementation commit: `2238a7e` on `issue-61`; stage starting point/review baseline: `08afd4b1ecf28baa62ee12f503db523f4c8230ae`.
- Approval: the user explicitly requested stage 1 under this plan with `/implement` and `/tdd` on 2026-10-26. The proposed publication interface and priority behaviors were used; no repeated approval question was needed.
- Completed behaviors: first publication with one existing General list, including explicit zero discount; UUIDv7 identity, ordinal 1, actor/time, validity and preserved complete rounding facts; Supplier Owner/Active acting Organization/Active relationship checks; real Price Publication scope verification; field-addressable rejection of invalid or unsupported terms; retryable verification 503 without publication; repeated first attempt returns 409. The Agreements-owned migration binds version scope and the latest identity/ordinal with same-module constraints. No group or normalized retry storage was introduced.
- Transaction evidence: deterministic separate-request/DbContext race produces one 201 and one 409 at Read Committed while the second waits on the Agreement lock. Injected failure immediately before commit rolls back version, coverage and latest; the same first publication then succeeds. Durable terms are checked through a fresh DbContext until stage 2 introduces exact GET.
- Concrete JSON shape for the supported request:

  ```json
  {
    "expectedLatestVersionId": null,
    "validFrom": "2026-10-26",
    "validTo": null,
    "coverage": [{ "priceListId": "<logical-list-uuid>", "type": "general", "generalDiscount": 0 }],
    "groupDiscounts": []
  }
  ```

  `expectedLatestVersionId` must be explicitly null for this stage; omission is a field-addressable 400. Nonempty groups, net coverage and multiple lists are explicitly rejected. A new publication returns 201/Location with supported complete facts. Exact version GET and matching-retry 200 remain for later stages. The source feature directory is `Features/PublishVersion` because the repository's Visual Studio ignore rule excludes directories named `publish`.
- Checks executed after the last production change:
  - `dotnet build PriceSync.slnx -m:1 --no-restore --nologo -v quiet` — passed, zero warnings/errors.
  - `dotnet test PriceSync.slnx -m:1 --no-restore --nologo -v minimal` — **389 passed**, zero failed/skipped: Architecture 29, Agreements integration 71, OrganizationAccess integration 56/unit 65, PricePublication integration 33, TradingRelationships integration 115/unit 20.
  - Focused publication tests were run in red–green cycles; Agreements integration and architecture tests were also run separately. PostgreSQL 16 Testcontainers applied fresh module migrations with the real list verifier. Docker initially required startup; no verification blocker remains.
  - `/review` used parallel Standards and Spec reviewers against the stage baseline: zero remaining findings on either axis. Architecture testing caught the generated migration's public visibility; it was made internal before the final build/full suite.
  - `git diff --cached --check` — passed before the implementation commit.
- Next step: stage 2 — add authorized exact HTTP version reads over this storage, without introducing consumer contracts or pagination yet. Continue on `issue-61`; do not repeat stage 1.
- Open decisions/blockers: no stage 1 blockers. Resolve the paged module-contract scope before stage 7. PR #68 remains draft; commits are local and have not been pushed in this session.

Starter prompt for the next session:

> Use /implement and /tdd for the next unfinished stage in docs/implementation-plan-issue-61.md. Read the current issue #61 and handoff first. Check that the user has approved implementation under this plan; do not infer approval from this template. Work on issue-61 and complete only that stage, one behavior at a time. Run the prescribed checks, use /review, commit and update the handoff. Follow approved interfaces and test priorities. Do not advance to the next stage in the same session.
