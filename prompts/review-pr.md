# Review PR

Provide critical review of a PR. Can be used standalone or delegated by `author-pr.md` for self-review.

## Inputs
- PR number (required)

## 1. Setup

- Get PR number from user or infer from current branch
- `gh pr view <number> --json title,body,author,baseRefName,headRefName,files`
- `gh pr diff <number>` — capture full diff
- Check working tree: `git status --porcelain`
  - If dirty: stash (`git stash push -m "pr-review-auto"`) or fail with explanation
- Checkout PR branch: `gh pr checkout <number>`

## Analysis

For each distinct component/area touched by the PR:

**Spawn subagent with:**
- Component path(s) and relevant diff hunks
- PR title and description for intent context
- Task: analyze changes against existing code

**SubAgent responsibilities:**
- Read surrounding code to understand the area
- Assess whether changes align with stated intent
- Identify: bugs, edge cases, style violations, missing tests
- Report: summary, concerns (with file:line refs), suggestions

## Synthesis

- Aggregate subagent findings
- Deduplicate overlapping concerns
- Prioritize by severity: blocking → should-fix → nit
- Draft review summary

## Submit Review

- `gh pr review <number> --approve|--request-changes|--comment --body "<summary>"`
- For inline comments: `gh pr review <number> --comment --body "<comment>" --path <file> --line <line>`
- Or batch via: `gh api repos/{owner}/{repo}/pulls/<number>/reviews --method POST`

## Escalate When

- PR touches unfamiliar architecture (request human guidance)
- Conflicting requirements between components
- Security implications requiring human sign-off
