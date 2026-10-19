import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useMutation, useQuery } from "@tanstack/react-query";
import { api } from "./api";
import "./admin.css";
export function InvitePage() {
  const { token } = useParams();
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const invite = useQuery({
    queryKey: ["invitation", token],
    queryFn: () =>
      api<{ email: string; role: string }>(
        `/api/invitations/${encodeURIComponent(token ?? "")}`,
      ),
    retry: false,
  });
  const redeem = useMutation({
    mutationFn: () =>
      api(`/api/invitations/${encodeURIComponent(token ?? "")}/redeem`, {
        method: "POST",
        body: JSON.stringify({ password }),
      }),
    onSuccess: () => {
      setPassword("");
      setConfirm("");
    },
  });
  return (
    <main className="login-page">
      <section className="login-card">
        <p className="eyebrow">EKİBE KATIL</p>
        <h1>
          {redeem.isSuccess ? "Hesabınız hazır." : "Kahvenin diğer tarafı."}
        </h1>
        {redeem.isSuccess ? (
          <>
            <p>
              Parolanız oluşturuldu. Personel alanında oturum açabilirsiniz.
            </p>
            <Link className="primary" to="/staff">
              Oturum aç
            </Link>
          </>
        ) : (
          <>
            {invite.isPending && <p role="status">Davet kontrol ediliyor…</p>}
            {invite.error && (
              <p className="error" role="alert">
                {invite.error.message}
              </p>
            )}
            {invite.data && (
              <form
                className="admin-form"
                onSubmit={(e) => {
                  e.preventDefault();
                  if (password === confirm) redeem.mutate();
                }}
              >
                <p>
                  {invite.data.email} ·{" "}
                  {invite.data.role === "Owner" ? "İşletme sahibi" : "Barista"}
                </p>
                <label>
                  Yeni parola
                  <input
                    type="password"
                    autoComplete="new-password"
                    required
                    minLength={12}
                    maxLength={128}
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                  />
                </label>
                <label>
                  Parolayı tekrar girin
                  <input
                    type="password"
                    autoComplete="new-password"
                    required
                    minLength={12}
                    maxLength={128}
                    value={confirm}
                    onChange={(e) => setConfirm(e.target.value)}
                  />
                </label>
                <p className="small-note">En az 12 karakter kullanın.</p>
                {confirm && password !== confirm && (
                  <p role="alert">Parolalar eşleşmiyor.</p>
                )}
                {redeem.error && (
                  <p className="error" role="alert">
                    {redeem.error.message}
                  </p>
                )}
                <button
                  className="primary"
                  disabled={redeem.isPending || password !== confirm}
                >
                  {redeem.isPending ? "Hesap hazırlanıyor…" : "Daveti kabul et"}
                </button>
              </form>
            )}
          </>
        )}
      </section>
    </main>
  );
}
