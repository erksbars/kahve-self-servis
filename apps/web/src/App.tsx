import { useState } from "react";
import type { FormEvent } from "react";
import { BrowserRouter, Link, Navigate, Route, Routes, matchPath, useLocation } from "react-router-dom";
import {
  QueryClient,
  QueryClientProvider,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ArrowRight,
  Coffee,
  ShieldCheck,
  Store,
  RefreshCw,
  LogOut,
  CircleCheck,
  CirclePause,
} from "lucide-react";
import { api, ApiError } from "./api";
import type { Branch, Session } from "./api";
import { CustomerMenu } from "./CustomerMenu";
import { BranchQr } from "./BranchQr";
import { SandboxPayment, OrderTracking } from "./PaymentPages";
import { StaffBoard } from "./StaffBoard";
import { MerchantAdmin } from "./MerchantAdmin";
import { InvitePage } from "./InvitePage";
import { IyzicoPayment } from "./IyzicoPayment";

const client = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: true } },
});
function Header() {
  const { pathname } = useLocation();
  const menu = matchPath('/b/:slug', pathname);
  const tracking = matchPath('/orders/track/:token', pathname);
  const iyzico = matchPath('/iyzico/pay/:token', pathname);
  const sandbox = matchPath('/sandbox/pay/:token', pathname);
  const key = tracking ? ['tracking', tracking.params.token] : iyzico ? ['iyzico', iyzico.params.token] : sandbox ? ['payment', sandbox.params.token] : ['header-branch'];
  // Observe the page's existing cache; do not issue a second request or guess a branch.
  const context = useQuery<{ branch: { slug: string } }>({ queryKey: key, enabled: false });
  const slug = menu?.params.slug ?? context.data?.branch.slug;
  const target = slug ? `/b/${encodeURIComponent(slug)}` : tracking || iyzico || sandbox ? null : '/staff';
  const brand = <><span className="brand-icon"><Coffee size={21} /></span>kahve<span className="brand-dot">.</span></>;
  return (
    <header className="header">
      {target ? <Link className="brand" to={target} aria-label="Şube menüsüne dön">{brand}</Link> : <span className="brand">{brand}</span>}
      <nav aria-label="Ana gezinme">
        <Link to="/staff">
          <Store size={16} /> Personel alanı <ArrowRight size={14} />
        </Link>
      </nav>
    </header>
  );
}
function ErrorBox({ error, retry }: { error: Error; retry?: () => void }) {
  return (
    <div className="error" role="alert">
      <p>{error.message}</p>
      {retry && (
        <button className="secondary" onClick={retry}>
          <RefreshCw size={16} /> Yeniden dene
        </button>
      )}
    </div>
  );
}
function Staff() {
  const [qrBranch, setQrBranch] = useState<Branch | null>(null);
  const qc = useQueryClient();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [message, setMessage] = useState("");
  const session = useQuery({
    queryKey: ["session"],
    queryFn: () => api<Session>("/api/session/me"),
    retry: false,
  });
  const branches = useQuery({
    queryKey: ["staff-branches"],
    queryFn: () => api<Branch[]>("/api/staff/branches"),
    enabled: !!session.data,
    refetchInterval: 15000,
  });
  const login = useMutation({
    mutationFn: () =>
      api<Session>("/api/session/login", {
        method: "POST",
        body: JSON.stringify({ email, password }),
      }),
    onSuccess: (data) => {
      setPassword("");
      qc.setQueryData(["session"], data);
    },
  });
  const logout = useMutation({
    mutationFn: () => api<void>("/api/session/logout", { method: "POST" }),
    onSuccess: () => {
      qc.clear();
      setMessage("");
    },
  });
  const toggle = useMutation({
    mutationFn: (b: Branch) =>
      api(`/api/staff/branches/${b.id}/accepting-orders`, {
        method: "PATCH",
        body: JSON.stringify({
          isAcceptingOrders: !b.isAcceptingOrders,
          expectedVersion: b.version,
        }),
      }),
    onSuccess: () => {
      setMessage("Şube durumu güncellendi.");
      void qc.invalidateQueries({ queryKey: ["staff-branches"] });
      void qc.invalidateQueries({ queryKey: ["menu"] });
    },
    onError: () => {
      setMessage("");
      void qc.invalidateQueries({ queryKey: ["staff-branches"] });
    },
  });
  function submit(e: FormEvent) {
    e.preventDefault();
    login.mutate();
  }
  if (session.isPending)
    return (
      <main className="staff-page">
        <p role="status">Oturum kontrol ediliyor…</p>
      </main>
    );
  if (
    session.error &&
    (!(session.error instanceof ApiError) || session.error.status !== 401)
  )
    return (
      <main className="staff-page">
        <ErrorBox error={session.error} retry={() => void session.refetch()} />
      </main>
    );
  if (
    !session.data ||
    (session.error instanceof ApiError && session.error.status === 401)
  )
    return (
      <main className="login-page">
        <section className="login-card">
          <span className="brand-icon">
            <Store size={24} />
          </span>
          <p className="eyebrow">PERSONEL ALANI</p>
          <h1>
            Kahvenin
            <br />
            <em>diğer tarafı.</em>
          </h1>
          <p>Yetkili olduğunuz şubelerin sipariş kabulünü yönetin.</p>
          <form onSubmit={submit}>
            <label>
              E-posta
              <input
                type="email"
                autoComplete="username"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                maxLength={254}
              />
            </label>
            <label>
              Parola
              <input
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                maxLength={256}
              />
            </label>
            {login.error && <ErrorBox error={login.error} />}
            <button className="primary" disabled={login.isPending}>
              {login.isPending ? "Giriş yapılıyor…" : "Oturum aç"}
              <ArrowRight size={18} />
            </button>
          </form>
          <p className="small">
            Demo hesap bilgileri için projenin README dosyasına bakın.
          </p>
        </section>
      </main>
    );
  return (
    <main className="staff-page">
      <div className="staff-title">
        <div>
          <p className="eyebrow">ŞUBE OPERASYONU</p>
          <h1>Merhaba, ekip.</h1>
          <p>
            {session.data.email} ·{" "}
            {session.data.role === "Owner" ? "İşletme sahibi" : "Barista"}
          </p>
        </div>
        <button
          className="secondary"
          onClick={() => logout.mutate()}
          disabled={logout.isPending}
        >
          <LogOut size={18} /> Çıkış yap
        </button>
      </div>
      {logout.error && <ErrorBox error={logout.error} />}
      {session.data.role === "Owner" && (
        <Link className="secondary" to="/admin">
          İşletme yönetimi →
        </Link>
      )}
      {branches.error && (
        <ErrorBox
          error={branches.error}
          retry={() => void branches.refetch()}
        />
      )}
      {toggle.error && <ErrorBox error={toggle.error} />}
      {message && (
        <p className="notice" role="status">
          {message}
        </p>
      )}
      {branches.data && <StaffBoard branches={branches.data} />}
      <div className="branch-grid">
        {branches.data?.map((b) => (
          <article className="branch-card" key={b.id}>
            <Store size={25} />
            <h2>{b.name}</h2>
            {b.isOpenNow === false && b.isAcceptingOrders && (
              <p className="small-note">
                Çalışma saatleri dışında sipariş kabulü kapalı.
              </p>
            )}
            {b.preparationCapacity && (
              <p className="small-note">
                Hazırlık kapasitesi: {b.preparationCapacity}
              </p>
            )}
            <p className={`status ${b.isAcceptingOrders ? "" : "paused"}`}>
              {b.isAcceptingOrders ? (
                <CircleCheck size={16} />
              ) : (
                <CirclePause size={16} />
              )}
              {b.isAcceptingOrders
                ? "Sipariş kabulü açık"
                : "Sipariş kabulü durduruldu"}
            </p>
            <Link to={`/b/${b.slug}`}>
              Şube menüsünü aç <ArrowRight size={16} />
            </Link>
            <button className="secondary" onClick={() => setQrBranch(b)}>
              Menü QR kodu
            </button>
            <button
              className="primary"
              onClick={() => toggle.mutate(b)}
              disabled={toggle.isPending || branches.isFetching}
            >
              {b.isAcceptingOrders
                ? "Sipariş kabulünü durdur"
                : "Sipariş kabulünü aç"}
            </button>
          </article>
        ))}
      </div>
      {branches.isPending && <p role="status">Şubeler yükleniyor…</p>}
      <div className="demo-note">
        <ShieldCheck size={18} />
        <div>
          <strong>Demo işletme</strong>
          <span>Siparişler sandbox ödemeden gelir. Gerçek para çekilmez.</span>
        </div>
      </div>
      {qrBranch && (
        <BranchQr branch={qrBranch} close={() => setQrBranch(null)} />
      )}
    </main>
  );
}
export default function App() {
  return (
    <QueryClientProvider client={client}>
      <BrowserRouter>
        <Header />
        <Routes>
          <Route path="/" element={<Navigate to="/b/demo-moda" replace />} />
          <Route path="/b/:slug" element={<CustomerMenu />} />
          <Route path="/staff" element={<Staff />} />
          <Route path="/admin" element={<MerchantAdmin />} />
          <Route path="/invite/:token" element={<InvitePage />} />
          <Route path="/sandbox/pay/:token" element={<SandboxPayment />} />
          <Route path="/iyzico/pay/:token" element={<IyzicoPayment />} />
          <Route path="/orders/track/:token" element={<OrderTracking />} />
          <Route
            path="*"
            element={
              <main className="page">
                <h1>Sayfa bulunamadı.</h1>
                <Link to="/">Menüye dön</Link>
              </main>
            }
          />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
