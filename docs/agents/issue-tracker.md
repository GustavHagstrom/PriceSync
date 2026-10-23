# Issue tracker: GitHub

Issues and PRDs live in GustavHagstrom/PriceSync on GitHub.
Use the gh CLI from this repository.

## Conventions

- Create: gh issue create --title "..." --body-file <file>
- Read: gh issue view <number> --comments
- List: gh issue list --state open --json number,title,body,labels
- Comment: gh issue comment <number> --body-file <file>
- Add labels: gh issue edit <number> --add-label "..."
- Remove labels: gh issue edit <number> --remove-label "..."
- Close: gh issue close <number> --comment "..."

For multiline bodies and comments, write the exact Markdown
to a temporary file and pass it with --body-file.

## Pull requests as a triage surface

PRs as a request surface: no.

## Skill instructions

When a skill says "publish to the issue tracker", create a
GitHub issue.

When a skill says "fetch the relevant ticket", read the
GitHub issue including its comments and labels.
