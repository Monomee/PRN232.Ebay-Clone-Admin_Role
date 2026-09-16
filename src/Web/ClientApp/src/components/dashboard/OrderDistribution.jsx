import React from 'react';
import { Doughnut } from 'react-chartjs-2';
import { GlassCard } from './GlassCard';

const fmt = (n) => (n != null ? Number(n).toLocaleString("en-US") : 0);

export const OrderDistribution = ({ orderStats }) => {
  const chartData = {
    labels: ['Completed', 'Delivered', 'Returned'],
    datasets: [{
      data: orderStats ? [orderStats.completed, orderStats.delivered, orderStats.returned] : [],
      backgroundColor: ['#15833b', '#0064d2', '#e0103a'],
      borderWidth: 0,
      hoverOffset: 10,
    }]
  };

  const options = {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '75%',
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: '#111',
        padding: 10,
        callbacks: {
          label: (ctx) => `${ctx.label}: ${fmt(ctx.parsed)}`
        }
      }
    }
  };

  return (
    <GlassCard className="p-4 h-100">
      <h6 className="fw-bold mb-4 text-center">Order Distribution</h6>
      <div style={{ height: 180, position: 'relative' }}>
        <Doughnut data={chartData} options={options} />
        <div style={{
          position: 'absolute',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          pointerEvents: 'none'
        }}>
          <div className="h4 fw-bold mb-0">{fmt(orderStats?.total)}</div>
          <div className="text-secondary" style={{ fontSize: 9 }}>TOTAL</div>
        </div>
      </div>
      <div className="mt-4 d-flex flex-wrap gap-2 justify-content-center">
        {chartData.labels.map((l, i) => (
          <div key={i} className="d-flex align-items-center small bg-light px-2 py-1 rounded-pill">
            <span className="rounded-circle me-1" style={{ width: 8, height: 8, background: chartData.datasets[0].backgroundColor[i] }}></span>
            <span className="text-secondary" style={{ fontSize: 10 }}>{l}: <strong>{fmt(chartData.datasets[0].data[i])}</strong></span>
          </div>
        ))}
      </div>
    </GlassCard>
  );
};
