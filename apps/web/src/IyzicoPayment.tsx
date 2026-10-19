import { useState, useEffect } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery } from "@tanstack/react-query";
import { api, money } from "./api";
import { releaseCheckout } from './checkout';
import type { PaymentSession } from "./api";
import { ReceiptItems } from "./PaymentPages";
import { ArrowRight, ShieldCheck, CreditCard } from "lucide-react";
import "./admin.css";
type IyzicoSession = PaymentSession & {
  initializationState: string;
  paymentUrl: string | null;
  expired: boolean;
  review: boolean;
};
export function IyzicoPayment() {
  const { token } = useParams();
  const navigate = useNavigate();
  const path = `/api/iyzico/sessions/${encodeURIComponent(token ?? "")}`;
  const [customer, set] = useState({
    name: "",
    surname: "",
    identityNumber: "",
    email: "",
    phone: "",
    address: "",
    city: "",
    country: "Turkey",
  });
  const session = useQuery({
    queryKey: ["iyzico", token],
    queryFn: () => api<IyzicoSession>(path),
    refetchInterval: 3000,
    retry: false,
  });
  const initialize = useMutation({
    mutationFn: () =>
      api<{ paymentUrl: string }>(path + "/initialize", {
        method: "POST",
        body: JSON.stringify(customer),
      }),
    onSuccess: (data) => {
      const target = new URL(data.paymentUrl);
      if (
        target.protocol === "https:" &&
        target.hostname === "sandbox-cpp.iyzipay.com"
      )
        window.location.assign(target.href);
    },
    onSettled: () => {
      void session.refetch();
    },
  });
  const verify = useMutation({
    mutationFn: () => api<IyzicoSession>(path + "/verify", { method: "POST" }),
    onSuccess: () => {
      void session.refetch();
    },
  });
  useEffect(() => {
    if (session.data?.trackingUrl && session.data.paymentStatus === "Paid") {
      releaseCheckout(session.data.branch.slug, true);
      navigate(session.data.trackingUrl, { replace: true });
    }
  }, [session.data, navigate]);
  useEffect(() => { if (session.data?.paymentStatus === 'Failed') releaseCheckout(session.data.branch.slug, false); }, [session.data]);
  const data = session.data;
  return (
    <main className="payment-page iyzico-page">
      <section className="payment-card iyzico-card">
        <div className="sandbox-badge"><ShieldCheck size={16} /> İYZİCO · TEST ÖDEMESİ</div>
        <h1>Kahven için<br /><em>son bir adım.</em></h1>
        <p className="payment-intro">
          Bu sandbox testidir; gerçek para çekilmez. Kart bilgileri yalnızca
          iyzico sayfasında girilir.
        </p>
        {session.isPending && <p role="status">Ödeme yükleniyor…</p>}
        {session.error && (
          <p className="error" role="alert">
            {session.error.message}
          </p>
        )}
        {data && (
          <>
            <h2>
              {data.branch.name} · Gel-al
            </h2>
            <ReceiptItems items={data.items} />
            <div className="receipt-total"><span>Ödenecek toplam</span><strong>{money(data.amount)}</strong></div>
            {data.paymentStatus === "Failed" && (
              <p role="alert">
                Ödeme başarısız. Yeni deneme için menüden sepetinizi yeniden
                doğrulayın.
              </p>
            )}
            {data.review && (
              <p className="notice" role="status">
                Ödeme iyzico incelemesinde. Tekrar ödeme yapmayın; sonuç
                bekleniyor.
              </p>
            )}
            {["Initializing", "Unknown"].includes(
              data.initializationState,
            ) && (
              <p className="notice" role="status">
                Ödeme başlatma sonucu kesinleşmedi. Bu sayfayı koruyun ve yeni
                ödeme başlatmayın.
              </p>
            )}
            {data.initializationState === 'Rejected' && <p className="notice" role="alert">iyzico ödeme isteğini reddetti. Bilgilerinizi kontrol edin; hata sürerse işletmeye başvurun.</p>}
            {data.expired && data.paymentStatus === "Pending" && (
              <p className="notice">
                Yeni ödeme başlatma süresi doldu. Önceden yaptığınız ödeme
                sonucu kontrol edilmeye devam ediyor.
              </p>
            )}
            {['NotStarted', 'Rejected'].includes(data.initializationState) &&
              !data.expired &&
              data.paymentStatus === "Pending" && (
                <form
                  className="admin-form payment-buyer-form"
                  onSubmit={(e) => {
                    e.preventDefault();
                    initialize.mutate();
                  }}
                >
                  <div className="buyer-form-heading"><CreditCard size={20} /><div><h2>Alıcı bilgileri</h2><p>Kart bilgilerini bir sonraki adımda iyzico'da gireceksin.</p></div></div>
                  <p className="small-note">
                    iyzico'nun istediği alıcı ve adres bilgileri ödeme
                    sağlayıcısına iletilir; uygulamanın sipariş kaydında
                    saklanmaz.
                  </p>
                  <div className="buyer-fields">{Object.entries({
                    name: "Ad",
                    surname: "Soyad",
                    identityNumber: "Kimlik numarası",
                    email: "E-posta",
                    phone: "Telefon",
                    address: "Fatura adresi",
                    city: "Şehir",
                    country: "Ülke",
                  }).map(([key, label]) => (
                    <label key={key} className={key === "address" ? "buyer-field-wide" : undefined}>
                      {label}
                      <input
                        required
                        maxLength={250}
                        autoComplete={({ name: "given-name", surname: "family-name", email: "email", phone: "tel", address: "street-address", city: "address-level2", country: "country-name", identityNumber: "off" } as Record<string, string>)[key]}
                        inputMode={key === "identityNumber" ? "numeric" : undefined}
                        type={
                          key === "email"
                            ? "email"
                            : key === "phone"
                              ? "tel"
                              : "text"
                        }
                        value={customer[key as keyof typeof customer]}
                        onChange={(e) =>
                          set({ ...customer, [key]: e.target.value })
                        }
                      />
                    </label>
                  ))}</div>
                  {initialize.error && (
                    <p className="error" role="alert">
                      {initialize.error.message}
                    </p>
                  )}
                  <button className="primary" disabled={initialize.isPending}>
                    {initialize.isPending
                      ? "Ödeme açılıyor…"
                      : "iyzico güvenli ödeme sayfasına geç"}
                    <ArrowRight size={17} />
                  </button>
                </form>
              )}
            {data.paymentUrl && data.paymentStatus === "Pending" && (
              <a className="primary" href={data.paymentUrl} rel="noreferrer">
                Aynı iyzico ödeme oturumuna dön
              </a>
            )}
            {data.initializationState !== "NotStarted" &&
              data.paymentStatus === "Pending" && (
                <button
                  className="secondary"
                  disabled={verify.isPending}
                  onClick={() => verify.mutate()}
                >
                  Ödeme sonucunu kontrol et
                </button>
              )}
            {verify.error && (
              <p className="error" role="alert">
                {verify.error.message}
              </p>
            )}
            <Link className="secondary" to={`/b/${data.branch.slug}`}>
              Menüye dön <ArrowRight size={16} />
            </Link>
          </>
        )}
      </section>
    </main>
  );
}
