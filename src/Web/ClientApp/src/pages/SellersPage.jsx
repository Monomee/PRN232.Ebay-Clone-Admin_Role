import React, { useState, useEffect, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { Modal, ModalHeader, ModalBody, ModalFooter, Button } from 'reactstrap';
import financeService from '../services/financeService';
import { apiRequest } from '../services/httpClient';
import { ToastMessage } from '../components/ToastMessage';
import { LoadingIndicator } from '../components/LoadingIndicator';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import './PayoutEnginePage.css'; // Inherit modern table/badge styles

export const SellersPage = () => {
    const [wallets, setWallets] = useState([]);
    const [criteria, setCriteria] = useState(null);
    const [loading, setLoading] = useState(true);
    const [toast, setToast] = useState({ message: '', type: 'success' });
    const [rtIndicator, setRtIndicator] = useState(false); // flashes when real-time update arrives
    const [timeLeft, setTimeLeft] = useState('');

    // Frontend Pagination, Search, Filter & Sort State
    const [search, setSearch] = useState("");
    const [currentPage, setCurrentPage] = useState(1);
    const [pageSize, setPageSize] = useState(10);
    const [filterLevel, setFilterLevel] = useState("All");
    const [filterStatus, setFilterStatus] = useState("All");
    const [sortConfig, setSortConfig] = useState({ key: 'availableBalance', direction: 'desc' });
    const [showGuide, setShowGuide] = useState(false);

    const [modal, setModal] = useState(false);
    const [selectedSeller, setSelectedSeller] = useState(null);

    const toggleModal = (seller = null) => {
        setSelectedSeller(seller);
        setModal(!modal);
    };

    // ── Countdown Timer ──────────────────────────────────────────────────────
    useEffect(() => {
        const calculateTimeLeft = () => {
            if (!criteria || !criteria.nextEvaluationDate) return "Calculating...";

            // Append Z to enforce UTC if the backend didn't
            const dateStr = criteria.nextEvaluationDate.endsWith('Z') ? criteria.nextEvaluationDate : criteria.nextEvaluationDate + 'Z';
            const target = new Date(dateStr);
            const now = new Date();
            const diff = target - now;

            if (diff <= 0) return "Evaluating now... (Refresh to check)";

            const MathFloor = Math.floor;
            const days = MathFloor(diff / (1000 * 60 * 60 * 24));
            const hours = MathFloor((diff / (1000 * 60 * 60)) % 24);
            const mins = MathFloor((diff / 1000 / 60) % 60);
            const secs = MathFloor((diff / 1000) % 60);

            return `${days}d ${hours.toString().padStart(2, '0')}h ${mins.toString().padStart(2, '0')}m ${secs.toString().padStart(2, '0')}s`;
        };

        setTimeLeft(calculateTimeLeft());
        const timer = setInterval(() => {
            setTimeLeft(calculateTimeLeft());
        }, 1000);

        return () => clearInterval(timer);
    }, [criteria]);

    // ── Real-time: connect to SellerHub ──────────────────────────────────────
    useEffect(() => {
        const connection = new HubConnectionBuilder()
            .withUrl('/hubs/seller')
            .withAutomaticReconnect()
            .configureLogging(LogLevel.Warning)
            .build();

        connection.start()
            .then(() => {
                console.log('[SellerHub] Connected ✓');

                connection.on('UpdateSellerWallet', (data) => {
                    console.log('[SellerHub] Wallet update:', data);
                    setWallets(prev => prev.map(w =>
                        w.sellerId === data.sellerId
                            ? { ...w, availableBalance: data.availableBalance, pendingBalance: data.pendingBalance, totalWithdrawn: data.totalWithdrawn }
                            : w
                    ));
                    // Flash the indicator briefly
                    setRtIndicator(true);
                    setTimeout(() => setRtIndicator(false), 1500);
                });

                connection.on('UpdateSellerMetrics', (metrics) => {
                    console.log('[SellerHub] Metrics update:', metrics);
                    setWallets(prev => prev.map(w =>
                        w.sellerId === metrics.id
                            ? { 
                                ...w, 
                                sellerLevel: metrics.sellerLevel, 
                                status: metrics.status, 
                                transactionCount: metrics.transactionCount, 
                                totalSales: metrics.totalSales, 
                                unresolvedCases: metrics.unresolvedCases, 
                                defectRate: metrics.defectRate, 
                                lateRate: metrics.lateRate,
                                activeDays: metrics.activeDays
                              }
                            : w
                    ));
                    // Flash the indicator briefly
                    setRtIndicator(true);
                    setTimeout(() => setRtIndicator(false), 1500);
                });
            })
            .catch(err => console.error('[SellerHub] Connection failed:', err));

        return () => { connection.stop(); };
    }, []);
    // ─────────────────────────────────────────────────────────────────────────

    useEffect(() => {
        loadWallets();
        loadCriteria();
    }, []);

    const loadCriteria = async () => {
        try {
            const data = await apiRequest('/api/Users/seller-level-criteria');
            setCriteria(data);
        } catch (e) { console.error("Failed to load criteria", e); }
    };

    const loadWallets = async () => {
        try {
            setLoading(true);
            const [walletData, metricsData] = await Promise.all([
                financeService.getSellerWallets(),
                apiRequest('/api/Users/seller-metrics')
            ]);

            const metrics = metricsData.items || metricsData;

            // Merge data
            const merged = walletData.map(w => {
                const metric = metrics.find(m => m.id === w.sellerId);
                return {
                    ...w,
                    sellerLevel: metric?.sellerLevel || 'BelowStandard',
                    status: metric?.status || 'Unknown',
                    transactionCount: metric?.transactionCount || 0,
                    totalSales: metric?.totalSales || 0,
                    unresolvedCases: metric?.unresolvedCases || 0,
                    defectRate: metric?.defectRate || 0,
                    lateRate: metric?.lateRate || 0,
                    activeDays: metric?.activeDays || 0
                };
            });

            setWallets(merged);
        } catch (error) {
            setToast({ message: 'Failed to load seller data', type: 'error' });
        } finally {
            setLoading(false);
        }
    };
    const resetFilters = () => {
        setSearch("");
        setFilterLevel("All");
        setFilterStatus("All");
        setSortConfig({ key: 'availableBalance', direction: 'desc' });
        setCurrentPage(1);
    };
    // Derived Data: Filtering & Sorting
    const filteredWallets = useMemo(() => {
        let result = [...wallets];

        // 1. Search filter
        if (search.trim()) {
            const s = search.toLowerCase();
            result = result.filter(w =>
                (w.sellerName && w.sellerName.toLowerCase().includes(s)) ||
                (w.sellerId && w.sellerId.toString().includes(s))
            );
        }

        // 2. Level filter
        if (filterLevel !== "All") {
            result = result.filter(w => w.sellerLevel === filterLevel);
        }

        // 3. Status filter
        if (filterStatus !== "All") {
            result = result.filter(w => w.status === filterStatus);
        }

        // 4. Sorting
        if (sortConfig !== null) {
            result.sort((a, b) => {
                let aValue = a[sortConfig.key];
                let bValue = b[sortConfig.key];

                // Fallback for null/undefined values to prevent sorting from breaking
                if (aValue === null || aValue === undefined) aValue = "";
                if (bValue === null || bValue === undefined) bValue = "";

                if (sortConfig.key === 'sellerLevel') {
                    const levelRank = { 'TopRated': 3, 'AboveStandard': 2, 'BelowStandard': 1 };
                    aValue = levelRank[aValue] || 0;
                    bValue = levelRank[bValue] || 0;
                } else {
                    if (typeof aValue === 'string') aValue = aValue.toLowerCase();
                    if (typeof bValue === 'string') bValue = bValue.toLowerCase();
                }

                if (aValue < bValue) return sortConfig.direction === 'asc' ? -1 : 1;
                if (aValue > bValue) return sortConfig.direction === 'asc' ? 1 : -1;
                return 0;
            });
        }

        return result;
    }, [wallets, search, filterLevel, filterStatus, sortConfig]);

    const requestSort = (key) => {
        let direction = 'asc';
        if (sortConfig && sortConfig.key === key && sortConfig.direction === 'asc') {
            direction = 'desc';
        }
        setSortConfig({ key, direction });
        setCurrentPage(1);
    };

    const getSortIcon = (key) => {
        if (!sortConfig || sortConfig.key !== key) return <i className="bi bi-arrow-down-up opacity-25 ms-1" style={{ fontSize: '0.7em' }}></i>;

        return (
            <span className="d-inline-flex align-items-center text-primary">
                {sortConfig.direction === 'asc'
                    ? <i className="bi bi-arrow-up ms-1" style={{ fontSize: '0.8em' }}></i>
                    : <i className="bi bi-arrow-down ms-1" style={{ fontSize: '0.8em' }}></i>}
            </span>
        );
    };

    // Derived Data: Pagination
    const paginatedWallets = useMemo(() => {
        const start = (currentPage - 1) * pageSize;
        return filteredWallets.slice(start, start + pageSize);
    }, [filteredWallets, currentPage, pageSize]);

    const totalPages = Math.ceil(filteredWallets.length / pageSize);

    const stats = useMemo(() => {
        return {
            total: wallets.length,
            active: wallets.filter(w => w.status === 'Active').length,
            totalAvailable: wallets.reduce((sum, w) => sum + (w.availableBalance || 0), 0),
            totalPending: wallets.reduce((sum, w) => sum + (w.pendingBalance || 0), 0)
        };
    }, [wallets]);

    const formatCurrency = (amount) => {
        return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount);
    };

    const renderLevelBadge = (wallet) => {
        const level = wallet.sellerLevel;
        let icon = "bi-exclamation-triangle";
        let colorClass = "text-danger";
        let label = "Below Std";

        if (level === 'TopRated') {
            icon = "bi-trophy-fill";
            colorClass = "text-success";
            label = "Top Rated";
        } else if (level === 'AboveStandard') {
            icon = "bi-shield-check";
            colorClass = "text-warning";
            label = "Above Std";
        }

        return (
            <div
                className={`d-flex align-items-center ${colorClass} fw-bold pe-badge-interactive`}
                onClick={() => toggleModal(wallet)}
                title={level}
                style={{ fontSize: '0.85rem' }}
            >
                <i className={`bi ${icon} me-2`} style={{ fontSize: '1rem' }}></i>
                <span className="hide-text-mobile">{label}</span>
            </div>
        );
    };

    return (
        <div className="container-fluid py-4 bg-white" style={{ minHeight: '100vh' }}>
            <ToastMessage
                message={toast.message}
                type={toast.type}
                onClose={() => setToast({ message: '', type: 'success' })}
            />

            <div className="mb-5">
                {/* ── Top Row: Title ── */}
                <div className="d-flex flex-column align-items-center mb-4 w-100 text-center">
                    <div className="d-flex align-items-center gap-3">
                        <h1 className="h2 fw-bold mb-0 text-dark" style={{ letterSpacing: '-1px' }}>Sellers Overview</h1>
                        <button
                            className={`btn btn-sm rounded-circle border-0 d-flex align-items-center justify-content-center transition-all ${showGuide ? 'btn-primary' : 'btn-outline-secondary'}`}
                            style={{ width: '36px', height: '36px', transition: 'all 0.3s ease' }}
                            onClick={() => setShowGuide(!showGuide)}
                            title="Toggle Seller Leveling Guide"
                        >
                            <i className={`bi ${showGuide ? 'bi-info-circle-fill' : 'bi-info-circle'}`} style={{ fontSize: '1.2rem' }}></i>
                        </button>
                    </div>
                    <p className="text-secondary mb-0 mt-2" style={{ fontSize: '0.95rem', lineHeight: '1.4' }}>
                        Monitor seller performance, manage account statuses, and track wallet balances in real-time.
                    </p>
                    {rtIndicator && <span className="badge bg-primary bg-opacity-10 text-primary mt-2" style={{ fontSize: '0.65rem', border: '1px solid rgba(13,110,253,0.2)' }}>Real-time Sync Active</span>}
                </div>

                {/* ── Middle Row: Quick Stats Grid ── */}
                <div className="d-flex flex-wrap justify-content-center gap-3 w-100 mx-auto mb-4" style={{ maxWidth: '1100px' }}>
                    <div className="bg-white border-0 shadow-sm rounded-4 px-4 py-3 d-flex align-items-center gap-3 flex-grow-1" style={{ minWidth: '180px' }}>
                        <div className="p-2 bg-primary bg-opacity-10 text-primary rounded-3">
                            <i className="bi bi-people-fill fs-5"></i>
                        </div>
                        <div>
                            <div className="text-secondary small fw-medium text-uppercase ls-1" style={{ fontSize: '0.65rem' }}>Total Sellers</div>
                            <div className="h5 mb-0 fw-bold">{stats.total}</div>
                        </div>
                    </div>

                    <div className="bg-white border-0 shadow-sm rounded-4 px-4 py-3 d-flex align-items-center gap-3 flex-grow-1" style={{ minWidth: '180px' }}>
                        <div className="p-2 bg-success bg-opacity-10 text-success rounded-3">
                            <i className="bi bi-patch-check-fill fs-5"></i>
                        </div>
                        <div>
                            <div className="text-secondary small fw-medium text-uppercase ls-1" style={{ fontSize: '0.65rem' }}>Active Now</div>
                            <div className="h5 mb-0 fw-bold">{stats.active}</div>
                        </div>
                    </div>

                    <div className="bg-white border-0 shadow-sm rounded-4 px-4 py-3 d-flex align-items-center gap-3 flex-grow-1" style={{ minWidth: '220px' }}>
                        <div className="p-2 bg-info bg-opacity-10 text-info rounded-3">
                            <i className="bi bi-wallet-fill fs-5"></i>
                        </div>
                        <div>
                            <div className="text-secondary small fw-medium text-uppercase ls-1" style={{ fontSize: '0.65rem' }}>Total Available</div>
                            <div className="h5 mb-0 fw-bold text-success">{formatCurrency(stats.totalAvailable)}</div>
                        </div>
                    </div>

                    <div className="bg-white border-0 shadow-sm rounded-4 px-4 py-3 d-flex align-items-center gap-3 flex-grow-1" style={{ minWidth: '220px' }}>
                        <div className="p-2 bg-warning bg-opacity-10 text-warning rounded-3">
                            <i className="bi bi-hourglass-split fs-5"></i>
                        </div>
                        <div>
                            <div className="text-secondary small fw-medium text-uppercase ls-1" style={{ fontSize: '0.65rem' }}>Total Pending</div>
                            <div className="h5 mb-0 fw-bold text-warning">{formatCurrency(stats.totalPending)}</div>
                        </div>
                    </div>
                </div>

                {/* ── Bottom Row: Evaluation Countdown (Right-aligned) ── */}
                <div className="d-flex justify-content-end w-100 mx-auto" style={{ maxWidth: '1100px' }}>
                    <div className="bg-light border-0 rounded-pill px-4 py-2 d-flex align-items-center shadow-inner" style={{ fontSize: '0.85rem' }}>
                        <i className="bi bi-clock-history text-primary me-2"></i>
                        <span className="text-dark fw-bold me-2">Auto-Evaluation:</span>
                        <strong className="text-primary font-monospace">{timeLeft}</strong>
                    </div>
                </div>
            </div>

            {/* Leveling Guide Note */}
            {criteria && showGuide && (
                <div className="guide-container mb-4">
                    <div className="d-flex align-items-center justify-content-between mb-3 px-1">
                        <h6 className="fw-bold text-uppercase text-primary mb-0" style={{ letterSpacing: '1px', fontSize: '0.75rem' }}>
                            <i className="bi bi-stars me-2"></i> Leveling Requirements
                        </h6>
                        <button className="btn-close small" onClick={() => setShowGuide(false)} style={{ fontSize: '0.6rem' }}></button>
                    </div>

                    <div className="row g-3">
                        {/* Top Rated Card */}
                        <div className="col-lg-4">
                            <div className="level-card h-100 border-0 shadow-sm rounded-4 bg-white p-3 border-top border-5 border-success">
                                <div className="d-flex align-items-center gap-2 mb-3">
                                    <div className="icon-box bg-success bg-opacity-10 text-success rounded-circle p-2">
                                        <i className="bi bi-trophy-fill fs-5"></i>
                                    </div>
                                    <h6 className="fw-bold mb-0">Top Rated</h6>
                                </div>
                                <div className="level-criteria-list">
                                    <div className="d-flex align-items-center justify-content-between small mb-2 p-2 bg-light rounded-3">
                                        <span className="text-muted"><i className="bi bi-cart-check me-2"></i>Orders</span>
                                        <span className="fw-bold">≥ {criteria.topRatedMinTransactions}</span>
                                    </div>
                                    <div className="d-flex align-items-center justify-content-between small mb-2 p-2 bg-light rounded-3">
                                        <span className="text-muted"><i className="bi bi-calendar-event me-2"></i>Active Days</span>
                                        <span className="fw-bold">≥ {criteria.topRatedMinDays}</span>
                                    </div>
                                    <div className="d-flex align-items-center justify-content-between small p-2 bg-light rounded-3">
                                        <span className="text-muted"><i className="bi bi-exclamation-triangle me-2"></i>Defect Rate</span>
                                        <span className="fw-bold text-success">≤ {(criteria.topRatedMaxDefectRate * 100).toFixed(1)}%</span>
                                    </div>
                                </div>
                            </div>
                        </div>

                        {/* Above Standard Card */}
                        <div className="col-lg-4">
                            <div className="level-card h-100 border-0 shadow-sm rounded-4 bg-white p-3 border-top border-5 border-warning">
                                <div className="d-flex align-items-center gap-2 mb-3">
                                    <div className="icon-box bg-warning bg-opacity-10 text-warning rounded-circle p-2">
                                        <i className="bi bi-shield-check fs-5"></i>
                                    </div>
                                    <h6 className="fw-bold mb-0">Above Standard</h6>
                                </div>
                                <div className="level-criteria-list">
                                    <div className="d-flex align-items-center justify-content-between small mb-2 p-2 bg-light rounded-3">
                                        <span className="text-muted"><i className="bi bi-calendar-event me-2"></i>Active Days</span>
                                        <span className="fw-bold">≥ {criteria.aboveStandardMinDays}</span>
                                    </div>
                                    <div className="d-flex align-items-center justify-content-between small mb-2 p-2 bg-light rounded-3">
                                        <span className="text-muted"><i className="bi bi-graph-down me-2"></i>Defect Rate</span>
                                        <span className="fw-bold text-warning">≤ {(criteria.aboveStandardMaxDefectRate * 100).toFixed(1)}%</span>
                                    </div>
                                    <div className="d-flex align-items-center justify-content-between small p-2 bg-light rounded-3">
                                        <span className="text-muted"><i className="bi bi-headset me-2"></i>Unresolved</span>
                                        <span className="fw-bold text-warning">≤ {criteria.aboveStandardMaxUnresolvedCases} cases</span>
                                    </div>
                                    <div className="mt-2 text-center">
                                        <small className="text-muted italic" style={{ fontSize: '0.7rem' }}>Maintains good service standing</small>
                                    </div>
                                </div>
                            </div>
                        </div>

                        {/* Below Standard Card */}
                        <div className="col-lg-4">
                            <div className="level-card h-100 border-0 shadow-sm rounded-4 bg-white p-3 border-top border-5 border-danger bg-opacity-75">
                                <div className="d-flex align-items-center gap-2 mb-3">
                                    <div className="icon-box bg-danger bg-opacity-10 text-danger rounded-circle p-2">
                                        <i className="bi bi-exclamation-octagon fs-5"></i>
                                    </div>
                                    <h6 className="fw-bold mb-0 text-danger">Below Standard</h6>
                                </div>
                                <div className="p-3 border border-dashed rounded-4 text-center">
                                    <p className="small text-muted mb-0">
                                        Accounts that fail to meet the minimum requirements for <span className="fw-bold">Above Standard</span> or <span className="fw-bold text-success">Top Rated</span> status.
                                    </p>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            <div className="glass-panel border-0 mb-4 overflow-hidden" stagger="stagger-1">
                <div className="p-4 border-bottom bg-white">
                    <div className="d-flex flex-column flex-lg-row justify-content-between align-items-lg-center gap-3">

                        {/* Bên trái: Chỉ để thanh Search và cho nó dài ra một chút */}
                        <div className="input-group" style={{ maxWidth: '350px' }}>
                            <span className="input-group-text bg-white border-end-0 text-muted pe-1">
                                <i className="bi bi-search"></i>
                            </span>
                            <input
                                type="text"
                                className="form-control border-start-0 shadow-none ps-2"
                                placeholder="Search name, email, UID..."
                                value={search}
                                onChange={(e) => { setSearch(e.target.value); setCurrentPage(1); }}
                            />
                            {search && (
                                <span
                                    className="input-group-text bg-white border-start-0"
                                    style={{ cursor: 'pointer' }}
                                    onClick={() => setSearch("")}
                                >
                                    <i className="bi bi-x text-muted"></i>
                                </span>
                            )}
                        </div>

                        {/* Bên phải: Chỉ giữ lại Dropdown Filter và nút Refresh */}
                        <div className="d-flex flex-wrap align-items-center gap-2">
                            <select
                                className="form-select shadow-none"
                                style={{ width: '140px', cursor: 'pointer' }}
                                value={filterStatus}
                                onChange={(e) => { setFilterStatus(e.target.value); setCurrentPage(1); }}
                            >
                                <option value="All">Status: All</option>
                                <option value="Active">Active</option>
                                <option value="Suspended">Suspended</option>
                            </select>

                            <select
                                className="form-select shadow-none"
                                style={{ width: '150px', cursor: 'pointer' }}
                                value={filterLevel}
                                onChange={(e) => { setFilterLevel(e.target.value); setCurrentPage(1); }}
                            >
                                <option value="All">Level: All</option>
                                <option value="TopRated">Top Rated</option>
                                <option value="AboveStandard">Above Std</option>
                                <option value="BelowStandard">Below Std</option>
                            </select>

                            <button
                                className="btn btn-outline-secondary d-flex align-items-center justify-content-center"
                                onClick={resetFilters}
                                title="Refresh data"
                                style={{ width: '38px', height: '38px' }}
                            >
                                <i className="bi bi-arrow-clockwise"></i>
                            </button>
                        </div>

                    </div>
                </div>
                <div className="table-responsive bg-white">
                    <table className="table table-hover align-middle pe-table mb-0">
                        <thead className="table-light">
                            <tr>
                                <th className="ps-4 cursor-pointer" onClick={() => requestSort('sellerId')} style={{ cursor: 'pointer' }}>
                                    <i className="bi bi-hash header-icon"></i>ID {getSortIcon('sellerId')}
                                </th>
                                <th className="cursor-pointer" onClick={() => requestSort('sellerName')} style={{ cursor: 'pointer' }}>
                                    <i className="bi bi-person header-icon"></i>Seller {getSortIcon('sellerName')}
                                </th>
                                <th className="cursor-pointer" onClick={() => requestSort('sellerLevel')} style={{ cursor: 'pointer' }}>
                                    <i className="bi bi-award header-icon"></i>Level {getSortIcon('sellerLevel')}
                                </th>
                                <th className="cursor-pointer" onClick={() => requestSort('status')} style={{ cursor: 'pointer' }}>
                                    <i className="bi bi-activity header-icon"></i>Status {getSortIcon('status')}
                                </th>
                                <th className="text-end cursor-pointer" onClick={() => requestSort('availableBalance')} style={{ cursor: 'pointer' }}>
                                    <i className="bi bi-wallet2 header-icon"></i>Available {getSortIcon('availableBalance')}
                                </th>
                                <th className="text-end cursor-pointer" onClick={() => requestSort('pendingBalance')} style={{ cursor: 'pointer' }}>
                                    <i className="bi bi-hourglass-split header-icon"></i>Pending {getSortIcon('pendingBalance')}
                                </th>
                                <th className="text-end cursor-pointer" onClick={() => requestSort('totalWithdrawn')} style={{ cursor: 'pointer' }}>
                                    <i className="bi bi-box-arrow-up header-icon"></i>Withdrawn {getSortIcon('totalWithdrawn')}
                                </th>
                                <th className="text-center">Detail</th>
                            </tr>
                        </thead>
                        <tbody>
                            {loading ? (
                                <tr><td colSpan="7" className="text-center py-5"><LoadingIndicator text="Loading sellers data..." /></td></tr>
                            ) : paginatedWallets.length === 0 ? (
                                <tr><td colSpan="7" className="text-center py-5 text-muted">No sellers found</td></tr>
                            ) : paginatedWallets.map(wallet => (
                                <tr key={wallet.id}>
                                    <td className="ps-4">
                                        <span className="text-muted text-monospace" style={{ fontSize: '0.8rem' }}>{wallet.sellerId}</span>
                                    </td>
                                    <td>
                                        <div className="fw-bold text-dark">{wallet.sellerName}</div>
                                    </td>
                                    <td>{renderLevelBadge(wallet)}</td>
                                    <td>
                                        <div className="status-indicator">
                                            <span className={`status-dot status-dot--${wallet.status.toLowerCase()}`}></span>
                                            <span className="small fw-bold text-secondary">{wallet.status}</span>
                                        </div>
                                    </td>
                                    <td className="text-end">
                                        <div className="balance-compact">
                                            <span className="text-success fw-bold" style={{ fontSize: '1rem' }}>{formatCurrency(wallet.availableBalance)}</span>
                                        </div>
                                    </td>
                                    <td className="text-end">
                                        <Link to={`/sellers/pending/${wallet.sellerId}`} className="pe-pending-amount text-warning fw-bold" title="View details">
                                            {formatCurrency(wallet.pendingBalance)}
                                            <i className="bi bi-chevron-right"></i>
                                        </Link>
                                    </td>
                                    <td className="text-end text-muted small fw-medium">{formatCurrency(wallet.totalWithdrawn)}</td>
                                    <td className="text-center">
                                        <Link to={`/users/${wallet.sellerId}`} className="btn btn-sm btn-light rounded-circle shadow-sm" style={{ width: '32px', height: '32px', padding: 0, display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }} title="View Details">
                                            <i className="bi bi-eye text-primary"></i>
                                        </Link>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>

                {/* Frontend Pagination Controls */}
                {!loading && filteredWallets.length > 0 && (
                    <div className="p-3 bg-white border-top d-flex justify-content-between align-items-center flex-wrap gap-2">
                        <div className="d-flex align-items-center gap-2">
                            <select
                                className="form-select border-0 bg-light rounded-pill py-1 ps-3 pe-5 text-muted small" // Thay px-3 bằng ps-3 và pe-5 (hoặc pe-6 nếu cần)
                                style={{
                                    width: 'auto',
                                    minWidth: '70px', // Đảm bảo đủ rộng ngay cả với số 10
                                    fontSize: '0.8rem'
                                }}
                                value={pageSize}
                                onChange={(e) => { setPageSize(Number(e.target.value)); setCurrentPage(1); }}
                            >
                                <option value={10}>10</option>
                                <option value={20}>20</option>
                                <option value={50}>50</option>
                            </select>
                            <span className="text-muted small">
                                Showing {(currentPage - 1) * pageSize + 1} to {Math.min(currentPage * pageSize, filteredWallets.length)} of {filteredWallets.length}
                            </span>
                        </div>
                        <div className="d-flex gap-2">
                            <Button
                                color="outline-secondary"
                                size="sm"
                                disabled={currentPage <= 1}
                                onClick={() => setCurrentPage(p => p - 1)}
                                className="rounded-pill px-3"
                            >
                                <i className="bi bi-chevron-left me-1"></i> Previous
                            </Button>
                            <div className="align-self-center px-3 small border-start border-end">
                                Page <strong>{currentPage}</strong> of {totalPages || 1}
                            </div>
                            <Button
                                color="outline-secondary"
                                size="sm"
                                disabled={currentPage >= totalPages}
                                onClick={() => setCurrentPage(p => p + 1)}
                                className="rounded-pill px-3"
                            >
                                Next <i className="bi bi-chevron-right ms-1"></i>
                            </Button>
                        </div>
                    </div>
                )}
            </div>

            {/* Performance Statistics Modal */}
            <Modal isOpen={modal} toggle={() => toggleModal()} centered size="md">
                <ModalHeader toggle={() => toggleModal()} className="border-0 pb-0">
                    <div className="d-flex align-items-center">
                        <i className="bi bi-graph-up-arrow me-3 text-primary h4 mb-0"></i>
                        <div>
                            <h5 className="mb-0 fw-bold">Performance Metrics</h5>
                            <small className="text-muted">{selectedSeller?.sellerName}</small>
                        </div>
                    </div>
                </ModalHeader>
                <ModalBody className="py-4">
                    {selectedSeller && (
                        <div className="row g-3">
                            <div className="col-4">
                                <div className="p-3 bg-light rounded-4 text-center h-100">
                                    <div className="text-secondary small mb-1">Transactions</div>
                                    <div className="h4 mb-0 fw-bold">{selectedSeller.transactionCount}</div>
                                </div>
                            </div>
                            <div className="col-4">
                                <div className="p-3 bg-light rounded-4 text-center h-100 border-start border-end">
                                    <div className="text-secondary small mb-1">Active Days</div>
                                    <div className="h4 mb-0 fw-bold">{selectedSeller.activeDays !== undefined ? selectedSeller.activeDays : 0}</div>
                                </div>
                            </div>
                            <div className="col-4">
                                <div className="p-3 bg-light rounded-4 text-center h-100">
                                    <div className="text-secondary small mb-1">Sales Vol.</div>
                                    <div className="h5 mb-0 fw-bold text-truncate">{formatCurrency(selectedSeller.totalSales)}</div>
                                </div>
                            </div>
                            <div className="col-12">
                                <hr className="my-2 opacity-25" />
                            </div>
                            <div className="col-4">
                                <div className="text-center">
                                    <div className="text-secondary x-small mb-1">Unresolved</div>
                                    <div className={`fw-bold ${selectedSeller.unresolvedCases > 0 ? 'text-danger' : 'text-success'}`}>
                                        {selectedSeller.unresolvedCases}
                                    </div>
                                </div>
                            </div>
                            <div className="col-4">
                                <div className="text-center border-start border-end">
                                    <div className="text-secondary x-small mb-1">Defect Rate</div>
                                    <div className={`fw-bold ${(selectedSeller.defectRate > 0.05) ? 'text-danger' : 'text-success'}`}>
                                        {(selectedSeller.defectRate * 100).toFixed(2)}%
                                    </div>
                                </div>
                            </div>
                            <div className="col-4">
                                <div className="text-center">
                                    <div className="text-secondary x-small mb-1">Late Rate</div>
                                    <div className={`fw-bold ${(selectedSeller.lateRate > 0.1) ? 'text-danger' : 'text-success'}`}>
                                        {(selectedSeller.lateRate * 100).toFixed(2)}%
                                    </div>
                                </div>
                            </div>
                        </div>
                    )}
                </ModalBody>
                <ModalFooter className="border-0 pt-0">
                    <Button color="light" onClick={() => toggleModal()} className="rounded-pill px-4">Close</Button>
                </ModalFooter>
                <style>{`
                    .x-small { font-size: 0.75rem; }
                    .bg-success-subtle { background-color: #d1e7dd !important; }
                    .bg-secondary-subtle { background-color: #e2e3e5 !important; }
                `}</style>
            </Modal>
        </div>
    );
};
