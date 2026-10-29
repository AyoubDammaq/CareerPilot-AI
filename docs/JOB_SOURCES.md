# Job sources: what works, what does not, and why

Checked 2026-10-28/29 against each source's own published documentation or terms where I could read them.
"Mock-tested" means automated tests with fixtures; it is not a live call. "Live" means real data was retrieved on the owner's machine.

| Source | Status in CareerPilot | Evidence and limits |
|---|---|---|
| Greenhouse, Lever, Ashby | **Implemented, live** (857 real jobs read on the owner's machine) | Public per-company board feeds; a company watchlist is required. |
| Himalayas | **Implemented, mock-tested; not yet run live** | Public JSON API, no key (himalayas.app/api). Keyword search, 5 pages x 20 jobs per sync, 1 request per `BaseDelay`. Keeps the unchanged `applicationLink` and a Himalayas source link, drops expired jobs, runs only on the backend (all as its terms require). The `jobs` response key is assumed from the docs' wording and must be confirmed by the first real run (use "Test board"). |
| Manual import (pasted text, public job-page URL) | **Implemented, tested** | Reads schema.org JobPosting data only; never guesses. Used for every source below. |
| Indeed MCP | **Not usable from CareerPilot** | Indeed's docs (docs.indeed.com/mcp) state the server is "available only for Claude Connector" (Claude.ai / Claude Desktop, user signs in with an Indeed account), in beta. Nothing documents access for a developer's own backend, so none is attempted. Use it inside Claude, then paste the job text here. |
| LinkedIn | **Manual import only** | No public job-search API; the Talent/Marketing APIs are partner-only. Scraping is not done. LinkedIn URLs are never fetched. |
| Keejob, Tanitjobs | **Manual import only (text paste)** | Tanitjobs' terms allow personal, non-commercial use and forbid overloading the site; they say nothing explicit about automation and I found no API or feed. I found nothing verifiable for Keejob. Both are on the never-fetch list until someone confirms permission; change `Discovery__ManualOnlyHosts` to alter it. |
| Wellfound | **Manual import only** | No public API verified. |
| Remote OK | **Planned, not built** | Reported to have a keyless JSON feed that asks for a link back, but I could not read its documentation here, so field names and terms are unverified. |

## Eligibility for remote jobs
CareerPilot never labels a job "worldwide". Himalayas jobs carry the source's own `locationRestrictions` in the location text; an empty list is shown as "no country restriction listed by Himalayas" and the deterministic location check still marks it **needs review** unless a restriction names one of your target countries. Confirm with the employer before applying.

## Importing a job found elsewhere
Job Search page, "Add a job you found elsewhere".
- **By address:** https only, standard port, public hosts only. Private, loopback, link-local, cloud-metadata and internal names are refused; DNS answers are re-checked at connect time; redirects (max 3) are re-validated; 1 MB and 20 s caps; no cookies, proxy or login. Sites on the never-fetch list, login walls, CAPTCHAs and refusals produce a clear message, never a workaround.
- **By pasted text:** title, company, description required; location and link optional (a link is stored, never fetched).
- Imported jobs are attributed to the "Manual" source, de-duplicated against discovered jobs (same company, title and place link to one vacancy), filtered and scored by the same engine, and flagged as unverifiable: they become **Unverified** after `Discovery__StaleAfterDays`.
- Job text is untrusted data: stored verbatim, never executed, and it cannot trigger an application.

## Approval
Importing or discovering a job never creates an application. Applications still need your explicit approval, enforced in the backend.

## Not built (honest)
Scheduled syncs, per-source rate-limit settings in the UI, description-similarity dedup (keys use company, title and place), an automatic re-check of manual jobs, Remote OK, Keejob/Tanitjobs/LinkedIn/Wellfound automation.
