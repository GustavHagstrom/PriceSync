# Delivery

Delivery owns the permanent record that a Customer approved a specific calculated price basis and the observable delivery/retrieval facts that follow.

## Price Delivery

`PriceDelivery` is a small immutable aggregate/fact created at Import Approval time. It does not permanently contain every Customer price row.

It retains enough immutable metadata and provenance to identify and deterministically reproduce the approved price basis, including the exact source version identifiers, calculation-rule version, Customer/channel scope, approval provenance, and a deterministic projection/content identity or hash.

The complete Customer Price Projection may be cached for performance but is not required to be stored permanently as rows inside `PriceDelivery`.

A later source publication or agreement activation never changes an existing Price Delivery. Repeated or concurrent approval of the same projection for the same Customer channel creates at most one Price Delivery.

## Delivery artifacts

An exported representation is separate from `PriceDelivery`. A `DeliveryArtifact` is an immutable representation generated from a Price Delivery for a specific export configuration and format.

This separation allows one immutable delivery to have more than one representation without changing the underlying approved business fact. The exact persistence and regeneration policy for artifact bytes remains to be refined.

## Retrieval facts

Retrieval state is not mutable state on `PriceDelivery`. Each successful retrieval is recorded as an immutable retrieval fact associated with the delivery or artifact that was retrieved.

`Available` and `Retrieved` are therefore derived states:

- Available: no successful retrieval fact exists for the relevant delivery/artifact
- Retrieved: one or more successful retrieval facts exist

Repeated retrieval is allowed and produces no new Price Delivery.

## Context relationships

Delivery consumes Import Approval from Customer Approval and uses immutable projection/source identities established by Customer Pricing, Price Publication, and Agreements. Integration Configuration may provide the Export Profile or equivalent configuration used to create a Delivery Artifact.
