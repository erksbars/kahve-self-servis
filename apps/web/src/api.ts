export type Product = {
  imageUrl?: string | null;
  imageAlt?: string | null;
  id: string;
  name: string;
  description: string;
  category: string;
  price: number;
  available: boolean;
  optionGroups: OptionGroup[];
};
export type OptionValue = {
  id: string;
  name: string;
  priceDelta: number;
  available: boolean;
};
export type OptionGroup = {
  id: string;
  name: string;
  minSelect: number;
  maxSelect: number;
  values: OptionValue[];
};
export type QuoteItem = {
  productId: string;
  name: string;
  quantity: number;
  basePrice: number;
  unitPrice: number;
  lineTotal: number;
  options: { id: string; group: string; name: string; priceDelta: number }[];
};
export type Quote = {
  branchSlug: string;
  currency: string;
  total: number;
  calculatedAt: string;
  items: QuoteItem[];
};
export type Branch = {
  preparationCapacity?: number;
  isOpenNow?: boolean;
  id: string;
  name: string;
  slug: string;
  isAcceptingOrders: boolean;
  version: number;
};
export type Menu = {
  checkoutProvider?: string;
  checkoutEnabled?: boolean;
  branch: Branch & { address: string };
  merchant: string;
  currency: string;
  demo: boolean;
  products: Product[];
};
export type Session = { email: string; role: "Owner" | "Barista" };
export type CheckoutReply = {
  paymentUrl: string;
  amount: number;
  currency: string;
  expiresAt: string;
};
export type PaymentSession = {
  amount: number;
  currency: string;
  paymentStatus: "Pending" | "Paid" | "Failed" | "Expired";
  expiresAt: string;
  branch: { name: string; slug: string };
  items: QuoteItem[];
  trackingUrl: string | null;
  demo: boolean;
};
export type TrackedOrder = {
  songRequest?: { text: string; status: string } | null;
  publicNumber: number;
  status: string;
  paymentStatus: string;
  total: number;
  currency: string;
  createdAt: string;
  paidAt: string | null;
  branch: { name: string; slug: string };
  items: QuoteItem[];
  demo: boolean;
};
export type StaffOrder = {
  id: string;
  publicNumber: number;
  status: "Received" | "Preparing" | "Ready";
  version: number;
  paidAt: string;
  items: QuoteItem[];
  paymentVerified: boolean;
};
export class ApiError extends Error {
  status: number;
  constructor(message: string, status: number) {
    super(message);
    this.status = status;
  }
}
export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  if (init?.body && !headers.has("Content-Type") && !(init.body instanceof FormData)) headers.set("Content-Type", "application/json");
  if (init?.method && init.method !== "GET") {
    const csrf = await fetch("/api/session/csrf", {
      credentials: "same-origin",
    });
    if (!csrf.ok)
      throw new ApiError(
        "Güvenlik doğrulaması alınamadı. Tekrar deneyin.",
        csrf.status,
      );
    headers.set("X-CSRF-TOKEN", (await csrf.json()).token);
  }
  const response = await fetch(path, {
    ...init,
    headers,
    credentials: "same-origin",
  }).catch(() => {
    throw new ApiError("Bağlantı kurulamadı. Tekrar deneyin.", 0);
  });
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new ApiError(
      body.message ??
        (response.status === 401
          ? "Oturum açmanız gerekiyor."
          : response.status === 404
            ? "Kayıt bulunamadı."
            : "İşlem tamamlanamadı. Tekrar deneyin."),
      response.status,
    );
  }
  return response.status === 204 ? (undefined as T) : response.json();
}
export const money = (amount: number) =>
  new Intl.NumberFormat("tr-TR", {
    style: "currency",
    currency: "TRY",
    maximumFractionDigits: 2,
  }).format(amount);
export function filterProducts(
  products: Product[],
  category: string,
  search: string,
) {
  return products.filter(
    (p) =>
      (category === "Tümü" || p.category === category) &&
      `${p.name} ${p.description}`
        .toLocaleLowerCase("tr-TR")
        .includes(search.toLocaleLowerCase("tr-TR").trim()),
  );
}
