import { useEffect, useRef, useState } from "react";
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
export type LiveState =
  | "connecting"
  | "live"
  | "fallback"
  | "offline"
  | "revoked";
export function useLive(
  kind: "staff" | "tracking",
  id: string,
  onChange: () => void,
  onRevoked?: () => void,
) {
  const callbacks = useRef({ onChange, onRevoked });
  callbacks.current = { onChange, onRevoked };
  const [state, setState] = useState<LiveState>("connecting");
  useEffect(() => {
    let disposed = false;
    let blocked = false;
    let timer: ReturnType<typeof setTimeout> | undefined;
    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/orders")
      .configureLogging(LogLevel.None)
      .withAutomaticReconnect([0, 1000, 2000, 5000])
      .build();
    function schedule() {
      if (disposed || blocked) return;
      clearTimeout(timer);
      timer = setTimeout(() => void start(), 3000);
    }
    async function join() {
      try {
        await connection.invoke(
          kind === "staff" ? "JoinStaff" : "JoinTracking",
          id,
        );
      } catch {
        if (!disposed) {
          blocked = true;
          setState("revoked");
          callbacks.current.onRevoked?.();
        }
        await connection.stop();
        return;
      }
      if (disposed) return;
      setState("live");
      callbacks.current.onChange();
    }
    async function start() {
      if (
        disposed ||
        blocked ||
        connection.state !== HubConnectionState.Disconnected
      )
        return;
      if (!navigator.onLine) {
        setState("offline");
        schedule();
        return;
      }
      setState("connecting");
      try {
        await connection.start();
        if (disposed) {
          await connection.stop();
          return;
        }
        await join();
      } catch {
        if (!disposed) {
          setState(navigator.onLine ? "fallback" : "offline");
          schedule();
        }
      }
    }
    connection.on("OrderChanged", () => callbacks.current.onChange());
    connection.on("BranchChanged", () => callbacks.current.onChange());
    connection.on("AccessRevoked", () => {
      blocked = true;
      setState("revoked");
      callbacks.current.onRevoked?.();
      void connection.stop();
    });
    connection.onreconnecting(() => {
      if (!disposed) setState(navigator.onLine ? "fallback" : "offline");
    });
    connection.onreconnected(() => void join());
    connection.onclose(() => {
      if (!disposed && !blocked) {
        setState(navigator.onLine ? "fallback" : "offline");
        schedule();
      }
    });
    function online() {
      if (!disposed) {
        callbacks.current.onChange();
        if (!blocked && connection.state === HubConnectionState.Connected)
          void join();
        else void start();
      }
    }
    function offline() {
      if (!disposed) setState("offline");
    }
    window.addEventListener("online", online);
    window.addEventListener("offline", offline);
    void start();
    return () => {
      disposed = true;
      clearTimeout(timer);
      window.removeEventListener("online", online);
      window.removeEventListener("offline", offline);
      void connection.stop();
    };
  }, [kind, id]);
  return state;
}
export function LiveBadge({ state }: { state: LiveState }) {
  return (
    <p className={`live-badge live-${state}`} role="status">
      {state === "live"
        ? "Canlı bağlantı açık"
        : state === "offline"
          ? "Bağlantı kesildi · Yeniden bağlanma bekleniyor"
          : state === "revoked"
            ? "Bağlantı yetkisi bulunamadı · Oturumu kontrol edin"
            : state === "connecting"
              ? "Canlı bağlantı kuruluyor…"
              : "Canlı bağlantı yeniden deneniyor · Düzenli sorgulama açık"}
    </p>
  );
}
