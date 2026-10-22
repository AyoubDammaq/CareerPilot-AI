# Security

CareerPilot AI is a **single-user** app that holds personal data (your profile, CVs, job applications). This page says what protects it, what does not, and what you must do yourself. For how each claim was checked, see the verification column and `PROJECT_STATUS.md`.

## What the system will and will not do
| Rule | Where it is enforced | Checked by |
|---|---|---|
| Nothing is submitted to an employer without your approval. The system cannot contact employers at all; you submit by hand and record the result | Domain model (`JobApplication`), not the UI or an AI | Unit tests, including one that fails if any other class can reach the application store |
| Approval is tied to the exact CV and letter you reviewed; changing either withdraws it | Domain model, SHA-256 bundle hash | Unit and integration tests |
| AI output never changes scores, statuses or decisions, and never writes to your profile | Application layer: AI results are stored as separate suggestions | Unit tests |
| CVs and letters only use facts you marked verified; a claim checker flags anything else | `CvBuilder`, `ClaimChecker` | Unit tests. It catches skills and numbers, not every free-text claim |
| CAPTCHAs, logins and anti-bot controls are never bypassed; only public company job feeds are read | Connectors are limited to Greenhouse, Lever, Ashby public APIs | Code review; no scraping code exists |

## What protects the running app
| Protection | Detail | State |
|---|---|---|
| Sign-in | One password (min 12 chars, checked at start; the example values from `.env.example` are refused). Tokens last 8 hours (`Auth__TokenLifetimeMinutes`) | Tested (CI) |
| Brute force | 5 sign-in attempts per minute per client address (`Security__LoginPerMinute`); failed attempts are logged without the password | Tested (CI) |
| Costly endpoints | AI analysis, cover letters, job-board searches, re-scoring and CV upload: 20 per minute (`Security__ExpensivePerMinute`). Whole API: 600 per minute | Tested (CI) |
| Real client address | Behind the supplied nginx the API reads the forwarded address, so one attacker cannot lock you out by using up the shared limit. Only turned on in the compose setup | Config tested; **not tested end to end through Docker** |
| Everything needs sign-in | Fallback authorization policy: every endpoint requires a token unless it is explicitly anonymous (login and health) | Tested (CI) |
| Response headers | API: `nosniff`, `DENY` framing, `no-referrer`, a CSP that allows nothing, `no-store` so browsers do not cache your data. Web server: the same plus a CSP that allows only this site's own scripts | API tested (CI). Web server headers checked in CI against the built image |
| Browser policy vs the real app | The production build is loaded in real Chromium under the exact policy nginx sends; sign-in and five pages must work without violations | Tested (CI, `web/e2e/smoke.py`) |
| Uploads | CV files up to 5 MB, PDF/DOCX/text only, read in memory and never stored; the whole request is capped at 7 MB (nginx and the API) | The 5 MB check is tested; the 7 MB caps are configuration only |
| Secrets | Passwords, signing key and AI key come only from the environment (`.env`, never committed). CI fails if a credential-shaped string or a `.env` file is committed | Tested (CI) |
| AI provider key | Never returned by any endpoint, never logged; provider error bodies are never shown | Tested (CI) |
| Untrusted text | Job postings and CV text are fenced and treated as data; the model has no tools, so injected instructions cannot do anything | Tested with fake models only |
| Containers | `no-new-privileges` on all; the API runs as a non-root user with all Linux capabilities dropped; database and API ports bind to `127.0.0.1` only | Config only; **never run in Docker by me** |
| Production start | Refuses to start with no database migrations instead of creating tables ad hoc | Written, unit-checked logic |
| Dependencies | Dependabot proposes updates weekly; CI reports known vulnerabilities in NuGet and npm packages (reports, does not block) | Configured |

## What it does NOT protect against
- **Someone with your password or your computer.** There is one password, no second factor.
- **Network eavesdropping.** The app speaks plain HTTP on localhost. If you reach it from another device, put **HTTPS** in front (see `OPERATIONS.md`). Never expose the plain ports to the internet.
- **Stolen tokens.** Tokens live in the browser tab (`sessionStorage`) and cannot be revoked before they expire. Closing the tab signs you out; changing `Auth__JwtKey` and restarting signs everything out.
- **A malicious or compromised AI provider.** If you turn AI on, the text you ask it to look at is sent to that provider (see below).
- **Mistakes in your own profile.** The system does not invent facts, but it cannot know whether what you marked verified is true.
- **Lost data.** Nothing is backed up unless you run the backup script.
- **Browser extensions or malware on your machine.**

## Data and AI
- Stored in your PostgreSQL database: your profile, job listings, applications, generated documents, an audit trail of actions (what happened, not document text).
- Sent to the AI provider **only if you configure one**: the posting text for "Analyze with AI"; your verified profile facts and the posting for a cover letter (without your email or phone); the whole CV text if you tick "Use AI" when importing a CV. Nothing is sent otherwise.
- A local model through Ollama keeps everything on your machine.
- Logs contain addresses, job and run ids, and error messages. They do not contain CV text, posting text, prompts, passwords or keys.

## If something goes wrong
- **AI key leaked:** revoke it at the provider, create a new one, update `.env`, run `docker compose up -d`.
- **Think someone knows your password:** change `AUTH_PASSWORD` and `AUTH_JWT_KEY` in `.env`, run `docker compose up -d api`. All sessions end.
- **Sent something you should not have:** CareerPilot cannot recall it. It never sends anything for you, so check what you uploaded yourself.
- **Found a vulnerability:** open a private security advisory on the GitHub repository.
