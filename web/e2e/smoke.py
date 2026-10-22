"""
Browser smoke test of the PRODUCTION build under the same Content-Security-Policy nginx sends.

It serves web/dist/web/browser with a fake API, signs in through the real form, and visits the main pages.
It fails if sign-in does not reach the dashboard, if any page throws, or if the browser reports a policy violation.
Unit tests cannot catch these: they never run the built bundle in a real browser.

Usage: python3 e2e/smoke.py   (after `npm run build`; needs `pip install playwright` and a Chromium)
Set CHROMIUM_PATH to use an existing Chromium binary instead of Playwright's own.
"""
import http.server, json, os, re, sys, threading
from playwright.sync_api import sync_playwright

HERE = os.path.dirname(os.path.abspath(__file__))
DIST = os.path.join(HERE, '..', 'dist', 'web', 'browser')
HEADERS = open(os.path.join(HERE, '..', 'security-headers.conf')).read()
CSP = re.search(r'add_header Content-Security-Policy "([^"]+)"', HEADERS).group(1)

SUMMARY = {"total": 0, "active": 0, "submitted": 0, "replied": 0, "rejected": 0, "interviewing": 0, "offers": 0,
           "responseRate": None, "medianDaysToResponse": None, "smallSample": True, "byStatus": [], "byCountry": [],
           "byScoreBand": [], "weekly": [], "notes": ["No applications yet."]}
PROFILE = {"fullName": "Test Owner", "email": "owner@example.test", "phone": None, "headline": "Engineer", "summary": "", "country": "Tunisia",
           "city": "Tunis", "githubUrl": None, "linkedinUrl": None, "websiteUrl": None, "version": 1, "workExperiences": [], "education": [],
           "skills": [], "projects": [], "languages": [],
           "preferences": {"targetCountries": ["TN"], "targetCities": [], "workModes": ["Remote"], "willingToRelocate": False,
                           "requiresVisaSponsorship": False, "includeInternationalRemote": True, "employmentTypes": ["Permanent"],
                           "targetTitles": [], "workingLanguages": ["English"], "includeSeniorRoles": False, "experienceLevel": "Mid",
                           "minSalary": None, "preferredSalary": None, "salaryCurrency": "EUR"}}
DASHBOARD = {"profileCompleteness": 50, "missingItems": [], "implementedModules": []}


class Handler(http.server.BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def reply(self, body, ctype):
        self.send_response(200)
        self.send_header('Content-Type', ctype)
        self.send_header('Content-Security-Policy', CSP)
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_POST(self):
        self.rfile.read(int(self.headers.get('Content-Length') or 0))
        self.reply(json.dumps({"token": "t", "expiresAt": "2030-01-01T00:00:00Z"}).encode(), 'application/json')

    def do_GET(self):
        path = self.path.split('?')[0]
        if path.startswith('/api/tracking/summary'):
            return self.reply(json.dumps(SUMMARY).encode(), 'application/json')
        if path.startswith('/api/profile'):
            return self.reply(json.dumps(PROFILE).encode(), 'application/json')
        if path.startswith('/api/dashboard'):
            return self.reply(json.dumps(DASHBOARD).encode(), 'application/json')
        if path.startswith('/api/ai/status'):
            return self.reply(json.dumps({"enabled": False, "provider": "", "model": "", "usedToday": 0, "dailyLimit": 0}).encode(), 'application/json')
        if path.startswith('/api/') or path.startswith('/health/'):
            return self.reply(b'[]', 'application/json')
        file = os.path.normpath(os.path.join(DIST, path.lstrip('/')))
        if path != '/' and file.startswith(os.path.normpath(DIST)) and os.path.isfile(file):
            ctype = {'js': 'text/javascript', 'css': 'text/css', 'ico': 'image/x-icon'}.get(file.rsplit('.', 1)[-1], 'application/octet-stream')
            return self.reply(open(file, 'rb').read(), ctype)
        self.reply(open(os.path.join(DIST, 'index.html'), 'rb').read(), 'text/html')


def main() -> int:
    server = http.server.ThreadingHTTPServer(('127.0.0.1', 0), Handler)
    base = f'http://127.0.0.1:{server.server_address[1]}'
    threading.Thread(target=server.serve_forever, daemon=True).start()

    problems = []
    with sync_playwright() as p:
        kwargs = {'executable_path': os.environ['CHROMIUM_PATH']} if os.environ.get('CHROMIUM_PATH') else {}
        browser = p.chromium.launch(**kwargs)
        page = browser.new_page()
        page.on('console', lambda m: problems.append(m.text) if ('Content Security Policy' in m.text or m.type == 'error') else None)
        page.on('pageerror', lambda e: problems.append(f'page error: {e}'))

        page.goto(base + '/')
        page.wait_for_selector('#password')
        page.wait_for_timeout(500)  # let the app finish starting
        page.fill('#password', 'a-long-enough-password')
        page.click('button[type=submit]')
        try:
            page.wait_for_url('**/dashboard', timeout=5000)
        except Exception:
            problems.append(f'sign-in did not reach the dashboard (stuck at {page.url})')
        else:
            for path, heading in [('analytics', 'Analytics'), ('applications', 'My Applications'), ('cv-library', 'Import CV'), ('profile', 'Career Profile')]:
                page.goto(f'{base}/{path}')
                page.wait_for_selector('h1')
                if heading not in page.inner_text('h1'):
                    problems.append(f'/{path} did not show "{heading}" (showed "{page.inner_text("h1")}")')
            if page.evaluate("getComputedStyle(document.body).backgroundColor") == 'rgba(0, 0, 0, 0)':
                problems.append('stylesheet did not load')
        browser.close()

    if problems:
        print('BROWSER SMOKE FAILED'); [print(' -', x) for x in problems]
        return 1
    print('Browser smoke passed: sign-in works and no policy violations on 5 pages.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
