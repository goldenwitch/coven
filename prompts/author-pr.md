# Author PR

Shepherd a PR from creation through to "ready for human review."

## Inputs
- Branch with changes (or uncommitted work)
- PR title and description (or infer from commits)

## 0. Tooling Check

If `gh` command fails, delegate exploration to find the executable:
- Common locations: `C:\Program Files\GitHub CLI\gh.exe`, `/usr/bin/gh`
- Use full path if not on PATH
- If not installed: escalate to human

## 1. Commit & Push

```bash
git status --porcelain
git add -A  # or selective
git commit -m "<message>"
git push origin HEAD
```

## 2. Create or Join PR

Check if PR already exists for this branch:
```bash
gh pr view --json number,title,state
```

**If PR exists:**
- Your changes are now part of that PR
- Note the PR number and continue to step 3
- If PR scope differs significantly from your changes: escalate (consider splitting to new branch)

**If no PR:**
```bash
gh pr create --title "<title>" --body "<description>" --base main
```

Capture PR number for subsequent steps.

## 3. Review

Check if already reviewed at current HEAD:
```bash
gh pr view <PR> --comments --json comments
# Look for agent review comment referencing current commit SHA
git rev-parse HEAD
```

**If already reviewed at this commit:** Skip to step 6 (Monitor Pipeline)

**If not reviewed (or new commits since last review):**
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
- `gh` CLI not available and cannot be located
- PR already exists with significantly different scope (split decision needed)
- Pipeline failures you cannot diagnose
- Conflicting requirements discovered during review
- Changes required outside the PR's intended scope
- Security implications requiring sign-off
