import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { NAV_ITEMS, phaseLabel } from '../core/nav';

@Component({
  selector: 'app-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="skip-link" href="#main">Skip to content</a>
    <div class="shell">
      <header class="topbar">
        <span class="brand">CareerPilot AI</span>
        <button type="button" class="btn nav-toggle" [attr.aria-expanded]="open()" aria-controls="site-nav" (click)="open.set(!open())">
          {{ open() ? 'Close menu' : 'Menu' }}
        </button>
      </header>
      <nav id="site-nav" class="sidebar" [class.open]="open()" aria-label="Main">
        <span class="brand brand-side">CareerPilot AI</span>
        <ul>
          @for (n of items; track n.path) {
            <li>
              <a [routerLink]="'/' + n.path" routerLinkActive="active" ariaCurrentWhenActive="page">
                {{ n.label }}
                @if (n.phase) {
                  <span class="soon">{{ soon(n.phase) }}</span>
                }
              </a>
            </li>
          }
        </ul>
        <button type="button" class="btn btn-block signout" (click)="signOut()">Sign out</button>
      </nav>
      <main id="main" class="content" tabindex="-1">
        <router-outlet />
      </main>
    </div>
  `,
})
export class Shell {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly items = NAV_ITEMS;
  readonly open = signal(false);

  constructor() {
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe(() => this.open.set(false));
  }

  soon(phase: string): string {
    return phaseLabel(phase).replace('Available in ', '');
  }

  signOut(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}
