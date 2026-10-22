import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api/api.service';
import { AiStatus, ImportApplyInput, ImportApplyResult, ImportProposal } from '../../core/models';
import { fieldErrors, problemMessage } from '../../shared/http-error';

const MAX_BYTES = 5 * 1024 * 1024;

@Component({
  selector: 'app-cv-import-page',
  imports: [RouterLink, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cv-import.page.html',
})
export class CvImportPage {
  private readonly api = inject(ApiService);

  readonly ai = signal<AiStatus | null>(null);
  readonly file = signal<File | null>(null);
  readonly useAi = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly proposal = signal<ImportProposal | null>(null);
  readonly result = signal<ImportApplyResult | null>(null);

  // What the owner ticked (indexes into the proposal lists).
  readonly contactOn = signal<Record<string, boolean>>({});
  readonly experiencesOn = signal<Set<number>>(new Set());
  readonly educationOn = signal<Set<number>>(new Set());
  readonly skillsOn = signal<Set<number>>(new Set());
  readonly projectsOn = signal<Set<number>>(new Set());
  readonly languagesOn = signal<Set<number>>(new Set());
  readonly markVerified = signal(false);

  constructor() {
    this.api.aiStatus().subscribe({ next: (s) => this.ai.set(s), error: () => this.ai.set(null) });
  }

  onFile(event: Event): void {
    const f = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.error.set(null);
    this.proposal.set(null);
    this.result.set(null);
    if (f && f.size > MAX_BYTES) {
      this.file.set(null);
      this.error.set('The file must be at most 5 MB.');
      return;
    }
    this.file.set(f);
  }

  onUseAi(event: Event): void {
    this.useAi.set((event.target as HTMLInputElement).checked);
  }

  preview(): void {
    const f = this.file();
    if (!f) {
      this.error.set('Choose a file first.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.result.set(null);
    this.api.previewImport(f, this.useAi()).subscribe({
      next: (p) => {
        this.busy.set(false);
        this.proposal.set(p);
        this.contactOn.set(Object.fromEntries(Object.entries(p.contact).filter(([, v]) => !!v).map(([k]) => [k, true])));
        const all = (n: number) => new Set(Array.from({ length: n }, (_, i) => i));
        this.experiencesOn.set(all(p.experiences.length));
        this.educationOn.set(all(p.education.length));
        this.skillsOn.set(all(p.skills.length));
        this.projectsOn.set(all(p.projects.length));
        this.languagesOn.set(all(p.languages.length));
        this.markVerified.set(false);
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(fieldErrors(e)['file']?.[0] ?? problemMessage(e, 'Could not read the file.'));
      },
    });
  }

  toggle(list: 'experiences' | 'education' | 'skills' | 'projects' | 'languages', i: number): void {
    const sig = { experiences: this.experiencesOn, education: this.educationOn, skills: this.skillsOn, projects: this.projectsOn, languages: this.languagesOn }[list];
    sig.update((s) => {
      const n = new Set(s);
      if (n.has(i)) n.delete(i);
      else n.add(i);
      return n;
    });
  }

  toggleContact(key: string): void {
    this.contactOn.update((c) => ({ ...c, [key]: !c[key] }));
  }

  onMarkVerified(event: Event): void {
    this.markVerified.set((event.target as HTMLInputElement).checked);
  }

  selectedCount(): number {
    const p = this.proposal();
    if (!p) return 0;
    return Object.values(this.contactOn()).filter(Boolean).length + this.experiencesOn().size + this.educationOn().size
      + this.skillsOn().size + this.projectsOn().size + this.languagesOn().size;
  }

  contactEntries(): [string, string][] {
    const c = this.proposal()?.contact;
    if (!c) return [];
    const labels: Record<string, string> = { fullName: 'Name', email: 'Email', phone: 'Phone', githubUrl: 'GitHub', linkedinUrl: 'LinkedIn' };
    return Object.entries(c).filter(([, v]) => !!v).map(([k, v]) => [k, `${labels[k] ?? k}: ${v}`]);
  }

  apply(): void {
    const p = this.proposal();
    if (!p || this.selectedCount() === 0) return;
    const on = this.contactOn();
    const pick = <T>(items: T[], set: Set<number>) => items.filter((_, i) => set.has(i));
    const input: ImportApplyInput = {
      version: p.profileVersion,
      markVerified: this.markVerified(),
      contact: {
        fullName: on['fullName'] ? p.contact.fullName : null,
        email: on['email'] ? p.contact.email : null,
        phone: on['phone'] ? p.contact.phone : null,
        githubUrl: on['githubUrl'] ? p.contact.githubUrl : null,
        linkedinUrl: on['linkedinUrl'] ? p.contact.linkedinUrl : null,
      },
      experiences: pick(p.experiences, this.experiencesOn()),
      education: pick(p.education, this.educationOn()),
      skills: pick(p.skills, this.skillsOn()),
      projects: pick(p.projects, this.projectsOn()),
      languages: pick(p.languages, this.languagesOn()),
    };
    this.busy.set(true);
    this.error.set(null);
    this.api.applyImport(input).subscribe({
      next: (r) => {
        this.busy.set(false);
        this.result.set(r);
        this.proposal.set(null);
        this.file.set(null);
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(problemMessage(e, 'Could not add these to your profile.'));
      },
    });
  }
}
