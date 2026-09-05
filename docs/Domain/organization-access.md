# Organization & Access

## Purpose

Own the identity of business parties and people in PriceSync and the rules for who may act for each Organization. Protocol-level authentication remains outside the domain area.

## Aggregate model

### Organization

`Organization` is the aggregate root for one legal business party. It owns the Organization's Legal Organization Identity and Organization-scoped display/state that must remain consistent for that business party.

Its transactional boundary exists because changes to one Organization's legal identity and organization-level state must be validated together without coupling unrelated Organizations or downstream pricing contexts.

### Organization Membership

`OrganizationMembership` is a separate aggregate root for one person's authority within one Organization. It owns that membership's assigned roles/permissions.

Its transactional boundary exists because membership status and assigned authority must change consistently for one person/Organization pair, while the same person may hold unrelated memberships in other Organizations.

## Commands / decisions

This area is responsible for decisions such as:

- establish or update an Organization's permitted identity/display state
- create, change, or end an Organization Membership
- assign or remove Organization-scoped roles/permissions

## Business facts

This area produces facts about Organization identity/state and membership/authorization changes. Authentication-provider tokens and sessions are not domain facts here.

## Core invariants

- A Legal Organization Identity is the combination of registration country and national organization number.
- A Legal Organization Identity identifies at most one Organization globally in PriceSync.
- The initial implementation supports Swedish Legal Organization Identities only. It accepts the national organization number with or without a hyphen, validates its format and check digit, and normalizes it to ten digits before storage and identity comparison.
- An Organization's Legal Organization Identity does not change after the Organization is created in the initial model.
- An Organization has a required display name that is mutable, non-unique presentation data and must never be used to establish its identity.
- An Organization is either Active or Archived in the initial model. It records when it was created; archiving records when it occurred and closes the Organization for new use without deleting its identity, memberships, or history.
- Archiving an Organization does not automatically end its Organization Memberships. An active member may still read the Organization's basic details and see it in their Organization list, but all commands and other Organization-scoped business operations are denied while the Organization remains Archived. Administrative recovery or support operations require separately defined authority.
- Archived is terminal for Organization in the initial model.
- Organization & Access owns each Person's stable PriceSync identity. A Person may be linked to one or more external identity-provider accounts over time without those accounts defining the Person.
- A Person acts for an Organization through an Organization Membership.
- Organization Membership references a Person by that Person's PriceSync identity. Until Person provisioning and identity-provider linking are implemented, the initial Organization use cases consume an already provisioned, trusted internal PersonId through ICurrentPerson and do not persist a Person record.
- Organization, Person, and Organization Membership identities are distinct concepts and must not be treated as interchangeable raw identifiers.
- An Organization Membership is either Active or Ended in the initial model. It records when it started; ending records when it occurred and removes current authority without deleting its history.
- Ended is terminal for Organization Membership in the initial model; later re-entry creates a new Organization Membership.
- A Person may have at most one active Organization Membership for the same Organization.
- Roles and permissions are scoped to one membership and therefore one Organization.
- The initial Organization Roles are Owner and Member.
- An active Organization Membership has a non-empty set of explicitly assigned Organization Roles. Independent roles may be combined; authority implied by another role does not require a redundant explicit role assignment.
- Owner includes Member authority in the initial role model.
- Every active Organization must have at least one active Owner after Organization creation completes.
- Creating an Organization establishes its first active Owner membership as part of the same business operation.
- Future use cases that end memberships or remove Owner roles must coordinate the affected aggregates and enforce the last-Owner rule under concurrency; the concrete concurrency strategy is chosen with the first such mutation.
- The same Person may have different roles in different Organizations.
- Authentication-provider details are outside this domain area's business model.

## References from other areas

Other domain areas reference Organizations by `OrganizationId`. They do not own or duplicate Organization identity, membership, or authorization state.

Supplier and Customer are contextual roles interpreted by other domain areas, not Organization subtypes owned here.
