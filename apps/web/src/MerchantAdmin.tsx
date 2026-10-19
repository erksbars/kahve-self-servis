import { useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery } from "@tanstack/react-query";
import { api, money } from "./api";
import type { Session } from "./api";
import type {
  AdminBranch,
  AdminCategory,
  AdminProduct,
  AdminStaff,
  Catalog,
  Summary,
} from "./adminTypes";
import {
  CategoryEditor,
  ProductEditor,
  BranchEditor,
  BranchPriceEditor,
  StaffEditor,
} from "./AdminForms";
import { BranchQr } from "./BranchQr";
import { MusicAdmin } from "./Music";
import "./admin.css";

type Editor =
  | { kind: "category"; value: AdminCategory }
  | { kind: "product"; value: AdminProduct }
  | { kind: "branch"; value: AdminBranch }
  | { kind: "staff"; value: AdminStaff }
  | { kind: "price"; value: AdminProduct; branch: AdminBranch }
  | { kind: "qr"; value: AdminBranch };
const today = () =>
  new Intl.DateTimeFormat("en-CA", {
    timeZone: "Europe/Istanbul",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
export function MerchantAdmin() {
  const session = useQuery({
    queryKey: ["session"],
    queryFn: () => api<Session>("/api/session/me"),
    retry: false,
  });
  if (session.isPending)
    return (
      <main className="staff-page" role="status">
        Oturum kontrol ediliyor…
      </main>
    );
  if (!session.data || session.error)
    return (
      <main className="staff-page">
        <h1>İşletme yönetimi</h1>
        <p>{session.error?.message}</p>
        <Link className="primary" to="/staff">
          Personel girişine git
        </Link>
      </main>
    );
  if (session.data.role !== "Owner")
    return (
      <main className="staff-page">
        <h1>İşletme sahibi erişimi gerekir.</h1>
        <Link className="secondary" to="/staff">
          Sipariş ekranına dön
        </Link>
      </main>
    );
  return <Management email={session.data.email} />;
}
function Management({ email }: { email: string }) {
  const [tab, setTab] = useState("Özet");
  const [editor, setEditor] = useState<Editor | null>(null);
  const [invitation, setInvitation] = useState("");
  const catalog = useQuery({
    queryKey: ["admin", "catalog"],
    queryFn: () => api<Catalog>("/api/admin/catalog"),
  });
  const branches = useQuery({
    queryKey: ["admin", "branches"],
    queryFn: () => api<AdminBranch[]>("/api/admin/branches"),
  });
  const staff = useQuery({
    queryKey: ["admin", "staff"],
    queryFn: () => api<AdminStaff[]>("/api/admin/staff"),
  });
  const renew = useMutation({
    mutationFn: (id: string) =>
      api<{ invitationUrl: string }>(`/api/admin/staff/${id}/invitation`, {
        method: "POST",
      }),
    onSuccess: (data) =>
      setInvitation(new URL(data.invitationUrl, window.location.origin).href),
  });
  const close = () => setEditor(null);
  const errors = [
    catalog.error,
    branches.error,
    staff.error,
    renew.error,
  ].filter(Boolean);
  return (
    <main className="admin-page">
      <div className="staff-title">
        <div>
          <p className="eyebrow">İŞLETME YÖNETİMİ</p>
          <h1>Her şey yerli yerinde.</h1>
          <p>{email}</p>
        </div>
        <Link className="secondary" to="/staff">
          Sipariş ekranı →
        </Link>
      </div>
      <nav className="admin-tabs" aria-label="Yönetim bölümleri">
        {["Özet", "Menü", "Şubeler", "İstek parçalar", "Personel"].map((t) => (
          <button
            className={tab === t ? "selected" : ""}
            aria-pressed={tab === t}
            key={t}
            onClick={() => setTab(t)}
          >
            {t}
          </button>
        ))}
      </nav>
      {errors.map((e, i) => (
        <p className="error" role="alert" key={i}>
          {e!.message}
        </p>
      ))}
      {(catalog.isPending || branches.isPending || staff.isPending) && (
        <p role="status">Yönetim bilgileri yükleniyor…</p>
      )}
      {invitation && (
        <section className="invite-result" aria-label="Davet bağlantısı">
          <div className="editor-heading">
            <h2>Davet hazır</h2>
            <button className="secondary" onClick={() => setInvitation("")}>
              Kapat
            </button>
          </div>
          <p>24 saat geçerli, tek kullanımlık bağlantı. Personelle paylaşın.</p>
          <input
            aria-label="Personel davet bağlantısı"
            readOnly
            value={invitation}
            onFocus={(e) => e.target.select()}
          />
        </section>
      )}
      {tab === "Özet" && branches.data && <Reports branches={branches.data} />}
      {tab === "İstek parçalar" && branches.data && <MusicAdmin branches={branches.data} />}
      {tab === "Menü" && catalog.data && branches.data && (
        <>
          <section className="admin-section">
            <div className="editor-heading">
              <h2>Kategoriler</h2>
              <button
                className="secondary"
                onClick={() =>
                  setEditor({
                    kind: "category",
                    value: { id: "", name: "", sortOrder: 0, version: 0 },
                  })
                }
              >
                Kategori ekle
              </button>
            </div>
            <div className="category-admin">
              {catalog.data.categories.map((c) => (
                <button
                  className="secondary"
                  key={c.id}
                  onClick={() => setEditor({ kind: "category", value: c })}
                >
                  {c.name} · Düzenle
                </button>
              ))}
            </div>
          </section>
          <section className="admin-section">
            <div className="editor-heading">
              <div>
                <h2>Ürünler</h2>
                <p>Fiyatlar, seçenekler ve şube bulunurluğu.</p>
              </div>
              <button
                className="primary"
                disabled={!catalog.data.categories.length}
                onClick={() =>
                  setEditor({
                    kind: "product",
                    value: {
                      id: "",
                      categoryId: catalog.data!.categories[0]?.id ?? "",
                      name: "",
                      description: "",
                      basePrice: 0,
                      taxRate: 0,
                      active: true,
                      sortOrder: 0,
                      version: 0,
                      optionGroups: [],
                    },
                  })
                }
              >
                Ürün ekle
              </button>
            </div>
            <div className="admin-products">
              {catalog.data.products.map((p) => (
                <article className="admin-product" key={p.id}>
                  <div className="editor-heading">
                    <div>
                      <h3>{p.name}</h3>
                      <p>
                        {
                          catalog.data!.categories.find(
                            (c) => c.id === p.categoryId,
                          )?.name
                        }{" "}
                        · {money(p.basePrice)} ·{" "}
                        {p.active ? "Satışta" : "Satışa kapalı"}
                      </p>
                    </div>
                    <button
                      className="secondary"
                      onClick={() => setEditor({ kind: "product", value: p })}
                    >
                      Ürünü düzenle
                    </button>
                  </div>
                  <p className="small-note">
                    {p.optionGroups.length} seçenek grubu · Fiyata dahil vergi %
                    {p.taxRate}
                  </p>
                  <div className="branch-prices">
                    {branches.data!.map((b) => {
                      const setting = catalog.data!.branchProducts.find(
                        (x) => x.branchId === b.id && x.productId === p.id,
                      );
                      return (
                        setting && (
                          <button
                            className="secondary"
                            key={b.id}
                            onClick={() =>
                              setEditor({ kind: "price", value: p, branch: b })
                            }
                          >
                            {b.name}:{" "}
                            {setting.available
                              ? money(setting.priceOverride ?? p.basePrice)
                              : "Mevcut değil"}{" "}
                            · Ayarla
                          </button>
                        )
                      );
                    })}
                  </div>
                </article>
              ))}
            </div>
          </section>
        </>
      )}
      {tab === "Şubeler" && branches.data && (
        <section className="admin-section">
          <div className="editor-heading">
            <h2>Şubeler</h2>
            <button
              className="primary"
              onClick={() =>
                setEditor({
                  kind: "branch",
                  value: {
                    id: "",
                    name: "",
                    slug: "",
                    address: "",
                    timeZone: "Europe/Istanbul",
                    isAcceptingOrders: true,
                    isOpenNow: true,
                    preparationCapacity: 30,
                    version: 0,
                    hours: [],
                  },
                })
              }
            >
              Şube ekle
            </button>
          </div>
          <div className="branch-grid">
            {branches.data.map((b) => (
              <article className="branch-card" key={b.id}>
                <h3>{b.name}</h3>
                <p>{b.address || "Adres eklenmemiş"}</p>
                <p>
                  {b.isOpenNow
                    ? "Şu an sipariş kabulü açık"
                    : "Şu an sipariş kabulü kapalı"}
                </p>
                <p className="small-note">
                  {b.timeZone} · Kapasite {b.preparationCapacity}
                  <br />
                  {b.hours.length
                    ? `${b.hours.length} gün için çalışma saati`
                    : "Saat kısıtlaması yok"}
                </p>
                <button
                  className="secondary"
                  onClick={() => setEditor({ kind: "branch", value: b })}
                >
                  Şubeyi düzenle
                </button>
                <button
                  className="secondary"
                  onClick={() => setEditor({ kind: "qr", value: b })}
                >
                  Menü QR kodu
                </button>
                <Link to={`/b/${b.slug}`}>Menüyü aç →</Link>
              </article>
            ))}
          </div>
        </section>
      )}
      {tab === "Personel" && staff.data && branches.data && (
        <section className="admin-section">
          <div className="editor-heading">
            <div>
              <h2>Personel ve yetkiler</h2>
              <p>Her hesap yalnızca verilen şubelere erişir.</p>
            </div>
            <button
              className="primary"
              onClick={() =>
                setEditor({
                  kind: "staff",
                  value: {
                    id: "",
                    email: "",
                    role: "Barista",
                    active: false,
                    invited: true,
                    version: 0,
                    branchIds: branches.data!.slice(0, 1).map((b) => b.id),
                  },
                })
              }
            >
              Personel davet et
            </button>
          </div>
          <div className="staff-list">
            {staff.data.map((p) => (
              <article className="admin-product" key={p.id}>
                <div className="editor-heading">
                  <div>
                    <h3>{p.email}</h3>
                    <p>
                      {p.role === "Owner" ? "İşletme sahibi" : "Barista"} ·{" "}
                      {p.invited
                        ? "Davet bekliyor"
                        : p.active
                          ? "Aktif"
                          : "Pasif"}
                    </p>
                    <p className="small-note">
                      {p.branchIds
                        .map(
                          (id) => branches.data!.find((b) => b.id === id)?.name,
                        )
                        .filter(Boolean)
                        .join(", ")}
                    </p>
                  </div>
                  <div className="form-actions">
                    <button
                      className="secondary"
                      onClick={() => setEditor({ kind: "staff", value: p })}
                    >
                      Yetkileri düzenle
                    </button>
                    {p.invited && (
                      <button
                        className="secondary"
                        disabled={renew.isPending}
                        onClick={() => renew.mutate(p.id)}
                      >
                        Daveti yenile
                      </button>
                    )}
                  </div>
                </div>
              </article>
            ))}
          </div>
        </section>
      )}
      <p className="demo-note">
        Demo ortamı · Raporlar sandbox siparişlerini gösterir. Gerçek tahsilat
        yapılmaz.
      </p>
      {editor?.kind === "category" && (
        <CategoryEditor category={editor.value} close={close} />
      )}
      {editor?.kind === "product" && catalog.data && (
        <ProductEditor
          product={editor.value}
          categories={catalog.data.categories}
          close={close}
        />
      )}
      {editor?.kind === "branch" && (
        <BranchEditor branch={editor.value} close={close} />
      )}
      {editor?.kind === "staff" && branches.data && (
        <StaffEditor
          person={editor.value}
          branches={branches.data}
          close={close}
          invited={setInvitation}
        />
      )}
      {editor?.kind === "price" && catalog.data && (
        <BranchPriceEditor
          product={editor.value}
          branch={editor.branch}
          catalog={catalog.data}
          close={close}
        />
      )}
      {editor?.kind === "qr" && (
        <BranchQr branch={editor.value} close={close} />
      )}
    </main>
  );
}
function Reports({ branches }: { branches: AdminBranch[] }) {
  const [draft, set] = useState({ from: today(), to: today(), branchId: "" });
  const [range, setRange] = useState(draft);
  const report = useQuery({
    queryKey: ["admin", "report", range],
    queryFn: () =>
      api<Summary>(
        "/api/admin/reports/summary?" +
          new URLSearchParams(Object.entries(range).filter(([, v]) => !!v)),
      ),
  });
  const r = report.data;
  return (
    <section className="admin-section">
      <div className="editor-heading">
        <div>
          <h2>Günün görünümü</h2>
          <p>Ödeme onayı alan siparişlerin toplamı.</p>
        </div>
      </div>
      <form
        className="report-filter admin-form"
        onSubmit={(e) => {
          e.preventDefault();
          setRange({ ...draft });
        }}
      >
        <label>
          Başlangıç
          <input
            type="date"
            required
            value={draft.from}
            onChange={(e) => set({ ...draft, from: e.target.value })}
          />
        </label>
        <label>
          Bitiş
          <input
            type="date"
            required
            min={draft.from}
            value={draft.to}
            onChange={(e) => set({ ...draft, to: e.target.value })}
          />
        </label>
        <label>
          Şube
          <select
            value={draft.branchId}
            onChange={(e) => set({ ...draft, branchId: e.target.value })}
          >
            <option value="">Tüm şubeler</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        </label>
        <button className="primary" disabled={report.isFetching}>
          Raporu göster
        </button>
      </form>
      {report.error && (
        <p className="error" role="alert">
          {report.error.message}
        </p>
      )}
      {report.isPending && <p role="status">Rapor hazırlanıyor…</p>}
      {r && (
        <>
          <div className="metric-grid">
            {[
              ["Onaylı sipariş", r.paidOrders],
              ["Satış toplamı", money(r.revenue)],
              ["Tamamlanan", r.completedOrders],
              [
                "Ort. hazırlık",
                r.averagePreparationMinutes === null
                  ? "Henüz ölçüm yok"
                  : `${r.averagePreparationMinutes} dk`,
              ],
              ["İptal edilen", r.cancelledOrders],
              ["Başarısız ödeme", r.failedPayments],
            ].map(([name, value]) => (
              <article className="metric-card" key={name}>
                <p>{name}</p>
                <strong>{value}</strong>
              </article>
            ))}
          </div>
          <p className="small-note">
            Saat dilimi: {r.timeZone}. Hazırlık süresi “Hazırlanıyor” ile
            “Hazır” arasından, {r.preparationSamples} siparişle hesaplanır.
            İptal ve başarısız ödeme oluşturma tarihine; satışlar ödeme tarihine
            göre filtrelenir.
          </p>
        </>
      )}
    </section>
  );
}
