import React from 'react';
import { GlassCard } from './GlassCard';

const fmt = (n) => (n != null ? Number(n).toLocaleString("en-US") : 0);
const fmtCurrency = (n) =>
  new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(n ?? 0);

const MetricItem = ({ label, value, icon, gradient, currency = false }) => (
  <div className="d-flex align-items-center px-4 py-2">
    <div className={`icon-box ${gradient} shadow-sm me-3 mb-0`} style={{ width: 32, height: 32, fontSize: '1rem' }}>
      <i className={icon}></i>
    </div>
    <div>
      <div className="metric-label" style={{ fontSize: '0.65rem' }}>{label}</div>
      <div className="metric-value" style={{ fontSize: '1.1rem' }}>{currency ? fmtCurrency(value) : fmt(value)}</div>
    </div>
  </div>
);

export const MetricTiles = ({ metrics }) => (
  <GlassCard className="mb-4 overflow-hidden" stagger="stagger-1">
    <div className="d-flex align-items-center flex-wrap divide-x">
      <MetricItem
        label="Active Users"
        value={metrics?.totalUsers}
        icon="bi bi-people"
        gradient="bg-gradient-green"
      />
      <div className="vr d-none d-md-block" style={{ height: '30px', opacity: 0.1 }}></div>
      <MetricItem
        label="Total Products"
        value={metrics?.totalProducts}
        icon="bi bi-bag-check"
        gradient="bg-gradient-yellow"
      />
      <div className="vr d-none d-md-block" style={{ height: '30px', opacity: 0.1 }}></div>
      <MetricItem
        label="Orders Today"
        value={metrics?.totalOrdersToday}
        icon="bi bi-cart-check"
        gradient="bg-gradient-red"
      />
    </div>
  </GlassCard>
);
