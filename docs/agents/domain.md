# Domain docs

PriceSync uses one shared domain context.

## Before exploring

- Read docs/Context.md for shared business language.
- Read relevant architectural decisions in docs/adr/.
- Follow links from docs/Context.md into docs/Domain/ when
  deeper domain rules are needed.

Keep the shared context in docs/Context.md; do not create a
duplicate CONTEXT.md at the repository root.

If a referenced context file or ADR directory is absent,
proceed silently. Domain-modeling creates documentation
when terms or decisions are resolved.

## Use canonical vocabulary

Use the terms defined in docs/Context.md in issues,
proposals, code, and tests. Respect its avoided synonyms.

If a needed concept is missing, reconsider the terminology
or note the gap for domain-modeling.

## Surface ADR conflicts

Explicitly identify any proposal that contradicts an
existing ADR, and explain why the decision merits review.
