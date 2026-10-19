import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Clock, CircleCheck, Volume2, VolumeX, RefreshCw } from "lucide-react";
import { api, ApiError } from "./api";
import type { Branch, StaffOrder } from "./api";
import { LiveBadge, useLive } from "./useLive";
import { SongRequests, SongRequestShortcut } from "./Music";

const columns = [
  {
    status: "Received",
    name: "Yeni",
    next: "Preparing",
    action: "Hazırlamaya al",
  },
  {
    status: "Preparing",
    name: "Hazırlanıyor",
    next: "Ready",
    action: "Hazır olarak işaretle",
  },
  {
    status: "Ready",
    name: "Hazır",
    next: "Completed",
    action: "Teslim edildi",
  },
];
export function StaffBoard({ branches }: { branches: Branch[] }) {
  const [selected, setSelected] = useState(branches[0]?.id ?? "");
  const branch = branches.find((x) => x.id === selected) ?? branches[0];
  if (!branch)
    return <section className="notice">Yetkili şubeniz bulunmuyor.</section>;
  return (
    <section className="staff-board" aria-labelledby="board-title">
      <div className="board-title">
        <div>
          <p className="eyebrow">GEL-AL SİPARİŞLERİ</p>
          <h2 id="board-title">Sipariş panosu</h2>
        </div>
        <label>
          Şube
          <select
            aria-label="Sipariş şubesi"
            value={branch.id}
            onChange={(e) => setSelected(e.target.value)}
          >
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        </label>
      </div>
      <SongRequestShortcut key={`song-count-${branch.id}`} branchId={branch.id} />
      <BranchBoard key={branch.id} branch={branch} />
      <SongRequests key={`songs-${branch.id}`} branchId={branch.id} />
    </section>
  );
}
function BranchBoard({ branch }: { branch: Branch }) {
  const qc = useQueryClient();
  const [activeColumn, setActiveColumn] = useState("Received");
  const [now, setNow] = useState(Date.now());
  const [sound, setSound] = useState(false);
  const [notice, setNotice] = useState(0);
  const audio = useRef<AudioContext | null>(null);
  const seen = useRef<Set<string> | null>(null);
  const queryKey = ["staff-orders", branch.id];
  const live = useLive(
    "staff",
    branch.id,
    () => {
      void qc.invalidateQueries({ queryKey });
      void qc.invalidateQueries({ queryKey: ["staff-branches"] });
    },
    () => {
      void qc.invalidateQueries({ queryKey });
      void qc.invalidateQueries({ queryKey: ["staff-branches"] });
      void qc.invalidateQueries({ queryKey: ["session"] });
    },
  );
  const orders = useQuery({
    queryKey,
    queryFn: () => api<StaffOrder[]>(`/api/staff/branches/${branch.id}/orders`),
    refetchInterval: live === "live" ? 30000 : 2000,
    retry: false,
  });
  const update = useMutation({
    mutationFn: ({ order, next }: { order: StaffOrder; next: string }) =>
      api(`/api/staff/orders/${order.id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ status: next, expectedVersion: order.version }),
      }),
    onSuccess: (_, input) => {
      setActiveColumn(input.next === "Completed" ? "Received" : input.next);
      void qc.invalidateQueries({ queryKey });
    },
    onError: (error) => {
      void qc.invalidateQueries({ queryKey });
      if (error instanceof ApiError && error.status === 401)
        void qc.invalidateQueries({ queryKey: ["session"] });
    },
  });
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 5000);
    return () => {
      clearInterval(timer);
      void audio.current?.close();
    };
  }, []);
  useEffect(() => {
    if (!orders.data) return;
    const ids = new Set(orders.data.map((x) => x.id));
    const added = seen.current
      ? orders.data.filter(
          (x) => !seen.current!.has(x.id) && x.status === "Received",
        ).length
      : 0;
    seen.current = ids;
    if (added) {
      setNotice(added);
      if (sound && audio.current?.state === "running") {
        const oscillator = audio.current.createOscillator();
        const gain = audio.current.createGain();
        gain.gain.value = 0.12;
        oscillator.frequency.value = 740;
        oscillator.connect(gain);
        gain.connect(audio.current.destination);
        oscillator.start();
        oscillator.stop(audio.current.currentTime + 0.15);
      }
    }
  }, [orders.data, sound]);
  useEffect(() => {
    if (!notice) return;
    const timer = setTimeout(() => setNotice(0), 6000);
    return () => clearTimeout(timer);
  }, [notice]);
  async function toggleSound() {
    if (sound) {
      setSound(false);
      return;
    }
    try {
      audio.current ??= new AudioContext();
      await audio.current.resume();
      setSound(true);
    } catch {
      setSound(false);
    }
  }
  if (live === "revoked")
    return (
      <>
        <LiveBadge state={live} />
        <p className="error" role="alert">
          Şube yetkiniz değişti. Güncel şube listesi ve oturum kontrol ediliyor.
        </p>
      </>
    );
  const disabled =
    live === "offline" ||
    !!orders.error ||
    update.isPending ||
    !navigator.onLine;
  return (
    <>
      <div className="board-toolbar">
        <LiveBadge state={live} />
        <span>{orders.data?.length ?? 0} aktif sipariş</span>
        <button
          className="secondary"
          aria-pressed={sound}
          onClick={() => void toggleSound()}
        >
          {sound ? <Volume2 size={17} /> : <VolumeX size={17} />}
          {sound ? "Sesi kapat" : "Sesi aç"}
        </button>
        <button
          className="secondary"
          onClick={() => void orders.refetch()}
          disabled={orders.isFetching}
        >
          <RefreshCw size={16} /> Yenile
        </button>
      </div>
      {!branch.isAcceptingOrders && (
        <p className="notice">
          Yeni sipariş alımı kapalı. Önceki ücretli siparişleri
          tamamlayabilirsiniz.
        </p>
      )}
      {notice > 0 && (
        <p className="new-order-notice" role="status">
          {notice} yeni sipariş geldi.
        </p>
      )}
      {orders.error && (
        <p className="error" role="alert">
          {orders.error.message} Güncel durum alınana kadar işlem yapamazsınız.
        </p>
      )}
      {update.error && (
        <p className="error" role="alert">
          {update.error.message}
        </p>
      )}
      {orders.isPending && <p role="status">Siparişler yükleniyor…</p>}
      <nav className="board-tabs" aria-label="Sipariş sütunu">
        {columns.map((column) => (
          <button
            key={column.status}
            aria-pressed={activeColumn === column.status}
            onClick={() => setActiveColumn(column.status)}
          >
            {column.name}
            <span>
              {orders.data?.filter((o) => o.status === column.status).length ??
                0}
            </span>
          </button>
        ))}
      </nav>
      <div className="order-columns">
        {columns.map((column) => {
          const list =
            orders.data?.filter((x) => x.status === column.status) ?? [];
          return (
            <section
              className={`order-column column-${column.status} ${activeColumn === column.status ? "active-column" : ""}`}
              key={column.status}
              aria-label={column.name}
            >
              <div className="column-title">
                <h3>{column.name}</h3>
                <span>{list.length}</span>
              </div>
              {list.length === 0 && !orders.isPending && (
                <p className="column-empty">Bu aşamada sipariş yok.</p>
              )}
              {list.map((order) => {
                const minutes = Math.max(
                  0,
                  Math.floor((now - Date.parse(order.paidAt)) / 60000),
                );
                return (
                  <article
                    className={`order-card ${minutes >= 10 ? "overdue" : ""}`}
                    key={order.id}
                    aria-label={`Sipariş ${order.publicNumber}`}
                  >
                    <div className="order-card-heading">
                      <h4>#{order.publicNumber.toString().padStart(4, "0")}</h4>
                      <span>
                        <Clock size={13} />
                        {minutes < 1 ? "Yeni geldi" : `${minutes} dk`}
                      </span>
                    </div>
                    {minutes >= 10 && (
                      <p className="overdue-label">Uzun bekleme</p>
                    )}
                    <ul className="barista-items">
                      {order.items.map((item, index) => (
                        <li key={index}>
                          <strong>
                            {item.quantity} × {item.name}
                          </strong>
                          <p>{item.options.map((o) => o.name).join(" · ")}</p>
                        </li>
                      ))}
                    </ul>
                    <p className="paid-label">
                      <CircleCheck size={14} />
                      Ödeme doğrulandı
                    </p>
                    <button
                      className="primary"
                      disabled={disabled}
                      onClick={() =>
                        update.mutate({ order, next: column.next })
                      }
                    >
                      {update.isPending &&
                      update.variables?.order.id === order.id
                        ? "İşleniyor…"
                        : column.action}
                    </button>
                  </article>
                );
              })}
            </section>
          );
        })}
      </div>
    </>
  );
}
