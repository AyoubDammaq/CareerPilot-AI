import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ImportProposal } from '../../core/models';
import { CvImportPage } from './cv-import.page';

const proposal: ImportProposal = {
  contact: { fullName: null, email: 'a@b.tn', phone: null, githubUrl: null, linkedinUrl: null },
  experiences: [{ company: 'Acme', title: 'Engineer', location: '', startDate: '2024-01-01', endDate: null, description: '', achievements: [] }],
  education: [],
  skills: ['C#', 'Angular'],
  projects: [],
  languages: [],
  notes: ['One item was dropped because the CV does not contain it.'],
  aiUsed: true,
  textCharacters: 1200,
  profileVersion: 3,
};

describe('CvImportPage', () => {
  let ctrl: HttpTestingController;

  function create(aiEnabled = true): ComponentFixture<CvImportPage> {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
    const f = TestBed.createComponent(CvImportPage);
    f.detectChanges();
    ctrl.expectOne('/api/ai/status').flush({ enabled: aiEnabled, provider: 'Anthropic', model: 'm', usedToday: 0, dailyLimit: 20 });
    f.detectChanges();
    return f;
  }

  const el = (f: ComponentFixture<CvImportPage>) => f.nativeElement as HTMLElement;
  const button = (f: ComponentFixture<CvImportPage>, text: string) =>
    Array.from(el(f).querySelectorAll('button')).find((b) => b.textContent!.includes(text)) as HTMLButtonElement;

  function pick(f: ComponentFixture<CvImportPage>) {
    const input = el(f).querySelector('#cv-file') as HTMLInputElement;
    const file = new File(['x'], 'cv.pdf', { type: 'application/pdf' });
    Object.defineProperty(input, 'files', { value: [file], configurable: true });
    input.dispatchEvent(new Event('change'));
    f.detectChanges();
  }

  function readIt(f: ComponentFixture<CvImportPage>) {
    pick(f);
    button(f, 'Read the CV').click();
    ctrl.expectOne('/api/profile/import/preview').flush(proposal);
    f.detectChanges();
  }

  it('warns that the text goes to the provider when AI is available', () => {
    const f = create();
    expect(el(f).textContent).toContain('sends the text of your CV');
  });

  it('disables the AI option when it is not configured', () => {
    const f = create(false);
    expect((el(f).querySelector('#use-ai') as HTMLInputElement).disabled).toBe(true);
    expect(el(f).textContent).toContain('AI is not configured');
  });

  it('needs a file before reading', () => {
    const f = create();
    expect(button(f, 'Read the CV').disabled).toBe(true);
  });

  it('shows the proposal and notes, with everything ticked and verification off', () => {
    const f = create();
    readIt(f);
    expect(el(f).textContent).toContain('Engineer, Acme');
    expect(el(f).textContent).toContain('does not contain it');
    expect((el(f).querySelector('#mv') as HTMLInputElement).checked).toBe(false);
    expect(button(f, 'Add 4 items').disabled).toBe(false);
  });

  it('sends only what is still ticked, and not verified by default', () => {
    const f = create();
    readIt(f);
    (el(f).querySelector('#s-1') as HTMLInputElement).click();
    f.detectChanges();
    button(f, 'Add 3 items').click();
    const req = ctrl.expectOne('/api/profile/import/apply');
    expect(req.request.body.skills).toEqual(['C#']);
    expect(req.request.body.markVerified).toBe(false);
    expect(req.request.body.version).toBe(3);
    expect(req.request.body.contact.email).toBe('a@b.tn');
    req.flush({ added: 3, skipped: [] });
    f.detectChanges();
    expect(el(f).textContent).toContain('3 items were added');
  });

  it('shows an error when reading fails', () => {
    const f = create();
    pick(f);
    button(f, 'Read the CV').click();
    ctrl.expectOne('/api/profile/import/preview').flush({ title: 'Bad', detail: 'No text found.' }, { status: 400, statusText: 'Bad' });
    f.detectChanges();
    expect(el(f).querySelector('[role=alert]')).not.toBeNull();
  });
});
