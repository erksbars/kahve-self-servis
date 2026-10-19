import { describe, expect, it } from "vitest";
import { addLine, lineKey, readCart } from "./cart";
const id = "11111111-1111-1111-1111-111111111111";
const option = "22222222-2222-2222-2222-222222222222";
const line = {
  key: lineKey(id, [option]),
  productId: id,
  quantity: 1,
  optionValueIds: [option],
};
describe("guest cart", () => {
  it("combines identical selections independent of selection order", () => {
    expect(lineKey(id, ["a", "b"])).toBe(lineKey(id, ["b", "a"]));
    expect(addLine([line], line)[0].quantity).toBe(2);
    expect(
      addLine([line], { ...line, key: lineKey(id, []), optionValueIds: [] }),
    ).toHaveLength(2);
  });
  it("isolates carts by branch and recovers from invalid storage", () => {
    sessionStorage.clear();
    sessionStorage.setItem("kahve.cart.v1.moda", JSON.stringify([line]));
    expect(readCart("moda")).toHaveLength(1);
    expect(readCart("besiktas")).toHaveLength(0);
    sessionStorage.setItem("kahve.cart.v1.moda", "{broken");
    expect(readCart("moda")).toEqual([]);
    sessionStorage.setItem(
      "kahve.cart.v1.moda",
      JSON.stringify([{ ...line, quantity: 999 }]),
    );
    expect(readCart("moda")).toEqual([]);
  });
  it("rejects additions beyond the per-selection limit without changing the cart", () => {
    const cart = [{ ...line, quantity: 10 }];
    expect(() => addLine(cart, line)).toThrow("en fazla 10");
    expect(cart[0].quantity).toBe(10);
  });
});
