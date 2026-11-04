---
name: release
description: Deploy, release, promote or roll back this app, or show which version runs in which Environment — ship a merged change to dev, tag vX.Y.Z for the release tier, promote a version to prod, roll back to the previous version — following docs/deployment.md section 8. Use when the user asks to deploy, release, tag, promote, roll back, ship to production, or where a version runs.
---

# release

Runs the steps of [docs/deployment.md section 8](../../../docs/deployment.md#8-steps-for-developers) with `gh` and `git`. The workflows do the work; this skill checks the preconditions, starts them, watches them and reports. It never edits the workflows or `platform.yaml`.

## Commands

| Command | Does |
| --- | --- |
| `release status` | Lists the Environments and the version each runs, from the GitHub deployments (8.6) |
| `release deploy` | Follows a merged change to `dev`: finds the `deploy.yml` run of the latest commit on `main`, watches it, reports the result (8.1) |
| `release tag [version]` | Tags a commit of `main` and watches `release.yml` (8.2) |
| `release promote <version> [environment]` | Runs `promote.yml`, by default to `prod` (8.3) |
| `release rollback [environment]` | Promotes the version that ran before the current one (8.4) |

## Steps

1. **Find the repository:** `gh repo view --json nameWithOwner -q .nameWithOwner`. If it is a template repository (`gh repo view --json isTemplate`), stop: a template deploys nowhere.
2. **Read the state:** `git fetch origin --tags`, the Environments (`gh api repos/<repo>/environments --jq '.environments[].name'`) and the latest deployment of each (`gh api "repos/<repo>/deployments?environment=<name>&per_page=1"`).
3. **Check the preconditions** of the command, and stop with the reason when one fails:
   - `deploy`: the latest commit on `origin/main` has a `deploy.yml` run.
   - `tag`: the commit is on `origin/main` (`git merge-base --is-ancestor <commit> origin/main`), its `deploy.yml` run passed, and the version is new and higher than the last tag. Without a version, propose the next one from the changes since the last tag, and say why.
   - `promote`: `.github/workflows/promote.yml` exists, the target Environment exists, the version is not a release candidate when the target is `prod`, and the version is the latest deployment of a `release` tier Environment (`test`, `staging`, `uat`, `perf`, `demo`, or their `<customer>-` forms).
   - `rollback`: the previous version is the one deployed in the target Environment before the current one.
4. **Show what will happen** — the command, the commit or version, the Environments it reaches — and **ask for a yes** before every tag, promotion and rollback. `status` and `deploy` need none.
5. **Run it:** `git tag -a <version> <commit> -m "<version>"` and `git push origin <version>`; or `gh workflow run promote.yml -f version=<version> -f environment=<environment>`.
6. **Watch the run:** `gh run list --workflow <file> --limit 1`, then `gh run watch <run id> --exit-status`. A promotion to `prod` waits for a reviewer: say so, and that the reviewer approves it on GitHub.
7. **Report:** the run's result and link, and the Environment's new version (`release status`). On a failure, read `gh run view <run id> --log-failed` and match it to [section 8.5](../../../docs/deployment.md#85-when-a-run-fails).

## Rules

- Never move, delete or push again a tag that exists. A wrong release is replaced by the next version.
- Never run `deploy.yml` or `release.yml` in a way the docs don't describe, and never deploy by other means (`az`, the portal).
- Never skip an Environment's reviewers or ask anyone to.
- A rollback never reverses a database migration. Before a rollback across a release with a migration, say so and ask again.
