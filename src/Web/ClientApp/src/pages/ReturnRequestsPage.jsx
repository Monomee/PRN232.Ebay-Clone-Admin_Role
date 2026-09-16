import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import returnRequestService from '../services/returnRequestService';
import { useNotificationHub } from '../hooks/useNotificationHub.js';

const STATUS_TABS = [
  { key: 'NeedAction', label: 'Need Action',  color: '#ef4444' }, // Pending + Escalated + Frozen
  { key: 'InProgress', label: 'In Progress',  color: '#6366f1' }, // WaitingLabel, Provided, Awaiting, Transit
  { key: 'Approved',   label: 'Refunded',     color: '#10b981' },
  { key: 'Rejected',   label: 'Rejected',     color: '#94a3b8' },
];

const statusBadge = {
  Pending:               { bg: '#fef3c7', color: '#92400e', text: 'Pending Analysis' },
  Approved:              { bg: '#d1fae5', color: '#065f46', text: 'Refunded'         },
  Rejected:              { bg: '#fee2e2', color: '#991b1b', text: 'Rejected'         },
  WaitingForReturnLabel: { bg: '#e0e7ff', color: '#3730a3', text: 'Waiting for Label' },
  ReturnLabelProvided:   { bg: '#dcfce7', color: '#166534', text: 'Label Provided'   },
  Returned:              { bg: '#fef9c3', color: '#854d0e', text: 'Item Returned'     },
  Escalated:             { bg: '#ffedd5', color: '#9a3412', text: 'Escalated'       },
  AwaitingShipment:      { bg: '#e0f2fe', color: '#0369a1', text: 'Awaiting Shipment' },
  InTransit:             { bg: '#faf5ff', color: '#7e22ce', text: 'In Transit'        },
  Delivered:             { bg: '#ecfdf5', color: '#047857', text: 'Delivered'         },
  Completed:             { bg: '#f9fafb', color: '#111827', text: 'Completed'         },
  Frozen:                { bg: '#f1f5f9', color: '#475569', text: 'Frozen / Investigation' },
};

