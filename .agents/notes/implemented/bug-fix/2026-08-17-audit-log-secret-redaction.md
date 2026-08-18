# Design Note: Audit log redacts credentials and records the actor

Status: implemented

## Problem

`AuditLogInterceptor` serialized every tracked property of every changed entity into `AuditLog.ChangesJson`. Two of those properties are credential material:

- `User.PasswordHash` — written whenever a user is created or has their password changed.
- `RefreshToken.Token` — written on **every login**, since issuing a refresh token is an insert.

The audit trail therefore accumulated live refresh tokens in plaintext in the same database it audits. Anyone with read access to `AuditLogs` — a support query, a backup, a report — held valid session credentials without needing the `RefreshTokens` table at all.

Separately, `AuditLog` declared `UserId`, `Username`, and `IpAddress`, and `PdvDbContext` mapped and length-constrained all three, but the interceptor never assigned them. Every row recorded what changed and when, and left *who* permanently null — which is the column an audit trail exists for.

## Decision

Redact by property name against an explicit set, replacing the value with the literal `***REDACTED***` rather than omitting the key. The key must survive so the trail still records *that* the credential changed; only the value is withheld.

Populate the actor from `IHttpContextAccessor`: `UserId` from the `sub` claim (falling back to `NameIdentifier`, since inbound claim mapping differs by configuration), `Username` from the authenticated identity name, `IpAddress` from the connection's remote address. The accessor is an optional constructor parameter defaulting to null, so tests can build the interceptor with no request pipeline; outside a request the actor columns stay null, which is the honest answer for a seeder or a background write.

Four tests in `Archlab.Backend.Tests/AuditLogInterceptorTests.cs` pin both halves: that the hash and the token never appear in `ChangesJson`, that non-sensitive columns survive redaction, that the actor is recorded from a request, and that it stays null without one.

## Alternatives rejected

**Omit sensitive properties entirely.** Loses the fact that the credential changed at all — a password rotation would be indistinguishable from an unrelated edit to the same row. The point of an audit trail is that the event is visible even when the value is not.

**Mark properties on the domain entities with an attribute (`[NotAudited]`).** Couples `Domain/` to the auditing concern, which it otherwise knows nothing about, and fails *open*: a new credential column ships unprotected whenever someone forgets the attribute. Same fail-open weakness as the chosen design, with added coupling.

**Allowlist the properties that get audited.** Fails closed, which is the stronger security posture — but every new column then silently drops out of the trail until someone remembers to add it, and a silently incomplete audit log is its own defect.

## Consequences

Redaction matches on **property name**, so it fails open: a future credential column named something not in the set is audited in the clear until it is added. The set lives at the top of `AuditLogInterceptor` next to the reason it exists. Any new column holding a secret, hash, or token must be added there in the same change — treat that as part of adding the column, not a follow-up.

Name matching is deliberately not scoped per entity: a property named `Token` on any future entity is redacted. That over-redacts rather than under-redacts, which is the direction to err.

`Program.cs` now calls `AddHttpContextAccessor()`. The interceptor stays registered as scoped, which is what lets it resolve the accessor per request.
