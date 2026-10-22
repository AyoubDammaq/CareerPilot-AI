# CareerPilot AI

A personal job-search and application agent. Nothing is ever submitted without your explicit approval; the rule is enforced in the backend domain model, not in the UI.

**Status:** Phases 1 to 8 built (profile, job search, scoring, AI analysis, CV and cover letter, approval workflow, CV import, PDF export, tracking, hardening). See [PROJECT_STATUS.md](PROJECT_STATUS.md) for exactly what is tested and what is not. Architecture: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). Running it for real: [docs/OPERATIONS.md](docs/OPERATIONS.md). Security: [docs/SECURITY.md](docs/SECURITY.md).

## Run locally with Docker
```bash
cp .env.example .env      # then edit the three values (password >= 12 chars, JWT key >= 32 chars)
docker compose up --build
```
- Web UI: http://localhost:4200  (log in with `AUTH_PASSWORD`)
- API: http://localhost:5080  (health: `/health/ready`)

## Develop without Docker
Prerequisites: .NET 10 SDK, Node >= 22.22.3 (24 recommended), PostgreSQL 16+.
```bash
# API
export ConnectionStrings__Default="Host=localhost;Database=careerpilot;Username=careerpilot;Password=..."
export Auth__Password="a-password-of-12+-chars"
export Auth__JwtKey="$(openssl rand -base64 48)"
dotnet run --project src/CareerPilot.API

# Web (new terminal)
cd web && npm ci && npm start
```

## Tests
```bash
dotnet test tests/CareerPilot.Tests                       # unit tests, no database needed
CAREERPILOT_TEST_DB="Host=localhost;Database=careerpilot_test;Username=...;Password=..." \
  dotnet test tests/CareerPilot.IntegrationTests          # needs PostgreSQL
cd web && npx ng test --watch=false                       # frontend
```
CI (`.github/workflows/ci.yml`) runs all of the above plus production builds and Docker image builds.

## Database migrations
Migrations are committed and applied automatically at start. After changing an entity, run the **Generate EF migration** workflow on GitHub (or `dotnet ef migrations add <Name> --project src/CareerPilot.Infrastructure --startup-project src/CareerPilot.API --output-dir Persistence/Migrations`) and commit the result. A production start refuses to run with no migrations.


## Optional: AI job analysis
Off by default. To enable, set environment variables for the API (never commit keys):
`Llm__Provider=Anthropic` (or `OpenAiCompatible`), `Llm__Model=<model id>`, `Llm__ApiKey=<key>`.
For a local model via Ollama: `Llm__Provider=OpenAiCompatible`, `Llm__Model=<local model>`, no key, default URL `http://localhost:11434/v1`.
Cost controls: `Llm__DailyCallLimit` (default 20 new analyses per UTC day), `Llm__MaxOutputTokens` (800), `Llm__MaxPostingChars` (6000). Results are cached per job.
Import CV (sidebar): upload a PDF, DOCX or text CV, review what was found, tick what to add. Works without AI for contact details and technologies; with AI on it also proposes experience, education, projects and languages (the CV text is sent to your provider). `Llm__DailyImportLimit` caps AI imports (default 10).
Scoring weights can be tuned with `Scoring__Skills`, `Scoring__Title`, and so on (they are rescaled to 100).
