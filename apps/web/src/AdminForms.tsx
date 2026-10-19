import { useState } from "react";
import type { FormEvent, ReactNode } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "./api";
import { Dialog } from "./Dialog";
import { ProductImageEditor } from "./ProductImageEditor";
import { EMPTY_ID } from "./adminTypes";
import type {
  AdminCategory,
  AdminProduct,
  AdminGroup,
  AdminBranch,
  AdminStaff,
  Catalog,
  Hours,
} from "./adminTypes";

function SaveForm({
  path,
  method,
  body,
  children,
  close,
  onSaved,
  validation,
}: {
  path: string;
  method: string;
  body: unknown;
  children: ReactNode;
  close: () => void;
  onSaved?: (value: { invitationUrl?: string }) => void;
  validation?: string;
}) {
  const qc = useQueryClient();
  const save = useMutation({
    mutationFn: () =>
      api<{ invitationUrl?: string }>(path, {
        method,
        body: JSON.stringify(body),
      }),
    onSuccess: (value) => {
      void qc.invalidateQueries({ queryKey: ["admin"] });
      void qc.invalidateQueries({ queryKey: ["menu"] });
      void qc.invalidateQueries({ queryKey: ["staff-branches"] });
      onSaved?.(value);
      close();
    },
  });
  function submit(e: FormEvent) {
    e.preventDefault();
    if (!validation) save.mutate();
  }
  return (
    <form className="admin-form" onSubmit={submit}>
      {children}
      {validation && (
        <p className="error" role="alert">
          {validation}
        </p>
      )}
      {save.error && (
        <p className="error" role="alert">
          {save.error.message}
        </p>
      )}
      <div className="form-actions">
        <button
          type="button"
          className="secondary"
          disabled={save.isPending}
          onClick={close}
        >
          Vazgeç
        </button>
        <button className="primary" disabled={save.isPending || !!validation}>
          {save.isPending ? "Kaydediliyor…" : "Kaydet"}
        </button>
      </div>
    </form>
  );
}
export function CategoryEditor({
  category,
  close,
}: {
  category: AdminCategory;
  close: () => void;
}) {
  const [draft, set] = useState(category);
  return (
    <Dialog
      title={category.id ? "Kategori düzenle" : "Kategori ekle"}
      close={close}
    >
      <SaveForm
        close={close}
        method={category.id ? "PUT" : "POST"}
        path={`/api/admin/categories${category.id ? "/" + category.id : ""}`}
        body={{ ...draft, expectedVersion: category.version }}
      >
        <label>
          Kategori adı
          <input
            required
            maxLength={80}
            value={draft.name}
            onChange={(e) => set({ ...draft, name: e.target.value })}
          />
        </label>
        <label>
          Sıra
          <input
            type="number"
            required
            min={0}
            max={9999}
            value={draft.sortOrder}
            onChange={(e) =>
              set({ ...draft, sortOrder: Number(e.target.value) })
            }
          />
        </label>
      </SaveForm>
    </Dialog>
  );
}
export function ProductEditor({
  product,
  categories,
  close,
}: {
  product: AdminProduct;
  categories: AdminCategory[];
  close: () => void;
}) {
  const [draft, set] = useState<AdminProduct>(() => structuredClone(product));
  function changeGroup(index: number, group: AdminGroup) {
    set({
      ...draft,
      optionGroups: draft.optionGroups.map((g, i) => (i === index ? group : g)),
    });
  }
  return (
    <Dialog
      title={product.id ? `${product.name} · Ürün düzenle` : "Ürün ekle"}
      close={close}
      className="wide-modal"
    >
      <SaveForm
        close={close}
        method={product.id ? "PUT" : "POST"}
        path={`/api/admin/products${product.id ? "/" + product.id : ""}`}
        body={{ ...draft, expectedVersion: draft.version }}
      >
        <ProductImageEditor product={draft} saved={image => set(current => ({ ...current, ...image }))} />
        <label>
          Ürün adı
          <input
            required
            maxLength={120}
            value={draft.name}
            onChange={(e) => set({ ...draft, name: e.target.value })}
          />
        </label>
        <label>
          Açıklama
          <textarea
            maxLength={500}
            value={draft.description}
            onChange={(e) => set({ ...draft, description: e.target.value })}
          />
        </label>
        <label>
          Kategori
          <select
            required
            value={draft.categoryId}
            onChange={(e) => set({ ...draft, categoryId: e.target.value })}
          >
            <option value="">Kategori seçin</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </label>
        <div className="form-grid">
          <label>
            Temel fiyat (₺)
            <input
              type="number"
              required
              min={0}
              max={1000000}
              step="0.01"
              value={draft.basePrice}
              onChange={(e) =>
                set({ ...draft, basePrice: Number(e.target.value) })
              }
            />
          </label>
          <label>
            Fiyata dahil vergi oranı (%)
            <input
              type="number"
              required
              min={0}
              max={100}
              step="0.01"
              value={draft.taxRate}
              onChange={(e) =>
                set({ ...draft, taxRate: Number(e.target.value) })
              }
            />
          </label>
          <label>
            Menü sırası
            <input
              type="number"
              required
              min={0}
              max={9999}
              value={draft.sortOrder}
              onChange={(e) =>
                set({ ...draft, sortOrder: Number(e.target.value) })
              }
            />
          </label>
        </div>
        <label className="check-row">
          <input
            type="checkbox"
            checked={draft.active}
            onChange={(e) => set({ ...draft, active: e.target.checked })}
          />
          Ürün satışta
        </label>
        <p className="small-note">
          Vergi fiyatın içindedir; toplama tekrar eklenmez. Satıştan kapatmak
          geçmiş siparişleri değiştirmez.
        </p>
        <div className="editor-heading">
          <h3>Seçenek grupları</h3>
          <button
            type="button"
            className="secondary"
            disabled={draft.optionGroups.length >= 10}
            onClick={() =>
              set({
                ...draft,
                optionGroups: [
                  ...draft.optionGroups,
                  {
                    id: EMPTY_ID,
                    name: "",
                    minSelect: 0,
                    maxSelect: 1,
                    sortOrder: draft.optionGroups.length,
                    values: [
                      {
                        id: EMPTY_ID,
                        name: "",
                        priceDelta: 0,
                        available: true,
                        sortOrder: 0,
                      },
                    ],
                  },
                ],
              })
            }
          >
            Grup ekle
          </button>
        </div>
        {draft.optionGroups.map((g, index) => (
          <fieldset className="admin-group" key={index}>
            <legend>Grup {index + 1}</legend>
            <label>
              Grup adı
              <input
                required
                maxLength={80}
                value={g.name}
                onChange={(e) =>
                  changeGroup(index, { ...g, name: e.target.value })
                }
              />
            </label>
            <div className="form-grid">
              <label>
                En az seçim
                <input
                  type="number"
                  required
                  min={0}
                  max={10}
                  value={g.minSelect}
                  onChange={(e) =>
                    changeGroup(index, {
                      ...g,
                      minSelect: Number(e.target.value),
                    })
                  }
                />
              </label>
              <label>
                En fazla seçim
                <input
                  type="number"
                  required
                  min={1}
                  max={10}
                  value={g.maxSelect}
                  onChange={(e) =>
                    changeGroup(index, {
                      ...g,
                      maxSelect: Number(e.target.value),
                    })
                  }
                />
              </label>
            </div>
            {g.values.map((v, vi) => (
              <div className="option-edit" key={vi}>
                <label>
                  Seçenek adı
                  <input
                    required
                    maxLength={80}
                    value={v.name}
                    onChange={(e) =>
                      changeGroup(index, {
                        ...g,
                        values: g.values.map((x, i) =>
                          i === vi ? { ...x, name: e.target.value } : x,
                        ),
                      })
                    }
                  />
                </label>
                <label>
                  Ek ücret (₺)
                  <input
                    type="number"
                    required
                    step="0.01"
                    min={0}
                    max={1000000}
                    value={v.priceDelta}
                    onChange={(e) =>
                      changeGroup(index, {
                        ...g,
                        values: g.values.map((x, i) =>
                          i === vi
                            ? { ...x, priceDelta: Number(e.target.value) }
                            : x,
                        ),
                      })
                    }
                  />
                </label>
                <label className="check-row">
                  <input
                    type="checkbox"
                    checked={v.available}
                    onChange={(e) =>
                      changeGroup(index, {
                        ...g,
                        values: g.values.map((x, i) =>
                          i === vi ? { ...x, available: e.target.checked } : x,
                        ),
                      })
                    }
                  />
                  Mevcut
                </label>
                <button
                  type="button"
                  className="secondary"
                  onClick={() =>
                    changeGroup(index, {
                      ...g,
                      values: g.values.filter((_, i) => i !== vi),
                    })
                  }
                >
                  Seçeneği kaldır
                </button>
              </div>
            ))}
            <div className="form-actions">
              <button
                type="button"
                className="secondary"
                disabled={g.values.length >= 10}
                onClick={() =>
                  changeGroup(index, {
                    ...g,
                    values: [
                      ...g.values,
                      {
                        id: EMPTY_ID,
                        name: "",
                        priceDelta: 0,
                        available: true,
                        sortOrder: g.values.length,
                      },
                    ],
                  })
                }
              >
                Seçenek ekle
              </button>
              <button
                type="button"
                className="secondary"
                onClick={() =>
                  set({
                    ...draft,
                    optionGroups: draft.optionGroups.filter(
                      (_, i) => i !== index,
                    ),
                  })
                }
              >
                Grubu kaldır
              </button>
            </div>
          </fieldset>
        ))}
      </SaveForm>
    </Dialog>
  );
}
export function BranchPriceEditor({
  product,
  branch,
  catalog,
  close,
}: {
  product: AdminProduct;
  branch: AdminBranch;
  catalog: Catalog;
  close: () => void;
}) {
  const current = catalog.branchProducts.find(
    (x) => x.branchId === branch.id && x.productId === product.id,
  )!;
  const [available, setAvailable] = useState(current.available);
  const [price, setPrice] = useState(current.priceOverride?.toString() ?? "");
  const [options, setOptions] = useState(() =>
    product.optionGroups
      .flatMap((g) => g.values)
      .map((v) => {
        const override = catalog.branchOptions.find(
          (o) => o.branchId === branch.id && o.optionValueId === v.id,
        );
        return {
          optionValueId: v.id,
          name: v.name,
          available: override?.available ?? true,
          price: override?.priceOverride?.toString() ?? "",
        };
      }),
  );
  return (
    <Dialog title={`${branch.name} · ${product.name}`} close={close}>
      <SaveForm
        close={close}
        method="PUT"
        path={`/api/admin/branches/${branch.id}/products/${product.id}`}
        body={{
          available,
          priceOverride: price === "" ? null : Number(price),
          expectedVersion: current.version,
          options: options.map((o) => ({
            optionValueId: o.optionValueId,
            available: o.available,
            priceOverride: o.price === "" ? null : Number(o.price),
          })),
        }}
      >
        <label>
          Şube fiyatı (₺)
          <input
            type="number"
            min={0}
            max={1000000}
            step="0.01"
            placeholder={`Varsayılan: ${product.basePrice}`}
            value={price}
            onChange={(e) => setPrice(e.target.value)}
          />
        </label>
        <label className="check-row">
          <input
            type="checkbox"
            checked={available}
            onChange={(e) => setAvailable(e.target.checked)}
          />
          Bu şubede mevcut
        </label>
        <p className="small-note">
          Boş fiyat varsayılanı kullanır. Seçenek fiyatı ek ücretin tamamını
          değiştirir.
        </p>
        {options.map((o, index) => (
          <div className="option-edit" key={o.optionValueId}>
            <label className="check-row">
              <input
                type="checkbox"
                checked={o.available}
                onChange={(e) =>
                  setOptions(
                    options.map((x, i) =>
                      i === index ? { ...x, available: e.target.checked } : x,
                    ),
                  )
                }
              />
              {o.name}
            </label>
            <label>
              Şube ek ücreti (₺)
              <input
                type="number"
                min={0}
                max={1000000}
                step="0.01"
                value={o.price}
                onChange={(e) =>
                  setOptions(
                    options.map((x, i) =>
                      i === index ? { ...x, price: e.target.value } : x,
                    ),
                  )
                }
              />
            </label>
          </div>
        ))}
      </SaveForm>
    </Dialog>
  );
}
const days = [
  "Pazar",
  "Pazartesi",
  "Salı",
  "Çarşamba",
  "Perşembe",
  "Cuma",
  "Cumartesi",
];
export function BranchEditor({
  branch,
  close,
}: {
  branch: AdminBranch;
  close: () => void;
}) {
  const [draft, set] = useState(() => structuredClone(branch));
  const [schedule, setSchedule] = useState(branch.hours.length > 0);
  const [hours, setHours] = useState<Hours[]>(
    branch.hours.length
      ? branch.hours
      : [1, 2, 3, 4, 5, 6].map((day) => ({
          day,
          opens: "09:00",
          closes: "21:00",
        })),
  );
  return (
    <Dialog
      title={branch.id ? `${branch.name} · Şube ayarları` : "Şube ekle"}
      close={close}
    >
      <SaveForm
        close={close}
        method={branch.id ? "PUT" : "POST"}
        path={`/api/admin/branches${branch.id ? "/" + branch.id : ""}`}
        validation={
          schedule && hours.length === 0
            ? "En az bir çalışma günü seçin veya saat kısıtlamasını kaldırın."
            : undefined
        }
        body={{
          ...draft,
          hours: schedule ? hours : [],
          expectedVersion: branch.version,
        }}
      >
        <label>
          Şube adı
          <input
            required
            maxLength={80}
            value={draft.name}
            onChange={(e) => set({ ...draft, name: e.target.value })}
          />
        </label>
        <label>
          Şube bağlantı adresi
          <input
            required
            pattern="[a-z0-9]+(-[a-z0-9]+)*"
            disabled={!!branch.id}
            maxLength={100}
            value={draft.slug}
            onChange={(e) => set({ ...draft, slug: e.target.value })}
          />
        </label>
        <label>
          Adres
          <textarea
            maxLength={250}
            value={draft.address}
            onChange={(e) => set({ ...draft, address: e.target.value })}
          />
        </label>
        <div className="form-grid">
          <label>
            Saat dilimi
            <input
              required
              maxLength={100}
              value={draft.timeZone}
              onChange={(e) => set({ ...draft, timeZone: e.target.value })}
            />
          </label>
          <label>
            Hazırlık kapasitesi
            <input
              type="number"
              required
              min={1}
              max={1000}
              value={draft.preparationCapacity}
              onChange={(e) =>
                set({ ...draft, preparationCapacity: Number(e.target.value) })
              }
            />
          </label>
        </div>
        <label className="check-row">
          <input
            type="checkbox"
            checked={draft.isAcceptingOrders}
            onChange={(e) =>
              set({ ...draft, isAcceptingOrders: e.target.checked })
            }
          />
          Manuel sipariş kabulü açık
        </label>
        <label className="check-row">
          <input
            type="checkbox"
            checked={schedule}
            onChange={(e) => setSchedule(e.target.checked)}
          />
          Çalışma saatlerini uygula
        </label>
        {schedule && (
          <fieldset className="admin-group">
            <legend>Haftalık saatler</legend>
            {days.map((day, index) => {
              const current = hours.find((h) => h.day === index);
              return (
                <div className="hours-row" key={day}>
                  <label className="check-row">
                    <input
                      type="checkbox"
                      checked={!!current}
                      onChange={(e) =>
                        setHours(
                          e.target.checked
                            ? [
                                ...hours,
                                { day: index, opens: "09:00", closes: "21:00" },
                              ]
                            : hours.filter((h) => h.day !== index),
                        )
                      }
                    />
                    {day}
                  </label>
                  {current && (
                    <>
                      <input
                        aria-label={`${day} açılış`}
                        type="time"
                        required
                        value={current.opens}
                        onChange={(e) =>
                          setHours(
                            hours.map((h) =>
                              h.day === index
                                ? { ...h, opens: e.target.value }
                                : h,
                            ),
                          )
                        }
                      />
                      <input
                        aria-label={`${day} kapanış`}
                        type="time"
                        required
                        value={current.closes}
                        onChange={(e) =>
                          setHours(
                            hours.map((h) =>
                              h.day === index
                                ? { ...h, closes: e.target.value }
                                : h,
                            ),
                          )
                        }
                      />
                    </>
                  )}
                </div>
              );
            })}
          </fieldset>
        )}
        <p className="small-note">
          Saatler dışında checkout kapalıdır. Kapasite hazırlık ekibi için bilgi
          amaçlıdır; sipariş rezervasyon sınırı değildir.
        </p>
      </SaveForm>
    </Dialog>
  );
}
export function StaffEditor({
  person,
  branches,
  close,
  invited,
}: {
  person: AdminStaff;
  branches: AdminBranch[];
  close: () => void;
  invited: (url: string) => void;
}) {
  const [draft, set] = useState(() => structuredClone(person));
  return (
    <Dialog
      title={person.id ? `${person.email} · Yetkiler` : "Personel davet et"}
      close={close}
    >
      <SaveForm
        close={close}
        method={person.id ? "PUT" : "POST"}
        path={`/api/admin/staff${person.id ? "/" + person.id : ""}`}
        body={{ ...draft, expectedVersion: person.version }}
        onSaved={(data) => {
          if (data.invitationUrl)
            invited(new URL(data.invitationUrl, window.location.origin).href);
        }}
      >
        <label>
          E-posta
          <input
            type="email"
            required
            maxLength={254}
            disabled={!!person.id}
            value={draft.email}
            onChange={(e) => set({ ...draft, email: e.target.value })}
          />
        </label>
        <label>
          Rol
          <select
            value={draft.role}
            onChange={(e) =>
              set({ ...draft, role: e.target.value as "Owner" | "Barista" })
            }
          >
            <option value="Barista">Barista · yalnızca yetkili şubeler</option>
            <option value="Owner">İşletme sahibi · tam yönetim</option>
          </select>
        </label>
        {person.id && !person.invited && (
          <label className="check-row">
            <input
              type="checkbox"
              checked={draft.active}
              onChange={(e) => set({ ...draft, active: e.target.checked })}
            />
            Hesap aktif
          </label>
        )}
        <fieldset className="admin-group">
          <legend>Şube erişimi</legend>
          {branches.map((b) => (
            <label className="check-row" key={b.id}>
              <input
                type="checkbox"
                checked={draft.branchIds.includes(b.id)}
                onChange={(e) =>
                  set({
                    ...draft,
                    branchIds: e.target.checked
                      ? [...draft.branchIds, b.id]
                      : draft.branchIds.filter((id) => id !== b.id),
                  })
                }
              />
              {b.name}
            </label>
          ))}
        </fieldset>
        <p className="small-note">
          Yeni personel 24 saat geçerli bağlantıyla parolasını oluşturur.
          Bağlantıyı kendiniz paylaşabilirsiniz; otomatik e-posta gönderilmez.
        </p>
      </SaveForm>
    </Dialog>
  );
}
