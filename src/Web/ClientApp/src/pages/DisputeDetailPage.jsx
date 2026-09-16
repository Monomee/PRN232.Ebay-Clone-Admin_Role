import React, { useEffect, useState, useCallback, useRef } from "react";
import { Link, useParams, useNavigate } from "react-router-dom";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { ToastMessage } from "../components/ToastMessage";
import { getCurrentAdminId } from "../services/adminSession";
import { disputeService } from "../services/disputeService";
import { useDisputeHub } from "../hooks/useDisputeHub";

export function DisputeDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [dispute, setDispute] = useState(null);
  const [loading, setLoading] = useState(true);
  const [resolving, setResolving] = useState(false);
  const [toast, setToast] = useState({ message: "", type: "success" });
  const [showResolveModal, setShowResolveModal] = useState(false);
  const justResolvedRef = useRef(false); // Track if the current admin just resolved
  const [resolveForm, setResolveForm] = useState({
    winner: "Buyer",
    refundAmount: "",
    adminNotes: "",
    requireReturn: false,
    addSellerViolation: false,
    sendNotifications: true
  });

  const adminId = getCurrentAdminId();

  useEffect(() => {
    loadDisputeDetail();
  }, [id]);

  // ── Real-time: nếu admin khác resolve CHÍNH dispute này ──
  // Redis backplane đảm bảo nhận được dù admin đó ở pod nào
  const handleDisputeResolved = useCallback((data) => {
    if (String(data.disputeId) === String(id)) {
      // If THIS admin just resolved it, skip the duplicate reload triggered by their own SignalR event
      if (justResolvedRef.current) {
        justResolvedRef.current = false;
        return;
      }
      setToast({
        message: `⚖️ Case ${data.caseId} resolved by another admin. Winner: ${data.winner}. Refreshing...`,
        type: "info"
      });
      loadDisputeDetail();
    }
  }, [id]);

  useDisputeHub(handleDisputeResolved);

  const loadDisputeDetail = async () => {
    setLoading(true);
    try {
      const detail = await disputeService.getDisputeDetail(id);
      setDispute(detail);
      
      // Pre-fill refund amount based on dispute amount
      if (detail.amount) {
        setResolveForm(prev => ({
          ...prev,
          refundAmount: detail.amount.toString()
        }));
      }
    } catch (error) {
      setToast({ 
        message: error.message || "Failed to load dispute details", 
        type: "error" 
      });
      
      if (error.response?.status === 404) {
        navigate("/disputes");
      }
    } finally {
      setLoading(false);
    }
  };

  const handleAssignToMe = async () => {
    try {
      await disputeService.assignDispute(id);
      setToast({ message: "Dispute assigned to you successfully!", type: "success" });
      await loadDisputeDetail(); // Refresh
    } catch (error) {
      setToast({ message: error.response?.data || "Failed to assign dispute", type: "error" });
    }
  };

  const handleResolveSubmit = async (e) => {
    e.preventDefault();
    setResolving(true);
    
    try {
      const payload = {
        disputeId: parseInt(id),
        winner: resolveForm.winner,
        refundAmount: resolveForm.refundAmount ? parseFloat(resolveForm.refundAmount) : 0,
        adminNotes: resolveForm.adminNotes,
        requireReturn: resolveForm.requireReturn,
        addSellerViolation: resolveForm.addSellerViolation,
        sendNotifications: resolveForm.sendNotifications
      };

      justResolvedRef.current = true; // Set flag BEFORE calling API — SignalR may fire before API returns
      await disputeService.resolveDispute(id, payload);
      setToast({ message: "✅ Dispute resolved successfully!", type: "success" });
      setShowResolveModal(false);
      await loadDisputeDetail(); // Refresh once
    } catch (error) {
      // Handle validation errors or problem details returned by problem details middleware
      let errorMsg = "Failed to resolve dispute";
      if (error.response?.data) {
        if (typeof error.response.data === 'string') {
          errorMsg = error.response.data;
        } else if (error.response.data.title) {
          errorMsg = error.response.data.title;
          if (error.response.data.errors) {
            const firstError = Object.values(error.response.data.errors)[0];
            if (firstError && firstError.length > 0) {
              errorMsg += `: ${firstError[0]}`;
            }
          }
        }
      }
      
      setToast({ 
        message: errorMsg, 
        type: "error" 
      });
    } finally {
      setResolving(false);
    }
  };

  const handleWinnerChange = (winner) => {
    setResolveForm(prev => ({
      ...prev,
      winner,
      refundAmount: winner === "Seller" ? "0" : 
                   winner === "Split" ? (dispute?.amount ? (dispute.amount / 2).toString() : "") :
                   dispute?.amount?.toString() || ""
    }));
  };

  const getStatusBadge = (status) => {
    const badgeClasses = {
      Open: "badge bg-primary",
      AwaitingSellerResponse: "badge bg-warning text-dark",
      Escalated: "badge bg-danger",
      UnderReview: "badge bg-info text-dark",
      AssignedToAdmin: "badge bg-success",
      Resolved: "badge bg-success",
      Closed: "badge bg-secondary"
    };
    return <span className={badgeClasses[status] || "badge bg-light text-dark"}>{status}</span>;
  };

  const getPriorityBadge = (priority) => {
    const badgeClasses = {
      Critical: "badge bg-danger",
      High: "badge bg-warning text-dark",
      Medium: "badge bg-info text-dark",
      Low: "badge bg-secondary"
    };
    return <span className={badgeClasses[priority] || "badge bg-light text-dark"}>{priority}</span>;
  };

  const formatDate = (dateStr) => {
    if (!dateStr) return "-";
    return new Date(dateStr).toLocaleString();
  };

  if (loading) {
    return <LoadingIndicator text="Loading dispute details..." />;
  }

  if (!dispute) {
    return (
      <section className="py-3">
        <div className="alert alert-danger">
          Dispute not found. <Link to="/disputes">Return to disputes list</Link>
        </div>
      </section>
    );
  }

  return (
    <section className="py-3">
      <div className="d-flex justify-content-between align-items-start mb-4">
        <div>
          <nav aria-label="breadcrumb">
            <ol className="breadcrumb">
              <li className="breadcrumb-item">
                <Link to="/disputes">Disputes</Link>
              </li>
              <li className="breadcrumb-item active">{dispute.caseId}</li>
            </ol>
          </nav>
          <h1 className="h3 mb-1">Case: {dispute.caseId}</h1>
          <div className="d-flex gap-2 align-items-center">
            {getStatusBadge(dispute.status)}
            {getPriorityBadge(dispute.priority)}
            <span className="badge bg-light text-dark">{dispute.type}</span>
          </div>
        </div>
        <div className="d-flex gap-2">
          {!dispute.assignedTo && dispute.status !== "Resolved" && dispute.status !== "Closed" && (
            <button
              type="button"
              className="btn btn-outline-primary"
              onClick={handleAssignToMe}
            >
              <i className="fas fa-hand-paper me-2"></i>
              Assign to Me
            </button>
          )}
          {dispute.assignedTo && dispute.status !== "Resolved" && dispute.status !== "Closed" && (
            <button
              type="button"
              className="btn btn-success"
              onClick={() => setShowResolveModal(true)}
            >
              <i className="fas fa-gavel me-2"></i>
              Resolve Case
            </button>
          )}
        </div>
      </div>

      <ToastMessage
        message={toast.message}
        type={toast.type}
        onClose={() => setToast({ message: "", type: "success" })}
      />

      {/* Case Overview */}
      <div className="row g-4 mb-4">
        <div className="col-md-4">
          <div className="card">
            <div className="card-header">
              <h5 className="card-title mb-0">Case Information</h5>
            </div>
            <div className="card-body">
              <table className="table table-sm">
                <tbody>
                  <tr>
                    <td className="text-muted">Case ID:</td>
                    <td className="fw-bold">{dispute.caseId}</td>
                  </tr>
                  <tr>
                    <td className="text-muted">Amount:</td>
                    <td className="fw-bold">
                      {dispute.amount ? `$${dispute.amount.toFixed(2)}` : "-"}
                    </td>
                  </tr>
                  <tr>
                    <td className="text-muted">Created:</td>
                    <td>{formatDate(dispute.createdAt)}</td>
                  </tr>
                  <tr>
                    <td className="text-muted">Deadline:</td>
                    <td>
                      {dispute.deadline ? (
                        <span className={new Date(dispute.deadline) < new Date() ? "text-danger" : ""}>
                          {formatDate(dispute.deadline)}
                        </span>
                      ) : "-"}
                    </td>
                  </tr>
                  <tr>
                    <td className="text-muted">Assigned To:</td>
                    <td>
                      {dispute.assignedTo ? (
                        <span className="badge bg-success">Admin #{dispute.assignedTo}</span>
                      ) : (
                        <span className="text-muted">Unassigned</span>
                      )}
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
        
        <div className="col-md-4">
          <div className="card">
            <div className="card-header">
              <h5 className="card-title mb-0">Buyer Information</h5>
            </div>
            <div className="card-body">
              {dispute.buyer ? (
                <div>
                  <div className="fw-bold">{dispute.buyer.username}</div>
                  <div className="text-muted">{dispute.buyer.email}</div>
                </div>
              ) : (
                <span className="text-muted">No buyer information</span>
              )}
            </div>
          </div>
        </div>

        <div className="col-md-4">
          <div className="card">
            <div className="card-header">
              <h5 className="card-title mb-0">Seller Information</h5>
            </div>
            <div className="card-body">
              {dispute.seller ? (
                <div>
                  <div className="fw-bold">{dispute.seller.username}</div>
                  <div className="text-muted">{dispute.seller.email}</div>
                  {dispute.seller.feedbackScore && (
                    <div className="mt-2">
                      <span className="badge bg-warning text-dark">
                        ? {dispute.seller.feedbackScore}
                      </span>
                    </div>
                  )}
                </div>
              ) : (
                <span className="text-muted">No seller information</span>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Order & Product Info */}
      {dispute.order && (
        <div className="card mb-4">
          <div className="card-header">
            <h5 className="card-title mb-0">Order Information</h5>
          </div>
          <div className="card-body">
            <div className="row">
              <div className="col-md-6">
                <table className="table table-sm">
                  <tbody>
                    <tr>
                      <td className="text-muted">Order ID:</td>
                      <td className="fw-bold">#{dispute.order.id}</td>
                    </tr>
                    <tr>
                      <td className="text-muted">Order Date:</td>
                      <td>{formatDate(dispute.order.orderDate)}</td>
                    </tr>
                    <tr>
                      <td className="text-muted">Total Amount:</td>
                      <td className="fw-bold">${dispute.order.totalPrice?.toFixed(2) ?? '—'}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <div className="col-md-6">
                {dispute.order.products && dispute.order.products.length > 0 && (
                  <div>
                    <h6>Products:</h6>
                    {dispute.order.products.map((product, index) => (
                      <div key={index} className="border-bottom py-2">
                        <div className="fw-medium">{product.title}</div>
                        <div className="text-muted">Price: ${product.price}</div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Description & Evidence */}
      <div className="row g-4 mb-4">
        <div className="col-md-6">
          <div className="card h-100">
            <div className="card-header">
              <h5 className="card-title mb-0">Dispute Description</h5>
            </div>
            <div className="card-body">
              <p>{dispute.description || "No description provided"}</p>
              {dispute.desiredOutcome && (
                <div>
                  <strong>Desired Outcome:</strong> {dispute.desiredOutcome}
                </div>
              )}
            </div>
          </div>
        </div>

        <div className="col-md-6">
          <div className="card h-100">
            <div className="card-header">
              <h5 className="card-title mb-0">Evidence</h5>
            </div>
            <div className="card-body">
              {[
                { label: "Buyer Evidence", items: dispute.buyerEvidences },
                { label: "Seller Evidence", items: dispute.sellerEvidences }
              ].map(({ label, items }) => items && items.length > 0 && (
                <div className="mb-4" key={label}>
                  <strong className="d-block mb-2">{label}:</strong>
                  <div className="d-flex flex-wrap gap-3">
                    {items.map((e, i) => (
                      <div key={i} style={{width: '160px'}}>
                        {e.type === 'image' ? (
                          <a href={e.url} target="_blank" rel="noopener noreferrer">
                            <img
                              src={e.url}
                              alt={e.description}
                              className="rounded border"
                              style={{width:'160px', height:'110px', objectFit:'cover', cursor:'pointer'}}
                              onError={(ev) => { ev.target.style.display='none'; ev.target.nextSibling.style.display='flex'; }}
                            />
                            <div className="rounded border bg-light d-none align-items-center justify-content-center"
                              style={{width:'160px', height:'110px'}}>
                              <i className="fas fa-image fa-2x text-muted"></i>
                            </div>
                          </a>
                        ) : e.type === 'video' ? (
                          <a href={e.url} target="_blank" rel="noopener noreferrer"
                            className="rounded border bg-dark d-flex align-items-center justify-content-center text-decoration-none"
                            style={{width:'160px', height:'110px'}}>
                            <i className="fas fa-play-circle fa-3x text-white opacity-75"></i>
                          </a>
                        ) : (
                          <a href={e.url} target="_blank" rel="noopener noreferrer"
                            className="rounded border bg-light d-flex align-items-center justify-content-center text-decoration-none gap-2"
                            style={{width:'160px', height:'110px'}}>
                            <i className="fas fa-file-pdf fa-2x text-danger"></i>
                          </a>
                        )}
                        <div className="mt-1">
                          <span className="badge bg-secondary text-uppercase me-1" style={{fontSize:'0.6rem'}}>{e.type}</span>
                          <small className="text-muted" style={{fontSize:'0.7rem'}}>{e.description}</small>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              ))}
              {(!dispute.buyerEvidences || dispute.buyerEvidences.length === 0) &&
               (!dispute.sellerEvidences || dispute.sellerEvidences.length === 0) && (
                <span className="text-muted">No evidence provided</span>
              )}
            </div>
          </div>
        </div>
      </div>


      {/* Timeline */}
      {dispute.timeline && dispute.timeline.length > 0 && (
        <div className="card mb-4">
          <div className="card-header">
            <h5 className="card-title mb-0">Case Timeline</h5>
          </div>
          <div className="card-body">
            <div className="timeline">
              {dispute.timeline.map((event, index) => (
                <div key={index} className="d-flex mb-3">
                  <div className="flex-shrink-0">
                    <div className="bg-primary rounded-circle p-2" style={{width: '32px', height: '32px'}}>
                      <i className="fas fa-circle text-white" style={{fontSize: '0.5rem'}}></i>
                    </div>
                  </div>
                  <div className="flex-grow-1 ms-3">
                    <div className="fw-bold">{event.stage}</div>
                    <div className="text-muted small">
                      {formatDate(event.timestamp)} - {event.actor}
                    </div>
                    {event.notes && <div className="mt-1">{event.notes}</div>}
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Messages */}
      {dispute.messages && dispute.messages.length > 0 && (
        <div className="card mb-4">
          <div className="card-header">
            <h5 className="card-title mb-0">Messages</h5>
          </div>
          <div className="card-body">
            {dispute.messages.map((message) => (
              <div key={message.id} className="border-bottom pb-3 mb-3">
                <div className="d-flex justify-content-between">
                  <strong>{message.senderType}</strong>
                  <small className="text-muted">{formatDate(message.createdAt)}</small>
                </div>
                <div className="mt-2">{message.content}</div>
                {message.offerAmount && (
                  <div className="mt-2">
                    <span className="badge bg-warning text-dark">
                      Offer: ${message.offerAmount}
                    </span>
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Resolution Options (if available) */}
      {dispute.resolutionOptions && dispute.resolutionOptions.length > 0 && (
        <div className="card mb-4">
          <div className="card-header">
            <h5 className="card-title mb-0">AI-Powered Resolution Recommendations</h5>
          </div>
          <div className="card-body">
            {dispute.resolutionOptions.map((option, index) => (
              <div key={index} className="border rounded p-3 mb-3">
                <div className="d-flex justify-content-between align-items-start">
                  <div>
                    <h6>{option.outcome}</h6>
                    <p className="mb-2">{option.description}</p>
                    <small className="text-muted">{option.reasoning}</small>
                  </div>
                  {option.recommendedRefund && (
                    <span className="badge bg-info">${option.recommendedRefund}</span>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Resolve Modal */}
      {showResolveModal && (
        <div className="modal show d-block" style={{backgroundColor: 'rgba(0,0,0,0.5)'}}>
          <div className="modal-dialog modal-lg">
            <div className="modal-content">
              <div className="modal-header">
                <h5 className="modal-title">Resolve Dispute: {dispute.caseId}</h5>
                <button 
                  type="button" 
                  className="btn-close" 
                  onClick={() => setShowResolveModal(false)}
                ></button>
              </div>
              <form onSubmit={handleResolveSubmit}>
                <div className="modal-body">
                  {/* Seller Context Info */}
                  <div className="alert alert-info py-2 mb-3">
                    <div className="d-flex justify-content-between align-items-center">
                      <div>
                        <strong>Seller: </strong>
                        {dispute.seller?.username || "Unknown"} ({dispute.seller?.email || "No email"})
                      </div>
                      <div className="text-end">
                        <small className="d-block mb-1">Feedback Score: {dispute.seller?.feedbackScore || "0"} ?</small>
                      </div>
                    </div>
                  </div>

                  <div className="row g-3">
                    <div className="col-md-6">
                      <label className="form-label">Decision *</label>
                      <div className="d-grid gap-2">
                        {["Buyer", "Seller", "Split"].map(winner => (
                          <div key={winner} className="form-check">
                            <input
                              className="form-check-input"
                              type="radio"
                              name="winner"
                              id={`winner-${winner}`}
                              checked={resolveForm.winner === winner}
                              onChange={() => handleWinnerChange(winner)}
                            />
                            <label className="form-check-label" htmlFor={`winner-${winner}`}>
                              {winner === "Buyer" && "Buyer Wins - Full/Partial Refund"}
                              {winner === "Seller" && "Seller Wins - No Refund"}
                              {winner === "Split" && "Split Decision - Compromise"}
                            </label>
                          </div>
                        ))}
                      </div>
                    </div>
                    <div className="col-md-6">
                      <label className="form-label">Refund Amount ($)</label>
                      <input
                        type="number"
                        className="form-control"
                        step="0.01"
                        min="0"
                        max={dispute.amount || 999999}
                        value={resolveForm.refundAmount}
                        onChange={(e) => setResolveForm(prev => ({...prev, refundAmount: e.target.value}))}
                        disabled={resolveForm.winner === "Seller"}
                      />
                      <small className="text-muted">
                        Original amount: ${dispute.amount?.toFixed(2)}
                      </small>
                    </div>
                    <div className="col-12">
                      <label className="form-label">Admin Notes * (Minimum 50 characters)</label>
                      <textarea
                        className="form-control"
                        rows="4"
                        required
                        minLength={50}
                        placeholder="Provide detailed reasoning for your decision. This will be visible to both parties."
                        value={resolveForm.adminNotes}
                        onChange={(e) => setResolveForm(prev => ({...prev, adminNotes: e.target.value}))}
                      />
                      <small className="text-muted">
                        {resolveForm.adminNotes.length}/50 characters minimum
                      </small>
                    </div>
                    <div className="col-12">
                      <div className="form-check">
                        <input
                          className="form-check-input"
                          type="checkbox"
                          id="requireReturn"
                          checked={resolveForm.requireReturn}
                          onChange={(e) => setResolveForm(prev => ({...prev, requireReturn: e.target.checked}))}
                        />
                        <label className="form-check-label" htmlFor="requireReturn">
                          Require item to be returned
                        </label>
                      </div>
                      <div className="form-check">
                        <input
                          className="form-check-input"
                          type="checkbox"
                          id="addSellerViolation"
                          checked={resolveForm.addSellerViolation}
                          onChange={(e) => setResolveForm(prev => ({...prev, addSellerViolation: e.target.checked}))}
                        />
                        <label className="form-check-label" htmlFor="addSellerViolation">
                          Add violation to seller's record
                        </label>
                      </div>
                      <div className="form-check">
                        <input
                          className="form-check-input"
                          type="checkbox"
                          id="sendNotifications"
                          checked={resolveForm.sendNotifications}
                          onChange={(e) => setResolveForm(prev => ({...prev, sendNotifications: e.target.checked}))}
                        />
                        <label className="form-check-label" htmlFor="sendNotifications">
                          Send email notifications to parties
                        </label>
                      </div>
                    </div>
                  </div>
                </div>
                <div className="modal-footer">
                  <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={() => setShowResolveModal(false)}
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="btn btn-success"
                    disabled={resolving || resolveForm.adminNotes.length < 50}
                  >
                    {resolving ? (
                      <>
                        <span className="spinner-border spinner-border-sm me-2"></span>
                        Resolving...
                      </>
                    ) : (
                      "Resolve Dispute"
                    )}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}