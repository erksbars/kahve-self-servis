import { useEffect } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery } from "@tanstack/react-query";
import {
  ArrowRight,
  ShieldCheck,
  CircleCheck,
  CircleX,
  Clock,
  Coffee,
} from "lucide-react";
import { api, money } from "./api";
import type { PaymentSession, QuoteItem, TrackedOrder } from "./api";
import { releaseCheckout } from "./checkout";
import { LiveBadge, useLive } from "./useLive";

export function ReceiptItems({ items }: { items: QuoteItem[] }) {
  return (
    <div className="receipt-items">
      {items.map((item, index) => (
        <div className="receipt-item" key={index}>
          <div>
            <strong>
              {item.quantity} × {item.name}
            </strong>
            <p>{item.options.map((o) => o.name).join(" · ")}</p>
          </div>
          <strong>{money(item.lineTotal)}</strong>
        </div>
      ))}
    </div>
  );
}
function FetchError({ error, retry }: { error: Error; retry: () => void }) {
  return (
    <div className="error" role="alert">
      <p>{error.message}</p>
      <button className="secondary" onClick={retry}>
        Yeniden dene
      </button>
    </div>
  );
}

export function SandboxPayment() {
  const { token } = useParams();
  const navigate = useNavigate();
  const session = useQuery({
    queryKey: ["payment", token],
    queryFn: () =>
      api<PaymentSession>(
        `/api/sandbox/sessions/${encodeURIComponent(token!)}`,
      ),
    retry: false,
    refetchInterval: 5000,
  });
  const decision = useMutation({
    mutationFn: (outcome: string) =>
      api<PaymentSession>(
        `/api/sandbox/sessions/${encodeURIComponent(token!)}/decision`,
        { method: "POST", body: JSON.stringify({ outcome }) },
      ),
    onSuccess: (data) => {
      void session.refetch();
      if (data.trackingUrl && data.paymentStatus === "Paid") {
        releaseCheckout(data.branch.slug, true);
        navigate(data.trackingUrl, { replace: true });
      } else releaseCheckout(data.branch.slug, false);
    },
    onError: () => void session.refetch(),
  });
  useEffect(() => {
    const data = session.data;
    if (data?.paymentStatus === "Failed" || data?.paymentStatus === "Expired")
      releaseCheckout(data.branch.slug, false);
    if (data?.paymentStatus === "Paid" && data.trackingUrl) {
      releaseCheckout(data.branch.slug, true);
      navigate(data.trackingUrl, { replace: true });
    }
  }, [session.data, navigate]);
  if (!session.data)
    return (
      <main className="payment-page">
        {session.error ? (
          <FetchError
            error={session.error}
            retry={() => void session.refetch()}
          />
        ) : (
          <p role="status">Ödeme oturumu yükleniyor…</p>
        )}
      </main>
    );
  const data = session.data;
  if (data.paymentStatus === "Paid")
    return (
      <main className="payment-page">
        <p role="status">
          Ödeme doğrulandı; sipariş takip sayfasına geçiliyor…
        </p>
      </main>
    );
  const finished =
    data.paymentStatus !== "Pending" ||
    Date.parse(data.expiresAt) <= Date.now();
  return (
    <main className="payment-page">
      <section className="payment-card">
        <div className="sandbox-badge">
          <ShieldCheck size={18} /> SANDBOX · TEST ÖDEMESİ
        </div>
        <h1>
          Kahven için
          <br />
          <em>son bir adım.</em>
        </h1>
        <p className="picker-description">{data.branch.name} · Gel-al</p>
        <div className="notice">
          Bu bir test ekranıdır. Gerçek para çekilmez ve kart bilgisi istenmez.
        </div>
        <ReceiptItems items={data.items} />
        <div className="receipt-total">
          <span>Toplam</span>
          <strong>{money(data.amount)}</strong>
        </div>
        {session.error && (
          <FetchError
            error={session.error}
            retry={() => void session.refetch()}
          />
        )}
        {decision.error && (
          <p className="error" role="alert">
            {decision.error.message}
          </p>
        )}
        {finished ? (
          <div className="payment-result" role="status">
            <CircleX size={25} />
            <h2>
              {data.paymentStatus === "Failed"
                ? "Test ödemesi başarısız"
                : "Ödeme oturumunun süresi doldu"}
            </h2>
            <p>Sipariş operasyon kuyruğuna aktarılmadı. Sepetin korunuyor.</p>
            <Link className="primary" to={`/b/${data.branch.slug}`}>
              Sepetine dön <ArrowRight size={18} />
            </Link>
          </div>
        ) : (
          <div className="sandbox-actions">
            <button
              className="primary"
              disabled={decision.isPending || !!session.error}
              onClick={() => decision.mutate("paid")}
            >
              {decision.isPending
                ? "Sonuç doğrulanıyor…"
                : "Başarılı ödemeyi simüle et"}
              <CircleCheck size={18} />
            </button>
            <button
              className="secondary"
              disabled={decision.isPending || !!session.error}
              onClick={() => decision.mutate("failed")}
            >
              Başarısız ödemeyi simüle et
            </button>
            <p className="small-note">
              <Clock size={13} /> Oturum{" "}
              {new Date(data.expiresAt).toLocaleTimeString("tr-TR", {
                hour: "2-digit",
                minute: "2-digit",
              })}{" "}
              saatine kadar geçerli.
            </p>
          </div>
        )}
      </section>
    </main>
  );
}

