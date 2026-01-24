# Author PR

Shepherd a PR from creation through to "ready for human review."

## Inputs
- Branch with changes (or uncommitted work)
- PR title and description (or infer from commits)

## 1. Commit & Push

```bash
git status --porcelain
git add -A  # or selective
git commit -m "<message>"
git push origin HEAD
```

## 2. Create PR (if needed)

```bash
gh pr create --title "<title>" --body "<description>" --base main
```

Capture PR number for subsequent steps.

## 3. Review

Delegate a subagent with `review-pr.md`:
- Pass the PR number
- Task: provide critical feedback as external reviewer
- Receive: list of concerns, suggestions, blocking issues

## 4. Address Review Findings

For each finding, delegate appropriate subagent:

| Finding Type | Subagent | Action |
|--------------|----------|--------|
| Bug/logic error | Implementation | Fix the issue |
| Missing test | Implementation | Add test coverage |
| Unclear code | Implementation | Refactor or add comments |
| Question about intent | Exploration | Clarify, then decide |

## 5. Iterate

```bash
git add -A
git commit -m "<what was addressed>"
git push origin HEAD
```

Re-delegate `review-pr.md` if changes were significant.
Repeat until no blocking issues remain.

## 6. Monitor Pipeline

```bash
gh pr checks <PR> --watch
```

- If checks fail: delegate implementation subagent to fix
- If checks pass: proceed to ready state

## 7. Ready for Human Review

When:
- All self-review findings addressed
- All pipeline checks pass
- No unresolved conversations

```bash
gh pr ready <PR>  # if was draft
gh pr comment <PR> --body "Ready for human review."
```

## Escalation Triggers

Escalate to human when:
- Pipeline failures you cannot diagnose
- Conflicting requirements discovered during review
- Changes required outside the PR's intended scope
- Security implications requiring sign-off
