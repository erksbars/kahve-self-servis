import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Music2, RefreshCw } from "lucide-react";
import { api } from "./api";
import type { AdminBranch } from "./adminTypes";
import "./music.css";

type SongRequest = { id: string; text: string; publicNumber: number; status: string; version: number; createdAt: string };
type SongRequestPage = { items: SongRequest[]; pendingCount: number; total: number; page: number; pageSize: number; totalPages: number };
const labels: Record<string, string> = { Pending: "Yeni istek", Accepted: "Kabul edildi", Rejected: "Reddedildi" };
function useRequests(branchId: string, status = 'Pending', page = 1) {
  return useQuery({ queryKey: ["order-song-requests", branchId, status, page], queryFn: ({ signal }) => api<SongRequestPage>(`/api/staff/branches/${branchId}/song-requests?status=${status}&page=${page}&pageSize=20`, { signal }), refetchInterval: 5000, retry: false });
}
export function SongRequestShortcut({ branchId }: { branchId: string }) {
  const requests = useRequests(branchId);
  return <a className="secondary song-requests-shortcut" href={`#song-requests-${branchId}`}><Music2 size={17} /> İstek parçalar <span>{requests.error ? 'Sayaç güncellenemedi' : requests.data ? `${requests.data.pendingCount} yeni` : 'Yükleniyor…'}</span></a>;
}

export function MusicAdmin({ branches }: { branches: AdminBranch[] }) {
  const [branchId, setBranchId] = useState(branches[0]?.id ?? "");
  return <section className="admin-section">
    <p className="eyebrow">MÜZİK İSTEKLERİ</p><h2>Siparişlerin yanına bir şarkı.</h2>
    <label className="music-branch">İstek şubesi<select value={branchId} onChange={e => setBranchId(e.target.value)}>{branches.map(branch => <option key={branch.id} value={branch.id}>{branch.name}</option>)}</select></label>
    {branchId && <SongRequests key={branchId} branchId={branchId} />}
  </section>;
}

export function SongRequests({ branchId }: { branchId: string }) {
  const queries = useQueryClient();
  const [status, setStatus] = useState('Pending');
  const [page, setPage] = useState(1);
  const [notice, setNotice] = useState('');
  const path = `/api/staff/branches/${branchId}/song-requests`;
  const queryKey = ["order-song-requests", branchId];
  const requests = useRequests(branchId, status, page);
  const decision = useMutation({ mutationFn: ({ request, status }: { request: SongRequest; status: string }) => api(`${path}/${request.id}`, { method: "PUT", body: JSON.stringify({ status, expectedVersion: request.version }) }), onMutate: () => setNotice(''), onSuccess: (_, input) => setNotice(`#${input.request.publicNumber.toString().padStart(4, '0')} isteği ${input.status === 'Accepted' ? 'kabul edildi' : 'reddedildi'}.`), onSettled: () => void queries.invalidateQueries({ queryKey }) });
  const rows = requests.data?.items ?? [];
  return <section id={`song-requests-${branchId}`} className="song-requests-panel" aria-label="İstek parçalar">
    <div className="song-requests-heading"><div><p className="eyebrow"><Music2 size={16} /> MOLANIN RİTMİ</p><h2>İstek parçalar {requests.data && <span className="song-requests-count">{requests.data.pendingCount} yeni</span>}</h2><p>Ödemesi doğrulanan siparişlerden gelen istekler. Liste otomatik yenilenir.</p></div><button type="button" className="secondary" aria-label="İstekleri yenile" disabled={requests.isFetching} onClick={() => void requests.refetch()}><RefreshCw size={17} /> Yenile</button></div>
    <div className="song-requests-filter"><label htmlFor={`request-status-${branchId}`}>İstek durumu</label><select id={`request-status-${branchId}`} value={status} disabled={decision.isPending} onChange={e => { setStatus(e.target.value); setPage(1); setNotice(''); decision.reset(); }}><option value="Pending">Yeni istekler</option><option value="Accepted">Kabul edilenler</option><option value="Rejected">Reddedilenler</option><option value="All">Tümü</option></select></div>
    {notice && <p className="song-requests-notice" role="status">{notice}</p>}
    {requests.isPending && <p role="status">İstekler yükleniyor…</p>}
    {(requests.error || decision.error) && <p className="error" role="alert">{requests.error?.message ?? decision.error?.message}</p>}
    {requests.data && !rows.length && !requests.error && <p className="song-requests-empty">{status === 'Pending' ? "Yeni bir şarkı isteği yok." : "Bu filtrede bir şarkı isteği yok."}</p>}
    <div className="music-inbox">{rows.map(request => <article className="music-inbox-item" key={request.id}>
      <div><span className="music-tag">#{request.publicNumber.toString().padStart(4, "0")} · {labels[request.status] ?? request.status}</span><p className="song-request-text">{request.text}</p><small>{new Date(request.createdAt).toLocaleString("tr-TR")}</small></div>
      {request.status === "Pending" && <div className="form-actions"><button type="button" className="primary" disabled={decision.isPending || !!requests.error} onClick={() => decision.mutate({ request, status: "Accepted" })}>Kabul et</button><button type="button" className="secondary" disabled={decision.isPending || !!requests.error} onClick={() => decision.mutate({ request, status: "Rejected" })}>Reddet</button></div>}
    </article>)}</div>
    {requests.data && <nav className="song-requests-pagination" aria-label="İstek sayfaları"><button className="secondary" disabled={requests.isFetching || decision.isPending || requests.data.page <= 1} onClick={() => setPage(requests.data!.page - 1)}>Önceki</button><span>Sayfa {requests.data.page} / {requests.data.totalPages} · {requests.data.total} istek</span><button className="secondary" disabled={requests.isFetching || decision.isPending || requests.data.page >= requests.data.totalPages} onClick={() => setPage(requests.data!.page + 1)}>Sonraki</button></nav>}
    <p className="small-note">İstekler yalnızca metin olarak iletilir. Kabul etmek müzik çalmaz veya başka bir uygulamaya sıra eklemez.</p>
  </section>;
}
