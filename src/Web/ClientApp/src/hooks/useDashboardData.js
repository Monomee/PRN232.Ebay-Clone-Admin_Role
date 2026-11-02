import { useState, useEffect, useCallback } from "react";
import { dashboardService } from "../services/dashboardService";
import reportService from "../services/reportService";
import { useAuth } from "../context/AuthContext";

// Local calendar date (yyyy-MM-dd). toISOString() converts to UTC first, which shifts the day back in UTC+7.
function toIso(date) {
  if (!date) return null;
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

function getPresetRange(preset, customStart, customEnd) {
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  switch (preset) {
    case "custom":
      return {
        // "yyyy-MM-dd" alone parses as UTC midnight; add a time so it is read as a local date
        start: customStart ? new Date(`${customStart}T00:00`) : today,
        end: customEnd ? new Date(`${customEnd}T00:00`) : today
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
  const { user } = useAuth();
  const canViewReports = user?.role === "SuperAdmin" || user?.role === "Monitor" || user?.role === "Administrator" || user?.roles?.some((r) => ["SuperAdmin", "Monitor", "Administrator"].includes(r));
  const [metrics, setMetrics] = useState(null);
  const [stats, setStats] = useState({ revenue: null, users: null, orders: null });
  const [loading, setLoading] = useState(true);
  const [preset, setPreset] = useState("month");

  // Custom date state (defaults)
  const todayStr = toIso(new Date());
  const weekAgoStr = toIso(new Date(new Date().setDate(new Date().getDate() - 7)));

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

      const [rev, usr, ord] = canViewReports ? await Promise.allSettled([
        reportService.getRevenue(start, end),
        reportService.getUserGrowth(start, end),
        reportService.getOrderStats(start, end),
      ]) : [{ status: "rejected" }, { status: "rejected" }, { status: "rejected" }];

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
  }, [preset, customStart, customEnd, canViewReports]);

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
