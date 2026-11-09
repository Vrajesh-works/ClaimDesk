/**
 * Client-side mirror of EvidencePolicy on the API. It only gives fast feedback; the API
 * re-checks size, extension and file signature and is the authority.
 */
export const EVIDENCE_MAX_BYTES = 10 * 1024 * 1024;
export const EVIDENCE_EXTENSIONS = ['.pdf', '.png', '.jpg', '.jpeg'];
export const EVIDENCE_ACCEPT = EVIDENCE_EXTENSIONS.join(',');

export function checkEvidence(file: Pick<File, 'name' | 'size'>): string | null {
  if (file.size <= 0) {
    return 'File is empty.';
  }
  if (file.size > EVIDENCE_MAX_BYTES) {
    return 'File exceeds the 10 MB limit.';
  }
  const dot = file.name.lastIndexOf('.');
  const ext = dot >= 0 ? file.name.slice(dot).toLowerCase() : '';
  if (!EVIDENCE_EXTENSIONS.includes(ext)) {
    return 'Only PDF, PNG and JPEG files are accepted.';
  }
  return null;
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
