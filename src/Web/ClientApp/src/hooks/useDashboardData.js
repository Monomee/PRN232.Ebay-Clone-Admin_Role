import { useState, useEffect, useCallback } from "react";
import { dashboardService } from "../services/dashboardService";
import reportService from "../services/reportService";

function toIso(date) {
  if (!date) return null;
  return date.toISOString().split("T")[0];
}

function getPresetRange(preset, customStart, customEnd) {
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  switch (preset) {
    case "custom":
      return {
        start: customStart ? new Date(customStart) : today,
        end: customEnd ? new Date(customEnd) : today
      };
    case "all":
      return { start: null, end: null };
    case "today":
      return { start: today, end: today };
    case "week": {
      const mon = new Date(today);
      mon.setDate(today.getDate() - today.getDay() + 1);
      return { start: mon, end: today };
    }
    case "month":
      return { start: new Date(today.getFullYear(), today.getMonth(), 1), end: today };
    case "quarter": {
      const q = Math.floor(today.getMonth() / 3);
      return { start: new Date(today.getFullYear(), q * 3, 1), end: today };
    }
    default:
      return { start: new Date(today.getFullYear(), today.getMonth(), 1), end: today };
  }
}

export function useDashboardData() {
  const [metrics, setMetrics] = useState(null);
  const [stats, setStats] = useState({ revenue: null, users: null, orders: null });
  const [loading, setLoading] = useState(true);
  const [preset, setPreset] = useState("month");

  // Custom date state (defaults)
  const todayStr = new Date().toISOString().split('T')[0];
  const weekAgoStr = new Date(new Date().setDate(new Date().getDate() - 7)).toISOString().split('T')[0];

  const [customStart, setCustomStart] = useState(weekAgoStr);
  const [customEnd, setCustomEnd] = useState(todayStr);

  const [toast, setToast] = useState({ message: "", type: "success" });

  const [currentRange, setCurrentRange] = useState({ start: null, end: null });

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const range = getPresetRange(preset, customStart, customEnd);
      setCurrentRange(range);
      const start = toIso(range.start);
      const end = toIso(range.end);

      const currentMetrics = await dashboardService.getMetrics(start, end);
      setMetrics(currentMetrics);

      const [rev, usr, ord] = await Promise.allSettled([
        reportService.getRevenue(start, end),
        reportService.getUserGrowth(start, end),
        reportService.getOrderStats(start, end),
      ]);

      setStats({
        revenue: rev.status === "fulfilled" ? rev.value : null,
        users: usr.status === "fulfilled" ? usr.value : null,
        orders: ord.status === "fulfilled" ? ord.value : null,
      });

    } catch (error) {
           setToast({ message: "Failed to load dashboard data.", type: "error" });
    } finally {
      setLoading(false);
    }
  }, [preset, customStart, customEnd]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  return {
    metrics,
    stats,
    loading,
    preset,
    setPreset,
    customStart,
    setCustomStart,
    customEnd,
    setCustomEnd,
    fetchData,
    toast,
    setToast,
    currentRange
  };
}
