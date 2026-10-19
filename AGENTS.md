# Project workflow

This repository is a portfolio project. The user requested that completed work be committed regularly and published to their `erksbars` GitHub account.

- Work in meaningful, reviewable units. Run checks appropriate to each change before committing.
- After a completed implementation unit, create a descriptive commit and push to the configured origin when access is available, unless the user asks otherwise. Keep unfinished or failing work clearly identified.
- Use the repository-local Git identity and the existing configured remote. Do not switch GitHub accounts or create another repository without user direction.
- Never fabricate activity, backdate commits, split changes merely to inflate the contribution graph, or create empty scheduled commits.
- Never commit `.env`, real API keys, tokens, customer data, runtime database files, `.local`, dependency folders or build/test output. Keep `.env.example` limited to placeholders.
- Preserve tenant/branch authorization, server-side price validation, payment idempotency, existing data and order capability boundaries.
- Subscription work and third-party music catalog/playback integrations are outside the current scope. Music is an optional text request attached to a paid order.
- Do not claim production readiness or successful CI checks without evidence. Record material limitations in the README or relevant docs.
- Use `codex/` for new feature branches. Do not rewrite published history or force-push unless explicitly requested.
