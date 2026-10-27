import React, { useState, useEffect, useCallback } from 'react';
import './ReviewMonitoring.css';
import { reviewsService } from '../services/reviewsService';

export const ReviewMonitoringPage = () => {
    // Active Tab: 'flagged' | 'seller-feedbacks'
    const [activeTab, setActiveTab] = useState('flagged');

    // Stats State
    const [stats, setStats] = useState(null);
    const [statsLoading, setStatsLoading] = useState(false);

    // Flagged Reviews State
    const [flaggedReviews, setFlaggedReviews] = useState([]);
    const [reviewsLoading, setReviewsLoading] = useState(true);
    const [reviewPage, setReviewPage] = useState(1);
    const [reviewPagination, setReviewPagination] = useState({ totalPages: 1, hasNextPage: false, hasPreviousPage: false });
    
    // Filters for Flagged Reviews
    const [statusFilter, setStatusFilter] = useState('All');
    const [reasonFilter, setReasonFilter] = useState('All');
    const [ratingFilter, setRatingFilter] = useState('');
    const [searchTerm, setSearchTerm] = useState('');
    const [searchInputValue, setSearchInputValue] = useState('');

    // Seller Feedbacks State
    const [sellerFeedbacks, setSellerFeedbacks] = useState([]);
    const [feedbacksLoading, setFeedbacksLoading] = useState(false);
    const [feedbackPage, setFeedbackPage] = useState(1);
    const [feedbackPagination, setFeedbackPagination] = useState({ totalPages: 1, hasNextPage: false, hasPreviousPage: false });
    const [feedbackSearchTerm, setFeedbackSearchTerm] = useState('');
    const [feedbackSearchInputValue, setFeedbackSearchInputValue] = useState('');
    const [tierFilter, setTierFilter] = useState('All');

    // Action State
    const [actionLoading, setActionLoading] = useState({});
    const [selectedReviewModal, setSelectedReviewModal] = useState(null);
    const [toastMessage, setToastMessage] = useState(null);

    // Fetch Stats
    const fetchStats = async () => {
        setStatsLoading(true);
        try {
            const data = await reviewsService.getReviewStatistics();
            setStats(data);
        } catch (error) {
            console.error("Error fetching review statistics:", error);
        } finally {
            setStatsLoading(false);
        }
    };

    // Fetch Flagged Reviews
    const fetchFlaggedReviews = useCallback(async (page = 1) => {
        setReviewsLoading(true);
        try {
            const response = await reviewsService.getFlaggedReviews({
                pageNumber: page,
                pageSize: 10,
                status: statusFilter,
                reason: reasonFilter,
                searchTerm: searchTerm,
                rating: ratingFilter
            });
            setFlaggedReviews(response.items || []);
            setReviewPagination({
                totalPages: response.totalPages || 1,
                hasNextPage: response.hasNextPage,
                hasPreviousPage: response.hasPreviousPage
            });
        } catch (error) {
            console.error("Error fetching flagged reviews:", error);
            showToast("Failed to load review list", "danger");
        } finally {
            setReviewsLoading(false);
        }
    }, [statusFilter, reasonFilter, searchTerm, ratingFilter]);

    // Fetch Seller Feedbacks
    const fetchSellerFeedbacks = useCallback(async (page = 1) => {
        setFeedbacksLoading(true);
        try {
            const response = await reviewsService.getSellerFeedbacks({
                pageNumber: page,
                pageSize: 10,
                searchTerm: feedbackSearchTerm,
                tier: tierFilter
            });
            setSellerFeedbacks(response.items || []);
            setFeedbackPagination({
                totalPages: response.totalPages || 1,
                hasNextPage: response.hasNextPage,
                hasPreviousPage: response.hasPreviousPage
            });
        } catch (error) {
            console.error("Error fetching seller feedbacks:", error);
            showToast("Failed to load seller reputation list", "danger");
        } finally {
            setFeedbacksLoading(false);
        }
    }, [feedbackSearchTerm, tierFilter]);

    // Initial load & Tab changes
    useEffect(() => {
        fetchStats();
    }, []);

    useEffect(() => {
        if (activeTab === 'flagged') {
            fetchFlaggedReviews(reviewPage);
        } else {
            fetchSellerFeedbacks(feedbackPage);
        }
    }, [activeTab, reviewPage, feedbackPage, fetchFlaggedReviews, fetchSellerFeedbacks]);

    const showToast = (text, type = "success") => {
        setToastMessage({ text, type });
        setTimeout(() => setToastMessage(null), 3500);
    };

    // Handle Moderation Action: Keep or Hide
    const handleAction = async (id, status, action) => {
        setActionLoading(prev => ({ ...prev, [id]: true }));
        try {
            await reviewsService.updateReviewStatus(id, { status, action, adminId: 1 });
            showToast(action === 'Hide' 
                ? "Review hidden and penalty applied to the offending account!" 
                : "Review approved and retained publicly!");
            fetchFlaggedReviews(reviewPage);
            fetchStats();
        } catch (error) {
            console.error("Error updating review:", error);
            showToast("An error occurred while updating the review", "danger");
        } finally {
            setActionLoading(prev => ({ ...prev, [id]: false }));
        }
    };

    // Search handlers
    const handleReviewSearchSubmit = (e) => {
        e.preventDefault();
        setReviewPage(1);
        setSearchTerm(searchInputValue);
    };

    const handleFeedbackSearchSubmit = (e) => {
        e.preventDefault();
        setFeedbackPage(1);
        setFeedbackSearchTerm(feedbackSearchInputValue);
    };

    const resetFilters = () => {
        setStatusFilter('All');
        setReasonFilter('All');
        setRatingFilter('');
        setSearchTerm('');
        setSearchInputValue('');
        setReviewPage(1);
    };

    return (
        <div style={{ minHeight: '100vh', background: '#f8fafc', fontFamily: "'Inter', sans-serif", padding: '24px 20px' }}>
            <div className="container-fluid" style={{ maxWidth: 1240 }}>
                
                {/* Toast Notification */}
                {toastMessage && (
                    <div className="position-fixed top-0 end-0 p-3" style={{ zIndex: 1100 }}>
                        <div className={`toast show align-items-center text-white bg-${toastMessage.type === 'danger' ? 'danger' : 'success'} border-0 shadow-lg`} role="alert">
                            <div className="d-flex">
                                <div className="toast-body fw-medium">
                                    <i className={`bi ${toastMessage.type === 'danger' ? 'bi-exclamation-triangle-fill' : 'bi-check-circle-fill'} me-2`}></i>
                                    {toastMessage.text}
                                </div>
                                <button type="button" className="btn-close btn-close-white me-2 m-auto" onClick={() => setToastMessage(null)}></button>
                            </div>
                        </div>
                    </div>
                )}

                {/* Page Header */}
                <div className="d-flex flex-wrap justify-content-between align-items-center mb-4 pb-2 border-bottom">
                    <div>
                        <h1 className="h3 fw-bold text-dark mb-1" style={{ letterSpacing: '-0.5px' }}>
                            <i className="bi bi-shield-check text-primary me-2"></i>
                            Review & Feedback Moderation
                        </h1>
                        <p className="text-secondary mb-0 small">
                            Moderate flagged reviews, resolve seller disputes, and monitor seller feedback ratings on eBay.
                        </p>
                    </div>
                    <div className="d-flex gap-2 mt-2 mt-sm-0">
                        <button 
                            className="btn btn-sm btn-white border shadow-sm rounded-pill px-3 py-1 fw-semibold text-secondary"
                            onClick={() => {
                                fetchStats();
                                if (activeTab === 'flagged') fetchFlaggedReviews(reviewPage);
                                else fetchSellerFeedbacks(feedbackPage);
                            }}
                        >
                            <i className="bi bi-arrow-clockwise me-1"></i> Refresh
                        </button>
                    </div>
                </div>

                {/* Quick Stats Grid */}
                <div className="row g-3 mb-4">
                    <div className="col-12 col-sm-6 col-lg-3">
                        <div className="bg-white border rounded-4 p-3 shadow-sm d-flex align-items-center gap-3 h-100 hover-translate-y">
                            <div className="p-3 bg-danger bg-opacity-10 text-danger rounded-3 fs-4">
                                <i className="bi bi-flag-fill"></i>
                            </div>
                            <div>
                                <span className="text-muted small fw-bold text-uppercase">Pending Moderation</span>
                                <h4 className="mb-0 fw-bold text-dark">
                                    {stats ? stats.flaggedCount : <span className="spinner-border spinner-border-sm"></span>}
                                </h4>
                                <small className="text-muted" style={{ fontSize: '0.72rem' }}>
                                    {stats ? `${stats.pendingCount} pending` : ''}
                                </small>
                            </div>
                        </div>
                    </div>

                    <div className="col-12 col-sm-6 col-lg-3">
                        <div className="bg-white border rounded-4 p-3 shadow-sm d-flex align-items-center gap-3 h-100 hover-translate-y">
                            <div className="p-3 bg-warning bg-opacity-10 text-warning rounded-3 fs-4">
                                <i className="bi bi-shop"></i>
                            </div>
                            <div>
                                <span className="text-muted small fw-bold text-uppercase">Seller Reported</span>
                                <h4 className="mb-0 fw-bold text-dark">
                                    {stats ? stats.sellerReportedCount : <span className="spinner-border spinner-border-sm"></span>}
                                </h4>
                                <small className="text-muted" style={{ fontSize: '0.72rem' }}>Seller disputes on bad reviews</small>
                            </div>
                        </div>
                    </div>

                    <div className="col-12 col-sm-6 col-lg-3">
                        <div className="bg-white border rounded-4 p-3 shadow-sm d-flex align-items-center gap-3 h-100 hover-translate-y">
                            <div className="p-3 bg-primary bg-opacity-10 text-primary rounded-3 fs-4">
                                <i className="bi bi-people-fill"></i>
                            </div>
                            <div>
                                <span className="text-muted small fw-bold text-uppercase">Community Reported</span>
                                <h4 className="mb-0 fw-bold text-dark">
                                    {stats ? stats.communityReportedCount : <span className="spinner-border spinner-border-sm"></span>}
                                </h4>
                                <small className="text-muted" style={{ fontSize: '0.72rem' }}>Spam, counterfeit, abusive</small>
                            </div>
                        </div>
                    </div>

                    <div className="col-12 col-sm-6 col-lg-3">
                        <div className="bg-white border rounded-4 p-3 shadow-sm d-flex align-items-center gap-3 h-100 hover-translate-y">
                            <div className="p-3 bg-success bg-opacity-10 text-success rounded-3 fs-4">
                                <i className="bi bi-star-half"></i>
                            </div>
                            <div>
                                <span className="text-muted small fw-bold text-uppercase">Average Rating</span>
                                <h4 className="mb-0 fw-bold text-dark">
                                    {stats ? `${stats.averageRating} / 5.0` : <span className="spinner-border spinner-border-sm"></span>}
                                </h4>
                                <small className="text-muted" style={{ fontSize: '0.72rem' }}>
                                    {stats ? `Total ${stats.totalReviews} reviews` : ''}
                                </small>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Tabs Navigation */}
                <div className="bg-white border rounded-4 shadow-sm mb-4">
                    <ul className="nav review-nav-tabs px-3 pt-2">
                        <li className="nav-item">
                            <button 
                                className={`nav-link ${activeTab === 'flagged' ? 'active' : ''}`}
                                onClick={() => setActiveTab('flagged')}
                            >
                                <i className="bi bi-exclamation-octagon me-2"></i>
                                Flagged Reviews Queue
                                {stats && stats.flaggedCount > 0 && (
                                    <span className="badge bg-danger ms-2 rounded-pill">{stats.flaggedCount}</span>
                                )}
                            </button>
                        </li>
                        <li className="nav-item">
                            <button 
                                className={`nav-link ${activeTab === 'seller-feedbacks' ? 'active' : ''}`}
                                onClick={() => setActiveTab('seller-feedbacks')}
                            >
                                <i className="bi bi-award me-2"></i>
                                Seller Reputation Monitoring (Feedback)
                            </button>
                        </li>
                    </ul>
                </div>

                {/* ========================================================================= */}
                {/* TAB 1: FLAGGED REVIEWS QUEUE */}
                {/* ========================================================================= */}
                {activeTab === 'flagged' && (
                    <div className="card border-0 shadow-sm rounded-4 overflow-hidden animate-fade-in">
                        {/* Filters Bar */}
                        <div className="p-3 bg-white border-bottom">
                            <div className="row g-2 align-items-center">
                                {/* Search */}
                                <div className="col-12 col-md-4">
                                    <form onSubmit={handleReviewSearchSubmit} className="position-relative">
                                        <i className="bi bi-search position-absolute text-muted" style={{ top: '10px', left: '12px' }}></i>
                                        <input 
                                            type="text"
                                            className="form-control form-control-sm ps-5 rounded-pill border-light-subtle"
                                            placeholder="Search reviewer, product, comment..."
                                            value={searchInputValue}
                                            onChange={(e) => setSearchInputValue(e.target.value)}
                                        />
                                    </form>
                                </div>

                                {/* Status Filter */}
                                <div className="col-6 col-md-2">
                                    <select 
                                        className="form-select form-select-sm rounded-pill"
                                        value={statusFilter}
                                        onChange={(e) => { setStatusFilter(e.target.value); setReviewPage(1); }}
                                    >
                                        <option value="All">Status: All</option>
                                        <option value="PendingReview">Pending Review</option>
                                        <option value="Hidden">Hidden</option>
                                        <option value="Visible">Visible</option>
                                    </select>
                                </div>

                                {/* Reason Filter */}
                                <div className="col-6 col-md-3">
                                    <select 
                                        className="form-select form-select-sm rounded-pill"
                                        value={reasonFilter}
                                        onChange={(e) => { setReasonFilter(e.target.value); setReviewPage(1); }}
                                    >
                                        <option value="All">Reason: All</option>
                                        <option value="SellerReported">Seller Reported</option>
                                        <option value="CommunityReported">Community Reported</option>
                                        <option value="FlaggedBySystem">System Bot Flagged</option>
                                        <option value="LowRating">Low Rating (1-2 stars)</option>
                                    </select>
                                </div>

                                {/* Rating Filter */}
                                <div className="col-6 col-md-2">
                                    <select 
                                        className="form-select form-select-sm rounded-pill"
                                        value={ratingFilter}
                                        onChange={(e) => { setRatingFilter(e.target.value); setReviewPage(1); }}
                                    >
                                        <option value="">Rating: All</option>
                                        <option value="1">1 Star (Very Poor)</option>
                                        <option value="2">2 Stars (Poor)</option>
                                        <option value="3">3 Stars (Average)</option>
                                        <option value="4">4 Stars (Good)</option>
                                        <option value="5">5 Stars (Excellent)</option>
                                    </select>
                                </div>

                                {/* Reset Filter */}
                                <div className="col-6 col-md-1 text-end">
                                    <button 
                                        className="btn btn-sm btn-outline-secondary rounded-pill w-100"
                                        title="Reset filters"
                                        onClick={resetFilters}
                                    >
                                        <i className="bi bi-x-circle"></i>
                                    </button>
                                </div>
                            </div>
                        </div>

                        {/* Reviews Table */}
                        <div className="table-responsive">
                            <table className="table pe-table mb-0 align-middle">
                                <thead>
                                    <tr>
                                        <th style={{ width: '20%' }}>Reviewer</th>
                                        <th style={{ width: '22%' }}>Product & Seller</th>
                                        <th style={{ width: '30%' }}>Content & Feedback</th>
                                        <th style={{ width: '14%' }}>Flag Reason</th>
                                        <th style={{ width: '14%' }} className="text-end pe-4">Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {reviewsLoading ? (
                                        <tr>
                                            <td colSpan="5" className="text-center py-5">
                                                <div className="spinner-border text-primary" role="status">
                                                    <span className="visually-hidden">Loading...</span>
                                                </div>
                                                <div className="text-muted small mt-2">Loading moderation list...</div>
                                            </td>
                                        </tr>
                                    ) : flaggedReviews.length === 0 ? (
                                        <tr>
                                            <td colSpan="5" className="text-center py-5">
                                                <i className="bi bi-check2-circle text-success" style={{ fontSize: '3rem' }}></i>
                                                <h6 className="mt-2 text-dark fw-bold">No flagged reviews found</h6>
                                                <p className="text-muted small mb-0">System is clean, no reviews currently pending moderation.</p>
                                            </td>
                                        </tr>
                                    ) : (
                                        flaggedReviews.map((review) => (
                                            <tr key={review.id}>
                                                {/* Reviewer */}
                                                <td>
                                                    <div className="d-flex align-items-center gap-2">
                                                        <div className="bg-primary bg-opacity-10 text-primary rounded-circle d-flex align-items-center justify-content-center fw-bold shadow-sm" style={{ width: 34, height: 34, fontSize: '0.85rem' }}>
                                                            {review.reviewerName?.charAt(0).toUpperCase() || 'U'}
                                                        </div>
                                                        <div>
                                                            <div className="fw-bold text-dark small">{review.reviewerName}</div>
                                                            <div className="text-muted" style={{ fontSize: '0.7rem' }}>
                                                                ID: #{review.reviewerId} • {review.createdAt ? new Date(review.createdAt).toLocaleDateString('en-US') : ''}
                                                            </div>
                                                        </div>
                                                    </div>
                                                </td>

                                                {/* Product & Seller */}
                                                <td>
                                                    <div className="fw-semibold text-dark small text-truncate" style={{ maxWidth: '200px' }} title={review.productTitle}>
                                                        {review.productTitle}
                                                    </div>
                                                    <div className="text-muted small mt-1 d-flex align-items-center gap-1" style={{ fontSize: '0.72rem' }}>
                                                        <i className="bi bi-shop text-secondary"></i>
                                                        <span>Seller: <strong>{review.sellerName || 'N/A'}</strong></span>
                                                    </div>
                                                </td>

                                                {/* Rating & Comment */}
                                                <td>
                                                    {/* Stars */}
                                                    <div className="d-flex align-items-center gap-1 mb-1">
                                                        {[...Array(5)].map((_, i) => (
                                                            <i 
                                                                key={i} 
                                                                className={`bi bi-star${i < (review.rating || 0) ? '-fill text-warning' : ' text-black-50'}`} 
                                                                style={{ fontSize: '0.75rem' }}
                                                            ></i>
                                                        ))}
                                                        <span className="badge bg-light text-dark fw-semibold ms-1" style={{ fontSize: '0.65rem' }}>
                                                            {review.rating}/5
                                                        </span>
                                                        <span className={`badge ms-1 ${review.status === 'Hidden' ? 'badge-soft-danger' : review.status === 'Visible' ? 'badge-soft-success' : 'badge-soft-warning'}`} style={{ fontSize: '0.65rem' }}>
                                                            {review.status === 'Hidden' ? 'Hidden' : review.status === 'Visible' ? 'Visible' : 'Pending'}
                                                        </span>
                                                    </div>

                                                    {/* Review Comment */}
                                                    <div className="text-secondary small fst-italic mb-1" style={{ lineHeight: '1.4' }}>
                                                        "{review.comment}"
                                                    </div>

                                                    {/* Seller Reply if present */}
                                                    {review.sellerReply && (
                                                        <div className="seller-reply-bubble">
                                                            <div className="fw-bold text-primary d-flex align-items-center gap-1" style={{ fontSize: '0.72rem' }}>
                                                                <i className="bi bi-reply-fill"></i> Seller Reply ({review.sellerName}):
                                                            </div>
                                                            <div className="text-dark small mt-1">"{review.sellerReply}"</div>
                                                        </div>
                                                    )}
                                                </td>

                                                {/* Reason Badges */}
                                                <td>
                                                    <div className="d-flex flex-wrap gap-1">
                                                        {review.reportedBySeller && (
                                                            <span className="badge badge-soft-danger fw-bold" style={{ fontSize: '0.65rem' }}>
                                                                <i className="bi bi-shop me-1"></i>SELLER REPORTED
                                                            </span>
                                                        )}
                                                        {review.reports && review.reports.length > 0 && (
                                                            <button 
                                                                type="button"
                                                                className="badge badge-soft-warning border-0 fw-bold d-inline-flex align-items-center text-decoration-none" 
                                                                style={{ fontSize: '0.65rem', cursor: 'pointer' }}
                                                                onClick={() => setSelectedReviewModal(review)}
                                                            >
                                                                <i className="bi bi-flag-fill me-1"></i>
                                                                {review.reports.length} COMMUNITY REPORTS
                                                            </button>
                                                        )}
                                                        {review.flaggedBySystem && (
                                                            <span className="badge badge-soft-danger fw-bold" style={{ fontSize: '0.65rem' }}>
                                                                <i className="bi bi-robot me-1"></i>BOT: {review.flagReason || 'PROFANITY'}
                                                            </span>
                                                        )}
                                                        {!review.reportedBySeller && (!review.reports || review.reports.length === 0) && !review.flaggedBySystem && (
                                                            <span className="badge badge-soft-secondary fw-bold" style={{ fontSize: '0.65rem' }}>
                                                                {review.rating <= 2 ? 'LOW RATING' : 'PERIODIC AUDIT'}
                                                            </span>
                                                        )}
                                                    </div>
                                                </td>

                                                {/* Actions */}
                                                <td className="pe-4 text-end">
                                                    <div className="d-flex justify-content-end gap-1">
                                                        {/* Detail modal button */}
                                                        <button 
                                                            className="btn btn-sm btn-light rounded-pill px-2"
                                                            title="View report details"
                                                            onClick={() => setSelectedReviewModal(review)}
                                                        >
                                                            <i className="bi bi-eye"></i>
                                                        </button>

                                                        {/* Hide Action */}
                                                        <button 
                                                            className="btn btn-sm btn-outline-danger rounded-pill px-3 fw-semibold"
                                                            onClick={() => handleAction(review.id, 'Hidden', 'Hide')}
                                                            disabled={review.status === 'Hidden' || actionLoading[review.id]}
                                                            style={{ fontSize: '0.75rem' }}
                                                            title="Hide review and warn account"
                                                        >
                                                            {actionLoading[review.id] ? (
                                                                <span className="spinner-border spinner-border-sm"></span>
                                                            ) : (
                                                                <>
                                                                    <i className="bi bi-eye-slash me-1"></i>Hide
                                                                </>
                                                            )}
                                                        </button>

                                                        {/* Keep Action */}
                                                        <button 
                                                            className="btn btn-sm btn-primary rounded-pill px-3 fw-semibold shadow-sm"
                                                            onClick={() => handleAction(review.id, 'Visible', 'Keep')}
                                                            disabled={review.status === 'Visible' || actionLoading[review.id]}
                                                            style={{ fontSize: '0.75rem' }}
                                                            title="Approve and retain publicly"
                                                        >
                                                            {actionLoading[review.id] ? (
                                                                <span className="spinner-border spinner-border-sm"></span>
                                                            ) : (
                                                                <>
                                                                    <i className="bi bi-check-lg me-1"></i>Keep
                                                                </>
                                                            )}
                                                        </button>
                                                    </div>
                                                </td>
                                            </tr>
                                        ))
                                    )}
                                </tbody>
                            </table>
                        </div>

                        {/* Pagination */}
                        <div className="px-4 py-3 bg-white border-top d-flex justify-content-between align-items-center">
                            <span className="text-muted small">
                                Page <strong>{reviewPage}</strong> / {reviewPagination.totalPages}
                            </span>
                            <nav>
                                <ul className="pagination pagination-sm mb-0 gap-1">
                                    <li className={`page-item ${!reviewPagination.hasPreviousPage ? 'disabled' : ''}`}>
                                        <button className="page-link rounded-pill border-0 px-3" onClick={() => setReviewPage(prev => Math.max(1, prev - 1))}>
                                            <i className="bi bi-chevron-left me-1"></i> Prev
                                        </button>
                                    </li>
                                    <li className={`page-item ${!reviewPagination.hasNextPage ? 'disabled' : ''}`}>
                                        <button className="page-link rounded-pill border-0 px-3" onClick={() => setReviewPage(prev => prev + 1)}>
                                            Next <i className="bi bi-chevron-right ms-1"></i>
                                        </button>
                                    </li>
                                </ul>
                            </nav>
                        </div>
                    </div>
                )}

                {/* ========================================================================= */}
                {/* TAB 2: SELLER FEEDBACK & REPUTATION MONITORING */}
                {/* ========================================================================= */}
                {activeTab === 'seller-feedbacks' && (
                    <div className="card border-0 shadow-sm rounded-4 overflow-hidden animate-fade-in">
                        {/* Filters Bar */}
                        <div className="p-3 bg-white border-bottom">
                            <div className="row g-2 align-items-center">
                                <div className="col-12 col-md-6">
                                    <form onSubmit={handleFeedbackSearchSubmit} className="position-relative">
                                        <i className="bi bi-search position-absolute text-muted" style={{ top: '10px', left: '12px' }}></i>
                                        <input 
                                            type="text"
                                            className="form-control form-control-sm ps-5 rounded-pill border-light-subtle"
                                            placeholder="Search seller by name or email..."
                                            value={feedbackSearchInputValue}
                                            onChange={(e) => setFeedbackSearchInputValue(e.target.value)}
                                        />
                                    </form>
                                </div>
                                <div className="col-12 col-md-4">
                                    <select 
                                        className="form-select form-select-sm rounded-pill"
                                        value={tierFilter}
                                        onChange={(e) => { setTierFilter(e.target.value); setFeedbackPage(1); }}
                                    >
                                        <option value="All">Reputation Tier: All</option>
                                        <option value="TopRated">🌟 Top Rated Seller (≥95% positive, ≥4.5 stars)</option>
                                        <option value="AboveStandard">🔹 Above Standard (80 - 94% positive)</option>
                                        <option value="BelowStandard">⚠️ Below Standard (&lt;80% positive - Needs Attention)</option>
                                    </select>
                                </div>
                                <div className="col-12 col-md-2 text-end">
                                    <button 
                                        className="btn btn-sm btn-outline-secondary rounded-pill w-100"
                                        onClick={() => {
                                            setFeedbackSearchInputValue('');
                                            setFeedbackSearchTerm('');
                                            setTierFilter('All');
                                            setFeedbackPage(1);
                                        }}
                                    >
                                        Reset
                                    </button>
                                </div>
                            </div>
                        </div>

                        {/* Feedbacks Table */}
                        <div className="table-responsive">
                            <table className="table pe-table mb-0 align-middle">
                                <thead>
                                    <tr>
                                        <th style={{ width: '30%' }}>Seller</th>
                                        <th style={{ width: '20%' }}>Average Rating</th>
                                        <th style={{ width: '25%' }}>Positive Feedback Rate</th>
                                        <th style={{ width: '15%' }}>Total Reviews</th>
                                        <th style={{ width: '10%' }} className="text-end pe-4">Tier</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {feedbacksLoading ? (
                                        <tr>
                                            <td colSpan="5" className="text-center py-5">
                                                <div className="spinner-border text-primary" role="status">
                                                    <span className="visually-hidden">Loading...</span>
                                                </div>
                                                <div className="text-muted small mt-2">Loading seller reputation data...</div>
                                            </td>
                                        </tr>
                                    ) : sellerFeedbacks.length === 0 ? (
                                        <tr>
                                            <td colSpan="5" className="text-center py-5">
                                                <i className="bi bi-inbox text-muted" style={{ fontSize: '3rem' }}></i>
                                                <h6 className="mt-2 text-dark fw-bold">No matching sellers found</h6>
                                                <p className="text-muted small mb-0">Try adjusting your search query or tier filter.</p>
                                            </td>
                                        </tr>
                                    ) : (
                                        sellerFeedbacks.map((seller) => (
                                            <tr key={seller.id}>
                                                {/* Seller */}
                                                <td>
                                                    <div className="d-flex align-items-center gap-3">
                                                        <div className="bg-primary bg-opacity-10 text-primary rounded-circle d-flex align-items-center justify-content-center fw-bold shadow-sm" style={{ width: 38, height: 38, fontSize: '0.9rem' }}>
                                                            {seller.sellerName?.charAt(0).toUpperCase() || 'S'}
                                                        </div>
                                                        <div>
                                                            <div className="fw-bold text-dark small">{seller.sellerName}</div>
                                                            <div className="text-muted" style={{ fontSize: '0.72rem' }}>{seller.sellerEmail}</div>
                                                        </div>
                                                    </div>
                                                </td>

                                                {/* Average Rating */}
                                                <td>
                                                    <div className="d-flex align-items-center gap-2">
                                                        <span className="fw-bold fs-6 text-dark">{seller.averageRating}</span>
                                                        <div className="d-flex text-warning" style={{ fontSize: '0.8rem' }}>
                                                            {[...Array(5)].map((_, i) => (
                                                                <i 
                                                                    key={i} 
                                                                    className={`bi bi-star${i < Math.round(seller.averageRating || 0) ? '-fill' : ' text-black-50'}`}
                                                                ></i>
                                                            ))}
                                                        </div>
                                                    </div>
                                                    <small className="text-muted" style={{ fontSize: '0.7rem' }}>out of 5.0</small>
                                                </td>

                                                {/* Positive Rate Bar */}
                                                <td>
                                                    <div className="d-flex justify-content-between align-items-center mb-1">
                                                        <span className="fw-bold small text-dark">{seller.positiveRate}% Positive</span>
                                                    </div>
                                                    <div className="progress" style={{ height: '7px', borderRadius: '4px' }}>
                                                        <div 
                                                            className={`progress-bar ${seller.positiveRate >= 95 ? 'bg-success' : seller.positiveRate >= 80 ? 'bg-primary' : 'bg-danger'}`} 
                                                            role="progressbar" 
                                                            style={{ width: `${seller.positiveRate}%` }}
                                                        ></div>
                                                    </div>
                                                </td>

                                                {/* Total Reviews */}
                                                <td>
                                                    <span className="fw-bold text-dark small">{seller.totalReviews}</span>
                                                    <span className="text-muted small ms-1">reviews</span>
                                                </td>

                                                {/* Reputation Tier Badge */}
                                                <td className="pe-4 text-end">
                                                    {seller.reputationTier === 'TopRated' && (
                                                        <span className="badge badge-soft-success fw-bold px-2 py-1" style={{ fontSize: '0.7rem' }}>
                                                            <i className="bi bi-patch-check-fill me-1"></i>TOP RATED
                                                        </span>
                                                    )}
                                                    {seller.reputationTier === 'AboveStandard' && (
                                                        <span className="badge badge-soft-info fw-bold px-2 py-1" style={{ fontSize: '0.7rem' }}>
                                                            <i className="bi bi-shield-check me-1"></i>ABOVE STANDARD
                                                        </span>
                                                    )}
                                                    {seller.reputationTier === 'BelowStandard' && (
                                                        <span className="badge badge-soft-danger fw-bold px-2 py-1" style={{ fontSize: '0.7rem' }}>
                                                            <i className="bi bi-exclamation-triangle-fill me-1"></i>BELOW STANDARD
                                                        </span>
                                                    )}
                                                </td>
                                            </tr>
                                        ))
                                    )}
                                </tbody>
                            </table>
                        </div>

                        {/* Pagination */}
                        <div className="px-4 py-3 bg-white border-top d-flex justify-content-between align-items-center">
                            <span className="text-muted small">
                                Page <strong>{feedbackPage}</strong> / {feedbackPagination.totalPages}
                            </span>
                            <nav>
                                <ul className="pagination pagination-sm mb-0 gap-1">
                                    <li className={`page-item ${!feedbackPagination.hasPreviousPage ? 'disabled' : ''}`}>
                                        <button className="page-link rounded-pill border-0 px-3" onClick={() => setFeedbackPage(prev => Math.max(1, prev - 1))}>
                                            <i className="bi bi-chevron-left me-1"></i> Prev
                                        </button>
                                    </li>
                                    <li className={`page-item ${!feedbackPagination.hasNextPage ? 'disabled' : ''}`}>
                                        <button className="page-link rounded-pill border-0 px-3" onClick={() => setFeedbackPage(prev => prev + 1)}>
                                            Next <i className="bi bi-chevron-right ms-1"></i>
                                        </button>
                                    </li>
                                </ul>
                            </nav>
                        </div>
                    </div>
                )}

                {/* ========================================================================= */}
                {/* DETAIL MODAL: COMMUNITY REPORTS & MODERATION HISTORY */}
                {/* ========================================================================= */}
                {selectedReviewModal && (
                    <div className="modal show d-block" tabIndex="-1" style={{ backgroundColor: 'rgba(15, 23, 42, 0.55)', backdropFilter: 'blur(2px)' }}>
                        <div className="modal-dialog modal-dialog-centered modal-lg">
                            <div className="modal-content border-0 shadow-lg rounded-4 overflow-hidden">
                                <div className="modal-header bg-light border-bottom px-4 py-3">
                                    <h5 className="modal-title fw-bold text-dark h6 mb-0">
                                        <i className="bi bi-card-text text-primary me-2"></i>
                                        Review Details & Violation Reports #{selectedReviewModal.id}
                                    </h5>
                                    <button type="button" className="btn-close" onClick={() => setSelectedReviewModal(null)}></button>
                                </div>
                                <div className="modal-body p-4">
                                    {/* Product and Reviewer details */}
                                    <div className="row g-3 mb-3">
                                        <div className="col-md-6">
                                            <div className="bg-light p-3 rounded-3 h-100">
                                                <small className="text-muted fw-bold text-uppercase d-block mb-1" style={{ fontSize: '0.68rem' }}>Reviewed Product</small>
                                                <div className="fw-bold text-dark small">{selectedReviewModal.productTitle}</div>
                                                <div className="text-muted small mt-1">Seller: <strong>{selectedReviewModal.sellerName || 'N/A'}</strong></div>
                                            </div>
                                        </div>
                                        <div className="col-md-6">
                                            <div className="bg-light p-3 rounded-3 h-100">
                                                <small className="text-muted fw-bold text-uppercase d-block mb-1" style={{ fontSize: '0.68rem' }}>Reviewer</small>
                                                <div className="fw-bold text-dark small">{selectedReviewModal.reviewerName} (ID: #{selectedReviewModal.reviewerId})</div>
                                                <div className="text-muted small mt-1">
                                                    Rating: <strong>{selectedReviewModal.rating}/5 stars</strong> • {selectedReviewModal.createdAt ? new Date(selectedReviewModal.createdAt).toLocaleString('en-US') : ''}
                                                </div>
                                            </div>
                                        </div>
                                    </div>

                                    {/* Review Comment */}
                                    <div className="mb-4">
                                        <label className="form-label fw-bold small text-secondary">Review comment:</label>
                                        <div className="p-3 bg-light rounded-3 text-dark fst-italic border">
                                            "{selectedReviewModal.comment}"
                                        </div>
                                    </div>

                                    {/* Seller Reply if present */}
                                    {selectedReviewModal.sellerReply && (
                                        <div className="mb-4">
                                            <label className="form-label fw-bold small text-primary">
                                                <i className="bi bi-reply-fill me-1"></i>
                                                Seller reply ({selectedReviewModal.sellerName}):
                                            </label>
                                            <div className="seller-reply-bubble">
                                                "{selectedReviewModal.sellerReply}"
                                            </div>
                                        </div>
                                    )}

                                    {/* Community Reports List */}
                                    <div>
                                        <h6 className="fw-bold text-dark small mb-2 d-flex align-items-center gap-2">
                                            <i className="bi bi-flag-fill text-danger"></i>
                                            Community Reports List ({selectedReviewModal.reports?.length || 0}):
                                        </h6>
                                        {!selectedReviewModal.reports || selectedReviewModal.reports.length === 0 ? (
                                            <div className="text-muted small p-3 bg-light rounded-3 text-center">
                                                No direct user reports (flagged by system automated filter or seller).
                                            </div>
                                        ) : (
                                            <div className="list-group list-group-flush border rounded-3 overflow-hidden">
                                                {selectedReviewModal.reports.map((rep, idx) => (
                                                    <div key={idx} className="list-group-item p-3">
                                                        <div className="d-flex justify-content-between align-items-center mb-1">
                                                            <span className="fw-bold text-dark small">
                                                                <i className="bi bi-person me-1 text-secondary"></i>
                                                                {rep.reporterName}
                                                            </span>
                                                            <span className="badge badge-soft-danger fw-bold" style={{ fontSize: '0.65rem' }}>
                                                                {rep.reason?.toUpperCase()}
                                                            </span>
                                                        </div>
                                                        <div className="text-secondary small">
                                                            {rep.description || 'No detailed description provided'}
                                                        </div>
                                                        <div className="text-muted mt-1" style={{ fontSize: '0.68rem' }}>
                                                            {rep.createdAt ? new Date(rep.createdAt).toLocaleString('en-US') : ''}
                                                        </div>
                                                    </div>
                                                ))}
                                            </div>
                                        )}
                                    </div>
                                </div>
                                <div className="modal-footer bg-light px-4 py-3 d-flex justify-content-between">
                                    <button type="button" className="btn btn-sm btn-outline-secondary rounded-pill px-3" onClick={() => setSelectedReviewModal(null)}>
                                        Close
                                    </button>
                                    <div className="d-flex gap-2">
                                        <button 
                                            type="button" 
                                            className="btn btn-sm btn-outline-danger rounded-pill px-4 fw-semibold"
                                            onClick={() => {
                                                handleAction(selectedReviewModal.id, 'Hidden', 'Hide');
                                                setSelectedReviewModal(null);
                                            }}
                                            disabled={selectedReviewModal.status === 'Hidden'}
                                        >
                                            <i className="bi bi-eye-slash me-1"></i> Hide Review (Penalize)
                                        </button>
                                        <button 
                                            type="button" 
                                            className="btn btn-sm btn-primary rounded-pill px-4 fw-semibold shadow-sm"
                                            onClick={() => {
                                                handleAction(selectedReviewModal.id, 'Visible', 'Keep');
                                                setSelectedReviewModal(null);
                                            }}
                                            disabled={selectedReviewModal.status === 'Visible'}
                                        >
                                            <i className="bi bi-check-lg me-1"></i> Approve & Keep
                                        </button>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
};

export default ReviewMonitoringPage;
