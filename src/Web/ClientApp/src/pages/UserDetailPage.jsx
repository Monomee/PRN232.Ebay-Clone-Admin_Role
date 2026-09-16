import React, { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { ToastMessage } from "../components/ToastMessage";
import { banUser, getUserById, unbanUser } from "../services/userService";
import { getCurrentAdminId } from "../services/adminSession";

function formatDate(value) {
  if (!value) {
    return "-";
  }

  return new Date(value).toLocaleString();
}

export function UserDetailPage() {
  const { userId } = useParams();
  const adminId = useMemo(() => getCurrentAdminId(), []);
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState({ message: "", type: "success" });

  const loadDetail = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getUserById(userId);
      setUser(data);
    } catch (error) {
      setToast({ message: error.message, type: "error" });
    } finally {
      setLoading(false);
    }
  }, [userId]);

  useEffect(() => {
    loadDetail();
  }, [loadDetail]);

  const handleBan = async () => {
    const reason = window.prompt("Ban reason:");
    if (!reason) {
      return;
    }

    try {
      await banUser(user.id, reason, adminId);
      setToast({ message: "User has been banned.", type: "success" });
      await loadDetail();
    } catch (error) {
      setToast({ message: error.message, type: "error" });
    }
  };

  const handleUnban = async () => {
    try {
      await unbanUser(user.id, adminId);
      setToast({ message: "User has been unbanned.", type: "success" });
      await loadDetail();
    } catch (error) {
      setToast({ message: error.message, type: "error" });
    }
  };

  return (
    <div style={{ minHeight: '100vh', background: '#ffffff', fontFamily: "'Inter', sans-serif", padding: '28px 20px' }}>
      <div className="container-fluid" style={{ maxWidth: 1400 }}>
        {/* ── Page Header (Standardized) ── */}
        <div className="text-center mb-5">
          <div className="d-flex justify-content-center align-items-center gap-3 mb-2">
            <Link to="/users" className="btn btn-sm btn-light rounded-circle shadow-sm d-flex align-items-center justify-content-center" style={{ width: '36px', height: '36px' }} title="Back to Users">
              <i className="bi bi-arrow-left text-primary"></i>
            </Link>
            <h1 className="h2 fw-bold text-dark mb-0" style={{ letterSpacing: '-1px' }}>User Intelligence</h1>
          </div>
          <p className="text-secondary mx-auto mb-0" style={{ maxWidth: '600px', fontSize: '0.95rem' }}>
            Comprehensive profile analysis, behavioral history, and administrative control center.
          </p>
        </div>

        <ToastMessage
          message={toast.message}
          type={toast.type}
          onClose={() => setToast({ message: "", type: "success" })}
        />

        {loading ? (
          <div className="py-5 text-center"><LoadingIndicator text="Retreiving user dossier..." /></div>
        ) : !user ? (
          <div className="py-5 text-center text-muted">User record not found.</div>
        ) : (
          <div className="row g-4">
            {/* ── Left Column: Personal Dossier ── */}
            <div className="col-lg-4">
              <div className="card border-0 shadow-sm rounded-4 overflow-hidden h-100">
                <div className="card-header bg-white border-bottom py-3 px-4 d-flex align-items-center justify-content-between">
                  <h5 className="mb-0 fw-bold text-dark"><i className="bi bi-person-badge me-2 text-primary"></i>Profile Details</h5>
                  <span className={`badge rounded-pill px-3 py-1 ${user.status === 'Banned' ? 'bg-danger-subtle text-danger' : 'bg-success-subtle text-success'}`} style={{ fontSize: '0.65rem', fontWeight: 700 }}>
                    {user.status?.toUpperCase()}
                  </span>
                </div>
                <div className="card-body p-4">
                  <div className="text-center mb-4">
                    <div className="bg-primary bg-opacity-10 text-primary rounded-circle d-flex align-items-center justify-content-center fw-bold shadow-sm mx-auto mb-3" style={{ width: 80, height: 80, fontSize: '2rem' }}>
                      {user.username?.charAt(0) || 'U'}
                    </div>
                    <h4 className="fw-bold text-dark mb-1">{user.username}</h4>
                    <p className="text-muted small mb-3">{user.email}</p>
                    <div className="d-flex justify-content-center gap-2">
                      {user.status === "Banned" ? (
                        <button type="button" className="btn btn-warning rounded-pill px-4 fw-bold shadow-sm" onClick={handleUnban}>
                          <i className="bi bi-shield-check me-2"></i>Lift Ban
                        </button>
                      ) : (
                        <button type="button" className="btn btn-danger rounded-pill px-4 fw-bold shadow-sm" onClick={handleBan}>
                          <i className="bi bi-shield-x me-2"></i>Restrict User
                        </button>
                      )}
                    </div>
                  </div>

                  <hr className="my-4 opacity-10" />

                  <div className="vstack gap-3">
                    <div className="d-flex justify-content-between">
                      <span className="text-secondary small fw-bold text-uppercase" style={{ letterSpacing: '0.5px' }}>Member ID</span>
                      <span className="text-dark fw-medium">#{user.id}</span>
                    </div>
                    <div className="d-flex justify-content-between">
                      <span className="text-secondary small fw-bold text-uppercase" style={{ letterSpacing: '0.5px' }}>Platform Role</span>
                      <span className="badge bg-light text-primary border rounded-pill px-3 fw-bold">{user.role || "USER"}</span>
                    </div>
                    <div className="d-flex justify-content-between">
                      <span className="text-secondary small fw-bold text-uppercase" style={{ letterSpacing: '0.5px' }}>Verification</span>
                      <span className={`fw-bold small ${user.approvalStatus === 'Approved' ? 'text-success' : 'text-warning'}`}>{user.approvalStatus}</span>
                    </div>
                    <div className="d-flex justify-content-between">
                      <span className="text-secondary small fw-bold text-uppercase" style={{ letterSpacing: '0.5px' }}>Violations</span>
                      <span className={`fw-bold ${user.violationCount > 0 ? 'text-danger' : 'text-success'}`}>{user.violationCount} Records</span>
                    </div>
                    <div className="d-flex justify-content-between">
                      <span className="text-secondary small fw-bold text-uppercase" style={{ letterSpacing: '0.5px' }}>Phone (Masked)</span>
                      <span className="text-dark small">{user.maskedPhone || "-"}</span>
                    </div>
                    <div className="d-flex justify-content-between">
                      <span className="text-secondary small fw-bold text-uppercase" style={{ letterSpacing: '0.5px' }}>Nat. ID (Masked)</span>
                      <span className="text-dark small">{user.maskedNationalId || "-"}</span>
                    </div>
                  </div>

                  {user.role === "Seller" && (
                    <div className="mt-4 p-3 bg-light rounded-4">
                      <div className="d-flex align-items-center justify-content-between mb-3">
                        <span className="text-secondary small fw-bold text-uppercase" style={{ letterSpacing: '0.5px' }}>Seller Standing</span>
                        <span className={`badge rounded-pill px-3 py-1 ${
                          user.sellerLevel === 'TopRated' ? 'bg-success text-white' : 
                          user.sellerLevel === 'AboveStandard' ? 'bg-warning text-dark' : 
                          'bg-danger text-white'
                        }`} style={{ fontSize: '0.65rem', fontWeight: 800 }}>
                          {(user.sellerLevel === 'TopRated' ? 'Top Rated' : user.sellerLevel === 'AboveStandard' ? 'Above Std' : (user.sellerLevel || "Below Std")).toUpperCase()}
                        </span>
                      </div>
                      <button className="btn btn-sm btn-primary w-100 rounded-pill fw-bold py-2 shadow-sm" onClick={async () => {
                        try {
                           const res = await fetch('/api/Users/evaluate-sellers', {
                             method: 'POST',
                             headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
                           });
                           if(res.ok) alert("Seller evaluation protocol completed successfully.");
                           loadDetail();
                        } catch(e) { }
                      }}>
                        <i className="bi bi-lightning-charge-fill me-2"></i>Evaluate Level
                      </button>
                    </div>
                  )}
                </div>
              </div>
            </div>

            {/* ── Right Column: Activity & Governance ── */}
            <div className="col-lg-8">
              <div className="vstack gap-4">
                {/* ── Order History ── */}
                <div className="card border-0 shadow-sm rounded-4 overflow-hidden">
                  <div className="card-header bg-white border-bottom py-3 px-4">
                    <h5 className="mb-0 fw-bold text-dark"><i className="bi bi-bag-check me-2 text-primary"></i>Purchase History</h5>
                  </div>
                  <div className="card-body p-0">
                    <div className="table-responsive">
                      <table className="table pe-table mb-0 align-middle">
                        <thead className="bg-light text-secondary fw-bold small text-uppercase">
                          <tr>
                            <th className="ps-4 py-3 border-0">Order ID</th>
                            <th className="py-3 border-0">Timestamp</th>
                            <th className="py-3 border-0">Status</th>
                            <th className="pe-4 py-3 border-0 text-end">Grand Total</th>
                          </tr>
                        </thead>
                        <tbody>
                          {(user.orderHistory || []).length === 0 ? (
                            <tr><td colSpan="4" className="text-center py-5 text-muted">No transactional history found for this account.</td></tr>
                          ) : (
                            (user.orderHistory || []).map((order) => (
                              <tr key={order.orderId} className="border-bottom">
                                <td className="ps-4 py-3 fw-bold text-dark">#{order.orderId}</td>
                                <td>{formatDate(order.orderDate)}</td>
                                <td>
                                  <span className="badge bg-light text-dark border rounded-pill px-3 py-1 fw-bold" style={{ fontSize: '0.7rem' }}>
                                    {order.status || "UNKNOWN"}
                                  </span>
                                </td>
                                <td className="pe-4 text-end fw-bold text-primary">{order.totalPrice != null ? `$${order.totalPrice}` : "-"}</td>
                              </tr>
                            ))
                          )}
                        </tbody>
                      </table>
                    </div>
                  </div>
                </div>

                {/* ── Violation History ── */}
                <div className="card border-0 shadow-sm rounded-4 overflow-hidden">
                  <div className="card-header bg-white border-bottom py-3 px-4">
                    <h5 className="mb-0 fw-bold text-dark"><i className="bi bi-shield-exclamation me-2 text-primary"></i>Governance Records</h5>
                  </div>
                  <div className="card-body p-0">
                    <div className="table-responsive">
                      <table className="table pe-table mb-0 align-middle">
                        <thead className="bg-light text-secondary fw-bold small text-uppercase">
                          <tr>
                            <th className="ps-4 py-3 border-0">Enforcement Action</th>
                            <th className="py-3 border-0">Admin Identity</th>
                            <th className="py-3 border-0">Timestamp</th>
                            <th className="pe-4 py-3 border-0">Dossier Details</th>
                          </tr>
                        </thead>
                        <tbody>
                          {(user.violationHistory || []).length === 0 ? (
                            <tr><td colSpan="4" className="text-center py-5 text-muted">No disciplinary records found in governance database.</td></tr>
                          ) : (
                            (user.violationHistory || []).map((item) => (
                              <tr key={item.auditLogId} className="border-bottom">
                                <td className="ps-4 py-3">
                                  <span className="badge bg-danger-subtle text-danger rounded-pill px-3 py-1 fw-bold" style={{ fontSize: '0.65rem' }}>
                                    {item.action?.toUpperCase()}
                                  </span>
                                </td>
                                <td className="fw-medium text-dark">{item.adminUsername || `Admin #${item.adminId}`}</td>
                                <td className="small text-muted">{formatDate(item.createdAt)}</td>
                                <td className="pe-4" style={{ minWidth: 250 }}>
                                  <div className="p-2 bg-light rounded-3 small text-secondary" style={{ fontSize: '0.75rem', maxHeight: '100px', overflowY: 'auto' }}>
                                    {item.details || "No supplementary data available."}
                                  </div>
                                </td>
                              </tr>
                            ))
                          )}
                        </tbody>
                      </table>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
