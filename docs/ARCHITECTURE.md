# CareerPilot AI — MVP Definition, Architecture and Plan

Status of this document: planning deliverable (Phase 0). Nothing described here is implemented yet unless PROJECT_STATUS.md says so.

## 1. Proposed MVP

A single-user web application that:

1. Stores one canonical, editable career profile (imported from a CV after review).
2. Pulls real vacancies from legitimate sources, normalizes and deduplicates them.
3. Scores each vacancy with a configurable, explainable 100-point model (deterministic rules first, LLM second).
4. Generates a tailored, fact-checked CV (DOCX + PDF) and cover letter per selected vacancy.
5. Requires explicit, server-validated approval before any application leaves the system.
6. Submits only through a supported channel, otherwise hands me a complete "application package" and a guided manual flow. A submission is recorded as confirmed only with real evidence.
7. Tracks the pipeline, follow-ups and analytics, and runs scheduled searches in a background worker.

### What the MVP deliberately is NOT

- It is not a scraper of LinkedIn/Indeed. See section 5: neither offers a permitted public search API.
- It does not auto-submit through ATS APIs on my behalf. See section 5: the official apply endpoints are employer-side.
- It does not send follow-ups without approval.

## 2. Architecture and rationale

Clean Architecture, as requested, on the current .NET LTS (recommended: .NET 10 LTS; confirm with `dotnet --list-sdks`. .NET 9 is a short-term release and reaches end of support in November 2026, so starting a new project on it is not advisable).

| Layer / project | Responsibility | Depends on |
|---|---|---|
| CareerPilot.Domain | Entities, enums, state machines, invariants (approval rules, status transitions). No external dependencies. | nothing |
| CareerPilot.Application | Use cases (commands/queries), interfaces (IJobSourceConnector, ILlmProvider, IDocumentRenderer, IApplicationSubmitter, INotifier, IFileStorage), scoring engine, fact validator. | Domain |
| CareerPilot.Infrastructure | EF Core + PostgreSQL, connectors, LLM providers (cloud + Ollama), DOCX/PDF rendering, storage, email. | Application |
| CareerPilot.API | ASP.NET Core REST API, auth, validation, ProblemDetails, health checks. | Application, Infrastructure |
| CareerPilot.Worker | Hosted service: scheduled searches, verification sweeps, document generation queue. | Application, Infrastructure |
| CareerPilot.Tests | Unit, integration (Testcontainers/PostgreSQL), e2e with mocked sources/submitters. | all |
| careerpilot-web (Angular) | Separate SPA, responsive, talks only to the API. | API contract (OpenAPI) |

Key design decisions:

- **The backend enforces the workflow, not the LLM.** The LLM only returns structured JSON that is schema-validated. No LLM component holds a reference to `IApplicationSubmitter`.
- **Approval is cryptographically bound to content.** `ApplicationApproval` stores a SHA-256 hash of the exact CV version, cover-letter version and answers approved. The submit use case recomputes the hash immediately before submitting. If anything changed, the approval is void. A batch approval is a set of per-application approvals, so it cannot authorize anything outside the selection.
- **Duplicate prevention at the database level.** Unique constraint: one active Application per (UserProfileId, CanonicalJobId). `SubmissionAttempt` has a unique idempotency key.
- **Matching = rules + LLM.** Hard filters, skill overlap, geography, language, salary and experience are computed deterministically. The LLM extracts structured requirements from the job text and writes the human-readable rationale. Weights live in configuration (database), not code.
- **Prompt-injection posture.** Job text, employer pages and uploaded CVs are passed as clearly delimited data, outputs are schema-validated, and the LLM has no tools that cause side effects. Injection attempts found in job text are flagged on the job.
- **Cost control.** Deterministic pre-filter before any LLM call, content-hash caching of analyses, per-day/month token budgets, and a score threshold plus quota for CV generation in scheduled runs.
- **Frontend:** Angular with a standalone-components setup and an accessible component library. Version chosen at scaffold time, not hardcoded here.
- **Infra:** Docker Compose (postgres, api, worker, web, optional Ollama), structured logging (Serilog), health checks, OpenTelemetry-ready.

