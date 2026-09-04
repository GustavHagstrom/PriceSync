# PriceSync Documentation

This directory contains the product, business, architecture, and domain documentation for PriceSync.

## Documentation map

- [Context](./Context.md) — the canonical glossary for business terms used throughout the project.
- [Established domain areas](./Domain/readme.md) — the established domain map, cross-context relationships, aggregates, invariants, and focused bounded-context documentation.
- [Product vision](./product-vision.md) — the problem, product boundaries, adoption principles, and intended end state.
- [Business model](./business-model.md) — commercial hypotheses, pricing principles, and adoption risks.
- [Architecture](./architecture.md) — architectural principles, boundaries, candidate model, and technical risks.
- [Architecture decisions](./adr/) — accepted architectural decisions that explain important trade-offs.

## How to navigate

Start with the [product vision](./product-vision.md) to understand why PriceSync exists. Use [Context](./Context.md) whenever a business term is unclear. Use the [domain index](./Domain/readme.md) to navigate the established domain model and its bounded contexts. Read [Architecture](./architecture.md) for system boundaries and technical direction.

Unresolved domain discovery should be captured in the relevant issue or pull request while it is being explored. Once a conclusion is established, move it into `Context.md` and/or the relevant page under `Domain/` rather than maintaining a parallel discovery document as a second source of truth.

## Documentation conventions

- `Context.md` is a glossary only: short, business-focused definitions without implementation details.
- `Domain/readme.md` is the entry point for the established domain map and cross-context relationships.
- `Domain/` contains one Markdown file per established domain concept or cohesive module.
- `adr/` contains short architecture decision records for decisions that are costly to reverse, surprising without context, and based on real trade-offs.
- Domain pages describe purpose, ownership, aggregates, invariants, relationships, and materially unresolved questions.
- Open hypotheses must be labelled as such and must not be presented as settled decisions.
- When a canonical term changes, update `Context.md`, the relevant domain page, and incoming links together.
