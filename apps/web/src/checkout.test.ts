import { afterEach, expect, it, vi } from "vitest";
import { checkoutKey, releaseCheckout, readSongDraft, saveSongDraft } from "./checkout";
afterEach(() => {
  sessionStorage.clear();
  releaseCheckout("test", false);
  vi.restoreAllMocks();
});
it("reuses the key for the exact request and changes it when the cart changes", () => {
  const body = { items: [{ quantity: 1 }] };
  const key = checkoutKey("test", body);
  expect(checkoutKey("test", body)).toBe(key);
  expect(checkoutKey("test", { items: [{ quantity: 2 }] })).not.toBe(key);
});
it("keeps a stable key in memory when browser storage is unavailable", () => {
  vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
    throw new Error("blocked");
  });
  vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
    throw new Error("blocked");
  });
  const body = { items: [] };
  expect(checkoutKey("test", body)).toBe(checkoutKey("test", body));
});
it("releases a failed attempt but preserves the cart for retry", () => {
  sessionStorage.setItem("kahve.cart.v1.test", "cart");
  const key = checkoutKey("test", {});
  releaseCheckout("test", false);
  expect(checkoutKey("test", {})).not.toBe(key);
  expect(sessionStorage.getItem("kahve.cart.v1.test")).toBe("cart");
});
it("clears the cart only after a server-confirmed successful payment", () => {
  sessionStorage.setItem("kahve.cart.v1.test", "cart");
  checkoutKey("test", {});
  releaseCheckout("test", true);
  expect(sessionStorage.getItem("kahve.cart.v1.test")).toBeNull();
  expect(sessionStorage.getItem("kahve.checkout.v1.test")).toBeNull();
});
it('keeps song drafts isolated by branch and preserves them on failed payment', () => {
  saveSongDraft('test', 'Scorpions');
  saveSongDraft('other', 'Sezen Aksu');
  releaseCheckout('test', false);
  expect(readSongDraft('test')).toBe('Scorpions');
  releaseCheckout('test', true);
  expect(readSongDraft('test')).toBe('');
  expect(readSongDraft('other')).toBe('Sezen Aksu');
});
it('ignores oversized stored drafts and remains usable when storage is blocked', () => {
  sessionStorage.setItem('kahve.song-draft.v1.test', 'x'.repeat(241));
  expect(readSongDraft('test')).toBe('');
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('blocked'); });
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('blocked'); });
  expect(readSongDraft('test')).toBe('');
  expect(() => saveSongDraft('test', 'song')).not.toThrow();
});
