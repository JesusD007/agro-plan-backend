---
status: testing
phase: 04-applied-farm-intelligence
source: [04-VERIFICATION.md]
started: 2026-11-11
updated: 2026-11-11
---

## Current Test

number: 1
name: Validate the selected speech provider for Dominican Spanish and Haitian Creole
expected: |
  Before production activation, validate the exact transcription operation with at least 20 consented representative recordings per language plus consented Spanish–Kreyòl code-switch cases. Human-labeled reference transcripts show at least 95% exact field accuracy separately for each language. Both speech languages remain disabled until every criterion passes.
awaiting: user response

## Tests

### 1. Validate the selected speech provider for Dominican Spanish and Haitian Creole
expected: The exact provider operation passes the consented bilingual evaluation set and both language thresholds; otherwise both speech languages remain disabled.
result: [pending]

### 2. Complete privacy/legal review for external media processing
expected: Vendor terms, retention/deletion, data residency, secrets handling, lawful basis, and production media-processing notice are approved before any external provider operation is enabled.
result: [pending]

### 3. Validate agronomic observations and trusted agronomist review
expected: Agronomists confirm visible-sign descriptions and evidence requests are useful without pre-review diagnosis/treatment; agronomist identities are verified through the trusted process before farm assignment; only a currently verified and farm-authorized agronomist can confirm diagnosis/treatment.
result: [pending]

## Summary

total: 3
passed: 0
issues: 0
pending: 3
skipped: 0
blocked: 0

## Gaps

These are production activation gates requiring vendor-specific evidence, privacy/legal approval, domain-expert review, and trusted identity provisioning. The implementation remains fail-closed while they are pending. They do not represent automated implementation gaps.
