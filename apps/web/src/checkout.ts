// One key per exact checkout request; retained across refresh/network failure.
const memory = new Map<string, { fingerprint: string; key: string }>();
export function checkoutKey(slug: string, request: unknown): string {
  const fingerprint = JSON.stringify(request);
  const storageKey = `kahve.checkout.v1.${slug}`;
  try {
    const saved = JSON.parse(sessionStorage.getItem(storageKey) ?? "null");
    if (
      saved?.fingerprint === fingerprint &&
      typeof saved.key === "string" &&
      /^[a-f0-9-]{36}$/i.test(saved.key)
    ) {
      memory.set(slug, saved);
      return saved.key;
    }
  } catch {
    /* Ignore malformed browser state. */
  }
  const previous = memory.get(slug);
  if (previous?.fingerprint === fingerprint) return previous.key;
  const key = crypto.randomUUID();
  memory.set(slug, { fingerprint, key });
  try {
    sessionStorage.setItem(storageKey, JSON.stringify({ fingerprint, key }));
  } catch {
    /* Browser may disable storage. */
  }
  return key;
}
export function releaseCheckout(slug: string, paid: boolean) {
  memory.delete(slug);
  try {
    sessionStorage.removeItem(`kahve.checkout.v1.${slug}`);
    if (paid) {
      sessionStorage.removeItem(`kahve.cart.v1.${slug}`);
      sessionStorage.removeItem(`kahve.song-draft.v1.${slug}`);
    }
  } catch {
    /* UI remains usable without storage. */
  }
}
export function readSongDraft(slug: string): string {
  try { const text = sessionStorage.getItem(`kahve.song-draft.v1.${slug}`); return text && text.length <= 240 ? text : ''; }
  catch { return ''; }
}
export function saveSongDraft(slug: string, text: string) {
  try { if (text) sessionStorage.setItem(`kahve.song-draft.v1.${slug}`, text.slice(0, 240)); else sessionStorage.removeItem(`kahve.song-draft.v1.${slug}`); }
  catch { /* Keep the form usable without storage. */ }
}
