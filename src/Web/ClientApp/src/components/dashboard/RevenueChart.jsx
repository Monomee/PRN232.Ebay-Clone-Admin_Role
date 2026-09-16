import React from 'react';
import { Line } from 'react-chartjs-2';
import { GlassCard } from './GlassCard';

const fmt = (n) => (n != null ? Number(n).toLocaleString("en-US") : 0);
const fmtCurrency = (n) =>
  new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(n ?? 0);

export const RevenueChart = ({ revenueData }) => {
  const chartData = {
    labels: revenueData?.dailyRevenue?.map(d => new Date(d.date).toLocaleDateString("en-US", { day: '2-digit', month: 'short' })) || [],
    datasets: [{
      label: 'Revenue',
      data: revenueData?.dailyRevenue?.map(d => d.amount ?? d.revenue ?? 0) || [],
      borderColor: '#0064d2',
      backgroundColor: 'rgba(0, 100, 210, 0.1)',
      fill: true,
      tension: 0.4,
      pointRadius: 4,
      pointBackgroundColor: '#fff',
      pointBorderWidth: 2,
      pointHoverRadius: 6,
    }]
  };

  const options = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: '#111',
        padding: 10,
        callbacks: {
          label: (context) => fmtCurrency(context.parsed.y)
        }
      }
    },
    scales: {
      x: { grid: { display: false }, ticks: { font: { size: 10 }, color: '#888' } },
      y: {
        beginAtZero: true,
        grid: { color: 'rgba(0,0,0,0.02)', drawBorder: false },
        ticks: { font: { size: 10 }, color: '#888', callback: (v) => '$' + fmt(v) }
      }
    }
  };

  return (
    <GlassCard className="p-4 h-100">
      <div className="d-flex justify-content-between align-items-center mb-4">
        <div>
          <h5 className="fw-bold mb-0">Revenue Trend</h5>
          <small className="text-secondary">Historical analysis for the selected period</small>
        </div>
        <div className="text-end">
          <div className="h4 fw-bold mb-0 text-primary">{fmtCurrency(revenueData?.totalRevenue)}</div>
          <small className="text-secondary">Total for current range</small>
        </div>
      </div>
      <div className="py-2" style={{ height: 320 }}>
        <Line data={chartData} options={options} />
      </div>
    </GlassCard>
  );
};
