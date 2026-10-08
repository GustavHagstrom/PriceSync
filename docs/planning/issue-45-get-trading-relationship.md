# Issue #45: Implement Get Trading Relationship

Status: Implementation pending.

Issue: https://github.com/GustavHagstrom/PriceSync/issues/45

Parent: #12. Dependency: #43.

## Scope

Implement scoped GetById for Trading Relationships according to the
[Trading Relationships implementation plan](issue-12-trading-relationships-implementation-plan.md).
The acting Organization must have relationship-read capability authorized
through OrganizationAccess and must be either Supplier or Customer.

## Acceptance criteria

- An authorized Supplier or Customer can retrieve its relationship.
- The response uses the compact representation shared by the module's HTTP slices.
- Unknown and out-of-scope IDs both return 404 without revealing existence.
- Acting Organization context alone does not authorize access.
- Focused API/integration tests cover successful reads, capability enforcement,
  and hidden out-of-scope behaviour.

This branch prepares the implementation PR; no runtime behaviour is changed yet.
