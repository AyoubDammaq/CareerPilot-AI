import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { GroupStat, TrackingSummary } from '../../core/models';
import { problemMessage } from '../../shared/http-error';
import { statusLabel } from '../../shared/status-label';

@Component({
  selector: 'app-analytics-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './analytics.page.html',
})
export class AnalyticsPage {
  private readonly api = inject(ApiService);

  readonly summary = signal<TrackingSummary | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly maxWeek = computed(() => Math.max(1, ...(this.summary()?.weekly.map((w) => w.submitted) ?? [1])));
  readonly label = statusLabel;

  constructor() {
    this.api.trackingSummary().subscribe({
      next: (s) => {
        this.summary.set(s);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.loading.set(false);
        this.error.set(problemMessage(e, 'Could not load the statistics.'));
      },
    });
  }

  share(g: GroupStat): string {
    return g.submitted === 0 ? '—' : `${Math.round((100 * g.replied) / g.submitted)}%`;
  }

  bar(count: number): string {
    return `${Math.round((100 * count) / this.maxWeek())}%`;
  }
}
