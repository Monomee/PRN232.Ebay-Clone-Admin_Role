import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import returnRequestService from '../services/returnRequestService';

export default function ReturnRequestDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [detail, setDetail] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [messages, setMessages] = useState([]);
  const [loadingMessages, setLoadingMessages] = useState(false);

  // Modal state
  const [modal, setModal] = useState(null); // 'approve' | 'reject' | 'adjudicate' | 'provideLabel' | 'freeze' | null
  const [adminNote, setAdminNote] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState('');

  // New state for Adjudication
  const [resolutionAction, setResolutionAction] = useState('RequireReturn');
  const [isRefundedByEbayFund, setIsRefundedByEbayFund] = useState(false);
  const [returnLabelUrl, setReturnLabelUrl] = useState('');

  // Lightbox
  const [lightbox, setLightbox] = useState(null);

  useEffect(() => {
    (async () => {
      setLoading(true);
      try {
        const [data, msgs] = await Promise.all([
          returnRequestService.getReturnRequestDetail(id),
          returnRequestService.getReturnRequestMessages(id)
        ]);
        setDetail(data);
        setMessages(msgs);
      } catch {
        setError('Could not load request details.');
      } finally {
        setLoading(false);
      }
    })();
  }, [id]);

  const handleAction = async () => {
    if ((modal === 'reject' || modal === 'adjudicate' || modal === 'freeze') && !adminNote.trim()) {
      setSubmitError('Please enter a note/reason.');
      return;
    }
    if (modal === 'provideLabel' && !returnLabelUrl.trim()) {
      setSubmitError('Please enter a return label URL.');
      return;
    }

    setSubmitting(true);
    setSubmitError('');
    try {
      if (modal === 'approve') {
        await returnRequestService.approveReturnRequest(Number(id), adminNote);
      } else if (modal === 'reject') {
        await returnRequestService.rejectReturnRequest(Number(id), adminNote);
      } else if (modal === 'adjudicate') {
        await returnRequestService.adjudicateReturnRequest(Number(id), {
          adminNote,
          resolutionAction,
          isRefundedByEbayFund,
        });
      } else if (modal === 'provideLabel') {
        await returnRequestService.provideReturnLabel(Number(id), returnLabelUrl);
      } else if (modal === 'freeze') {
        await returnRequestService.freezeReturnRequest(Number(id), adminNote);
      }
      
      const successMsg = modal === 'approve' ? 'Accepted!' 
                         : modal === 'reject' ? 'Rejected!' 
                         : modal === 'adjudicate' ? 'Adjudicated!' 
                         : modal === 'freeze' ? 'Frozen for investigation!'
                         : 'Return label provided!';

      navigate('/return-requests', { state: { toast: successMsg } });
    } catch {
      setSubmitError('Action failed. Please try again.');
    } finally {
      setSubmitting(false);
    }
  };

  const parseImages = (raw) => {
    if (!raw) return [];
    try { return JSON.parse(raw); } catch { return [raw]; }
  };

  if (loading) {
    return (
      <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#f8fafc' }}>
        <link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;500;600;700&display=swap" rel="stylesheet" />
        <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
        <div style={{ textAlign: 'center' }}>
          <div style={{ width: 44, height: 44, border: '3px solid #e5e7eb', borderTop: '3px solid #6366f1', borderRadius: '50%', animation: 'spin 0.8s linear infinite', margin: '0 auto 14px' }} />
          <p style={{ color: '#9ca3af', fontFamily: "'DM Sans', sans-serif" }}>Loading details...</p>
        </div>
      </div>
    );
  }

  if (error || !detail) {
    return (
      <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#f8fafc' }}>
        <link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;500;600;700&display=swap" rel="stylesheet" />
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontSize: 48 }}>⚠️</div>
          <p style={{ color: '#ef4444', fontFamily: "'DM Sans', sans-serif", marginTop: 12 }}>{error || 'Request not found.'}</p>
          <button onClick={() => navigate('/return-requests')} style={backBtnStyle}>← Back to list</button>
        </div>
      </div>
    );
  }

  const images = parseImages(detail.evidenceImages);
  const isPending = detail.status === 'Pending';

  const getEscalationTimer = () => {
    if (detail.status !== 'Pending' || !detail.createdAt) return null;
    const createdDate = new Date(detail.createdAt);
    const escalationDate = new Date(createdDate.getTime() + 3 * 24 * 60 * 60 * 1000);
    const now = new Date();
    const diff = escalationDate - now;

    if (diff <= 0) return <span style={{ color: '#ef4444', fontWeight: 700, fontSize: 11, background: '#fee2e2', padding: '2px 8px', borderRadius: 4 }}>DUE TO ESCALATE</span>;

    const days = Math.floor(diff / (24 * 60 * 60 * 1000));
    const hours = Math.floor((diff % (24 * 60 * 60 * 1000)) / (60 * 60 * 1000));
    
    return (
      <span style={{ color: '#d97706', fontWeight: 600, fontSize: 11, background: '#fef3c7', padding: '2px 8px', borderRadius: 4 }}>
         ⌛ Escalates in {days}d {hours}h
      </span>
    );
  };

  return (
    <div style={{ minHeight: '100vh', background: '#f8fafc', fontFamily: "'DM Sans', sans-serif" }}>
      <link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;500;600;700&display=swap" rel="stylesheet" />

      {/* Top Bar */}
      <div style={{ background: '#fff', borderBottom: '1px solid #e5e7eb', padding: '16px 36px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
          <button
            onClick={() => navigate('/return-requests')}
            style={{ background: 'none', border: '1.5px solid #e5e7eb', borderRadius: 8, padding: '7px 14px', cursor: 'pointer', fontSize: 13, fontWeight: 600, color: '#374151', fontFamily: "'DM Sans', sans-serif" }}
          >
            ← Back
          </button>
          <div>
            <h1 style={{ margin: 0, fontSize: 18, fontWeight: 700, color: '#111827' }}>
              Return Request Details #{detail.id}
            </h1>
            <p style={{ margin: '2px 0 0', fontSize: 12, color: '#9ca3af', display: 'flex', alignItems: 'center', gap: 10 }}>
              Order #{detail.orderId} · Created at {detail.createdAt ? new Date(detail.createdAt).toLocaleString('en-US') : '—'}
              {getEscalationTimer()}
            </p>
          </div>
        </div>

        <StatusChip status={detail.status} />
      </div>

      {/* Main Content */}
      <div style={{ padding: '28px 36px', display: 'grid', gridTemplateColumns: '1fr 340px', gap: 24, maxWidth: 1200, margin: '0 auto' }}>

        {/* LEFT COLUMN */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>

          {/* Request Information */}
          <Card title="📋 Request Information">
            <Row label="Customer" value={
              <span>
                <strong>{detail.buyerUsername || '—'}</strong>
                {detail.buyerEmail && <span style={{ color: '#6b7280', marginLeft: 8, fontSize: 12 }}>({detail.buyerEmail})</span>}
              </span>
            } />
            <Row label="Return Reason" value={<span style={{ color: '#dc2626', fontWeight: 600 }}>{detail.reason || '—'}</span>} />
            <Row label="Seller Solution" value={detail.shopSolution || <span style={{ color: '#9ca3af', fontStyle: 'italic' }}>Seller has not proposed a solution yet</span>} />
            {detail.adminNote && <Row label="Admin Note" value={detail.adminNote} />}
            {detail.resolvedAt && <Row label="Resolution Date" value={new Date(detail.resolvedAt).toLocaleString('en-US')} />}
          </Card>

          {/* Evidence Images */}
          <Card title="🖼️ Evidence Images from Customer">
            {images.length === 0 ? (
              <div style={{ padding: '24px 0', textAlign: 'center', color: '#9ca3af', fontSize: 13 }}>
                Customer has not provided any evidence images.
              </div>
            ) : (
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, marginTop: 4 }}>
                {images.map((url, i) => (
                  <div
                    key={i}
                    onClick={() => setLightbox(url)}
                    style={{
                      width: 130, height: 130, borderRadius: 10,
                      overflow: 'hidden', cursor: 'zoom-in',
                      border: '2px solid #e5e7eb',
                      transition: 'border-color 0.2s, transform 0.2s',
                      flexShrink: 0,
                    }}
                    onMouseEnter={e => { e.currentTarget.style.borderColor = '#6366f1'; e.currentTarget.style.transform = 'scale(1.03)'; }}
                    onMouseLeave={e => { e.currentTarget.style.borderColor = '#e5e7eb'; e.currentTarget.style.transform = 'scale(1)'; }}
                  >
                    <img src={url} alt={`evidence-${i}`} style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                  </div>
                ))}
              </div>
            )}
          </Card>

          {/* Products in Order */}
          <Card title="🛒 Products in Order">
            {(!detail.orderItems || detail.orderItems.length === 0) ? (
              <p style={{ color: '#9ca3af', fontSize: 13 }}>No product data available.</p>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid #f3f4f6' }}>
                    {['Product', 'Qty', 'Unit Price', 'Total'].map(h => (
                      <th key={h} style={{ padding: '8px 10px', textAlign: 'left', color: '#6b7280', fontWeight: 600, fontSize: 11, textTransform: 'uppercase' }}>{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {detail.orderItems.map((item, i) => (
                    <tr key={i} style={{ borderBottom: '1px solid #f9fafb' }}>
                      <td style={{ padding: '10px 10px', color: '#111827', fontWeight: 500 }}>{item.productTitle || `ID #${item.productId}`}</td>
                      <td style={{ padding: '10px 10px', color: '#374151' }}>{item.quantity}</td>
                      <td style={{ padding: '10px 10px', color: '#374151' }}>{item.unitPrice ? `$${Number(item.unitPrice).toLocaleString('en-US')}` : '—'}</td>
                      <td style={{ padding: '10px 10px', fontWeight: 600, color: '#111827' }}>
                        {item.unitPrice && item.quantity ? `$${(item.unitPrice * item.quantity).toLocaleString('en-US')}` : '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </Card>

            {/* Timeline / Audit Log */}
            <Card title="📜 Activity Timeline (Audit Log)">
              {(!detail.history || detail.history.length === 0) ? (
                <div style={{ padding: '12px 0', textAlign: 'center', color: '#9ca3af', fontSize: 13 }}>
                  No actions recorded yet.
                </div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 16, marginTop: 10 }}>
                  {detail.history.map((item, i) => (
                    <div key={i} style={{ display: 'flex', gap: 12 }}>
                      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                        <div style={{ width: 10, height: 10, borderRadius: '50%', background: '#6366f1', marginTop: 4 }}></div>
                        {i < detail.history.length - 1 && <div style={{ width: 1, flex: 1, background: '#e5e7eb', margin: '4px 0' }}></div>}
                      </div>
                      <div style={{ flex: 1, paddingBottom: i < detail.history.length - 1 ? 16 : 0 }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
                          <span style={{ fontSize: 13, fontWeight: 700, color: '#111827' }}>{item.action}</span>
                          <span style={{ fontSize: 11, color: '#9ca3af' }}>{new Date(item.createdAt).toLocaleString()}</span>
                        </div>
                        <div style={{ fontSize: 12, color: '#4b5563', marginTop: 2 }}>{item.details}</div>
                        <div style={{ fontSize: 11, color: '#9ca3af', marginTop: 4 }}>Performed by: {item.adminUsername}</div>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </Card>

            {/* Chat Evidence */}
            <Card title="💬 Buyer & Seller Chat History (Evidence)">
              {messages.length === 0 ? (
                <div style={{ padding: '24px 0', textAlign: 'center', color: '#9ca3af', fontSize: 13 }}>
                  No conversation history found between the two parties.
                </div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 12, maxHeight: 400, overflowY: 'auto', paddingRight: 8 }}>
                  {messages.map((msg, i) => {
                    const isBuyer = msg.senderId === detail.userId;
                    return (
                      <div key={i} style={{
                        alignSelf: isBuyer ? 'flex-start' : 'flex-end',
                        maxWidth: '85%',
                        background: isBuyer ? '#f3f4f6' : '#eff6ff',
                        borderRadius: 12,
                        padding: '10px 14px',
                        border: `1px solid ${isBuyer ? '#e5e7eb' : '#dbeafe'}`,
                      }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 12, marginBottom: 4 }}>
                          <span style={{ fontSize: 11, fontWeight: 700, color: isBuyer ? '#374151' : '#1d4ed8' }}>
                            {msg.senderUsername} {isBuyer ? '(Buyer)' : '(Seller)'}
                          </span>
                          <span style={{ fontSize: 10, color: '#9ca3af' }}>
                            {new Date(msg.timestamp).toLocaleString('en-US', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit' })}
                          </span>
                        </div>
                        <p style={{ margin: 0, fontSize: 13, color: '#111827', lineHeight: 1.5, whiteSpace: 'pre-wrap' }}>
                          {msg.content}
                        </p>
                      </div>
                    );
                  })}
                </div>
              )}
              <p style={{ marginTop: 16, fontSize: 11, color: '#6b7280', fontStyle: 'italic', textAlign: 'center' }}>
                ⚠️ Note: This is the full direct chat history between the parties on the eBay system.
              </p>
            </Card>
          </div>

        {/* RIGHT COLUMN */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>

          {/* Order Summary */}
          <Card title="📦 Order Summary">
            <Row label="Order ID" value={<strong style={{ color: '#4f46e5' }}>#{detail.orderId}</strong>} />
            <Row label="Order Date" value={detail.orderDate ? new Date(detail.orderDate).toLocaleDateString('en-US') : '—'} />
            <Row label="Order Status" value={detail.orderStatus} />
            <div style={{ borderTop: '1px dashed #e5e7eb', margin: '12px 0' }} />
            <Row label="Total Refund Amount" value={
              <span style={{ fontSize: 18, fontWeight: 700, color: '#059669' }}>
                {detail.totalPrice ? `$${Number(detail.totalPrice).toLocaleString('en-US')}` : '—'}
              </span>
            } />
            {detail.resolutionAction && (
              <Row label="Resolution Path" value={
                <span style={{ fontWeight: 600, color: '#4f46e5' }}>
                  {detail.resolutionAction === 'RequireReturn' ? 'Return & Refund' : 
                   detail.resolutionAction === 'KeepItem' ? 'Keep Item & Refund' : 'Immediate Refund'}
                </span>
              } />
            )}
            {detail.isRefundedByEbayFund && (
              <div style={{ background: '#fef9c3', padding: '4px 8px', borderRadius: 4, fontSize: 11, color: '#854d0e', marginTop: 8, textAlign: 'center' }}>
                 🛡️ Paid by eBay Trust Fund
              </div>
            )}
          </Card>

          {/* Shipping Info */}
          {(detail.trackingNumber || detail.returnLabelUrl) && (
            <Card title="🚚 Shipping Information">
              {detail.trackingNumber && <Row label="Tracking Number" value={<strong>{detail.trackingNumber}</strong>} />}
              {detail.deliveryStatus && <Row label="Delivery Status" value={detail.deliveryStatus} />}
              {detail.returnLabelUrl && (
                <div style={{ marginTop: 12 }}>
                  <a href={detail.returnLabelUrl} target="_blank" rel="noreferrer" style={{
                    display: 'block', textAlign: 'center', padding: '8px', 
                    background: '#f3f4f6', borderRadius: 8, fontSize: 12, 
                    color: '#4f46e5', fontWeight: 600, textDecoration: 'none',
                    border: '1.5px solid #e5e7eb'
                  }}>
                    📄 View Return Label (PDF/Image)
                  </a>
                </div>
              )}
            </Card>
          )}

          {/* ACTION PANEL */}
          {(isPending || detail.status === 'Escalated' || detail.status === 'Frozen') && (
            <div style={{
              background: '#fff', borderRadius: 14, padding: 24,
              border: '1px solid #e5e7eb', boxShadow: '0 1px 6px rgba(0,0,0,0.04)',
            }}>
              <h3 style={{ margin: '0 0 6px', fontSize: 14, fontWeight: 700, color: '#111827' }}>⚖️ Decision Panel</h3>
              <p style={{ margin: '0 0 16px', fontSize: 12, color: '#6b7280' }}>
                Select an action to process this request.
              </p>
              
              <button
                onClick={() => { setModal('adjudicate'); setAdminNote(''); setSubmitError(''); }}
                style={{
                  width: '100%', padding: '12px', borderRadius: 10,
                  background: '#6366f1', color: '#fff', border: 'none',
                  fontWeight: 700, fontSize: 14, cursor: 'pointer',
                  fontFamily: "'DM Sans', sans-serif",
                  marginBottom: 10, transition: 'background 0.2s',
                }}
              >
                ⚖️ Adjudicate
              </button>

              <div style={{ display: 'flex', gap: 10 }}>
                <button
                  onClick={() => { setModal('approve'); setAdminNote(''); setSubmitError(''); }}
                  style={{
                    flex: 1, padding: '10px', borderRadius: 10,
                    background: '#10b981', color: '#fff', border: 'none',
                    fontWeight: 700, fontSize: 13, cursor: 'pointer',
                    fontFamily: "'DM Sans', sans-serif",
                    transition: 'background 0.2s',
                  }}
                >
                  ✅ Approve
                </button>
                <button
                  onClick={() => { setModal('reject'); setAdminNote(''); setSubmitError(''); }}
                  style={{
                    flex: 1, padding: '10px', borderRadius: 10,
                    background: '#fff', color: '#ef4444',
                    border: '1.5px solid #ef4444',
                    fontWeight: 700, fontSize: 13, cursor: 'pointer',
                    fontFamily: "'DM Sans', sans-serif",
                    transition: 'all 0.2s',
                  }}
                >
                  ❌ Reject
                </button>
              </div>

              {detail.status !== 'Frozen' && (
                <button
                  onClick={() => { setModal('freeze'); setAdminNote(''); setSubmitError(''); }}
                  style={{
                    width: '100%', padding: '10px', borderRadius: 10,
                    background: '#f8fafc', color: '#475569',
                    border: '1.5px solid #e2e8f0',
                    fontWeight: 700, fontSize: 13, cursor: 'pointer',
                    fontFamily: "'DM Sans', sans-serif",
                    marginTop: 10, transition: 'all 0.2s',
                  }}
                >
                  ❄️ Freeze Request
                </button>
              )}
            </div>
          )}

          {/* Provide Return Label Panel */}
          {detail.status === 'WaitingForReturnLabel' && (
             <div style={{
               background: '#fff', borderRadius: 14, padding: 24,
               border: '1px solid #e5e7eb', boxShadow: '0 1px 6px rgba(0,0,0,0.04)',
             }}>
               <h3 style={{ margin: '0 0 6px', fontSize: 14, fontWeight: 700, color: '#111827' }}>📮 Provide Return Label</h3>
               <p style={{ margin: '0 0 16px', fontSize: 12, color: '#6b7280' }}>
                 This request is waiting for the Admin to provide a return shipping label to the customer.
               </p>
               <button
                 onClick={() => { setModal('provideLabel'); setReturnLabelUrl(''); setSubmitError(''); }}
                 style={{
                   width: '100%', padding: '12px', borderRadius: 10,
                   background: '#10b981', color: '#fff', border: 'none',
                   fontWeight: 700, fontSize: 14, cursor: 'pointer',
                   fontFamily: "'DM Sans', sans-serif",
                   transition: 'background 0.2s',
                 }}
               >
                 📦 Upload Shipping Label
               </button>
             </div>
          )}

          {/* Processed Status */}
          {!isPending && (
            <div style={{
              background: detail.status === 'Approved' ? '#f0fdf4' : '#fef2f2',
              border: `1px solid ${detail.status === 'Approved' ? '#bbf7d0' : '#fecaca'}`,
              borderRadius: 14, padding: 20, textAlign: 'center',
            }}>
              <div style={{ fontSize: 36, marginBottom: 8 }}>
                {detail.status === 'Approved' ? '✅' : '❌'}
              </div>
              <p style={{ margin: 0, fontWeight: 700, color: detail.status === 'Approved' ? '#065f46' : '#991b1b', fontSize: 14 }}>
                {detail.status === 'Approved' ? 'Refunded' : 
                 detail.status === 'WaitingForReturnLabel' ? 'Waiting for Return Label' :
                 detail.status === 'ReturnLabelProvided' ? 'Label Provided' :
                 detail.status === 'Returned' ? 'Item Received' :
                 detail.status === 'Rejected' ? 'Rejected' : detail.status}
              </p>
              {detail.resolvedAt && (
                <p style={{ margin: '6px 0 0', fontSize: 11, color: '#6b7280' }}>
                  At {new Date(detail.resolvedAt).toLocaleString('en-US')}
                </p>
              )}
            </div>
          )}
        </div>
      </div>

      {/* MODAL — Confirm Action */}
      {modal && (
        <div
          onClick={() => !submitting && setModal(null)}
          style={{
            position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.45)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            zIndex: 1000, backdropFilter: 'blur(3px)',
          }}
        >
          <div
            onClick={e => e.stopPropagation()}
            style={{
              background: '#fff', borderRadius: 16, padding: 32,
              width: 420, boxShadow: '0 20px 60px rgba(0,0,0,0.2)',
              animation: 'slideUp 0.2s ease',
            }}
          >
            <style>{`@keyframes slideUp { from { transform: translateY(20px); opacity: 0; } to { transform: translateY(0); opacity: 1; } }`}</style>

            <div style={{ fontSize: 36, textAlign: 'center', marginBottom: 12 }}>
              {modal === 'approve' ? '✅' : modal === 'reject' ? '❌' : '⚖️'}
            </div>
            <h2 style={{ margin: '0 0 6px', textAlign: 'center', fontSize: 18, fontWeight: 700, color: '#111827' }}>
              {modal === 'approve' ? 'Confirm Approval' : modal === 'reject' ? 'Confirm Rejection' : modal === 'adjudicate' ? 'Adjudicate Request' : modal === 'freeze' ? 'Freeze Request' : 'Provide Shipping Label'}
            </h2>
            <p style={{ margin: '0 0 20px', textAlign: 'center', color: '#6b7280', fontSize: 13 }}>
              {modal === 'approve'
                ? `Refund $${detail.totalPrice ? Number(detail.totalPrice).toLocaleString('en-US') : ''} to customer ${detail.buyerUsername || ''}.`
                : modal === 'reject' ? 'The request will be rejected and the customer will be notified.'
                : modal === 'adjudicate' ? 'Decide the final outcome for this return request.'
                : modal === 'freeze' ? 'Freeze this request to hold auto-processes during investigation. Admin note is required.'
                : 'Enter the URL where the customer can download the shipping label.'}
            </p>

            {modal === 'adjudicate' && (
              <div style={{ marginBottom: 20 }}>
                <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#374151', marginBottom: 6 }}>
                  Resolution Path
                </label>
                <select 
                  value={resolutionAction}
                  onChange={e => setResolutionAction(e.target.value)}
                  style={{ width: '100%', padding: '10px', borderRadius: 8, border: '1.5px solid #e5e7eb', fontSize: 13, marginBottom: 12, outline: 'none' }}
                >
                  <option value="RequireReturn">Require Item Return & Refund</option>
                  <option value="KeepItem">Customer Keeps Item & Full Refund</option>
                  <option value="RefundWithoutReturn">Refund without requiring return</option>
                </select>

                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <input 
                    type="checkbox" 
                    id="ebayFund" 
                    checked={isRefundedByEbayFund} 
                    onChange={e => setIsRefundedByEbayFund(e.target.checked)} 
                  />
                  <label htmlFor="ebayFund" style={{ fontSize: 13, color: '#374151' }}>
                    Compensate using eBay Trust Fund
                  </label>
                </div>
              </div>
            )}

            {modal === 'provideLabel' ? (
               <div style={{ marginBottom: 20 }}>
                 <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#374151', marginBottom: 6 }}>
                    Shipping Label URL <span style={{ color: '#ef4444' }}>*</span>
                 </label>
                 <input
                   type="text"
                   placeholder="https://example.com/labels/123.pdf"
                   value={returnLabelUrl}
                   onChange={e => setReturnLabelUrl(e.target.value)}
                   style={{
                     width: '100%', padding: '10px 12px', borderRadius: 8,
                     border: '1.5px solid #e5e7eb', fontSize: 13,
                     fontFamily: "'DM Sans', sans-serif", boxSizing: 'border-box',
                     outline: 'none',
                   }}
                 />
               </div>
            ) : (
              <>
                <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#374151', marginBottom: 6 }}>
                  Admin Note {(modal === 'reject' || modal === 'adjudicate' || modal === 'freeze') && <span style={{ color: '#ef4444' }}>*</span>}
                </label>
                <textarea
                  placeholder={modal === 'approve' ? 'Enter a note (optional)...' : 'Enter a reason/note (required)...'}
                  value={adminNote}
                  onChange={e => setAdminNote(e.target.value)}
                  rows={3}
                  style={{
                    width: '100%', padding: '10px 12px', borderRadius: 8,
                    border: '1.5px solid #e5e7eb', fontSize: 13, resize: 'vertical',
                    fontFamily: "'DM Sans', sans-serif", boxSizing: 'border-box',
                    outline: 'none',
                  }}
                  onFocus={e => e.target.style.borderColor = '#6366f1'}
                  onBlur={e => e.target.style.borderColor = '#e5e7eb'}
                />
              </>
            )}

            {submitError && (
              <p style={{ color: '#ef4444', fontSize: 12, margin: '6px 0 0' }}>⚠️ {submitError}</p>
            )}

            <div style={{ display: 'flex', gap: 10, marginTop: 20 }}>
              <button
                onClick={() => setModal(null)}
                disabled={submitting}
                style={{
                  flex: 1, padding: 12, borderRadius: 10,
                  border: '1.5px solid #e5e7eb', background: '#fff',
                  color: '#374151', fontWeight: 600, fontSize: 14,
                  cursor: 'pointer', fontFamily: "'DM Sans', sans-serif",
                }}
              >
                Cancel
              </button>
              <button
                onClick={handleAction}
                disabled={submitting}
                style={{
                  flex: 1, padding: 12, borderRadius: 10,
                  border: 'none',
                  background: (modal === 'approve' || modal === 'provideLabel') ? '#10b981' : modal === 'reject' ? '#ef4444' : '#6366f1',
                  color: '#fff', fontWeight: 700, fontSize: 14,
                  cursor: submitting ? 'not-allowed' : 'pointer',
                  opacity: submitting ? 0.7 : 1,
                  fontFamily: "'DM Sans', sans-serif",
                }}
              >
                {submitting ? 'Processing...' : 
                 (modal === 'approve' ? 'Confirm Approval' : 
                  modal === 'reject' ? 'Confirm Rejection' :
                  modal === 'adjudicate' ? 'Confirm Adjudication' :
                  modal === 'freeze' ? 'Confirm Freeze' : 'Send Shipping Label')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* LIGHTBOX */}
      {lightbox && (
        <div
          onClick={() => setLightbox(null)}
          style={{
            position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.85)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            zIndex: 2000, cursor: 'zoom-out',
          }}
        >
          <img src={lightbox} alt="evidence" style={{ maxWidth: '90vw', maxHeight: '90vh', borderRadius: 12, boxShadow: '0 20px 80px rgba(0,0,0,0.5)' }} />
          <button
            onClick={() => setLightbox(null)}
            style={{
              position: 'fixed', top: 20, right: 24,
              background: 'rgba(255,255,255,0.15)', border: 'none',
              color: '#fff', width: 40, height: 40, borderRadius: '50%',
              fontSize: 20, cursor: 'pointer', backdropFilter: 'blur(4px)',
            }}
          >
            ✕
          </button>
        </div>
      )}
    </div>
  );
}

/* ── Reusable sub-components ── */

function Card({ title, children }) {
  return (
    <div style={{
      background: '#fff', borderRadius: 14, padding: 24,
      border: '1px solid #e5e7eb', boxShadow: '0 1px 6px rgba(0,0,0,0.04)',
    }}>
      <h3 style={{ margin: '0 0 16px', fontSize: 14, fontWeight: 700, color: '#111827' }}>{title}</h3>
      {children}
    </div>
  );
}

function Row({ label, value }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12, fontSize: 13 }}>
      <span style={{ color: '#6b7280', minWidth: 120 }}>{label}</span>
      <span style={{ color: '#111827', textAlign: 'right', flex: 1 }}>{value}</span>
    </div>
  );
}

function StatusChip({ status }) {
  const map = {
    Pending:               { bg: '#fef3c7', color: '#92400e', label: '⏳ Pending Review'    },
    Approved:              { bg: '#d1fae5', color: '#065f46', label: '✅ Refunded'         },
    Rejected:              { bg: '#fee2e2', color: '#991b1b', label: '❌ Rejected'         },
    WaitingForReturnLabel: { bg: '#e0e7ff', color: '#3730a3', label: '📮 Waiting for Label' },
    ReturnLabelProvided:   { bg: '#dcfce7', color: '#166534', label: '🚚 Label Provided'   },
    Returned:              { bg: '#fef9c3', color: '#854d0e', label: '📦 Item Returned'     },
    Escalated:             { bg: '#ffedd5', color: '#9a3412', label: '⚖️ Escalated'       },
    AwaitingShipment:      { bg: '#e0f2fe', color: '#0369a1', label: '🚢 Awaiting Shipment' },
    InTransit:             { bg: '#faf5ff', color: '#7e22ce', label: '✈️ In Transit'        },
    Delivered:             { bg: '#ecfdf5', color: '#047857', label: '🏁 Delivered'         },
    Completed:             { bg: '#f9fafb', color: '#111827', label: '🏁 Completed'         },
    Frozen:                { bg: '#f1f5f9', color: '#475569', label: '❄️ Frozen'      },
  };
  const s = map[status] || { bg: '#f3f4f6', color: '#374151', label: status };
  return (
    <span style={{
      background: s.bg, color: s.color,
      padding: '6px 16px', borderRadius: 20,
      fontSize: 13, fontWeight: 700,
      fontFamily: "'DM Sans', sans-serif"
    }}>
      {s.label}
    </span>
  );
}

const backBtnStyle = {
  marginTop: 16, padding: '9px 18px',
  background: '#6366f1', color: '#fff',
  border: 'none', borderRadius: 8,
  cursor: 'pointer', fontSize: 13, fontWeight: 600,
  fontFamily: "'DM Sans', sans-serif",
};