import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useMutation, useQuery } from "@tanstack/react-query";
import {
  Coffee,
  MapPin,
  Search,
  ShieldCheck,
  ShoppingBag,
  ArrowRight,
  Trash2,
  CircleCheck,
  Snowflake,
  Leaf,
  Wheat,
  CakeSlice,
  Droplets,
} from "lucide-react";
import { api, filterProducts, money } from "./api";
import type { Menu, Product, Quote, CheckoutReply } from "./api";
import { checkoutKey, readSongDraft, saveSongDraft } from "./checkout";
import { addLine, previewUnit, readCart } from "./cart";
import type { CartLine } from "./cart";
import { Dialog } from "./Dialog";
import { ProductPicker } from "./ProductPicker";
import { ProductPhoto } from "./ProductPhoto";
import "./music.css";

const categoryIcons: Record<string, typeof Coffee> = {
  'Espresso bazlı': Coffee, 'Soğuk kahveler': Snowflake, 'Filtre kahve': Droplets,
  'Kahve dışı': Leaf, 'Fırından': Wheat, 'Tatlılar': CakeSlice,
};

export function CustomerMenu() {
  const { slug } = useParams();
  return <BranchMenu key={slug} slug={slug!} />;
}
function BranchMenu({ slug }: { slug: string }) {
  const navigate = useNavigate();
  const [category, setCategory] = useState("Tümü");
  const [search, setSearch] = useState("");
  const [lines, setLines] = useState<CartLine[]>(() => readCart(slug));
  const [picker, setPicker] = useState<Product | null>(null);
  const [cartOpen, setCartOpen] = useState(false);
  const [requestedSong, setRequestedSong] = useState(() => readSongDraft(slug));
  useEffect(() => saveSongDraft(slug, requestedSong), [slug, requestedSong]);
  const [verified, setVerified] = useState<{
    key: string;
    quote: Quote;
  } | null>(null);
  const menu = useQuery({
    queryKey: ["menu", slug],
    queryFn: () => api<Menu>(`/api/branches/${encodeURIComponent(slug)}/menu`),
    refetchInterval: 15000,
  });
  useEffect(() => {
    try {
      sessionStorage.setItem(`kahve.cart.v1.${slug}`, JSON.stringify(lines));
    } catch {
      /* Cart remains usable when browser storage is unavailable. */
    }
  }, [lines, slug]);
  const payload = {
    branchSlug: slug,
    items: lines.map((x) => ({
      productId: x.productId,
      quantity: x.quantity,
      optionValueIds: x.optionValueIds,
    })),
  };
  const key = JSON.stringify({
    payload,
    products: menu.data?.products,
    accepting: menu.data?.branch.isAcceptingOrders,
  });
  const quote = useMutation({
    mutationFn: (input: { payload: typeof payload; key: string }) =>
      api<Quote>("/api/checkouts/quote", {
        method: "POST",
        body: JSON.stringify(input.payload),
      }),
    onSuccess: (data, input) => setVerified({ key: input.key, quote: data }),
    onError: () => {
      setVerified(null);
      void menu.refetch();
    },
  });
  const checkout = useMutation({
    mutationFn: async (input: { expectedTotal: number; payload: typeof payload; key: string; song: string }) => {
      const fresh = await api<Quote>("/api/checkouts/quote", { method: "POST", body: JSON.stringify(input.payload) });
      if (fresh.total !== input.expectedTotal) return { quote: fresh };
      const request = { ...input.payload, expectedTotal: fresh.total, requestedSong: input.song.trim() || null };
      return api<CheckoutReply>("/api/checkouts", {
        method: "POST",
        headers: { "Idempotency-Key": checkoutKey(slug, request) },
        body: JSON.stringify(request),
      });
    },
    onSuccess: (data, input) => {
      if ('paymentUrl' in data) navigate(data.paymentUrl);
      else setVerified({ key: input.key, quote: data.quote });
    },
    onError: () => {
      setVerified(null);
      void menu.refetch();
    },
  });
  function update(next: CartLine[]) {
    if (checkout.isPending) return;
    checkout.reset();
    setVerified(null);
    quote.reset();
    setLines(next);
  }
  if (!menu.data)
    return (
      <main className="page">
        {menu.error ? (
          <div className="error" role="alert">
            <p>{menu.error.message}</p>
            <button className="secondary" onClick={() => void menu.refetch()}>
              Yeniden dene
            </button>
          </div>
        ) : (
          <p role="status">Menü hazırlanıyor…</p>
        )}
      </main>
    );
  const data = menu.data;
  const categories = ["Tümü", ...new Set(data.products.map((p) => p.category))];
  const products = filterProducts(data.products, category, search).sort(
    (a, b) => categories.indexOf(a.category) - categories.indexOf(b.category),
  );
  const preview = lines.reduce(
    (total, x) =>
      total +
      previewUnit(
        data.products.find((p) => p.id === x.productId),
        x.optionValueIds,
      ) *
        x.quantity,
    0,
  );
  const count = lines.reduce((total, x) => total + x.quantity, 0);
  const currentQuote = verified?.key === key ? verified.quote : null;
  const currentPicker = picker && data.products.find((p) => p.id === picker.id);
  return (
    <main className={`page premium-menu ${count ? "has-cart" : ""}`}>
      <div className="branch-strip">
        <span>
          <MapPin size={15} />
          {data.merchant} · {data.branch.name}
        </span>
        <span
          className={`status ${data.branch.isAcceptingOrders ? "" : "paused"}`}
        >
          <i />
          {data.branch.isAcceptingOrders
            ? "Sipariş kabulü açık"
            : "Sipariş kabulü kapalı"}
        </span>
      </div>
      <section className="hero">
        <div className="hero-copy">
          <p className="eyebrow">GÜNÜN GÜZEL TARAFI</p>
          <h1>
            İyi kahve.
            <br />
            <em>İyi bir gün.</em>
          </h1>
          <p className="hero-description">
            Bir fincan, biraz keyif. Gerisi senin.
          </p>
          <a className="hero-cta" href="#menu-title">Menüyü keşfet <ArrowRight size={18} aria-hidden="true" /></a>
          <div className="hero-meta">
            <Coffee size={17} />
            Şubeden teslim alma <span>·</span>Hesap gerektirmez
          </div>
        </div>
        <div className="hero-photo"><ProductPhoto src={data.products.find(p => p.name === 'Flat White')?.imageUrl || data.products.find(p => p.imageUrl)?.imageUrl} alt="Kahve molası" eager /><span className="hero-photo-caption">Günün küçük mutluluğu.</span><span className="hero-menu-badge"><strong>{data.products.length}</strong><span>lezzet<br />bir mola</span></span></div>
      </section>
      {data.demo && (
        <div className="demo-note">
          <ShieldCheck size={18} />
          <div>
            <strong>Demo menü</strong>
            <span>
              Ürün, fiyat ve görseller temsilidir. Sandbox ödemesi gerçek para çekmez.
            </span>
          </div>
        </div>
      )}
      {menu.error && (
        <p className="error" role="alert">
          Menü yenilenemedi. Bağlantını kontrol et; sepetin korunuyor.
        </p>
      )}
      {data.checkoutEnabled === false && (
        <p className="notice" role="status">
          Online ödeme şu anda kullanılamıyor. Menüyü inceleyebilirsiniz.
        </p>
      )}
      {!data.branch.isAcceptingOrders && (
        <p className="notice" role="status">
          Şube sipariş kabulünü geçici olarak durdurdu. Menüyü
          inceleyebilirsiniz.
        </p>
      )}
      <section className="menu-section" aria-labelledby="menu-title">
        <div className="section-heading">
          <div>
            <p className="eyebrow">MENÜYÜ KEŞFET</p>
            <h2 id="menu-title">Bugün canın ne çekiyor?</h2>
          </div>
          <label className="search">
            <Search size={18} />
            <input
              aria-label="Menüde ara"
              placeholder="Kahve, matcha, tatlı…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
        </div>
        <p className="menu-count">{products.length} lezzet · {category === 'Tümü' ? 'Kahveden tatlıya, molana eşlik edenler.' : category}</p>
        <div className="categories" aria-label="Menü kategorileri">
          {categories.map((c, index) => {
            const Icon = categoryIcons[c] || Coffee;
            const categoryCount = c === 'Tümü' ? data.products.length : data.products.filter(p => p.category === c).length;
            return (
            <button
              key={c}
              className={`category-chip category-tone-${index % 4}`}
              aria-label={c}
              aria-pressed={category === c}
              onClick={() => setCategory(c)}
            >
              <Icon size={16} aria-hidden="true" />{c}<span className="category-count" aria-hidden="true">{categoryCount}</span>
            </button>
            );
          })}
        </div>
        <div className="product-grid">
          {products.map((p) => (
            <article
              className={`product ${p.available ? "" : "unavailable"}`}
              key={p.id}
            >
              <ProductPhoto src={p.imageUrl} alt={p.imageAlt || p.name} />
              <div className="product-copy">
                <span className="product-category">{p.category}</span>
                <h3>{p.name}</h3>
                <p>{p.description}</p>
                <div className="product-bottom">
                  <strong>
                    {money(p.price)}
                    <small>başlangıç fiyatı</small>
                  </strong>
                  <button
                    className="choose-button"
                    onClick={() => setPicker(p)}
                    disabled={!p.available || !data.branch.isAcceptingOrders}
                  >
                    {p.available ? "Seç" : "Tükendi"}
                    <ArrowRight size={15} />
                  </button>
                </div>
              </div>
            </article>
          ))}
        </div>
        {products.length === 0 && (
          <p className="empty" role="status">
            Bu aramada ürün bulunamadı. Başka bir kahve arayın.
          </p>
        )}
        <p className="menu-footnote">
          Fiyatlar bu şubeye özeldir · Hazırlanma süresi işletmeye göre
          değişebilir
        </p>
      </section>
      <footer>
        <span>{data.branch.address}</span>
        <span>Bir sonraki güzel molana kadar.</span>
      </footer>
      {count > 0 && (
        <div className="cart-dock">
          <button
            onClick={() => setCartOpen(true)}
            aria-label={`Sepeti aç, ${count} adet`}
          >
            <span className="cart-count">
              <ShoppingBag size={18} />
              {count}
            </span>
            <span>
              Sepetini gör<small>Gel-al · Tahmini toplam</small>
            </span>
            <strong>{money(preview)}</strong>
            <ArrowRight size={18} />
          </button>
        </div>
      )}
      {currentPicker && (
        <ProductPicker
          key={currentPicker.id}
          product={currentPicker}
          accepting={data.branch.isAcceptingOrders}
          close={() => setPicker(null)}
          add={(line) => update(addLine(lines, line))}
        />
      )}
      {cartOpen && (
        <Dialog
          title="Sepetin"
          close={() => setCartOpen(false)}
          className="cart-modal"
        >
          <p className="picker-description">
            {data.merchant} · {data.branch.name} · Gel-al
          </p>
          {lines.length === 0 ? (
            <p className="empty">Sepetin boş. Menüden bir kahve seç.</p>
          ) : (
            <>
              {lines.map((line) => {
                const p = data.products.find((p) => p.id === line.productId);
                const allOptions =
                  p?.optionGroups.flatMap((g) => g.values) ?? [];
                return (
                  <article className="cart-line" key={line.key}>
                    <div>
                      <h3>{p?.name ?? "Menüden kaldırılmış ürün"}</h3>
                      <p>
                        {line.optionValueIds
                          .map(
                            (id) =>
                              allOptions.find((v) => v.id === id)?.name ??
                              "Artık menüde olmayan seçenek",
                          )
                          .join(" · ")}
                      </p>
                      {(!p || !p.available) && (
                        <p className="cart-warning">
                          Bu ürün artık satışta değil; sepetten çıkarın.
                        </p>
                      )}
                      <div className="cart-line-controls">
                        <label>
                          Adet
                          <input
                            type="number"
                            min={1}
                            max={10}
                            aria-label={`${p?.name ?? "Ürün"} adedi`}
                            value={line.quantity}
                            disabled={checkout.isPending}
                            onChange={(e) => {
                              const qty = Number(e.target.value);
                              if (
                                Number.isInteger(qty) &&
                                qty >= 1 &&
                                qty <= 10
                              )
                                update(
                                  lines.map((x) =>
                                    x.key === line.key
                                      ? { ...x, quantity: qty }
                                      : x,
                                  ),
                                );
                            }}
                          />
                        </label>
                        <button
                          className="icon-button"
                          aria-label={`${p?.name ?? "Ürün"} sepetten çıkar`}
                          disabled={checkout.isPending}
                          onClick={() =>
                            update(lines.filter((x) => x.key !== line.key))
                          }
                        >
                          <Trash2 size={18} />
                        </button>
                      </div>
                    </div>
                    <strong>
                      {money(
                        previewUnit(p, line.optionValueIds) * line.quantity,
                      )}
                    </strong>
                  </article>
                );
              })}
              <div className="cart-total">
                <span>Tahmini toplam</span>
                <strong>{money(preview)}</strong>
              </div>
              <p className="small-note">
                Seçenek ücretleri toplama dahildir. Güncel tutarı doğrula.
              </p>
              {quote.error && (
                <p className="error" role="alert">
                  {quote.error.message}
                </p>
              )}
              {checkout.error && (
                <p className="error" role="alert">
                  {checkout.error.message}
                </p>
              )}
              <section className="order-song-field" aria-label="Siparişle şarkı isteği">
                <label htmlFor="requested-song">Bir şarkı isteğin var mı? <span>(İsteğe bağlı)</span></label>
                <textarea id="requested-song" rows={2} maxLength={240} value={requestedSong} disabled={checkout.isPending} onChange={e => { setRequestedSong(e.target.value); checkout.reset(); }} placeholder="Örn. Scorpions — Still Loving You" />
                <p>Şarkı ve sanatçı adını yaz. Siparişin ödemesi doğrulandığında isteğin işletmeye iletilir. Çalınma garantisi yoktur.</p>
              </section>
              {!data.branch.isAcceptingOrders && (
                <p className="notice">
                  Şube şu anda sipariş kabul etmiyor. Sepetin korunuyor.
                </p>
              )}
              {currentQuote && (
                <div className="quote-success" role="status">
                  <CircleCheck size={22} />
                  <div>
                    <strong>
                      Sepet doğrulandı · {money(currentQuote.total)}
                    </strong>
                    <p>
                      {currentQuote.items
                        .map(
                          (x) =>
                            `${x.quantity} × ${x.name}: ${money(x.lineTotal)}`,
                        )
                        .join(" / ")}
                    </p>
                    <p>
                      Gel-al toplamı güncel fiyatlarla hesaplandı. Henüz ödeme
                      alınmadı ve sipariş oluşturulmadı.
                    </p>
                  </div>
                </div>
              )}
              {checkout.data && 'quote' in checkout.data && currentQuote && <p className="notice" role="alert">Sepetin tutarı değişti. Güncel toplam {money(currentQuote.total)}. Devam etmeden önce kontrol et; henüz ödeme başlatılmadı.</p>}
              <div className="cart-checkout-actions">
              <button className="primary picker-submit" disabled={quote.isPending || checkout.isPending || data.checkoutEnabled === false || !data.branch.isAcceptingOrders || !!menu.error}
                onClick={() => checkout.mutate({ expectedTotal: currentQuote?.total ?? preview, payload, key, song: requestedSong })}>
                {checkout.isPending ? 'Sepet kontrol ediliyor…' : checkout.data && 'quote' in checkout.data && currentQuote ? 'Güncel tutarla ödemeye devam' : 'Ödemeye devam'}<ArrowRight size={18} />
              </button>
              <button
                className="secondary picker-submit"
                onClick={() => {
                  setVerified(null);
                  quote.mutate({ payload, key });
                }}
                disabled={
                  quote.isPending ||
                  checkout.isPending ||
                  !data.branch.isAcceptingOrders ||
                  !!menu.error
                }
              >
                {quote.isPending
                  ? "Doğrulanıyor…"
                  : currentQuote
                    ? "Fiyatı yeniden doğrula"
                    : "Sepeti doğrula"}
                <ArrowRight size={18} />
              </button>
              </div>
              {data.checkoutEnabled === false && (
                <p className="notice">
                  Online ödeme şu anda kullanılamıyor. Sepetin korunuyor.
                </p>
              )}
              {data.demo && <p className="small-note">Demo ödeme · Gerçek para çekilmez.</p>}
            </>
          )}
        </Dialog>
      )}
    </main>
  );
}
