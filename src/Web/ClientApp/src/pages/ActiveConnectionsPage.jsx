import React, { useState, useEffect, useCallback, useRef } from 'react';
import { useNotificationHub } from '../hooks/useNotificationHub';

const ActiveConnectionsPage = () => {
  const [connections, setConnections] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const connectionsRef = useRef([]);

  useEffect(() => {
    connectionsRef.current = connections;
  }, [connections]);

  const fetchConnections = useCallback(async () => {
    try {
      setLoading(true);
      const response = await fetch('/api/SystemAdmin/active-ips', {
        headers: {
          'Authorization': `Bearer ${window.localStorage.getItem('token') || ''}`
        },
        credentials: 'include'
      });

      if (!response.ok) {
        throw new Error('You have no permission to access this page.');
      }

      const data = await response.json();
      setConnections(data);
      setError(null);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchConnections();
  }, [fetchConnections]);

  useNotificationHub('ConnectionUpdated', useCallback((data) => {
    // Check if it's a connection update
    // Note: The data structure depends on what we broadcast in backend
    // Backend broadcasts "ConnectionUpdated" with { IpAddress, Timestamp, ActiveCount }
    // Or we could just re-fetch to be safe and simple, or update state manually.

    // To be most accurate, let's re-fetch the full list when a change occurs
    // to ensure we have the correct "lastSeen" for everyone and the list is sorted.
    fetchConnections();
  }, [fetchConnections]));

  const activeCount = connections.length;

  // Calculate newly seen IPs (within last 5 mins)
  const fiveMinsAgo = new Date(Date.now() - 5 * 60000);
  const newlySeenCount = connections.filter(c => new Date(c.lastSeen) >= fiveMinsAgo).length;

  return (
    <div style={{ minHeight: '100vh', background: '#ffffff', fontFamily: "'Inter', sans-serif", padding: '28px 20px' }}>
      <div className="container-fluid" style={{ maxWidth: 1300 }}>
        {/* ── Page Header (Standardized) ── */}
        <div className="text-center mb-5">
          <h1 className="h2 fw-bold text-dark mb-2" style={{ letterSpacing: '-1px' }}>Network Connections</h1>
          <p className="text-secondary mx-auto mb-0" style={{ maxWidth: '600px', fontSize: '0.95rem' }}>
            Monitor real-time inbound traffic and active IP addresses traversing the system perimeter.
          </p>
        </div>

        {error && (
          <div className="alert alert-danger border-0 rounded-4 shadow-sm mb-4">
            <i className="bi bi-exclamation-triangle-fill me-2"></i>
            {error}
          </div>
        )}

        {/* ── Quick Stats Grid ── */}
        <div className="row g-3 mb-5 justify-content-center">
          {[
            { label: 'Active Sessions', value: activeCount, icon: 'bi-hdd-network-fill', color: 'primary' },
            { label: 'New Connections (5m)', value: newlySeenCount, icon: 'bi-activity', color: 'success' },
            { label: 'Tracking Mode', value: 'Live', icon: 'bi-radar', color: 'warning' },
          ].map((stat, idx) => (
            <div key={idx} className="col-12 col-sm-6 col-lg-4">
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
            {/* ── Enhanced Toolbar ── */}
            <div className="px-4 py-3 bg-light border-bottom d-flex align-items-center justify-content-between">
              <div className="fw-bold text-secondary text-uppercase small" style={{ letterSpacing: '0.5px' }}>
                <i className="bi bi-clock-history me-2"></i>Last 60 Minutes History
              </div>
              <button className="btn btn-sm btn-outline-primary rounded-pill px-4 shadow-sm fw-bold transition-all" onClick={fetchConnections}>
                <i className="bi bi-arrow-clockwise me-2"></i>Refresh Table
              </button>
            </div>

            {loading && connections.length === 0 ? (
              <div className="py-5 text-center">
                <div className="spinner-border text-primary" role="status">
                  <span className="visually-hidden">Loading...</span>
                </div>
                <p className="mt-2 text-secondary small fst-italic">Scanning network perimeter...</p>
              </div>
            ) : (
              <div className="table-responsive">
                <table className="table pe-table mb-0 align-middle">
                  <thead className="bg-primary bg-opacity-10 text-primary fw-bold small text-uppercase">
                    <tr>
                      <th className="ps-4 py-3 border-0">Node #</th>
                      <th className="py-3 border-0">IP Address Origin</th>
                      <th className="py-3 border-0">Last Detected Pulse</th>
                      <th className="pe-4 py-3 border-0 text-end">Health Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {connections.length === 0 ? (
                      <tr>
                        <td colSpan="4" className="text-center py-5 text-muted">
                          <i className="bi bi-shield-check h1 d-block mb-3 opacity-25"></i>
                          No active IPs detected currently.
                        </td>
                      </tr>
                    ) : (
                      connections.map((conn, idx) => (
                        <tr key={idx} className="transition-all border-bottom">
                          <td className="ps-4 py-3">
                            <span className="text-secondary small">SEC-{String(idx + 1).padStart(3, '0')}</span>
                          </td>
                          <td className="fw-bold text-dark">
                            <div className="d-flex align-items-center gap-2">
                              <i className="bi bi-router text-primary opacity-75"></i>
                              {conn.ipAddress}
                            </div>
                          </td>
                          <td className="text-secondary small">
                            {new Date(conn.lastSeen).toLocaleString()}
                          </td>
                          <td className="pe-4 text-end">
                            {conn.isWhitelisted ? (
                              <div className="badge bg-success-subtle text-success border border-success-subtle rounded-pill px-3 py-1" style={{ fontSize: '0.7rem', fontWeight: 700 }}>
                                <span className="status-dot status-dot--active me-2" style={{ width: 6, height: 6, display: 'inline-block', borderRadius: '50%', backgroundColor: '#198754' }}></span>
                                SECURED
                              </div>
                            ) : (
                              <div className="badge bg-danger-subtle text-danger border border-danger-subtle rounded-pill px-3 py-1" style={{ fontSize: '0.7rem', fontWeight: 700 }}>
                                <span className="status-dot status-dot--active me-2" style={{ width: 6, height: 6, display: 'inline-block', borderRadius: '50%', backgroundColor: '#dc3545' }}></span>
                                UNTRUSTED
                              </div>
                            )}
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>

        <div className="text-center mt-4">
          <small className="text-muted">Managed by Remote Security Monitor</small>
        </div>
      </div>
    </div>
  );
};

export default ActiveConnectionsPage;
