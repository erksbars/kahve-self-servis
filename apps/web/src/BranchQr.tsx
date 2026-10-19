import { useEffect, useState } from "react";
import QRCode from "qrcode";
import { Dialog } from "./Dialog";
import type { Branch } from "./api";

export function BranchQr({
  branch,
  close,
}: {
  branch: Branch;
  close: () => void;
}) {
  const [origin, setOrigin] = useState(window.location.origin);
  const [image, setImage] = useState("");
  const [error, setError] = useState("");
  let url = "";
  try {
    const base = new URL(origin);
    if (
      !["http:", "https:"].includes(base.protocol) ||
      base.username ||
      base.password
    )
      throw new Error();
    url = new URL(`/b/${encodeURIComponent(branch.slug)}`, base.origin).href;
  } catch {
    /* Show validation below. */
  }
  useEffect(() => {
    let cancelled = false;
    setImage("");
    setError("");
    if (!url) return;
    QRCode.toDataURL(url, { width: 300, margin: 4, errorCorrectionLevel: "M" })
      .then((value) => {
        if (!cancelled) setImage(value);
      })
      .catch(() => {
        if (!cancelled) setError("QR kodu oluşturulamadı.");
      });
    return () => {
      cancelled = true;
    };
  }, [url]);
  const local =
    url && ["127.0.0.1", "localhost", "[::1]"].includes(new URL(url).hostname);
  return (
    <Dialog
      title={`${branch.name} · Menü QR kodu`}
      close={close}
      className="qr-modal"
    >
      <p className="picker-description">
        Bu kod doğrudan {branch.name} menüsünü açar.
      </p>
      <label className="qr-origin">
        Site adresi
        <input
          type="url"
          value={origin}
          onChange={(e) => setOrigin(e.target.value)}
          aria-describedby="qr-help"
        />
      </label>
      {!url && (
        <p className="error" role="alert">
          Geçerli bir http veya https adresi girin.
        </p>
      )}
      {local && (
        <p className="notice" id="qr-help">
          Bu yerel adres yalnızca bu bilgisayarda açılır. Telefonla paylaşmak
          için erişilebilir site adresini kullanın.
        </p>
      )}
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      {image && (
        <>
          <img
            src={image}
            alt={`${branch.name} menüsünün QR kodu`}
            width={300}
            height={300}
          />
          <a href={url} className="qr-link">
            {url}
          </a>
          <div className="qr-actions">
            <a
              className="primary"
              href={image}
              download={`menu-${branch.slug}.png`}
            >
              QR kodunu indir
            </a>
            <button className="secondary" onClick={() => window.print()}>
              Yazdır
            </button>
          </div>
        </>
      )}
    </Dialog>
  );
}
