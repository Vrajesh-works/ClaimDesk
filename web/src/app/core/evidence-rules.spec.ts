import { EVIDENCE_MAX_BYTES, checkEvidence, formatBytes } from './evidence-rules';

describe('checkEvidence', () => {
  it('accepts PDF, PNG and JPEG within the size limit', () => {
    for (const name of ['a.pdf', 'b.PNG', 'c.jpg', 'd.jpeg']) {
      expect(checkEvidence({ name, size: 1024 })).withContext(name).toBeNull();
    }
  });

  it('rejects other extensions, empty files and oversize files', () => {
    expect(checkEvidence({ name: 'notes.txt', size: 10 })).toBe('Only PDF, PNG and JPEG files are accepted.');
    expect(checkEvidence({ name: 'noext', size: 10 })).toBe('Only PDF, PNG and JPEG files are accepted.');
    expect(checkEvidence({ name: 'a.png', size: 0 })).toBe('File is empty.');
    expect(checkEvidence({ name: 'a.png', size: EVIDENCE_MAX_BYTES + 1 })).toBe('File exceeds the 10 MB limit.');
  });

  it('formats sizes', () => {
    expect(formatBytes(512)).toBe('512 B');
    expect(formatBytes(2048)).toBe('2.0 KB');
    expect(formatBytes(3 * 1024 * 1024)).toBe('3.0 MB');
  });
});
