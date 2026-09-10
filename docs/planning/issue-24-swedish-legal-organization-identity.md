# Issue 24: Swedish Legal Organization Identity

**Status:** Implemented

**Authority:** This document records the delivery decisions for issue #24 so the implementation can proceed without reopening boundary questions. Current authoritative behavior belongs in domain documentation, ADRs, tests, and implemented code.

Planning base for [issue #24](https://github.com/GustavHagstrom/PriceSync/issues/24).

## Goal

Model Legal Organization Identity for the initial Organization & Access domain slice. The identity is the combination of registration country and national organization number, limited to Swedish identities for this issue.

## Working Decisions

- Registration country is represented by a domain value object in `PriceSync.Modules.OrganizationAccess`.
- Registration country input is normalized by trimming and uppercasing before validation and comparison.
- Issue #24 accepts only Swedish registration country `SE`; other countries are rejected by domain validation.
- Swedish national organization number validation and normalization belongs in an internal `SwedishOrganizationNumber` value object.
- Swedish organization-number format validation includes the rule that the third digit is at least `2`.
- Swedish organization-number input accepts only ten digits without separators or the common `######-####` hyphen form.
- Swedish organization-number input rejects spaces, other separators, twelve-digit variants, and hyphens in any other position.
- Issue #24 excludes sole proprietorship identity based on a natural person's personal identity number. That requires a separate future identity-modeling decision because it is not a Swedish legal-person organization number.
- `LegalOrganizationIdentity` is composed from a `RegistrationCountry` and a normalized national organization number.
- `LegalOrganizationIdentity.NationalOrganizationNumber` exposes the canonical representation as exactly ten digits without separators.
- Domain value objects throw standard argument exceptions for invalid input in issue #24. Application/API error mapping is deferred to later slices.
- Domain types created in issue #24 remain internal to `PriceSync.Modules.OrganizationAccess`.
- `PriceSync.Modules.OrganizationAccess.UnitTests` receives access to internal Organization & Access domain types through `InternalsVisibleTo`.

## Open Decisions

None for the planned issue #24 implementation scope. New decisions should be opened only if implementation reveals a concrete contradiction or missing requirement.

## Exit Criteria

- Legal Organization Identity cannot be created without a registration country and national organization number.
- Only Swedish registration country `SE` is supported in this slice; unsupported countries are rejected by domain validation.
- Swedish organization numbers are accepted with or without a hyphen when otherwise valid.
- Swedish organization numbers are validated for format and check digit.
- Swedish organization numbers are normalized to ten digits for storage, equality, and uniqueness comparison.
- Registration country is normalized consistently for identity comparison.
- Focused database-free unit tests cover valid input, invalid input, unsupported countries, hyphen handling, check digit validation, and canonical equality.
- The solution builds and all tests pass.

## Session Log

- 2026-09-10: Created implementation branch `codex/issue-24-from-pr-a-spec` from PR #22 branch `codex/issue-11-pr-a-spec`.
- 2026-09-10: Decided to model registration country as its own domain value object that normalizes trim plus uppercase and rejects unsupported countries.
- 2026-09-10: Decided to model Swedish national organization numbers with an internal `SwedishOrganizationNumber` value object.
- 2026-09-10: Decided that issue #24 domain value objects throw standard argument exceptions for invalid input instead of introducing a result/error abstraction.
- 2026-09-10: Decided that the canonical national organization number exposed by `LegalOrganizationIdentity` is always ten digits without separators.
- 2026-09-10: Decided to enforce the Swedish legal-person organization-number format rule that the third digit is at least `2`, and to exclude sole proprietorship identity based on a natural person's personal identity number from issue #24.
- 2026-09-10: Decided that Swedish organization-number input accepts only ten digits without separators or the `######-####` hyphen form.
- 2026-09-10: Decided that issue #24 domain types remain internal to `PriceSync.Modules.OrganizationAccess`, with unit-test access granted through `InternalsVisibleTo`.
- 2026-09-10: Completed grilling and selected a single implementation slice: internal domain value objects, database-free unit tests, build/test, review, and commit.
- 2026-09-10: Implemented the selected issue #24 slice.

## Implementation Outcome

Implemented on 2026-09-10 in branch `codex/issue-24-from-pr-a-spec`.

No deviations from the plan.
