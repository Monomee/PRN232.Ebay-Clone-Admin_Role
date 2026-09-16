import React from "react";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { ToastMessage } from "../components/ToastMessage";
import { useDashboardData } from "../hooks/useDashboardData";

// Modular Components
import { MetricTiles } from "../components/dashboard/MetricTiles";
import { RevenueChart } from "../components/dashboard/RevenueChart";
import { OrderDistribution } from "../components/dashboard/OrderDistribution";
import { ActionCenter } from "../components/dashboard/ActionCenter";
import { DashboardActions } from "../components/dashboard/DashboardActions";
import { PlatformFeeWidget } from "../components/dashboard/PlatformFeeWidget";
import { SellerLevelConfigWidget } from "../components/dashboard/SellerLevelConfigWidget";

// Chart.js registration (Required once in the app, usually better in a central place but keeping it here for dashboard context)
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  BarElement,
  ArcElement,
  Title,
  Tooltip,
  Legend,
  Filler,
} from 'chart.js';

ChartJS.register(
  CategoryScale, LinearScale, PointElement, LineElement,
  BarElement, ArcElement, Title, Tooltip, Legend, Filler
);

export function DashboardPage() {
  const {
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
  } = useDashboardData();

  const formatDateRange = () => {
    if (!currentRange || (!currentRange.start && !currentRange.end)) return "All Time Statistics";
    
    const options = { month: 'short', day: 'numeric', year: 'numeric' };
    const startStr = currentRange.start.toLocaleDateString('en-US', options);
    const endStr = currentRange.end.toLocaleDateString('en-US', options);

    if (startStr === endStr) return startStr;
    return `${startStr} — ${endStr}`;
  };

  if (loading && !metrics) return <LoadingIndicator text="Assembling your workspace..." />;

  return (
    <section className="py-2" id="dashboard-container">
      <ToastMessage
        message={toast.message}
        type={toast.type}
        onClose={() => setToast({ message: "", type: "success" })}
      />

      {/* Header & Date Range Selection */}
      <div className="d-flex justify-content-between align-items-center mb-4 flex-wrap gap-3 p-3">
        <div>
          <div className="d-flex align-items-center gap-3 mb-1">
            <h1 className="h3 fw-bold mb-0">Main Dashboard</h1>
            <DashboardActions 
              metrics={metrics} 
              stats={stats} 
              dashboardId="dashboard-container" 
              dateRange={formatDateRange()} 
            />
          </div>
          <div className="d-flex align-items-center text-secondary small">
            <i className="bi bi-calendar3 me-2"></i>
            <span className="fw-medium">{formatDateRange()}</span>
          </div>
        </div>

        <div className="date-presets-container bg-light p-1 rounded-pill shadow-sm d-flex align-items-center">
          {["today", "week", "month", "quarter", "all", "custom"].map((p) => (
            <button
              key={p}
              onClick={() => setPreset(p)}
              className={`date-preset-btn ${preset === p ? "active" : ""}`}
            >
              {p === 'today' ? 'Today' : p === 'week' ? 'Weekly' : p === 'month' ? 'Monthly' : p === 'quarter' ? 'Quarterly' : p === 'all' ? 'All Time' : 'Custom'}
            </button>
          ))}

          {preset === "custom" && (
            <div className="d-flex align-items-center ms-3 me-2 animate-fade-in" style={{ gap: '8px' }}>
              <input
                type="date"
                className="form-control form-control-sm border-0 bg-white rounded-pill px-3"
                value={customStart}
                onChange={(e) => setCustomStart(e.target.value)}
                style={{ fontSize: '0.75rem', width: '130px', height: '32px' }}
              />
              <span className="text-secondary small">→</span>
              <input
                type="date"
                className="form-control form-control-sm border-0 bg-white rounded-pill px-3"
                value={customEnd}
                onChange={(e) => setCustomEnd(e.target.value)}
                style={{ fontSize: '0.75rem', width: '130px', height: '32px' }}
              />
            </div>
          )}

        </div>
      </div>

        <div className="p-3 bg-light rounded-4">
          {/* KPI Section - Easily extendable by adding more tiles or rows */}
          <MetricTiles metrics={metrics} />

          <div className="d-flex flex-column gap-4">
            {/* Row 1: Revenue Trend & Action Center (Matched Height) */}
            <div className="row g-4">
              <div className="col-lg-8">
                <RevenueChart revenueData={stats.revenue} />
              </div>
              <div className="col-lg-4">
                <ActionCenter metrics={metrics} />
              </div>
            </div>

            {/* Row 2: Secondary Insights & Configurations (Equal Height) */}
            <div className="row g-4">
              <div className="col-lg-4">
                <OrderDistribution orderStats={stats.orders} />
              </div>
              <div className="col-lg-4">
                <PlatformFeeWidget />
              </div>
              <div className="col-lg-4">
                <SellerLevelConfigWidget />
              </div>
            </div>
          </div>
        </div>

      <style>{`
        @keyframes spin { to { transform: rotate(360deg); } }
        .spin { animation: spin 0.8s linear infinite; display: inline-block; }
        .bg-soft-blue { background: rgba(0, 100, 210, 0.1); }
        .bg-soft-red { background: rgba(224, 16, 58, 0.1); }
        .bg-soft-yellow { background: rgba(246, 175, 2, 0.1); }
        .text-blue { color: #0064d2; }
        .text-red { color: #e0103a; }
        .text-yellow { color: #f6af02; }
      `}</style>
    </section>
  );
}
