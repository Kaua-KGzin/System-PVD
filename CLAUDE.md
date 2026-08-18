# ARCHNEXUS

## Naming

**ARCHlab is the company. ARCHNEXUS is this product** (the PDV/ERP). The distinction is load-bearing:

- Code identity keeps the company: namespaces `Archlab.Backend`, project folders `Archlab.Backend` / `Archlab.Backend.Tests` / `Archlab.Desktop`, `ARCHlab.slnx`, the Docker service names, the Postgres database and user `archlab`, the JWT issuer `archlab-api`. **Do not rename these to ArchNexus.**
- Product identity is what users see: window titles, the SPA `<title>`, the sidebar and login wordmark, the README, and the shipped executable `ArchNexus.exe` (`Product=ARCHNEXUS`, `Company=ARCHlab`).

The wordmark is one word, `ARCHNEXUS`, rendered as `ARCH` in the logo violet plus `NEXUS` in light. Brand colors are sampled from `assets/archnexus-logo.png`: `--primary #550CC5`, `--primary-hover #4708AF`, `--brand-accent #972CFB`. Semantic status colors (success green, danger red, the blue `badge-open`) deliberately do **not** follow the brand — they carry meaning, not identity.

## This machine

**There is no .NET SDK on PATH by default.** It was installed via winget (10.0.400) at `C:\Program Files\dotnet`, but shells here do not always pick it up. Prepend it explicitly:

```powershell
$env:PATH = [Environment]::GetEnvironmentVariable("PATH","Machine") + ";" + [Environment]::GetEnvironmentVariable("PATH","User")
```

The container path still works and needs no SDK at all:

```powershell
docker run --rm -v "C:\Users\kauag\Documents\GitHub\System-PVD:/src" -w /src `
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Archlab.Backend.Tests/Archlab.Backend.Tests.csproj --nologo
```

**Smart App Control was disabled** by the user so the unsigned desktop build could run. That is irreversible without reinstalling Windows. Any other terminal with SAC on will still block `ArchNexus.exe` — it is `NotSigned`, and real distribution needs a code-signing certificate (OV or EV). This is not solvable in code.

Docker Desktop lives at `%LOCALAPPDATA%\Programs\DockerDesktop` and has gone down mid-session before; check `docker info` before blaming a build.

See `.claude/skills/pre-push-checks` for which checks cover which surface.

## Three deployment shapes, one API

`ArchlabApi.Build(builder, httpsRedirection, serveStaticFiles)` in `Archlab.Backend/ArchlabApi.cs` owns the whole API composition. It was extracted from `Program.cs` because top-level statements are unreachable from another executable. Three callers:

1. **Web/Docker** — `Archlab.Backend/Program.cs`, defaults (HTTPS redirect on, API only). `docker compose up -d`, API on 8080, Postgres on 5433.
2. **Frontend dev** — Vite on 5173 proxies `/api` to 8080.
3. **Desktop** — `Archlab.Desktop`, in-process Kestrel on a random loopback port, SQLite, SPA served from embedded resources. One process, offline-capable.

The two flags exist for real reasons: on the desktop, `UseHttpsRedirection` would redirect to an HTTPS endpoint that does not exist, and the CORS policy throws outside Development unless origins are configured — which is meaningless for a single-origin host.

Desktop state lives in `%LOCALAPPDATA%\ARCHNEXUS`: `archlab.db`, `signing.key` (per-install JWT key, owner-only ACL), `webview/`, `ui.json`. Build it with `powershell -ExecutionPolicy Bypass -File scripts\build-desktop.ps1` — React build, embedded into the assembly, single ~142 MB self-contained exe. `dist-desktop/` and `Archlab.Desktop/wwwroot/` are gitignored.

Desktop build gotchas already paid for: `ValidateExecutableReferencesMatchSelfContained=false` (a self-contained exe cannot reference the Web SDK exe project), an explicit `FrameworkReference Microsoft.AspNetCore.App`, and explicit ASP.NET usings (the WinForms SDK supplies none). WinForms also does not adopt `ApplicationIcon` for a form — `AppIcon.cs` does that.

## Known open items

- **`ui.json` is written at startup with `FullScreen: false`, before any keypress.** The only writer is `RememberWindowMode`, reachable only from `OnWebMessageReceived`, so a web message appears to arrive at boot. Not root-caused. F11 fullscreen itself works (verified: the `WS_CAPTION` style bit clears). Only the persistence of the mode is suspect.
- **Historical audit-log rows still hold a live refresh token in plaintext** in the Postgres dev database, from before the redaction fix. The admin token seen was valid and unrevoked. Decided fix (not yet applied): `UPDATE` old rows to `***REDACTED***` and revoke the token. See `.agents/notes/implemented/bug-fix/2026-08-17-audit-log-secret-redaction.md`.
- **5 `@typescript-eslint/no-explicit-any` errors** in `frontend/src`: CategoriesPage:42, CustomersPage:48, PdvPage:64, SuppliersPage:109. Pre-existing; `tsc -b` and `npm run build` are green.
- **The fiscal document is simulated.** `FiscalDocumentService` issues with CNPJ `00000000000000` and states in the payload that it does not replace SEFAZ-authorized NFC-e. Real NFC-e needs a certificate, SEFAZ integration, and contingency mode — a far larger job than packaging.
- **No hardware integration exists**: no thermal printer, cash drawer, TEF, or scale. The desktop shell is the place that makes it possible (C# host), not something already done.

<!-- code-review-graph MCP tools -->
## MCP Tools: code-review-graph

**IMPORTANT: This project has a knowledge graph. ALWAYS use the
code-review-graph MCP tools BEFORE using Grep/Glob/Read to explore
the codebase.** The graph is faster, cheaper (fewer tokens), and gives
you structural context (callers, dependents, test coverage) that file
scanning cannot.

### When to use graph tools FIRST

- **Exploring code**: `semantic_search_nodes` or `query_graph` instead of Grep
- **Understanding impact**: `get_impact_radius` instead of manually tracing imports
- **Code review**: `detect_changes` + `get_review_context` instead of reading entire files
- **Finding relationships**: `query_graph` with callers_of/callees_of/imports_of/tests_for
- **Architecture questions**: `get_architecture_overview` + `list_communities`

Fall back to Grep/Glob/Read **only** when the graph doesn't cover what you need.

### Key Tools

| Tool | Use when |
|------|----------|
| `detect_changes` | Reviewing code changes — gives risk-scored analysis |
| `get_review_context` | Need source snippets for review — token-efficient |
| `get_impact_radius` | Understanding blast radius of a change |
| `get_affected_flows` | Finding which execution paths are impacted |
| `query_graph` | Tracing callers, callees, imports, tests, dependencies |
| `semantic_search_nodes` | Finding functions/classes by name or keyword |
| `get_architecture_overview` | Understanding high-level codebase structure |
| `refactor_tool` | Planning renames, finding dead code |

### Workflow

1. The graph auto-updates on file changes (via hooks).
2. Use `detect_changes` for code review.
3. Use `get_affected_flows` to understand impact.
4. Use `query_graph` pattern="tests_for" to check coverage.
