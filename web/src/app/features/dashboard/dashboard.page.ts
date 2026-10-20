import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { DashboardDto } from '../../core/models';
import { NAV_ITEMS, phaseLabel } from '../../core/nav';

@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.page.html',
})
export class DashboardPage {
  private readonly api = inject(ApiService);

  readonly data = signal<DashboardDto | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly upcoming = NAV_ITEMS.filter((n) => n.phase !== null).map((n) => ({ ...n, label2: phaseLabel(n.phase!) }));

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getDashboard().subscribe({
      next: (d) => {
        this.data.set(d);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.loading.set(false);
        this.error.set(
          e instanceof HttpErrorResponse && e.status === 0 ? 'Cannot reach the server.' : 'Could not load the dashboard.',
        );
      },
    });
  }

  /** Clamp to 0-100 for display. */
  pct(d: DashboardDto): number {
    return Math.max(0, Math.min(100, Math.round(d.profileCompleteness)));
  }
}
