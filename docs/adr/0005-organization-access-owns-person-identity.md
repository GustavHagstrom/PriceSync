# Organization & Access owns Person identity

Organization & Access owns the stable PriceSync Person identity, PersonId, and future links from external identity-provider accounts to that Person because membership authority depends on this canonical actor. The API host owns protocol-level authentication and supplies the authenticated PersonId through an Organization & Access contract; external provider accounts and tokens never replace the Person as the business identity.

Issue #11 is a transitional slice: it consumes an already provisioned, trusted internal PersonId through ICurrentPerson but does not yet persist Person records or implement provisioning and external-account linking. Production authentication must not expose the Organization endpoints until it can supply that canonical PersonId. The absence of a Person foreign key in the initial OrganizationMembership schema is therefore intentional and temporary, not a transfer of Person ownership.
