# Modular monolith boundaries

PriceSync will start as a modular monolith with one .NET module project per established bounded context, paired with a `.Contracts` project for each module. This makes domain ownership boundaries enforceable without introducing microservices, distributed transactions, or shared domain models before the product needs them.

Implementation projects may depend on their own contracts, other modules' contracts, and the shared kernel, but not on other module implementation projects. The API host remains the composition root, modules register their own internals through explicit registration extensions, and cross-module workflows live in a thin application orchestration layer when they cannot belong to a single module.