export default function ReturnRequestsPage() {
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState('NeedAction');
  const [currentPage, setCurrentPage] = useState(1);
  const [requests, setRequests] = useState([]);
  const [loading, setLoading] = useState(false);
  const [searchId, setSearchId] = useState('');
  const [error, setError] = useState('');
  const PAGE_SIZE = 7;

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const data = await returnRequestService.getReturnRequests(''); // Fetch all requests
      setRequests(data);
    } catch {
      setError('Could not load requests. Please try again.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData(); // Sẽ chỉ fetch 1 lần khi load (bỏ activeTab ra khỏi dependency)
  }, [fetchData]);

  useEffect(() => {
    setCurrentPage(1);
  }, [activeTab, searchId]);

  useNotificationHub(['ReturnRequestCreated', 'ReturnRequestUpdated'], useCallback(() => {
      // Re-fetch data when a return request is created or updated
      fetchData();
  }, [fetchData]));

  // Derived Statistics
  const totalCount = requests.length;
  const pendingCount = requests.filter(r => r.status === 'Pending' || r.status === 'Escalated' || r.status === 'Frozen').length;
  const inProgressCount = requests.filter(r => 
    ['WaitingForReturnLabel', 'ReturnLabelProvided', 'AwaitingShipment', 'InTransit', 'Delivered'].includes(r.status)
  ).length;
  const approvedCount = requests.filter(r => r.status === 'Approved' || r.status === 'Completed').length;

  const getRequestsForTab = () => {
    if (activeTab === 'NeedAction') {
      return requests.filter(r => r.status === 'Pending' || r.status === 'Escalated' || r.status === 'Frozen');
    }
    if (activeTab === 'InProgress') {
      return requests.filter(r => ['WaitingForReturnLabel', 'ReturnLabelProvided', 'AwaitingShipment', 'InTransit', 'Delivered'].includes(r.status));
    }
    return requests.filter((r) => r.status === activeTab);
  };

  const requestsForTab = getRequestsForTab();
  const filtered = requestsForTab.filter((r) =>
    searchId ? String(r.orderId).includes(searchId.trim()) : true
  );

  // Pagination Logic
  const totalItems = filtered.length;
  const totalPages = Math.ceil(totalItems / PAGE_SIZE);
  const startIndex = (currentPage - 1) * PAGE_SIZE;
  const paginatedItems = filtered.slice(startIndex, startIndex + PAGE_SIZE);

  const badge = (status) => {
    const b = statusBadge[status] || { color: '#64748b', text: status };
    return (
      <span className="d-inline-flex align-items-center gap-2 px-2 py-1 rounded-pill" style={{ fontSize: '0.7rem', fontWeight: 700, letterSpacing: '0.3px', background: 'rgba(0,0,0,0.03)' }}>
        <span style={{ width: 6, height: 6, borderRadius: '50%', background: b.color }}></span>
        <span style={{ color: '#1e293b' }}>{b.text?.toUpperCase()}</span>
      </span>
    );
  };

  const getEscalationTimer = (status, createdAt) => {
    if (status !== 'Pending' || !createdAt) return null;
    const createdDate = new Date(createdAt);
    const escalationDate = new Date(createdDate.getTime() + 3 * 24 * 60 * 60 * 1000);
    const now = new Date();
    const diff = escalationDate - now;

    if (diff <= 0) return <span className="badge bg-danger bg-opacity-10 text-danger border border-danger border-opacity-25 rounded-pill px-2" style={{ fontSize: '0.65rem' }}>DUE TO ESCALATE</span>;

    const days = Math.floor(diff / (24 * 60 * 60 * 1000));
    const hours = Math.floor((diff % (24 * 60 * 60 * 1000)) / (60 * 60 * 1000));
    
    return (
      <div className="d-flex align-items-center gap-1 text-warning mt-1" style={{ fontSize: '0.7rem', fontWeight: 600 }}>
        <i className="bi bi-hourglass-split"></i>
        <span>{days}d {hours}h left</span>
      </div>
    );
  };

  return (
    <div style={{ minHeight: '100vh', background: '#ffffff', fontFamily: "'Inter', sans-serif", padding: '28px 20px' }}>
      <div className="container-fluid" style={{ maxWidth: 1200 }}>
        {/* ── Page Header (Standardized) ── */}
        <div className="text-center mb-5">
          <h1 className="h2 fw-bold text-dark mb-2" style={{ letterSpacing: '-0.5px' }}>Return & Refund Management</h1>
          <p className="text-secondary mx-auto mb-0" style={{ maxWidth: '600px', fontSize: '0.95rem' }}>
            Review, adjudicate, and process customer return requests to ensure fair outcomes and marketplace trust.
          </p>
        </div>

        {/* ── Quick Stats Grid ── */}
        <div className="row g-3 mb-5 justify-content-center">
          {[
            { label: 'Total Requests', value: totalCount, icon: 'bi-box-seam', color: 'primary' },
            { label: 'Need Action', value: pendingCount, icon: 'bi-exclamation-octagon', color: 'danger' },
            { label: 'In Progress', value: inProgressCount, icon: 'bi-arrow-repeat', color: 'info' },
            { label: 'Refunded / Finished', value: approvedCount, icon: 'bi-check-circle-fill', color: 'success' },
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
            {/* ── Table Toolbar ── */}
            <div className="px-4 py-3 bg-light border-bottom">
              <div className="row align-items-center g-3">
                <div className="col-md-7">
                  <div className="d-flex flex-wrap gap-1 p-1 bg-white border rounded-pill shadow-sm" style={{ width: 'fit-content' }}>
                    {STATUS_TABS.map((tab) => (
                      <button
                        key={tab.key}
                        onClick={() => { setActiveTab(tab.key); setSearchId(''); }}
                        className={`btn btn-sm rounded-pill px-3 py-1 fw-bold transition-all ${activeTab === tab.key ? 'btn-primary shadow-sm' : 'btn-link text-secondary text-decoration-none'}`}
                        style={{ fontSize: '0.75rem' }}
                      >
                        {tab.label}
                        {activeTab === tab.key && requestsForTab.length > 0 && (
                          <span className="badge bg-white text-primary ms-2 rounded-pill px-2">{requestsForTab.length}</span>
                        )}
                      </button>
                    ))}
                  </div>
                </div>
                <div className="col-md-5">
                  <div className="position-relative">
                    <i className="bi bi-search position-absolute top-50 start-0 translate-middle-y ms-3 text-secondary" style={{ fontSize: '0.85rem' }}></i>
                    <input
                      type="text"
                      className="form-control form-control-sm border-0 bg-white rounded-pill ps-5 py-2 shadow-sm"
                      placeholder="Search Order ID..."
                      value={searchId}
                      onChange={(e) => setSearchId(e.target.value)}
                      style={{ fontSize: '0.85rem' }}
                    />
                  </div>
                </div>
              </div>
            </div>

            <div className="table-responsive">
              <table className="table pe-table mb-0 align-middle">
                <thead className="bg-primary bg-opacity-10 text-primary fw-bold small text-uppercase">
                  <tr>
                    <th className="ps-4 py-3 border-0" style={{ width: '80px' }}>ID</th>
                    <th className="py-3 border-0" style={{ width: '120px' }}>Order #</th>
                    <th className="py-3 border-0">Customer</th>
                    <th className="py-3 border-0" style={{ width: '250px' }}>Return Reason</th>
                    <th className="py-3 border-0">Price</th>
                    <th className="py-3 border-0">Requested</th>
                    <th className="pe-4 py-3 border-0 text-end">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr>
                      <td colSpan="7" className="text-center py-5">
                        <div className="spinner-border text-primary" role="status">
                          <span className="visually-hidden">Loading...</span>
                        </div>
                      </td>
                    </tr>
                  ) : filtered.length === 0 ? (
                    <tr>
                      <td colSpan="7" className="text-center py-5 text-muted">
                        <i className="bi bi-inbox h1 d-block mb-3 opacity-25"></i>
                        {searchId ? `No orders found for #${searchId}` : 'No return requests found.'}
                      </td>
                    </tr>
                  ) : (
                    paginatedItems.map(r => (
                      <tr key={r.id} onClick={() => navigate(`/return-requests/${r.id}`)} style={{ cursor: 'pointer' }} className="transition-all">
                        <td className="ps-4 py-3 text-secondary fw-medium">#{r.id}</td>
                        <td>
                          <span className="fw-bold text-primary">#{r.orderId}</span>
                        </td>
                        <td>
                          <div className="d-flex align-items-center gap-2">
                            <div className="bg-primary bg-opacity-10 text-primary rounded-circle d-flex align-items-center justify-content-center fw-bold shadow-sm" style={{ width: 32, height: 32, fontSize: '0.8rem' }}>
                              {r.buyerUsername?.charAt(0) || 'U'}
                            </div>
                            <div>
                              <div className="fw-bold text-dark small">{r.buyerUsername || 'Anonymous'}</div>
                              <div className="text-muted" style={{ fontSize: '0.7rem' }}>{r.buyerEmail}</div>
                            </div>
                          </div>
                        </td>
                        <td>
                          <div className="text-dark small text-truncate" style={{ maxWidth: 200 }} title={r.reason}>
                            {r.reason || 'No reason provided'}
                          </div>
                        </td>
                        <td>
                          <span className="fw-bold text-dark">{r.totalPrice ? `$${Number(r.totalPrice).toLocaleString('en-US')}` : '—'}</span>
                        </td>
                        <td className="text-secondary small">
                          <div>{r.createdAt ? new Date(r.createdAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : '—'}</div>
                          {getEscalationTimer(r.status, r.createdAt)}
                        </td>
                        <td className="pe-4 text-end">
                          {badge(r.status)}
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            {/* ── Pagination Controls ── */}
            {totalPages > 1 && (
              <div className="px-4 py-3 bg-white border-top d-flex align-items-center justify-content-between">
                <div className="text-secondary small">
                  Showing <span className="fw-bold text-dark">{startIndex + 1}</span> to <span className="fw-bold text-dark">{Math.min(startIndex + PAGE_SIZE, totalItems)}</span> of <span className="fw-bold text-dark">{totalItems}</span> results
                </div>
                <nav>
                  <ul className="pagination pagination-sm mb-0 gap-1">
                    <li className={`page-item ${currentPage === 1 ? 'disabled' : ''}`}>
                      <button className="page-link rounded-pill border-0 px-3" onClick={() => setCurrentPage(p => Math.max(1, p - 1))}>
                        <i className="bi bi-chevron-left"></i>
                      </button>
                    </li>
                    {[...Array(totalPages)].map((_, i) => (
                      <li key={i} className={`page-item ${currentPage === i + 1 ? 'active' : ''}`}>
                        <button className="page-link rounded-pill border-0 px-3 fw-bold" onClick={() => setCurrentPage(i + 1)}>
                          {i + 1}
                        </button>
                      </li>
                    ))}
                    <li className={`page-item ${currentPage === totalPages ? 'disabled' : ''}`}>
                      <button className="page-link rounded-pill border-0 px-3" onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}>
                        <i className="bi bi-chevron-right"></i>
                      </button>
                    </li>
                  </ul>
                </nav>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}