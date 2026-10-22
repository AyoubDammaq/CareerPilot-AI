# Running CareerPilot AI for real

## 1. First start
```bash
cp .env.example .env     # edit it: POSTGRES_PASSWORD, AUTH_PASSWORD (12+ chars), AUTH_JWT_KEY (openssl rand -base64 48)
docker compose up -d --build
```
The API refuses to start if the password or key is missing, too short, or still the example text. Open http://localhost:4200.

## 2. Reaching it from other devices: add HTTPS
The app is bound to `127.0.0.1` on purpose. To use it from your phone or another computer, put a reverse proxy with a real certificate in front of the **web** container (port 4200), for example Caddy:
```
careerpilot.example.com {
    reverse_proxy 127.0.0.1:4200
}
```
Then set `SECURITY_FORWARD_LIMIT=2` in `.env` (two proxies now sit in front of the API: yours and nginx) and `docker compose up -d`. Do **not** publish ports 5080 or 5432. Without HTTPS your password and token cross the network in clear text.

## 3. Optional: AI
Uncomment and fill the `LLM_*` lines in `.env`, then `docker compose up -d api`. Read the "Data and AI" section of `SECURITY.md` first. `LLM_DAILY_CALL_LIMIT` caps cost.

## 4. Backups
```bash
scripts/backup.sh                 # writes backups/careerpilot-<time>.dump, keeps the newest 14
KEEP=30 scripts/backup.sh /path/to/folder
scripts/restore.sh backups/careerpilot-<time>.dump     # replaces current data; asks you to type RESTORE
```
- Backups contain your personal data. They are created readable by you only. Keep them somewhere you trust and, ideally, off this machine.
- Run it daily with cron: `0 2 * * * cd /path/to/careerpilot-ai && scripts/backup.sh >> backups/backup.log 2>&1`
- **Test a restore** on a spare machine once. A backup you have never restored is a hope, not a backup.
- The scripts were exercised against a real PostgreSQL 16 (backup, read-back check, restore, retention, bad-file rejection). The `docker compose exec` calls inside them were not run in Docker.

## 5. Updating
```bash
git pull
scripts/backup.sh
docker compose up -d --build
```
Database migrations are applied when the API starts. If a start fails, `docker compose logs api` says why; restore the backup if needed.

## 6. Health and logs
- `http://localhost:5080/health/live` (process is up) and `/health/ready` (database reachable).
- `docker compose logs -f api`. Failed sign-ins appear as `Failed sign-in from <address>`.

## 7. Settings reference (environment variables)
| Variable | Default | Meaning |
|---|---|---|
| `Security__LoginPerMinute` | 5 | Sign-in attempts per minute per address |
| `Security__ExpensivePerMinute` | 20 | AI, search, re-score and CV-upload calls per minute |
| `Security__GlobalPerMinute` | 600 | Any API call per minute |
| `Security__MaxRequestBodyMb` | 7 | Largest request accepted |
| `Security__TrustForwardedHeaders` / `ForwardLimit` | on / 1 in compose | Read the client address from the proxy |
| `Auth__TokenLifetimeMinutes` | 480 | How long a sign-in lasts |
| `Llm__*` | off | AI provider, model, key, limits |
| `Scoring__*`, `Tracking__*`, `Discovery__*` | see README | Match weights, reminder thresholds, search limits |

## 8. What has and has not been verified
Verified in CI: the application logic, the database, the API and its security headers and rate limits, the production web build in real Chromium, the web server configuration and headers in the built image, the compose file's syntax.
**Not verified by anyone yet:** a full `docker compose up` run, HTTPS through a real proxy, real job-board feeds, a real AI provider, opening the generated PDF and DOCX in Word or an ATS, and backup and restore through Docker. Please do these once on your own machine and tell me what you see.
