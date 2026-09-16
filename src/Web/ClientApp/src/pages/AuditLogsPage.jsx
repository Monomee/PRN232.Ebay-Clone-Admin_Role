import React, { useCallback, useEffect, useState } from "react";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { ToastMessage } from "../components/ToastMessage";
import { getAuditLogs } from "../services/auditLogService";

function formatDate(value) {
  if (!value) {
    return "-";
  }

  return new Date(value).toLocaleString();
}

function formatJsonText(text) {
  if (!text) {
    return "-";
  }

  try {
    return JSON.stringify(JSON.parse(text), null, 2);
  } catch {
    return text;
  }
}

export function AuditLogsPage() {
  const [filters, setFilters] = useState({
    adminUsername: "",
    targetType: "",
    action: "",
    fromDate: "",
    toDate: "",
  });
  const [pageNumber, setPageNumber] = useState(1);
  const [logsData, setLogsData] = useState({ items: [], totalCount: 0, totalPages: 0 });
  const [loading, setLoading] = useState(false);
  const [toast, setToast] = useState({ message: "", type: "success" });

  const loadLogs = useCallback(async () => {
    setLoading(true);
    try {
      const result = await getAuditLogs({
        pageNumber,
        pageSize: 20,
        adminUsername: filters.adminUsername,
        targetType: filters.targetType,
        action: filters.action,
        fromDate: filters.fromDate ? new Date(filters.fromDate).toISOString() : undefined,
        toDate: filters.toDate ? new Date(filters.toDate).toISOString() : undefined,
      });
      setLogsData(result);
    } catch (error) {
      setToast({ message: error.message, type: "error" });
    } finally {
      setLoading(false);
    }
  }, [filters.action, filters.adminUsername, filters.fromDate, filters.targetType, filters.toDate, pageNumber]);

  useEffect(() => {
    loadLogs();
  }, [loadLogs]);

  const criticalCount = (logsData.items || []).filter(l => l.action?.toLowerCase().includes('delete') || l.action?.toLowerCase().includes('ban')).length;

  return (
    <div style={{ minHeight: '100vh', background: '#ffffff', fontFamily: "'Inter', sans-serif", padding: '28px 20px' }}>
      <div className="container-fluid" style={{ maxWidth: 1400 }}>
        {/* ── Page Header (Standardized) ── */}
        <div className="text-center mb-5">
          <h1 className="h2 fw-bold text-dark mb-2" style={{ letterSpacing: '-1px' }}>Audit Logs</h1>
          <p className="text-secondary mx-auto mb-0" style={{ maxWidth: '600px', fontSize: '0.95rem' }}>
            Accountability and transparency through comprehensive system activity tracking.
          </p>
        </div>

        <ToastMessage
          message={toast.message}
          type={toast.type}
          onClose={() => setToast({ message: "", type: "success" })}
        />

        {/* ── Quick Stats Grid ── */}
        <div className="row g-3 mb-5 justify-content-center">
          {[
            { label: 'Total Activity', value: logsData.totalCount, icon: 'bi-journal-text', color: 'primary' },
            { label: 'Critical Actions', value: criticalCount > 0 ? criticalCount : 'Monitored', icon: 'bi-shield-lock-fill', color: 'danger' },
            { label: 'Governance Health', value: 'Excellent', icon: 'bi-check-circle-fill', color: 'success' },
            { label: 'Log Retention', value: '30 Days', icon: 'bi-archive-fill', color: 'info' },
          ].map((stat, idx) => (
            <div key={idx} className="col-12 col-sm-6 col-lg-3">
              <div className="bg-white border rounded-4 p-3 shadow-sm d-flex align-items-center gap-3 h-100 transition-all">
                <div className={`p-3 bg-${stat.color} bg-opacity-10 text-${stat.color} rounded-circle`}>
                  <i className={`bi ${stat.icon} h4 mb-0`}></i>
                </div>
                <div>
                  <h6 className="text-secondary mb-1 small fw-bold text-uppercase" style={{ letterSpacing: '0.5px' }}>{stat.label}</h6>
                  <h5 className="mb-0 fw-bold text-dark">{stat.value}</h5>
                </div>
              </div>
            </div>
          ))}
        </div>

        <div className="card border-0 shadow-sm rounded-4 overflow-hidden mb-4">
          <div className="card-body p-0">
            {/* ── Enhanced Filter Toolbar ── */}
            <div className="px-4 py-4 bg-light border-bottom">
              <div className="row g-3">
                <div className="col-md-3">
                  <div className="position-relative">
                    <i className="bi bi-person position-absolute top-50 start-0 translate-middle-y ms-3 text-secondary"></i>
                    <input
                      className="form-control border-0 bg-white rounded-pill ps-5 py-2 shadow-sm"
                      placeholder="Admin Username..."
                      value={filters.adminUsername}
                      onChange={(e) => setFilters(p => ({ ...p, adminUsername: e.target.value }))}
                      style={{ height: '42px', fontSize: '0.85rem' }}
                    />
                  </div>
                </div>
                <div className="col-md-2">
                  <div className="position-relative">
                    <i className="bi bi-box position-absolute top-50 start-0 translate-middle-y ms-3 text-secondary"></i>
                    <input
                      className="form-control border-0 bg-white rounded-pill ps-5 py-2 shadow-sm"
                      placeholder="Entity (Product, User...)"
                      value={filters.targetType}
                      onChange={(e) => setFilters(p => ({ ...p, targetType: e.target.value }))}
                      style={{ height: '42px', fontSize: '0.85rem' }}
                    />
                  </div>
                </div>
                <div className="col-md-2">
                  <div className="position-relative">
                    <i className="bi bi-activity position-absolute top-50 start-0 translate-middle-y ms-3 text-secondary"></i>
                    <input
                      className="form-control border-0 bg-white rounded-pill ps-5 py-2 shadow-sm"
                      placeholder="Action (Ban, Update...)"
                      value={filters.action}
                      onChange={(e) => setFilters(p => ({ ...p, action: e.target.value }))}
                      style={{ height: '42px', fontSize: '0.85rem' }}
                    />
                  </div>
                </div>
                <div className="col-md-5">
                  <div className="d-flex gap-2">
                    <input
                      className="form-control border-0 bg-white rounded-pill px-3 py-2 shadow-sm flex-grow-1"
                      type="date"
                      value={filters.fromDate}
                      onChange={(e) => setFilters(p => ({ ...p, fromDate: e.target.value }))}
                      style={{ height: '42px', fontSize: '0.85rem' }}
                    />
                    <i className="bi bi-arrow-right align-self-center text-muted"></i>
                    <input
                      className="form-control border-0 bg-white rounded-pill px-3 py-2 shadow-sm flex-grow-1"
                      type="date"
                      value={filters.toDate}
                      onChange={(e) => setFilters(p => ({ ...p, toDate: e.target.value }))}
                      style={{ height: '42px', fontSize: '0.85rem' }}
                    />
                    <button className="btn btn-primary rounded-pill px-4 shadow-sm fw-bold" onClick={() => { setPageNumber(1); loadLogs(); }}>
                      Apply
                    </button>
                  </div>
                </div>
              </div>
            </div>

            {loading ? (
              <div className="py-5 text-center">
                <div className="spinner-border text-primary" role="status"></div>
                <p className="mt-2 text-secondary small">Traversing historical records...</p>
              </div>
            ) : (
              <div className="table-responsive">
                <table className="table pe-table mb-0 align-middle">
                  <thead className="bg-primary bg-opacity-10 text-primary fw-bold small text-uppercase">
                    <tr>
                      <th className="ps-4 py-3 border-0">Actor</th>
                      <th className="py-3 border-0">Operation</th>
                      <th className="py-3 border-0">Target Entity</th>
                      <th className="py-3 border-0">Timestamp</th>
                      <th className="py-3 border-0">Previous State</th>
                      <th className="pe-4 py-3 border-0">Resulting State</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(logsData.items || []).length === 0 ? (
                      <tr>
                        <td colSpan="6" className="text-center py-5 text-muted">No audit records match your criteria.</td>
                      </tr>
                    ) : (
                      (logsData.items || []).map((log) => (
                        <tr key={log.id} className="transition-all border-bottom">
                          <td className="ps-4 py-3">
                            <div className="d-flex align-items-center gap-2">
                              <div className="bg-light p-2 rounded-circle">
                                <i className="bi bi-person-badge text-primary"></i>
                              </div>
                              <span className="fw-bold text-dark">{log.user || log.adminUsername || `Admin #${log.adminId}`}</span>
                            </div>
                          </td>
                          <td>
                            <span className={`badge rounded-pill px-3 py-1 ${
                              log.action?.includes('Delete') || log.action?.includes('Ban') ? 'bg-danger-subtle text-danger' :
                              log.action?.includes('Create') ? 'bg-success-subtle text-success' : 'bg-primary-subtle text-primary'
                            }`} style={{ fontSize: '0.7rem', fontWeight: 700 }}>
                              {log.action}
                            </span>
                          </td>
                          <td>
                            <div className="fw-medium text-dark">{log.targetType}</div>
                            {log.targetId && <div className="text-muted x-small">Ref: #{log.targetId}</div>}
                          </td>
                          <td>
                            <div className="text-dark small fw-medium">{formatDate(log.createdAt).split(',')[0]}</div>
                            <div className="text-muted x-small">{formatDate(log.createdAt).split(',')[1]}</div>
                          </td>
                          <td style={{ minWidth: 200 }}>
                            <details className="audit-detail">
                              <summary className="small text-primary cursor-pointer">Changes (Before)</summary>
                              <pre className="bg-dark text-light p-3 rounded-3 mt-2 mb-0 shadow-sm" style={{ fontSize: '0.75rem', maxHeight: '200px', overflowY: 'auto' }}>
                                {formatJsonText(log.beforeData)}
                              </pre>
                            </details>
                          </td>
                          <td className="pe-4" style={{ minWidth: 200 }}>
                            <details className="audit-detail">
                              <summary className="small text-primary cursor-pointer">Changes (After)</summary>
                              <pre className="bg-dark text-light p-3 rounded-3 mt-2 mb-0 shadow-sm" style={{ fontSize: '0.75rem', maxHeight: '200px', overflowY: 'auto' }}>
                                {formatJsonText(log.afterData)}
                              </pre>
                            </details>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            )}

            {/* ── Standard Pagination ── */}
            <div className="px-4 py-3 bg-light border-top d-flex justify-content-between align-items-center">
              <span className="text-muted small">
                Showing <strong className="text-dark">{(pageNumber - 1) * 20 + 1}</strong> - <strong className="text-dark">{Math.min(pageNumber * 20, logsData.totalCount)}</strong> of {logsData.totalCount}
              </span>
              <nav>
                <ul className="pagination pagination-sm mb-0 gap-1">
                  <li className={`page-item ${pageNumber <= 1 ? 'disabled' : ''}`}>
                    <button className="page-link rounded-pill border-0 fw-bold px-3 py-2" onClick={() => setPageNumber(p => p - 1)}>
                      <i className="bi bi-chevron-left"></i>
                    </button>
                  </li>
                  <li className="page-item disabled px-2">
                    <span className="page-link bg-transparent border-0 text-dark fw-bold py-2">
                      {pageNumber} / {logsData.totalPages || 1}
                    </span>
                  </li>
                  <li className={`page-item ${pageNumber >= (logsData.totalPages || 1) ? 'disabled' : ''}`}>
                    <button className="page-link rounded-pill border-0 fw-bold px-3 py-2" onClick={() => setPageNumber(p => p + 1)}>
                      <i className="bi bi-chevron-right"></i>
                    </button>
                  </li>
                </ul>
              </nav>
            </div>
          </div>
        </div>
      </div>
      <style>{`
        .audit-detail summary::-webkit-details-marker { display: none; }
        .audit-detail summary { list-style: none; font-weight: 600; text-decoration: underline; }
        .x-small { font-size: 0.7rem; }
      `}</style>
    </div>
  );
}