### Deployment note

The worker needs to run somewhere that stays on. For a personal project, a small VPS or container host running Docker Compose is the simplest. Scheduling is in the worker, so it does not depend on my laptop.

## 3. Initial directory structure

```
careerpilot/
├── CareerPilot.sln
├── docker-compose.yml
├── .env.example
├── PROJECT_STATUS.md
├── docs/
│   ├── ARCHITECTURE.md
│   ├── SOURCES.md              # per-source access, limits, terms
│   └── SECURITY.md
├── src/
│   ├── CareerPilot.Domain/
│   │   ├── Profiles/  Jobs/  Applications/  Documents/  Common/
│   ├── CareerPilot.Application/
│   │   ├── Profiles/  Jobs/  Matching/  Documents/  Applications/  Abstractions/
│   ├── CareerPilot.Infrastructure/
│   │   ├── Persistence/ (DbContext, configurations, migrations)
│   │   ├── Sources/ (GreenhouseConnector, LeverConnector, AshbyConnector, ...)
│   │   ├── Llm/ (Ollama, cloud providers, prompt templates/versioned)
│   │   ├── Documents/ (DOCX, PDF renderers, CV import parser)
│   │   └── Submission/ (guided-manual, adapters)
│   ├── CareerPilot.API/
│   └── CareerPilot.Worker/
├── tests/
│   └── CareerPilot.Tests/
└── web/                        # Angular application
```

## 4. Key entities and relationships

```
UserProfile 1─* WorkExperience, Education, Skill, Project, Language
UserProfile 1─1 JobPreference (countries, cities, work modes, salary, weights)
JobSource 1─* JobListing            (a listing = one posting on one source)
CanonicalJob 1─* JobListing         (dedup: same vacancy across sources)
JobListing 1─* JobSkill
CanonicalJob 1─* JobMatch (per profile version) 1─* MatchExplanation
CanonicalJob 1─0..1 Application     (UNIQUE active per user + job)
Application 1─* GeneratedDocument 1─* DocumentVersion
Application 1─* ApplicationAnswer
Application 1─* ApplicationApproval (content hash, approver, timestamp, scope)
Application 1─* SubmissionAttempt   (idempotency key, evidence, outcome)
Application 1─* Interview, FollowUp
SearchRun 1─* JobListing (discovered in run)
Notification, IntegrationConfiguration, AuditEvent (append-only)
```

Notes:
- `CanonicalJob` is added to the prompt's entity list so that cross-source duplicates collapse to one vacancy.
- `UserProfile` is versioned, and `JobMatch` records which profile version it scored against.
- `AuditEvent` is append-only: actor, action, entity, document hashes, timestamp, outcome.
- Application statuses follow the list in the brief, with transitions enforced in the Domain (for example `Submitted` is unreachable without a valid approval and a confirmed or unconfirmed attempt).

## 5. Job sources: what can actually be accessed

Verified during planning (via documentation summaries; re-verify against official docs when each connector is built):

| Source | Access method | Auth | Notes |
|---|---|---|---|
| Greenhouse boards | `GET https://boards-api.greenhouse.io/v1/boards/{token}/jobs?content=true` | none | Per-company board token. Returns apply URL. |
| Lever | `GET https://api.lever.co/v0/postings/{site}?mode=json` (EU: `api.eu.lever.co`) | none | Per-company site. Paginate with `skip`/`limit`. Can intermittently return HTML, so check Content-Type and retry. |
| Ashby | `GET https://api.ashbyhq.com/posting-api/job-board/{board}?includeCompensation=true` | none | Per-company board. Skip entries with `isListed: false`. |
| Indeed | No public job-search API. The old publisher API was closed to new sign-ups in 2020 and stopped answering by 2024. Remaining APIs are for employers/ATS partners. | partner only | Not usable for personal search. Respect its terms; no scraping. |
| LinkedIn Jobs | No public search API for individuals (partner programs only). | partner only | Not planned. I can paste or open links manually. |
| EURES | I found only third-party scrapers, not a confirmed official API. | unverified | Must check the official EURES/EU developer pages before building. Until then: guided link-out. |
| Wellfound | Not verified. | unverified | Treat as link-out until an official route is confirmed. |

