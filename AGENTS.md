# Agent Instructions

Follow [CONTRIBUTING.md](CONTRIBUTING.md), including its human audit and
[EditorConfig](.editorconfig) requirements. Keep changes focused and easy to review.

## Human Approval

Before creating or amending a Git commit, ask the human for explicit approval
of that specific operation and wait for their answer. Do the same before
creating a pull request. A request to prepare or make a commit or pull request
is not itself approval; present the intended changes or PR draft first. Do not
commit or open a pull request if approval is withheld or unavailable.

## Pull Request Drafts

For every PR description you generate, place this exact standalone marker in
the middle of the description, between substantive sections:

`AI-AUDIT-REVIEW-REQUIRED`

Tell the human to review the entire description and all changes, check
correctness, security, and project style, and personally remove the marker.
Never remove it on the human's behalf or claim the human has performed an
audit. For AI-assisted contributions, include an AI Audit Confirmation for the
human to complete after their audit, as required by CONTRIBUTING.md. Leave it
unchecked:

```markdown
## AI Audit Confirmation
- [ ] I have personally reviewed and understood the changes, checked them for correctness, security, and project style, and accept responsibility for them.
```

The PR audit workflow fails while the marker remains. Its status must be made
required in the repository's branch protection or ruleset to block merging.