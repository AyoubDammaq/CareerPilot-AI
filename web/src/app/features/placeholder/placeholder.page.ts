import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NAV_ITEMS, NavItem, phaseLabel } from '../../core/nav';

@Component({
  selector: 'app-placeholder-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <h1>{{ item.label }}</h1>
    </header>
    <section class="card">
      <p class="badge">{{ label }}</p>
      <p>{{ item.blurb }}</p>
      <p class="muted">This page is not built yet. Nothing is shown here until it is.</p>
      <p><a routerLink="/dashboard">Back to the dashboard</a></p>
    </section>
  `,
})
export class PlaceholderPage {
  readonly item: NavItem;
  readonly label: string;

  constructor() {
    const path = inject(ActivatedRoute).snapshot.routeConfig?.path;
    this.item = NAV_ITEMS.find((n) => n.path === path) ?? NAV_ITEMS[0];
    this.label = this.item.phase ? phaseLabel(this.item.phase) : '';
  }
}