const labels: Record<string, string> = {
  PaymentPending: "Ödeme bekleniyor",
  Paid: "Ödeme doğrulandı",
  Received: "Sipariş alındı",
  Preparing: "Hazırlanıyor",
  Ready: "Kahven hazır",
  Completed: "Teslim edildi",
  PaymentFailed: "Ödeme başarısız",
  Expired: "Ödeme oturumu sona erdi",
};
export function OrderTracking() {
  const { token } = useParams();
  const live = useLive("tracking", token!, () => void order.refetch());
  const order = useQuery({
    queryKey: ["tracking", token],
    queryFn: () =>
      api<TrackedOrder>(`/api/orders/track/${encodeURIComponent(token!)}`),
    retry: false,
    refetchInterval: live === "live" ? 30000 : 2000,
  });
  useEffect(() => {
    if (order.data?.paymentStatus === "Paid")
      releaseCheckout(order.data.branch.slug, true);
  }, [order.data]);
  if (!order.data)
    return (
      <main className="payment-page">
        {order.error ? (
          <FetchError error={order.error} retry={() => void order.refetch()} />
        ) : (
          <p role="status">Sipariş yükleniyor…</p>
        )}
      </main>
    );
  const data = order.data;
  const steps = ["Received", "Preparing", "Ready", "Completed"];
  const active = steps.indexOf(data.status);
  const description: Record<string, string> = {
    PaymentPending: "Ödeme tamamlandığında siparişini buradan takip edebilirsin.",
    Paid: "Ödemen doğrulandı. Siparişin işletmeye iletiliyor.",
    Received: "Siparişin işletmeye ulaştı. Hazırlık başladığında burada göreceksin.",
    Preparing: "Baristan siparişini hazırlıyor. Hazır olduğunda bu ekran güncellenecek.",
    Ready: "Sipariş numaranı göstererek kahveni teslim alabilirsin.",
    Completed: "Kahve molanın tadını çıkar. Yeniden görüşmek üzere!",
    PaymentFailed: "Ödeme tamamlanamadı. Menüye dönerek sepetini yeniden kontrol edebilirsin.",
    Expired: "Ödeme oturumunun süresi doldu. Menüden yeniden başlayabilirsin.",
  };
  return (
    <main className="payment-page order-tracking-page">
      <section className="payment-card tracking-card">
        <div className="tracking-topline"><span className="tracking-mode"><Coffee size={17} /> KAHVE MOLAN</span><LiveBadge state={live} /></div>
        <p className="eyebrow">
          {data.branch.name.toLocaleUpperCase("tr-TR")} · GEL-AL
        </p>
        <h1>{labels[data.status] ?? "Sipariş durumu"}</h1>
        <p className="tracking-description">{description[data.status] ?? "Siparişinin güncel durumunu buradan takip edebilirsin."}</p>
        <div className="order-number">
          <span>Sipariş numaran<small>Teslim alırken bu numarayı göster.</small></span>
          <strong>#{data.publicNumber.toString().padStart(4, "0")}</strong>
        </div>
        {data.paymentStatus === "Paid" && (
          <p className="verified-payment">
            <CircleCheck size={16} /> {data.demo ? 'Sandbox ödemesi doğrulandı' : 'Ödeme doğrulandı'} ·{" "}
            {money(data.total)}
          </p>
        )}
        {data.status === "Paid" && (
          <p className="notice" role="status">
            Ödeme doğrulandı. Sipariş operasyon kuyruğuna aktarılıyor.
          </p>
        )}
        {data.paymentStatus === "Paid" && (
          <ol className="tracking-steps" aria-label="Sipariş aşamaları">
            {steps.map((step, index) => (
              <li
                key={step}
                className={index <= active ? "reached" : ""}
                aria-current={index === active ? "step" : undefined}
              >
                <span>
                  {index < active ? <CircleCheck size={16} /> : index + 1}
                </span>
                {labels[step]}
              </li>
            ))}
          </ol>
        )}
        <p className="small-note">Son doğrulama: {new Date(order.dataUpdatedAt).toLocaleTimeString('tr-TR')}</p>
        {(order.error || live === 'offline') && <div className="notice" role="status"><p>Sipariş durumu şu an güncellenemiyor. Aşağıda son doğrulanan bilgi görünüyor.</p><button className="secondary" disabled={order.isFetching} onClick={() => void order.refetch()}>Durumu yenile</button></div>}
      </section>
      <section className="payment-card order-receipt" aria-label="Sipariş özeti">
        <div className="receipt-heading"><h2>Siparişin</h2><span>{data.branch.name} · Gel-al</span></div>
        <ReceiptItems items={data.items} />
        <div className="receipt-total"><span>Toplam</span><strong>{money(data.total)}</strong></div>
        {data.songRequest && <section className="order-song-field" aria-label="Şarkı isteğin"><h3>Şarkı isteğin</h3><p className="song-request-text">{data.songRequest.text}</p><p>{({ Pending: 'İsteğin işletmeye iletildi.', Accepted: 'İşletme isteğini kabul etti.', Rejected: 'İşletme isteğini bu kez kabul edemedi.' } as Record<string, string>)[data.songRequest.status] ?? 'İsteğin alındı.'}</p><p>Kabul edilmesi çalınma garantisi vermez.</p></section>}
        <Link className="secondary receipt-menu-link" to={`/b/${data.branch.slug}`}>
          Menüye dön <ArrowRight size={16} />
        </Link>
      </section>
      <footer className="tracking-footer">
        <p className="small-note">
          Hazırlanma süresi işletmeye göre değişebilir. Bu bağlantı yalnızca
          senin siparişini gösterir; paylaşmak erişim izni verir.
        </p>
        {data.demo && <div className="demo-note">
          <ShieldCheck size={18} />
          <div>
            <strong>Demo sipariş</strong>
            <span>
              Gerçek ödeme alınmadı. Durum işletmenin işlem ekranından
              güncellenir.
            </span>
          </div>
        </div>}
      </footer>
    </main>
  );
}
