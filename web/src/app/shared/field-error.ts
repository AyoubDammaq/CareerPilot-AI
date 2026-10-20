import { ChangeDetectionStrategy, Component, effect, input, signal } from '@angular/core';
import { AbstractControl } from '@angular/forms';

/** Shows the server and client validation messages of a control. Wire it with aria-describedby via `errorId`. */
@Component({
  selector: 'app-field-error',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.id]': 'errorId()' },
  template: `@for (m of messages(); track $index) {<p class="field-error">{{ m }}</p>}`,
})
export class FieldError {
  readonly control = input.required<AbstractControl>();
  readonly errorId = input<string | null>(null);
  protected readonly messages = signal<string[]>([]);

  constructor() {
    effect((onCleanup) => {
      const c = this.control();
      this.refresh(c);
      const sub = c.events.subscribe(() => this.refresh(c));
      onCleanup(() => sub.unsubscribe());
    });
  }

  private refresh(c: AbstractControl): void {
    const e = c.errors;
    const out: string[] = [];
    if (e) {
      const server = e['server'] as string[] | undefined;
      if (server) out.push(...server);
      if (c.touched || c.dirty) {
        if (e['required']) out.push('This field is required.');
        if (e['email']) out.push('Enter a valid email address.');
      }
    }
    const prev = this.messages();
    if (prev.length !== out.length || prev.some((p, i) => p !== out[i])) this.messages.set(out);
  }
}
