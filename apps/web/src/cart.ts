import type { Product } from "./api";
export type CartLine = {
  key: string;
  productId: string;
  quantity: number;
  optionValueIds: string[];
};
export function lineKey(productId: string, optionIds: string[]) {
  return `${productId}:${[...optionIds].sort().join(",")}`;
}
export function readCart(slug: string): CartLine[] {
  try {
    const data: unknown = JSON.parse(
      sessionStorage.getItem(`kahve.cart.v1.${slug}`) ?? "[]",
    );
    const uuid = /^[a-f0-9-]{36}$/i;
    if (!Array.isArray(data) || data.length > 20) return [];
    if (
      data.some(
        (x) =>
          !x ||
          typeof x.productId !== "string" ||
          !uuid.test(x.productId) ||
          !Number.isInteger(x.quantity) ||
          x.quantity < 1 ||
          x.quantity > 10 ||
          !Array.isArray(x.optionValueIds) ||
          x.optionValueIds.length > 10 ||
          x.optionValueIds.some(
            (id: unknown) => typeof id !== "string" || !uuid.test(id),
          ) ||
          new Set(x.optionValueIds).size !== x.optionValueIds.length,
      )
    )
      return [];
    const lines = data.map((x) => ({
      productId: x.productId,
      quantity: x.quantity,
      optionValueIds: x.optionValueIds,
      key: lineKey(x.productId, x.optionValueIds),
    }));
    return new Set(lines.map((x) => x.key)).size === lines.length ? lines : [];
  } catch {
    return [];
  }
}
export function previewUnit(product: Product | undefined, optionIds: string[]) {
  if (!product) return 0;
  return (
    product.price +
    product.optionGroups
      .flatMap((g) => g.values)
      .filter((v) => optionIds.includes(v.id))
      .reduce((total, v) => total + v.priceDelta, 0)
  );
}
export function addLine(lines: CartLine[], next: CartLine): CartLine[] {
  const existing = lines.find((x) => x.key === next.key);
  if (existing && existing.quantity + next.quantity > 10)
    throw new Error(
      "Aynı ürün ve seçenekler için en fazla 10 adet ekleyebilirsiniz.",
    );
  if (!existing && lines.length >= 20)
    throw new Error("Sepette en fazla 20 farklı ürün seçimi olabilir.");
  if (lines.reduce((sum, x) => sum + x.quantity, 0) + next.quantity > 50)
    throw new Error("Sepette en fazla 50 adet olabilir.");
  return existing
    ? lines.map((x) =>
        x.key === next.key ? { ...x, quantity: x.quantity + next.quantity } : x,
      )
    : [...lines, next];
}
