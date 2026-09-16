import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { ToastMessage } from "../components/ToastMessage";
import { disputeService } from "../services/disputeService";
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  BarElement,
  Title,
  Tooltip,
  Legend,
  ArcElement,
  PointElement,
  LineElement,
} from 'chart.js';
import { Bar, Doughnut } from 'react-chartjs-2';

ChartJS.register(
  CategoryScale,
  LinearScale,
  BarElement,
  Title,
  Tooltip,
  Legend,
  ArcElement,
  PointElement,
  LineElement
);

export function DisputeDashboard() {
  const [statistics, setStatistics] = useState(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState({ message: "", type: "success" });

  useEffect(() => {
    loadStatistics();
  }, []);

  const loadStatistics = async () => {
    setLoading(true);
    try {
      const stats = await disputeService.getStatistics();
      setStatistics(stats);
    } catch (error) {
      setToast({ 
        message: error.message || "Failed to load statistics", 
        type: "error" 
      });
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <div style={{ minHeight: '100vh', background: '#ffffff', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <LoadingIndicator text="Synthesizing dispute intelligence..." />
      </div>
    );
  }

  // ── Chart Configurations ──
  
  const outcomeData = {
    labels: ['Buyer Wins', 'Seller Wins', 'Split Decisions'],
    datasets: [{
      data: [statistics?.buyerWins || 0, statistics?.sellerWins || 0, statistics?.splitDecisions || 0],
      backgroundColor: ['#0d6efd', '#ffc107', '#0dcaf0'],
      borderWidth: 0,
      hoverOffset: 4
    }]
  };

  const caseTypeData = {
    labels: ['INR', 'INAD', 'Damaged', 'Counterfeit'],
    datasets: [{
      label: 'Active Cases',
      data: [statistics?.inrCases || 0, statistics?.inadCases || 0, statistics?.damagedCases || 0, statistics?.counterfeitCases || 0],
      backgroundColor: 'rgba(13, 110, 253, 0.8)',
      borderRadius: 10,
      borderWidth: 0,
    }]
  };

  const financialData = {
    labels: ['Refunded', 'Remaining Disputed'],
    datasets: [{
      data: [statistics?.totalRefundedAmount || 0, (statistics?.totalDisputedAmount || 0) - (statistics?.totalRefundedAmount || 0)],
      backgroundColor: ['#198754', 'rgba(13, 110, 253, 0.1)'],
      borderWidth: 0,
    }]
  };

  const commonOptions = {
    plugins: {
      legend: { position: 'bottom', labels: { usePointStyle: true, font: { family: "'Inter', sans-serif", size: 11 } } }
    },
    responsive: true,
    maintainAspectRatio: false,
  };

  return (
    <div style={{ minHeight: '100vh', background: '#ffffff', fontFamily: "'Inter', sans-serif", padding: '28px 20px' }}>
      <div className="container-fluid" style={{ maxWidth: 1400 }}>
        
        {/* ── Page Header (Standardized) ── */}
        <div className="text-center mb-5">
          <div className="d-flex justify-content-center align-items-center gap-2 mb-2">
            <h1 className="h2 fw-bold text-dark mb-0" style={{ letterSpacing: '-1px' }}>Dispute Analytics</h1>
          </div>
          <p className="text-secondary mx-auto mb-4" style={{ maxWidth: '600px', fontSize: '0.95rem' }}>
            A high-fidelity overview of marketplace conflicts, resolution efficiency, and financial risk assessment.
          </p>
          <div className="d-flex justify-content-center gap-2">
            <button className="btn btn-sm btn-light rounded-pill px-3 fw-bold shadow-sm" onClick={loadStatistics} disabled={loading}>
              <i className="bi bi-arrow-clockwise me-2"></i>Refresh Dossier
            </button>
            <Link to="/disputes" className="btn btn-sm btn-primary rounded-pill px-4 fw-bold shadow-sm">
              <i className="bi bi-list-task me-2"></i>Case Management
            </Link>
          </div>
        </div>

        <ToastMessage
          message={toast.message}
          type={toast.type}
          onClose={() => setToast({ message: "", type: "success" })}
        />

        {statistics && (
          <div className="row g-4">
            {/* ── Top Metric Cards ── */}
            <div className="col-lg-3">
              <div className="card border-0 shadow-sm rounded-4 p-3 text-center bg-primary bg-opacity-10 h-100">
                <div className="text-primary small fw-bold text-uppercase mb-1">Open Cases</div>
                <div className="h2 fw-bold text-primary mb-0">{statistics.totalOpen}</div>
              </div>
            </div>
            <div className="col-lg-3">
              <div className="card border-0 shadow-sm rounded-4 p-3 text-center bg-danger bg-opacity-10 h-100">
                <div className="text-danger small fw-bold text-uppercase mb-1">Escalated Cases</div>
                <div className="h2 fw-bold text-danger mb-0">{statistics.totalEscalated}</div>
              </div>
            </div>
            <div className="col-lg-3">
              <div className="card border-0 shadow-sm rounded-4 p-3 text-center bg-warning bg-opacity-10 h-100">
                <div className="text-warning small fw-bold text-uppercase mb-1">Urgent Alerts</div>
                <div className="h2 fw-bold text-warning mb-0">{statistics.urgentCases}</div>
              </div>
            </div>
            <div className="col-lg-3">
              <div className="card border-0 shadow-sm rounded-4 p-3 text-center bg-success bg-opacity-10 h-100">
                <div className="text-success small fw-bold text-uppercase mb-1">Decided (30d)</div>
                <div className="h2 fw-bold text-success mb-0">{statistics.totalResolved}</div>
              </div>
            </div>

            {/* ── Main Charts Row ── */}
            <div className="col-lg-8">
              <div className="card border-0 shadow-sm rounded-4 overflow-hidden h-100">
                <div className="card-header bg-white border-bottom py-3 px-4 d-flex justify-content-between align-items-center">
                  <h6 className="mb-0 fw-bold text-dark text-uppercase small" style={{ letterSpacing: '1px' }}>Active Case Types Distribution</h6>
                  <span className="badge bg-light text-muted border fw-bold small">REAL-TIME DATA</span>
                </div>
                <div className="card-body p-4" style={{ height: '350px' }}>
                  <Bar 
                    data={caseTypeData} 
                    options={{
                      ...commonOptions,
                      indexAxis: 'y',
                      plugins: { ...commonOptions.plugins, legend: { display: false } },
                      scales: { x: { grid: { display: false } }, y: { grid: { display: false } } }
                    }} 
                  />
                </div>
              </div>
            </div>

            <div className="col-lg-4">
              <div className="card border-0 shadow-sm rounded-4 overflow-hidden h-100">
                <div className="card-header bg-white border-bottom py-3 px-4">
                  <h6 className="mb-0 fw-bold text-dark text-uppercase small" style={{ letterSpacing: '1px' }}>Resolution Outcomes</h6>
                </div>
                <div className="card-body p-4 text-center d-flex flex-column align-items-center justify-content-center" style={{ height: '350px' }}>
                  <div style={{ width: '100%', height: '250px' }}>
                    <Doughnut data={outcomeData} options={{ ...commonOptions, cutout: '70%' }} />
                  </div>
                  <div className="mt-3">
                    <span className="text-secondary small fw-bold">WIN RATE: </span>
                    <span className="text-primary h5 fw-bold mb-0">{statistics.buyerWinRate?.toFixed(1)}%</span>
                    <span className="text-muted small"> (BUYER)</span>
                  </div>
                </div>
              </div>
            </div>

            {/* ── Secondary Charts Row ── */}
            <div className="col-lg-4">
              <div className="card border-0 shadow-sm rounded-4 overflow-hidden h-100">
                <div className="card-header bg-white border-bottom py-3 px-4">
                  <h6 className="mb-0 fw-bold text-dark text-uppercase small" style={{ letterSpacing: '1px' }}>Financial Exposure</h6>
                </div>
                <div className="card-body p-4" style={{ height: '280px' }}>
                  <div className="d-flex justify-content-between mb-4">
                    <div className="text-start">
                      <div className="text-muted small fw-bold">TOTAL DISPUTED</div>
                      <div className="h4 fw-bold text-dark mb-0">${statistics.totalDisputedAmount?.toLocaleString()}</div>
                    </div>
                    <div className="text-end">
                      <div className="text-muted small fw-bold text-success">TOTAL REFUNDED</div>
                      <div className="h4 fw-bold text-success mb-0">${statistics.totalRefundedAmount?.toLocaleString()}</div>
                    </div>
                  </div>
                  <div style={{ height: '140px' }}>
                    <Doughnut data={financialData} options={{ ...commonOptions, plugins: { legend: { display: false } }, cutout: '85%' }} />
                  </div>
                </div>
              </div>
            </div>

            <div className="col-lg-4">
              <div className="card border-0 shadow-sm rounded-4 h-100">
                <div className="card-header bg-white border-bottom py-3 px-4">
                  <h6 className="mb-0 fw-bold text-dark text-uppercase small" style={{ letterSpacing: '1px' }}>Efficiency Metrics</h6>
                </div>
                <div className="card-body p-4">
                  <div className="mb-4">
                    <div className="d-flex justify-content-between mb-2">
                      <span className="text-secondary small fw-bold">AVG RESOLUTION TIME</span>
                      <span className="text-primary fw-bold">{statistics.averageResolutionTimeHours?.toFixed(1)}h</span>
                    </div>
                    <div className="progress rounded-pill overflow-hidden" style={{ height: '6px' }}>
                      <div className="progress-bar bg-primary" style={{ width: '85%' }}></div>
                    </div>
                  </div>
                  <div className="mb-0">
                    <div className="d-flex justify-content-between mb-2">
                      <span className="text-secondary small fw-bold">AVG INITIAL RESPONSE</span>
                      <span className="text-info fw-bold">{statistics.averageResponseTimeHours?.toFixed(1)}h</span>
                    </div>
                    <div className="progress rounded-pill overflow-hidden" style={{ height: '6px' }}>
                      <div className="progress-bar bg-info" style={{ width: '60%' }}></div>
                    </div>
                  </div>
                  <hr className="my-4 opacity-10" />
                  <div className="d-flex gap-2 justify-content-center">
                    <div className="badge bg-light text-dark border p-2 flex-grow-1">
                      <div className="xx-small text-muted mb-1 text-uppercase fw-bold">Critical cases</div>
                      <div className="h6 fw-bold mb-0 text-danger">{statistics.criticalPriority}</div>
                    </div>
                    <div className="badge bg-light text-dark border p-2 flex-grow-1">
                      <div className="xx-small text-muted mb-1 text-uppercase fw-bold">High priority</div>
                      <div className="h6 fw-bold mb-0 text-warning">{statistics.highPriority}</div>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <div className="col-lg-4">
              <div className="card border-0 shadow-sm rounded-4 h-100">
                <div className="card-header bg-white border-bottom py-3 px-4">
                  <h6 className="mb-0 fw-bold text-dark text-uppercase small" style={{ letterSpacing: '1px' }}>Tactical Actions</h6>
                </div>
                <div className="card-body p-4 d-flex flex-column gap-2">
                  <Link to="/disputes?status=Open&priority=Critical" className="btn btn-outline-danger btn-sm rounded-pill fw-bold py-2 w-100 text-start px-3 shadow-none border-opacity-25 transition-all">
                    <i className="bi bi-fire me-2"></i>Critical Enforcement
                  </Link>
                  <Link to="/disputes?onlyMyDisputes=true" className="btn btn-outline-primary btn-sm rounded-pill fw-bold py-2 w-100 text-start px-3 shadow-none border-opacity-25 transition-all">
                    <i className="bi bi-person-check me-2"></i>My Active Payload
                  </Link>
                  <Link to="/disputes?onlyUrgent=true" className="btn btn-outline-warning btn-sm rounded-pill fw-bold py-2 w-100 text-start px-3 shadow-none border-opacity-25 transition-all">
                    <i className="bi bi-alarm me-2"></i>Urgent Intervention
                  </Link>
                  <Link to="/disputes?status=Escalated" className="btn btn-outline-secondary btn-sm rounded-pill fw-bold py-2 w-100 text-start px-3 shadow-none border-opacity-25 transition-all">
                    <i className="bi bi-arrow-up-right-circle me-2"></i>Escalated Dossiers
                  </Link>
                </div>
              </div>
            </div>

          </div>
        )}
      </div>
    </div>
  );
}