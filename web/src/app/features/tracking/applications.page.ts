import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { ApplicationDto, Reminder } from '../../core/models';
import { problemMessage } from '../../shared/http-error';
import { statusLabel } from '../../shared/status-label';

const FINISHED = ['RejectedByEmployer', 'OfferAccepted', 'OfferDeclined', 'Withdrawn', 'ClosedOrExpired'];

@Component({
  selector: 'app-applications-page',
  imports: [DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './applications.page.html',
})
export class ApplicationsPage {
  private readonly api = inject(ApiService);

  readonly apps = signal<ApplicationDto[]>([]);
  readonly reminders = signal<Reminder[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly showFinished = signal(false);

  readonly visible = computed(() => this.apps().filter((a) => this.showFinished() || !FINISHED.includes(a.status)));
  readonly hiddenCount = computed(() => this.apps().length - this.apps().filter((a) => !FINISHED.includes(a.status)).length);

  readonly label = statusLabel;

  constructor() {
    this.api.listApplications().subscribe({
      next: (a) => {
        this.apps.set(a);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.loading.set(false);
        this.error.set(problemMessage(e, 'Could not load your applications.'));
      },
    });
    // Reminders are a convenience: the list still works if they fail.
    this.api.trackingReminders().subscribe({ next: (r) => this.reminders.set(r), error: () => this.reminders.set([]) });
  }

  toggleFinished(event: Event): void {
    this.showFinished.set((event.target as HTMLInputElement).checked);
  }

  remindersFor(id: string): Reminder[] {
    return this.reminders().filter((r) => r.applicationId === id);
  }
}
