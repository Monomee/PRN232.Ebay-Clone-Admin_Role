import React, { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { ToastMessage } from "../components/ToastMessage";
import { getCurrentAdminId } from "../services/adminSession";
import { disputeService } from "../services/disputeService";
import { useDisputeHub } from "../hooks/useDisputeHub";

const statusTabs = [
  { label: "All", value: "" },
  { label: "Open", value: "Open" },
  { label: "Escalated", value: "Escalated" },
  { label: "Under Review", value: "UnderReview" },
  { label: "Assigned to Admin", value: "AssignedToAdmin" },
  { label: "Resolved", value: "Resolved" },
];

const priorityOptions = [
  { label: "All Priorities", value: "" },
  { label: "Critical", value: "Critical" },
  { label: "High", value: "High" },
  { label: "Medium", value: "Medium" },
  { label: "Low", value: "Low" },
];

const typeOptions = [
  { label: "All Types", value: "" },
  { label: "Item Not Received (INR)", value: "INR" },
  { label: "Item Not As Described (INAD)", value: "INAD" },
  { label: "Damaged", value: "Damaged" },
  { label: "Counterfeit", value: "Counterfeit" },
  { label: "Other", value: "Other" },
];

const sortOptions = [
  { label: "Deadline", value: "Deadline" },
  { label: "Amount", value: "Amount" },
  { label: "Priority", value: "Priority" },
  { label: "Created Date", value: "CreatedAt" },
];

export function DisputesPage() {
  const [status, setStatus] = useState("");
  const [priority, setPriority] = useState("");
  const [type, setType] = useState("");
  const [onlyMyDisputes, setOnlyMyDisputes] = useState(false);
  const [onlyUrgent, setOnlyUrgent] = useState(false);
  const [searchTerm, setSearchTerm] = useState("");
  const [sortBy, setSortBy] = useState("Deadline");
  const [descending, setDescending] = useState(false);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize] = useState(15);
  const [disputesData, setDisputesData] = useState({
    items: [],
    totalCount: 0,
    totalPages: 0,
    hasNextPage: false,
    hasPreviousPage: false
  });
  const [loading, setLoading] = useState(false);
  const [toast, setToast] = useState({ message: "", type: "success" });

  const adminId = useMemo(() => getCurrentAdminId(), []);

  const loadDisputes = useCallback(async () => {
    setLoading(true);
    try {
      const params = {
        pageNumber,
        pageSize,
        sortBy,
        descending
      };

      // Add filters only if they have values
      if (status) params.status = status;
      if (priority) params.priority = priority;
      if (type) params.type = type;
      if (onlyMyDisputes) params.onlyMyDisputes = true;
      if (onlyUrgent) params.onlyUrgent = true;
      if (searchTerm.trim()) params.searchTerm = searchTerm.trim();

      const result = await disputeService.getDisputes(params);
      setDisputesData(result);
    } catch (error) {
      setToast({ message: error.message || "Failed to load disputes", type: "error" });
    } finally {
      setLoading(false);
    }
  }, [pageNumber, pageSize, status, priority, type, onlyMyDisputes, onlyUrgent, searchTerm, sortBy, descending]);

  useEffect(() => {
    loadDisputes();
  }, [loadDisputes]);

  // ── Real-time: nhận event khi bất kỳ admin nào resolve dispute ──
  // Redis backplane đảm bảo nhận được ngay cả khi admin đó kết nối vào pod khác
  const handleDisputeResolved = useCallback((data) => {
    setToast({
      message: `⚖️ Dispute ${data.caseId} resolved! Winner: ${data.winner}`,
      type: "success"
    });
    // Tự động refresh danh sách để phản ánh trạng thái mới nhất
    loadDisputes();
  }, [loadDisputes]);

  useDisputeHub(handleDisputeResolved);

  const handleAssignDispute = async (disputeId) => {
    try {
      await disputeService.assignDispute(disputeId);
      setToast({ message: "Dispute assigned successfully!", type: "success" });
      await loadDisputes(); // Refresh list
    } catch (error) {
      setToast({ message: error.response?.data || "Failed to assign dispute", type: "error" });
    }
  };

  const resetFilters = () => {
    setStatus("");
    setPriority("");
    setType("");
    setOnlyMyDisputes(false);
    setOnlyUrgent(false);
    setSearchTerm("");
    setSortBy("Deadline");
    setDescending(false);
    setPageNumber(1);
  };

  const applyFilters = () => {
    setPageNumber(1); // Reset to first page when filtering
  };

  const getPriorityBadge = (priority) => {
    const colors = {
      Critical: "#ef4444",
      High: "#f59e0b",
      Medium: "#3b82f6",
      Low: "#64748b"
    };
    const color = colors[priority] || "#64748b";
    return (
      <span className="d-inline-flex align-items-center gap-2 px-2 py-1 rounded-pill bg-light border" style={{ fontSize: '0.65rem', fontWeight: 700, letterSpacing: '0.3px' }}>
        <span style={{ width: 6, height: 6, borderRadius: '50%', background: color }}></span>
        <span style={{ color: '#1e293b' }}>{priority?.toUpperCase()}</span>
      </span>
    );
  };

  const getStatusBadge = (status) => {
    const colors = {
      Open: "#3b82f6",
      Escalated: "#ef4444",
      UnderReview: "#8b5cf6",
      AssignedToAdmin: "#10b981",
      Resolved: "#10b981",
      Closed: "#64748b"
    };
    const color = colors[status] || "#64748b";
    return (
      <span className="badge rounded-pill" style={{ background: `${color}15`, color: color, border: `1px solid ${color}30`, fontSize: '0.65rem', fontWeight: 700 }}>
        {status?.toUpperCase()}
      </span>
    );
  };

  const formatDeadline = (deadline) => {
    if (!deadline) return "-";

    const deadlineDate = new Date(deadline);
    const now = new Date();
    const timeDiff = deadlineDate.getTime() - now.getTime();
    const hoursLeft = Math.floor(timeDiff / (1000 * 3600));

    if (hoursLeft < 0) {
      return <span className="fw-bold text-danger"><i className="bi bi-clock-fill me-1"></i>Overdue</span>;
    } else if (hoursLeft < 24) {
      return <span className="fw-bold text-warning"><i className="bi bi-hourglass-split me-1"></i>{hoursLeft}h left</span>;
    } else {
      const daysLeft = Math.floor(hoursLeft / 24);
      return <span className="text-secondary fw-medium">{daysLeft}d left</span>;
    }
  };

  const unassignedCount = (disputesData.items || []).filter(d => !d.assignedTo).length;

  return (
    <div style={{ minHeight: '100vh', background: '#ffffff', fontFamily: "'Inter', sans-serif", padding: '28px 20px' }}>
      <div className="container-fluid" style={{ maxWidth: 1400 }}>
        {/* ── Page Header (Standardized) ── */}
        <div className="text-center mb-5 position-relative">
          <h1 className="h2 fw-bold text-dark mb-2" style={{ letterSpacing: '-1px' }}>Disputes Management</h1>
          <p className="text-secondary mx-auto mb-0" style={{ maxWidth: '600px', fontSize: '0.95rem' }}>
            Ensure buyer and seller protection by adjudicating conflicts with objectivity and speed.
          </p>
          <div className="position-absolute top-50 end-0 translate-middle-y d-none d-lg-block">
            <Link to="/disputes/dashboard" className="btn btn-sm btn-outline-primary rounded-pill px-4 py-2 fw-bold shadow-sm transition-all">
              <i className="bi bi-graph-up-arrow me-2"></i>Dashboard
            </Link>
          </div>
        </div>

        <ToastMessage
          message={toast.message}
          type={toast.type}
          onClose={() => setToast({ message: "", type: "success" })}
        />

        {/* ── Quick Stats Grid ── */}
        <div className="row g-3 mb-5 justify-content-center">
          {[
            { label: 'Total Cases', value: disputesData.totalCount, icon: 'bi-briefcase-fill', color: 'primary' },
            { label: 'Urgent Action', value: onlyUrgent ? disputesData.items.length : '...', icon: 'bi-lightning-charge-fill', color: 'warning' },
            { label: 'Unassigned', value: unassignedCount, icon: 'bi-person-plus-fill', color: 'danger' },
            { label: 'My Portfolio', value: onlyMyDisputes ? disputesData.items.length : '...', icon: 'bi-shield-shaded', color: 'success' },
          ].map((stat, idx) => (
            <div key={idx} className="col-12 col-sm-6 col-lg-3">
              <div className="bg-white border rounded-4 p-3 shadow-sm d-flex align-items-center gap-3 h-100 transition-all">
                <div className={`p-3 bg-${stat.color} bg-opacity-10 text-${stat.color} rounded-3`}>
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
            {/* ── Enhanced Toolbar ── */}
            <div className="px-4 py-3 bg-light border-bottom">
              <div className="d-flex flex-column gap-3">
                {/* Row 1: Status Tabs & Basic Checks */}
                <div className="d-flex flex-wrap align-items-center justify-content-between gap-3">
                  <div className="d-flex flex-wrap gap-1 p-1 bg-white border rounded-pill shadow-sm">
                    {statusTabs.map((tab) => (
                      <button
                        key={tab.value}
                        onClick={() => { setStatus(tab.value); setPageNumber(1); }}
                        className={`btn btn-sm rounded-pill px-3 py-1 fw-bold transition-all ${status === tab.value ? 'btn-primary shadow-sm' : 'btn-link text-secondary text-decoration-none'}`}
                        style={{ fontSize: '0.75rem' }}
                      >
                        {tab.label}
                      </button>
                    ))}
                  </div>

                  <div className="d-flex gap-3">
                    <div className="form-check form-switch mt-1">
                      <input className="form-check-input ms-0" type="checkbox" id="onlyMyDisputes" checked={onlyMyDisputes} onChange={(e) => setOnlyMyDisputes(e.target.checked)} />
                      <label className="form-check-label small fw-bold text-secondary ms-2" htmlFor="onlyMyDisputes">My Disputes</label>
                    </div>
                    <div className="form-check form-switch mt-1 text-danger">
                      <input className="form-check-input ms-0" type="checkbox" id="onlyUrgent" checked={onlyUrgent} onChange={(e) => setOnlyUrgent(e.target.checked)} />
                      <label className="form-check-label small fw-bold ms-2" htmlFor="onlyUrgent">Urgent Only</label>
                    </div>
                  </div>
                </div>

                {/* Row 2: Detailed Filters & Search */}
                <div className="row g-2">
                  <div className="col-lg-7">
                    <div className="row g-2">
                      <div className="col-md-4">
                        <select className="form-select form-select-sm rounded-pill border-0 shadow-sm px-3" value={priority} onChange={(e) => setPriority(e.target.value)} style={{ height: '38px', fontSize: '0.85rem' }}>
                          {priorityOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
                        </select>
                      </div>
                      <div className="col-md-4">
                        <select className="form-select form-select-sm rounded-pill border-0 shadow-sm px-3" value={type} onChange={(e) => setType(e.target.value)} style={{ height: '38px', fontSize: '0.85rem' }}>
                          {typeOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
                        </select>
                      </div>
                      <div className="col-md-4">
                        <div className="d-flex gap-2">
                          <select className="form-select form-select-sm rounded-pill border-0 shadow-sm px-3" value={sortBy} onChange={(e) => setSortBy(e.target.value)} style={{ height: '38px', fontSize: '0.85rem' }}>
                            {sortOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
                          </select>
                          <button className="btn btn-sm btn-light rounded-circle shadow-sm" onClick={() => setDescending(!descending)} title={descending ? "Descending" : "Ascending"} style={{ width: '38px', height: '38px' }}>
                            <i className={`bi bi-sort-${descending ? 'down' : 'up'} text-primary`}></i>
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                  <div className="col-lg-5">
                    <div className="d-flex gap-2">
                      <div className="position-relative flex-grow-1">
                        <i className="bi bi-search position-absolute top-50 start-0 translate-middle-y ms-3 text-secondary" style={{ fontSize: '0.85rem' }}></i>
                        <input
                          type="text"
                          className="form-control border-0 bg-white rounded-pill ps-5 py-2 shadow-sm"
                          placeholder="Search Case ID, Buyer, Content..."
                          value={searchTerm}
                          onChange={(e) => setSearchTerm(e.target.value)}
                          style={{ height: '38px', fontSize: '0.85rem' }}
                        />
                      </div>
                      <button className="btn btn-primary rounded-pill px-3 shadow-sm" onClick={resetFilters} title="Reset Filters">
                        <i className="bi bi-arrow-counterclockwise"></i>
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {loading ? (
              <div className="py-5 text-center">
                <div className="spinner-border text-primary" role="status">
                  <span className="visually-hidden">Loading...</span>
                </div>
                <p className="mt-2 text-secondary small">Synchronizing disputes registry...</p>
              </div>
            ) : (
              <div className="table-responsive">
                <table className="table pe-table mb-0 align-middle">
                  <thead className="bg-primary bg-opacity-10 text-primary fw-bold small text-uppercase">
                    <tr>
                      <th className="ps-4 py-3 border-0">Case ID</th>
                      <th className="py-3 border-0">Details</th>
                      <th className="py-3 border-0" style={{ width: '120px' }}>Priority</th>
                      <th className="py-3 border-0" style={{ width: '120px' }}>Deadline</th>
                      <th className="py-3 border-0">Financials</th>
                      <th className="py-3 border-0">Assignment</th>
                      <th className="pe-4 py-3 border-0 text-end">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(disputesData.items || []).length === 0 ? (
                      <tr>
                        <td colSpan="7" className="text-center py-5 text-muted">
                          <i className="bi bi-shield-slash h1 d-block mb-3 opacity-25"></i>
                          No active disputes match your current filters.
                        </td>
                      </tr>
                    ) : (
                      (disputesData.items || []).map((dispute) => (
                        <tr key={dispute.id} className="transition-all">
                          <td className="ps-4 py-3">
                            <Link to={`/disputes/${dispute.id}`} className="fw-bold text-primary text-decoration-none">
                              {dispute.caseId}
                            </Link>
                            <div className="mt-1">{getStatusBadge(dispute.status)}</div>
                          </td>
                          <td>
                            <div className="d-flex align-items-center gap-2">
                              <div className="bg-primary bg-opacity-10 text-primary rounded-circle d-flex align-items-center justify-content-center fw-bold shadow-sm" style={{ width: 32, height: 32, fontSize: '0.8rem' }}>
                                {dispute.buyerUsername?.charAt(0) || 'U'}
                              </div>
                              <div>
                                <div className="fw-bold text-dark small">{dispute.buyerUsername}</div>
                                <div className="text-muted text-truncate" style={{ fontSize: '0.7rem', maxWidth: '150px' }}>{dispute.productTitle || dispute.type}</div>
                              </div>
                            </div>
                          </td>
                          <td>{getPriorityBadge(dispute.priority)}</td>
                          <td>{formatDeadline(dispute.deadline)}</td>
                          <td>
                            <div className="fw-bold text-dark">{dispute.amount ? `$${dispute.amount.toFixed(2)}` : "-"}</div>
                            <div className="text-secondary small" style={{ fontSize: '0.7rem' }}>Platform Dispute</div>
                          </td>
                          <td>
                            {dispute.assignedTo ? (
                              <div className="d-flex align-items-center gap-1">
                                <span className="badge bg-success-subtle text-success border border-success-subtle fw-bold" style={{ fontSize: '0.65rem' }}>
                                  <i className="bi bi-person-check-fill me-1"></i>ADMIN #{dispute.assignedTo}
                                </span>
                              </div>
                            ) : (
                              <span className="text-muted small italic">Unassigned</span>
                            )}
                          </td>
                          <td className="pe-4 text-end">
                            <div className="d-flex justify-content-end gap-1">
                              {!dispute.assignedTo && dispute.status !== "Resolved" && dispute.status !== "Closed" && (
                                <button
                                  type="button"
                                  className="btn btn-sm btn-primary rounded-pill px-3 shadow-sm fw-bold"
                                  onClick={() => handleAssignDispute(dispute.id)}
                                  style={{ fontSize: '0.7rem' }}
                                >
                                  Take
                                </button>
                              )}
                              {dispute.assignedTo && dispute.assignedTo === adminId && dispute.status !== "Resolved" && (
                                <Link to={`/disputes/${dispute.id}`} className="btn btn-sm btn-success rounded-pill px-3 shadow-sm fw-bold" style={{ fontSize: '0.7rem' }}>
                                  Resolve
                                </Link>
                              )}
                              <Link to={`/disputes/${dispute.id}`} className="btn btn-sm btn-outline-secondary rounded-pill px-3 fw-bold" style={{ fontSize: '0.7rem' }}>
                                View
                              </Link>
                            </div>
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
                Showing <strong>{disputesData.items?.length || 0}</strong> of {disputesData.totalCount || 0}
              </span>
              <nav>
                <ul className="pagination pagination-sm mb-0 gap-1">
                  <li className={`page-item ${!disputesData.hasPreviousPage ? 'disabled' : ''}`}>
                    <button className="page-link rounded-pill border-0 fw-bold px-3 py-2" onClick={() => setPageNumber(prev => prev - 1)}>
                      <i className="bi bi-chevron-left"></i>
                    </button>
                  </li>
                  <li className="page-item disabled">
                    <span className="page-link bg-transparent border-0 text-dark fw-bold px-3 py-2">
                       {pageNumber} / {disputesData.totalPages || 1}
                    </span>
                  </li>
                  <li className={`page-item ${!disputesData.hasNextPage ? 'disabled' : ''}`}>
                    <button className="page-link rounded-pill border-0 fw-bold px-3 py-2" onClick={() => setPageNumber(prev => prev + 1)}>
                      <i className="bi bi-chevron-right"></i>
                    </button>
                  </li>
                </ul>
              </nav>
            </div>
          </div>
        </div>

        <div className="text-end text-muted small mt-2 opacity-50">
          Admin Session Active: <span className="fw-bold">#{adminId}</span> | System Integrity: Nominal
        </div>
      </div>
    </div>
  );
}