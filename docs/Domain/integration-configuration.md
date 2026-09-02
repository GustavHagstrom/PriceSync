# Integration Configuration

## Purpose

Own reusable configuration that translates between Organization-specific external formats and PriceSync's canonical model.

## Ownership

This area owns:

- Import Profiles
- Export Profiles

Import Profiles describe how external Excel/CSV structures are normalized into canonical input. Export Profiles describe Customer-specific transformations from canonical projections or deliveries into external formats.

## Core rules

- Profiles are private to their owning Organization.
- Profiles describe deterministic mappings and transformations; they do not own canonical pricing facts.
- Reusing an Import Profile never bypasses Price Publication validation.
- External format concerns remain at this seam rather than leaking into Price Publication, Agreements, or other pricing areas.

## References

Price Publication consumes canonical draft data produced through an Import Profile but does not own the profile. Delivery/consumption flows may later use Export Profiles while keeping canonical delivery facts independent of the exported representation.