Important consequences:

1. **Greenhouse, Lever and Ashby are not search engines.** They expose one company's board at a time. The connector needs a configurable **company watchlist** (board token per company), and I will need to curate and extend it. Public access does not equal a right to redistribute listings, which is fine for personal use.
2. **Official apply endpoints are employer-side.** The Lever apply endpoint needs an API key generated by the employer's admin (confirmed in documentation). Greenhouse's application endpoint is likewise understood to require an employer-issued key (to be re-verified). As a candidate I will not have these. So realistic submission for the MVP is the **guided manual application**: the system prepares the package, opens the official apply URL, and I submit. I then mark it submitted, and the system records it as user-reported (not system-confirmed) unless I attach confirmation evidence.
3. Candidates for additional official sources to evaluate in Phase 2 (not yet verified): France Travail API (France), Adzuna API (several European countries), Arbeitnow and Remotive public feeds (remote and Europe-friendly), Belgian regional services. Tunisia likely needs guided link-out unless an official feed exists.

Recommended first two connectors: **Greenhouse + Lever** (plus Ashby as a third, since it is nearly identical). They work without credentials and give real listings with real apply URLs, which satisfies MVP criteria 3 and 4 honestly.

## 6. Implementation plan and principal risks

Phases are as in the brief. Sequencing: Phase 1 foundation → Phase 2 sources → Phase 3 matching → Phase 4 documents → Phase 5 approval → Phase 6 guided submission → Phase 7 tracking/scheduling → Phase 8 hardening.

| Risk | Impact | Mitigation |
|---|---|---|
| No automated submission path for candidates | MVP criterion 11 | Use guided manual flow (permitted by criterion 11), document honestly. |
| Job sources are per-company, not search | Fewer listings than expected | Company watchlist plus aggregator APIs with official access. |
| LLM hallucinating CV content | Truthfulness | Fact validator: every claim in a generated CV must map to a profile fact id; unmapped claims block approval. |
| Prompt injection via job text | Security | Delimited untrusted input, schema-validated output, no side-effect tools for LLM. |
| LLM cost creep | Budget | Rule pre-filter, caching, quotas, thresholds. |
| Source terms/format changes | Broken connectors | Contract tests with fixtures, per-source health status, errors recorded per SearchRun. |
| Closed-job detection | Stale listings | Re-verify via source feed; expire only after a complete successful snapshot. |
| Personal data (CV) exposure | Privacy | Auth, encrypted storage at rest, retention controls, no PII in logs or in prompts to unrelated providers. |

## 7. What I will eventually need from you

| Item | Needed for | When |
|---|---|---|
| LLM provider choice plus API key, or Ollama model choice | Analysis, CV tailoring | Phase 3 |
| Initial company watchlist (Greenhouse/Lever/Ashby board names) or permission to propose one | Job discovery | Phase 2 |
| Your current CV (PDF/DOCX) | Profile import | Phase 4 (earlier if you want) |
| Correct facts: employment dates, achievements with real metrics, project links, work authorization, salary range | Truthful documents | Phase 1 to 4 |
| Aggregator credentials (France Travail, Adzuna, etc.) if wanted | More sources | Phase 2+ |
| SMTP or email provider (optional) | Notifications | Phase 7 |
| Hosting target (VPS/cloud) | Scheduled runs without your laptop | Phase 8 |
