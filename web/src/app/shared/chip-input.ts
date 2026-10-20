import { ChangeDetectionStrategy, Component, forwardRef, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/** Free-text list input. Enter or comma adds an item; each chip has a remove button. Value is string[]. */
@Component({
  selector: 'app-chip-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => ChipInput), multi: true }],
  template: `
    <div class="chips">
      @if (items().length > 0) {
        <ul class="chip-list" [attr.aria-label]="listLabel()">
          @for (item of items(); track $index) {
            <li class="chip">
              <span>{{ item }}</span>
              <button type="button" class="chip-remove" [disabled]="disabled()" (click)="remove($index)" [attr.aria-label]="'Remove ' + item">
                <span aria-hidden="true">&times;</span>
              </button>
            </li>
          }
        </ul>
      }
      <input
        type="text"
        [id]="inputId()"
        [placeholder]="placeholder()"
        [disabled]="disabled()"
        [attr.aria-describedby]="describedBy()"
        [attr.aria-invalid]="invalid() ? true : null"
        autocomplete="off"
        [value]="draft()"
        (input)="draft.set($any($event.target).value)"
        (keydown)="onKey($event)"
        (blur)="commitDraft(); touched()"
      />
    </div>
  `,
})
export class ChipInput implements ControlValueAccessor {
  readonly inputId = input.required<string>();
  readonly placeholder = input('Type and press Enter');
  readonly listLabel = input('Added items');
  readonly describedBy = input<string | null>(null);
  readonly invalid = input(false);

  protected readonly items = signal<string[]>([]);
  protected readonly draft = signal('');
  protected readonly disabled = signal(false);

  private onChange: (v: string[]) => void = () => undefined;
  protected touched: () => void = () => undefined;

  writeValue(v: string[] | null): void {
    this.items.set(Array.isArray(v) ? [...v] : []);
  }
  registerOnChange(fn: (v: string[]) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.touched = fn;
  }
  setDisabledState(d: boolean): void {
    this.disabled.set(d);
  }

  protected onKey(e: KeyboardEvent): void {
    if (e.key === 'Enter' || e.key === ',') {
      e.preventDefault(); // Enter must not submit the surrounding form
      this.commitDraft();
    } else if (e.key === 'Backspace' && this.draft() === '' && this.items().length > 0) {
      this.remove(this.items().length - 1);
    }
  }

  protected commitDraft(): void {
    const parts = this.draft()
      .split(',')
      .map((p) => p.trim())
      .filter((p) => p !== '');
    this.draft.set('');
    if (parts.length === 0) return;
    const next = [...this.items()];
    for (const p of parts) if (!next.some((n) => n.toLowerCase() === p.toLowerCase())) next.push(p);
    this.items.set(next);
    this.onChange(next);
  }

  protected remove(index: number): void {
    const next = this.items().filter((_, i) => i !== index);
    this.items.set(next);
    this.onChange(next);
  }
}
