import { afterEach, describe, expect, it, vi } from "vitest";
import { api, ApiError } from "./api";

afterEach(() => vi.unstubAllGlobals());
describe("API client", () => {
  it("obtains a fresh CSRF token before a mutation and sends same-origin cookies", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ token: "csrf-for-session" }), {
          status: 200,
        }),
      )
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    await api("/api/session/logout", { method: "POST" });
    const [, options] = fetchMock.mock.calls[1];
    expect(options.headers.get("X-CSRF-TOKEN")).toBe("csrf-for-session");
    expect(options.credentials).toBe("same-origin");
  });
  it("does not send the mutation when CSRF acquisition fails", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(null, { status: 503 }));
    vi.stubGlobal("fetch", fetchMock);
    await expect(
      api("/api/session/login", { method: "POST" }),
    ).rejects.toBeInstanceOf(ApiError);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
  it("surfaces concurrency errors rather than treating a rejected update as success", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ message: "Şube değişti." }), {
          status: 409,
        }),
      ),
    );
    await expect(api("/api/test")).rejects.toMatchObject({
      status: 409,
      message: "Şube değişti.",
    });
  });
});
