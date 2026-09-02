# Organization & Access

## Purpose

Own the identity of business parties in PriceSync and the rules for who may act for them.

## Ownership

This area owns:

- Organization
- Legal Organization Identity
- Organization Membership
- Organization-scoped roles and permissions

Supplier and Customer are not Organization types here. They are contextual roles interpreted by other domain areas.

## Core invariants

- A Legal Organization Identity is the combination of registration country and national organization number.
- A person acts for an Organization through an Organization Membership.
- Roles and permissions are scoped to a membership and therefore to one Organization.
- The same person may have different roles in different Organizations.
- Authentication-provider details are outside this domain area's business model.

## References from other areas

Other domain areas reference Organizations by `OrganizationId`. They do not own or duplicate Organization identity, membership, or authorization state.
