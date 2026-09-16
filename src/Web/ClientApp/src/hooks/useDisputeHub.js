import { useEffect, useRef, useCallback } from "react";
import * as signalR from "@microsoft/signalr";

/**
 * Custom hook để kết nối đến DisputeHub SignalR.
 *
 * Khi một admin resolve dispute, event "DisputeResolved" được push đến
 * TẤT CẢ admin đang online — kể cả những admin đang kết nối với pod khác
 * (nhờ Redis backplane).
 *
 * @param {Function} onDisputeResolved - Callback khi nhận event DisputeResolved
 * @returns {{ connectionState: string }}
 */
export function useDisputeHub(onDisputeResolved) {
  const connectionRef = useRef(null);
  const callbackRef = useRef(onDisputeResolved);

  // Cập nhật callback ref để tránh stale closure
  useEffect(() => {
    callbackRef.current = onDisputeResolved;
  }, [onDisputeResolved]);

  useEffect(() => {
    // Trong dev: trỏ thẳng vào backend (https://localhost:5001) để bypass vấn đề
    // WSS proxy của CRA dev server — proxy ko thể upgrade WebSocket Secure đáng tin cậy.
    // Trong production: biến env không tồn tại → dùng relative URL (cùng domain với API).
    const baseUrl = process.env.REACT_APP_HUB_BASE_URL || "";
    const hubUrl = `${baseUrl}/hubs/dispute`;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        // SignalR tự động fallback: WebSocket → Server-Sent Events → Long Polling
        transport:
          signalR.HttpTransportType.WebSockets |
          signalR.HttpTransportType.ServerSentEvents |
          signalR.HttpTransportType.LongPolling,
        // Cookie auth: không cần access_token query vì đã set withCredentials
        withCredentials: true,
      })
      .withAutomaticReconnect({
        // Thử reconnect theo pattern: 0, 2s, 10s, 30s rồi dừng
        nextRetryDelayInMilliseconds: (retryContext) => {
          const delays = [0, 2000, 10000, 30000];
          return delays[retryContext.previousRetryCount] ?? null;
        },
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    // Lắng nghe event từ server
    connection.on("DisputeResolved", (data) => {
      if (callbackRef.current) {
        callbackRef.current(data);
      }
    });

    // Bắt đầu kết nối
    connection.start().catch((err) => {
      console.warn("[DisputeHub] Connection failed:", err.message);
    });

    connectionRef.current = connection;

    // Cleanup khi component unmount
    return () => {
      connection.stop();
    };
  }, []); // Chỉ chạy 1 lần khi mount
}
