import { useEffect, useRef } from "react";
import * as signalR from "@microsoft/signalr";

/**
 * Custom hook để kết nối đến NotificationHub SignalR.
 * 
 * Lắng nghe event "NewNotification" để cập nhật real-time.
 * 
 * @param {Function} onNewNotification - Callback khi nhận thông báo mới
 */
export function useNotificationHub(events, callback, onReconnected) {
  let eventNames = ["NewNotification"];
  let cb = events;
  let reconnected = onReconnected;

  if (typeof events === 'function') {
    cb = events;
    reconnected = typeof callback === 'function' ? callback : undefined;
  } else if (typeof events === 'string') {
    eventNames = [events];
    cb = callback;
  } else if (Array.isArray(events)) {
    eventNames = events;
    cb = callback;
  }

  const callbackRef = useRef(cb);
  const reconnectedRef = useRef(reconnected);

  useEffect(() => {
    callbackRef.current = cb;
  }, [cb]);

  useEffect(() => {
    reconnectedRef.current = reconnected;
  }, [reconnected]);

  useEffect(() => {
    const baseUrl = process.env.REACT_APP_HUB_BASE_URL || "";
    const hubUrl = `${baseUrl}/hubs/notifications`;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        transport:
          signalR.HttpTransportType.WebSockets |
          signalR.HttpTransportType.ServerSentEvents |
          signalR.HttpTransportType.LongPolling,
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    eventNames.forEach(eventName => {
      connection.on(eventName, (...args) => {
        if (callbackRef.current) {
          callbackRef.current(...args);
        }
      });
    });

    connection.onreconnected(() => {
      reconnectedRef.current?.();
    });

    connection.start().catch((err) => {
      console.warn("[NotificationHub] Connection failed:", err.message);
    });

    return () => {
      connection.stop();
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [JSON.stringify(eventNames)]);
}
