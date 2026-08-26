# PriceSync Documentation

This directory contains the product, business, architecture, and domain documentation for PriceSync.

## Documentation map

- [Context](./Context.md) — the canonical glossary for business terms used throughout the project.
- [Working domain model](./DOMAIN.md) — the evolving domain surface used during discovery.
- [Established domain areas](./Domain/readme.md) — concepts and cohesive modules whose boundaries have been established.
- [Product vision](./product-vision.md) — the problem, product boundaries, adoption principles, and intended end state.
- [Business model](./business-model.md) — commercial hypotheses, pricing principles, and adoption risks.
- [Architecture](./architecture.md) — architectural principles, boundaries, candidate model, and technical risks.

## How to navigate

Start with the [product vision](./product-vision.md) to understand why PriceSync exists. Use [Context](./Context.md) whenever a business term is unclear. Once domain concepts have been discovered and documented, use the [domain index](./Domain/readme.md) to navigate between them. Read [Architecture](./architecture.md) for system boundaries and technical direction.

## Documentation conventions

- `Context.md` is a glossary only: short, business-focused definitions without implementation details.
- `DOMAIN.md` collects the domain surface while concepts, rules, and boundaries are still being discovered.
- `Domain/` contains one Markdown file per established domain concept or cohesive module.
- Domain pages describe purpose, relationships, rules, examples, and unresolved questions.
- Open hypotheses must be labelled as such and must not be presented as settled decisions.
- When a canonical term changes, update `Context.md`, the relevant domain page, and incoming links together.
