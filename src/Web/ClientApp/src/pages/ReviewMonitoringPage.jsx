import React, { useState, useEffect } from 'react';
import './ReviewMonitoring.css';
import { reviewsService } from '../services/reviewsService';

export const ReviewMonitoringPage = () => {
    const [flaggedReviews, setFlaggedReviews] = useState([]);
    const [loading, setLoading] = useState(true);
    const [pageNumber, setPageNumber] = useState(1);
    const [pagination, setPagination] = useState({ totalPages: 1, hasNextPage: false, hasPreviousPage: false });
    const [actionLoading, setActionLoading] = useState({}); // { id: true/false }

    useEffect(() => {
        fetchData(pageNumber);
    }, [pageNumber]);

    const fetchData = async (page) => {
        setLoading(true);
        try {
            const response = await reviewsService.getFlaggedReviews(page);
            setFlaggedReviews(response.items);
            setPagination({
                totalPages: response.totalPages,
                hasNextPage: response.hasNextPage,
                hasPreviousPage: response.hasPreviousPage
            });
        } catch (error) {
            console.error("Error fetching data:", error);
        } finally {
            setLoading(false);
        }
    };

    const handleAction = async (id, status, action) => {
        setActionLoading(prev => ({ ...prev, [id]: true }));
        try {
            await reviewsService.updateReviewStatus(id, { status, action, adminId: 1 });
            fetchData(pageNumber);
        } catch (error) {
            console.error("Error updating review:", error);
        } finally {
            setActionLoading(prev => ({ ...prev, [id]: false }));
        }
    };

    if (loading) return (
        <div className="d-flex justify-content-center align-items-center" style={{ height: '80vh' }}>
            <div className="spinner-border text-primary" role="status">
                <span className="visually-hidden">Loading...</span>
            </div>
        </div>
    );

    // Calculate metrics for stats cards
    const totalReports = flaggedReviews.length;
    const sellerReports = flaggedReviews.filter(r => r.reportedBySeller).length;
    const communityReports = flaggedReviews.filter(r => r.reports && r.reports.length > 0).length;
    const pendingAction = flaggedReviews.filter(r => r.status !== 'Hidden' && r.status !== 'Visible').length;

    return (
        <div style={{ minHeight: '100vh', background: '#ffffff', fontFamily: "'Inter', sans-serif", padding: '28px 20px' }}>
            <div className="container-fluid" style={{ maxWidth: 1200 }}>
                {/* ── Page Header (Standardized) ── */}
                <div className="text-center mb-5">
                    <h1 className="h2 fw-bold text-dark mb-2" style={{ letterSpacing: '-0.5px' }}>Review Monitoring</h1>
                    <p className="text-secondary mx-auto mb-0" style={{ maxWidth: '600px', fontSize: '0.95rem' }}>
                        Maintain marketplace integrity by reviewing flagged feedback, resolving disputes, and ensuring authentic customer reviews.
                    </p>
                </div>

                {/* ── Quick Stats Grid ── */}
                <div className="row g-3 mb-5 justify-content-center">
                    {[
                        { label: 'Total Flagged', value: totalReports, icon: 'bi-flag-fill' },
                        { label: 'Seller Reported', value: sellerReports, icon: 'bi-shop' },
                        { label: 'Community Reports', value: communityReports, icon: 'bi-people-fill' },
                        { label: 'Pending Action', value: pendingAction || totalReports, icon: 'bi-hourglass-split' },
                    ].map((stat, idx) => (
                        <div key={idx} className="col-12 col-sm-6 col-lg-3">
                            <div className="bg-white border rounded-4 p-3 shadow-sm d-flex align-items-center gap-3 h-100 transition-all">
                                <div className="p-3 bg-primary bg-opacity-10 text-primary rounded-3">
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
                        <div className="px-4 py-3 bg-light border-bottom d-flex justify-content-between align-items-center">
                            <h5 className="mb-0 fw-bold text-dark h6">Flagged Reviews Queue</h5>
                            <button className="btn btn-sm btn-outline-primary rounded-pill px-3 py-1 fw-medium" onClick={() => fetchData(pageNumber)}>
                                <i className="bi bi-arrow-clockwise me-1"></i> Refresh
                            </button>
                        </div>

                        <div className="table-responsive">
                            <table className="table pe-table mb-0 align-middle">
                                <thead className="bg-primary bg-opacity-10 text-primary fw-bold small text-uppercase">
                                    <tr>
                                        <th className="ps-4 py-3 border-0" style={{ width: '180px' }}>Reviewer</th>
                                        <th className="py-3 border-0">Product Info</th>
                                        <th className="py-1 border-0" style={{ width: '350px' }}>Review Content</th>
                                        <th className="py-3 border-0">Flag Reason / Metadata</th>
                                        <th className="pe-4 py-3 border-0 text-end">Action</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {flaggedReviews.length === 0 ? (
                                        <tr>
                                            <td colSpan="5" className="text-center py-5 text-muted">
                                                <i className="bi bi-check2-circle h1 d-block mb-3 opacity-25"></i>
                                                No reviews currently require monitoring.
                                            </td>
                                        </tr>
                                    ) : (
                                        flaggedReviews.map(review => (
                                            <tr key={review.id} style={{ opacity: review.status === 'Hidden' ? 0.6 : 1 }}>
                                                <td className="ps-4 py-3">
                                                    <div className="d-flex align-items-center gap-2">
                                                        <div className="bg-primary bg-opacity-10 text-primary rounded-circle d-flex align-items-center justify-content-center fw-bold" style={{ width: 32, height: 32, fontSize: '0.8rem' }}>
                                                            {review.reviewerName?.charAt(0) || 'U'}
                                                        </div>
                                                        <div>
                                                            <div className="fw-bold text-dark small">{review.reviewerName}</div>
                                                            <div className="text-muted" style={{ fontSize: '0.7rem' }}>ID: #{review.reviewerId}</div>
                                                        </div>
                                                    </div>
                                                </td>
                                                <td>
                                                    <div className="fw-medium text-dark small" style={{ maxWidth: '150px', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }} title={review.productTitle}>
                                                        {review.productTitle}
                                                    </div>
                                                </td>
                                                <td>
                                                    <div className="d-flex align-items-center gap-1 mb-1">
                                                        {[...Array(5)].map((_, i) => (
                                                            <i key={i} className={`bi bi-star${i < review.rating ? '-fill text-warning' : ' text-light text-opacity-75'}`} style={{ fontSize: '0.75rem' }}></i>
                                                        ))}
                                                    </div>
                                                    <p className="mb-0 text-secondary small text-truncate-2" style={{ lineHeight: '1.4', fontStyle: 'italic' }}>
                                                        "{review.comment}"
                                                    </p>
                                                </td>
                                                <td>
                                                    <div className="d-flex flex-wrap gap-1">
                                                        {review.reportedBySeller && (
                                                            <span className="badge bg-danger bg-opacity-10 text-danger border border-danger border-opacity-25 fw-bold" style={{ fontSize: '0.65rem' }}>
                                                                SELLER REPORTED
                                                            </span>
                                                        )}
                                                        {review.reports && review.reports.map((rep, idx) => (
                                                            <span key={idx} className="badge bg-warning bg-opacity-10 text-dark border border-warning border-opacity-25 fw-bold" style={{ fontSize: '0.65rem' }}>
                                                                {rep.reason?.toUpperCase()}
                                                            </span>
                                                        ))}
                                                        {!review.reportedBySeller && (!review.reports || review.reports.length === 0) && (
                                                            <span className="badge bg-secondary bg-opacity-10 text-secondary border border-secondary border-opacity-25 fw-bold" style={{ fontSize: '0.65rem' }}>
                                                                {review.flagReason?.toUpperCase() || (review.rating === 1 ? 'LOW RATING' : 'SYSTEM SCAN')}
                                                            </span>
                                                        )}
                                                    </div>
                                                </td>
                                                <td className="pe-4 text-end">
                                                    <div className="d-flex justify-content-end gap-2">
                                                        <button 
                                                            className="btn btn-sm btn-outline-danger rounded-pill px-3 transition-all"
                                                            onClick={() => handleAction(review.id, 'Hidden', 'Hide')}
                                                            disabled={review.status === 'Hidden' || actionLoading[review.id]}
                                                            style={{ fontSize: '0.75rem', fontWeight: 600 }}
                                                        >
                                                            {actionLoading[review.id] ? <span className="spinner-border spinner-border-sm me-1"></span> : 'Hide'}
                                                        </button>
                                                        <button 
                                                            className="btn btn-sm btn-primary rounded-pill px-3 transition-all shadow-sm"
                                                            onClick={() => handleAction(review.id, 'Visible', 'Keep')}
                                                            disabled={review.status === 'Visible' || actionLoading[review.id]}
                                                            style={{ fontSize: '0.75rem', fontWeight: 600 }}
                                                        >
                                                            {actionLoading[review.id] ? <span className="spinner-border spinner-border-sm me-1"></span> : 'Keep'}
                                                        </button>
                                                    </div>
                                                </td>
                                            </tr>
                                        ))
                                    )}
                                </tbody>
                            </table>
                        </div>

                        {/* ── Table Footer (Pagination) ── */}
                        <div className="px-4 py-3 bg-light border-top d-flex justify-content-between align-items-center">
                            <span className="text-muted small">
                                Page <strong>{pageNumber}</strong> of {pagination.totalPages}
                            </span>
                            <nav>
                                <ul className="pagination pagination-sm mb-0 gap-1">
                                    <li className={`page-item ${!pagination.hasPreviousPage ? 'disabled' : ''}`}>
                                        <button className="page-link rounded-pill border-0 fw-bold px-3 py-2" onClick={() => setPageNumber(prev => prev - 1)}>
                                            <i className="bi bi-chevron-left"></i>
                                        </button>
                                    </li>
                                    <li className={`page-item ${!pagination.hasNextPage ? 'disabled' : ''}`}>
                                        <button className="page-link rounded-pill border-0 fw-bold px-3 py-2" onClick={() => setPageNumber(prev => prev + 1)}>
                                            <i className="bi bi-chevron-right"></i>
                                        </button>
                                    </li>
                                </ul>
                            </nav>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};
