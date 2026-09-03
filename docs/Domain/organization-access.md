# Organization & Access

## Purpose

Own the identity of business parties in PriceSync and the rules for who may act for them.

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
- A person acts for an Organization through an Organization Membership.
- Roles and permissions are scoped to one membership and therefore one Organization.
- The same person may have different roles in different Organizations.
- Authentication-provider details are outside this domain area's business model.

## References from other areas

Other domain areas reference Organizations by `OrganizationId`. They do not own or duplicate Organization identity, membership, or authorization state.

Supplier and Customer are contextual roles interpreted by other domain areas, not Organization subtypes owned here.
