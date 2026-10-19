import { useState } from "react";
import type { FormEvent } from "react";
import { Plus, Minus } from "lucide-react";
import { money } from "./api";
import type { Product } from "./api";
import { Dialog } from "./Dialog";
import { ProductPhoto } from "./ProductPhoto";
import { lineKey, previewUnit } from "./cart";
import type { CartLine } from "./cart";

export function ProductPicker({
  product,
  accepting,
  close,
  add,
}: {
  product: Product;
  accepting: boolean;
  close: () => void;
  add: (line: CartLine) => void;
}) {
  const [selected, setSelected] = useState<string[]>(() =>
    product.optionGroups.flatMap((g) =>
      g.values
        .filter((v) => v.available)
        .slice(0, g.minSelect)
        .map((v) => v.id),
    ),
  );
  const [quantity, setQuantity] = useState(1);
  const [error, setError] = useState("");
  const valid = product.optionGroups.every((g) => {
    const count = g.values.filter(
      (v) => selected.includes(v.id) && v.available,
    ).length;
    return count >= g.minSelect && count <= g.maxSelect;
  });
  function submit(e: FormEvent) {
    e.preventDefault();
    if (!valid || !accepting || !product.available) return;
    try {
      add({
        key: lineKey(product.id, selected),
        productId: product.id,
        quantity,
        optionValueIds: selected,
      });
      close();
    } catch (error) {
      setError((error as Error).message);
    }
  }
  return (
    <Dialog title={product.name} close={close}>
      {product.imageUrl && <ProductPhoto src={product.imageUrl} alt={product.imageAlt || product.name} className="picker-photo" eager />}
      <p className="picker-description">{product.description}</p>
      <form onSubmit={submit}>
        {product.optionGroups.map((g) => {
          const count = g.values.filter((v) => selected.includes(v.id)).length;
          return (
            <fieldset className="option-group" key={g.id}>
              <legend>
                {g.name}{" "}
                <span>
                  {g.minSelect > 0 ? "Zorunlu" : "İsteğe bağlı"} ·{" "}
                  {g.maxSelect === 1 ? "1 seçim" : `En fazla ${g.maxSelect}`}
                </span>
              </legend>
              {g.values.map((v) => (
                <label
                  className={`option-row ${!v.available ? "disabled-option" : ""}`}
                  key={v.id}
                >
                  <input
                    type={g.maxSelect === 1 ? "radio" : "checkbox"}
                    name={g.id}
                    checked={selected.includes(v.id)}
                    disabled={
                      !v.available ||
                      (g.maxSelect > 1 &&
                        count >= g.maxSelect &&
                        !selected.includes(v.id))
                    }
                    onChange={(e) => {
                      setError("");
                      setSelected((previous) =>
                        g.maxSelect === 1
                          ? [
                              ...previous.filter(
                                (id) =>
                                  !g.values.some((option) => option.id === id),
                              ),
                              v.id,
                            ]
                          : e.target.checked
                            ? [...previous, v.id]
                            : previous.filter((id) => id !== v.id),
                      );
                    }}
                  />
                  <span>
                    {v.name}
                    {!v.available && <small>Şu an yok</small>}
                  </span>
                  <strong>
                    {v.priceDelta === 0 ? "Dahil" : `+${money(v.priceDelta)}`}
                  </strong>
                </label>
              ))}
            </fieldset>
          );
        })}
        <div className="quantity-row">
          <span>Adet</span>
          <div className="quantity-control">
            <button
              type="button"
              aria-label="Adedi azalt"
              onClick={() => setQuantity((q) => q - 1)}
              disabled={quantity === 1}
            >
              <Minus size={17} />
            </button>
            <output aria-label="Seçilen adet">{quantity}</output>
            <button
              type="button"
              aria-label="Adedi artır"
              onClick={() => setQuantity((q) => q + 1)}
              disabled={quantity === 10}
            >
              <Plus size={17} />
            </button>
          </div>
        </div>
        {error && (
          <p className="error" role="alert">
            {error}
          </p>
        )}
        {!valid && <p className="notice">Zorunlu seçenekleri tamamlayın.</p>}
        <button
          className="primary picker-submit"
          disabled={!valid || !accepting || !product.available}
        >
          Sepete ekle{" "}
          <strong>{money(previewUnit(product, selected) * quantity)}</strong>
        </button>
        <p className="small-note">Fiyat doğrulaması sepet adımında yapılır.</p>
      </form>
    </Dialog>
  );
}
