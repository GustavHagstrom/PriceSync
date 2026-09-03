# Integration Configuration

## Purpose

Own reusable Organization-specific configuration that translates between external formats and PriceSync's canonical model.

## Aggregate model

### ImportProfile

`ImportProfile` is an aggregate root owned by one Organization. It contains the reusable mapping/configuration required to normalize a compatible external Excel/CSV layout into canonical PriceSync input.

Its transactional boundary exists because one profile's mapping rules and compatibility constraints must change consistently as one reusable configuration unit. It does not own any Price Draft, Price List Version, or Source File content.

### ExportProfile

`ExportProfile` is an aggregate root owned by one Customer Organization. It contains the reusable transformation/configuration required to render a canonical Customer Price Projection for an external Customer system.

Its transactional boundary exists because one export mapping/configuration must change consistently as a reusable unit without mutating the canonical projection or retrieval history.

## Commands / decisions

This area is responsible for decisions such as:

- create/update/retire an Import Profile
- create/update/retire an Export Profile
- establish a new immutable/versioned profile configuration when historical reproducibility requires it

## Business facts

The area produces configuration-version facts that other contexts may reference for reproducible import/export provenance. It does not produce canonical pricing facts.

## Core rules

- Profiles are private to their owning Organization.
- Profiles describe deterministic mappings and transformations; they do not own canonical pricing facts.
- Reusing an Import Profile never bypasses Price Publication validation.
- Export configuration changes representation without changing the semantic Customer Price Projection.
- Historical retrieval provenance identifies the export configuration/version actually used.
- External-format concerns remain at this seam rather than leaking into Price Publication, Agreements, or Customer Pricing.

## References

Price Publication consumes canonical draft data produced through an Import Profile but does not own the profile. Retrieval & Consumption may reference an Export Profile/configuration version when producing an external representation. Both relationships are by identity/versioned configuration rather than shared mutable ownership.
