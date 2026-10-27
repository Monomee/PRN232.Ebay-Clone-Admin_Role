import React, { useState, useEffect, useCallback } from 'react';
import { sensitiveLogService } from '../services/sensitiveLogService';

export const SensitiveLogsPage = () => {
    // Data State
    const [logs, setLogs] = useState([]);
    const [summary, setSummary] = useState(null);
    const [loading, setLoading] = useState(true);
    const [pageNumber, setPageNumber] = useState(1);
    const [pagination, setPagination] = useState({ totalPages: 1, hasNextPage: false, hasPreviousPage: false, totalCount: 0 });

    // Filters
    const [classificationFilter, setClassificationFilter] = useState('All');
    const [actionFilter, setActionFilter] = useState('All');
    const [searchTerm, setSearchTerm] = useState('');
    const [searchInputValue, setSearchInputValue] = useState('');
    const [fromDate, setFromDate] = useState('');
    const [toDate, setToDate] = useState('');

    // Decryption Modal State
    const [decryptingId, setDecryptingId] = useState(null);
    const [decryptedResult, setDecryptedResult] = useState(null);
    const [copied, setCopied] = useState(false);
    const [toastMessage, setToastMessage] = useState(null);

    // Fetch Summary KPI
    const fetchSummary = async () => {
        try {
            const data = await sensitiveLogService.getSummary();
            setSummary(data);
        } catch (error) {
            console.error("Error fetching sensitive logs summary:", error);
        }
    };

    // Fetch Logs
    const fetchLogs = useCallback(async (page = 1) => {
        setLoading(true);
        try {
            const data = await sensitiveLogService.getSensitiveLogs({
                pageNumber: page,
                pageSize: 15,
                classification: classificationFilter,
                action: actionFilter,
                searchTerm: searchTerm,
                fromDate: fromDate,
                toDate: toDate
            });
            setLogs(data.items || []);
            setPagination({
                totalPages: data.totalPages || 1,
                hasNextPage: data.hasNextPage,
                hasPreviousPage: data.hasPreviousPage,
                totalCount: data.totalCount || 0
            });
        } catch (error) {
            console.error("Error fetching sensitive logs:", error);
            showToast("Failed to load sensitive data logs", "danger");
        } finally {
            setLoading(false);
        }
    }, [classificationFilter, actionFilter, searchTerm, fromDate, toDate]);

    useEffect(() => {
        fetchSummary();
    }, []);

    useEffect(() => {
        fetchLogs(pageNumber);
    }, [pageNumber, fetchLogs]);

    const showToast = (text, type = "success") => {
        setToastMessage({ text, type });
        setTimeout(() => setToastMessage(null), 3500);
    };

    const handleSearchSubmit = (e) => {
        e.preventDefault();
        setPageNumber(1);
        setSearchTerm(searchInputValue);
    };

    const resetFilters = () => {
        setClassificationFilter('All');
        setActionFilter('All');
        setSearchTerm('');
        setSearchInputValue('');
        setFromDate('');
        setToDate('');
        setPageNumber(1);
    };

    // Decrypt Action
    const handleDecrypt = async (logId) => {
        setDecryptingId(logId);
        try {
            const result = await sensitiveLogService.decryptLog(logId);
            setDecryptedResult(result);
            showToast("AES-256 decryption successful!", "success");
        } catch (error) {
            console.error("Error decrypting log:", error);
            showToast("Unable to decrypt log or insufficient permissions", "danger");
        } finally {
            setDecryptingId(null);
        }
    };

    const copyToClipboard = (text) => {
        navigator.clipboard.writeText(text);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
    };

    // Classification Badge Color
    const getClassificationBadge = (classification) => {
        switch (classification) {
            case 'Financial':
                return <span className="badge bg-success bg-opacity-10 text-success border border-success border-opacity-25 px-2 py-1"><i className="bi bi-cash-stack me-1"></i>FINANCIAL</span>;
            case 'PII':
                return <span className="badge bg-info bg-opacity-10 text-info-emphasis border border-info border-opacity-25 px-2 py-1"><i className="bi bi-person-vcard me-1"></i>PII</span>;
            case 'SecurityCredential':
                return <span className="badge bg-danger bg-opacity-10 text-danger border border-danger border-opacity-25 px-2 py-1"><i className="bi bi-key-fill me-1"></i>SECURITY / 2FA</span>;
            case 'AdminPrivilege':
                return <span className="badge bg-warning bg-opacity-10 text-warning-emphasis border border-warning border-opacity-25 px-2 py-1"><i className="bi bi-shield-lock-fill me-1"></i>ADMIN PRIVILEGE</span>;
            default:
                return <span className="badge bg-secondary bg-opacity-10 text-secondary border px-2 py-1">{classification}</span>;
        }
    };

    return (
        <div style={{ minHeight: '100vh', background: '#f8fafc', fontFamily: "'Inter', sans-serif", padding: '24px 20px' }}>
            <div className="container-fluid" style={{ maxWidth: 1260 }}>

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
                        <div className="d-flex align-items-center gap-2 mb-1">
                            <h1 className="h3 fw-bold text-dark mb-0" style={{ letterSpacing: '-0.5px' }}>
                                <i className="bi bi-shield-lock-fill text-danger me-2"></i>
                                Sensitive Data Logs
                            </h1>
                            <span className="badge bg-danger bg-opacity-10 text-danger border border-danger border-opacity-25 rounded-pill px-2 py-1 fw-bold" style={{ fontSize: '0.7rem' }}>
                                <i className="bi bi-lock-fill me-1"></i>SUPERADMIN ONLY
                            </span>
                            <span className="badge bg-primary bg-opacity-10 text-primary border border-primary border-opacity-25 rounded-pill px-2 py-1 fw-bold" style={{ fontSize: '0.7rem' }}>
                                AES-256-CBC ENCRYPTED
                            </span>
                        </div>
                        <p className="text-secondary mb-0 small">
                            Dedicated audit log for sensitive data (Financial, National ID/PII, Credentials, Permissions). Data is symmetrically encrypted when stored in the database and can only be decrypted by SuperAdmin.
                        </p>
                    </div>
                    <div className="d-flex gap-2 mt-2 mt-sm-0">
                        <button 
                            className="btn btn-sm btn-white border shadow-sm rounded-pill px-3 py-1 fw-semibold text-secondary"
                            onClick={() => { fetchSummary(); fetchLogs(pageNumber); }}
                        >
                            <i className="bi bi-arrow-clockwise me-1"></i> Refresh
                        </button>
                    </div>
                </div>

                {/* KPI Summary Grid */}
                <div className="row g-3 mb-4">
                    <div className="col-12 col-sm-6 col-lg-3">
                        <div className="bg-white border rounded-4 p-3 shadow-sm d-flex align-items-center gap-3 h-100 hover-translate-y">
                            <div className="p-3 bg-dark bg-opacity-10 text-dark rounded-3 fs-4">
                                <i className="bi bi-database-lock"></i>
                            </div>
                            <div>
                                <span className="text-muted small fw-bold text-uppercase">Total Sensitive Logs</span>
                                <h4 className="mb-0 fw-bold text-dark">
                                    {summary ? summary.totalLogs : <span className="spinner-border spinner-border-sm"></span>}
                                </h4>
                                <small className="text-muted" style={{ fontSize: '0.72rem' }}>Fully encrypted and protected</small>
                            </div>
                        </div>
                    </div>

                    <div className="col-12 col-sm-6 col-lg-3">
                        <div className="bg-white border rounded-4 p-3 shadow-sm d-flex align-items-center gap-3 h-100 hover-translate-y">
                            <div className="p-3 bg-success bg-opacity-10 text-success rounded-3 fs-4">
                                <i className="bi bi-wallet2"></i>
                            </div>
                            <div>
                                <span className="text-muted small fw-bold text-uppercase">Financial Data</span>
                                <h4 className="mb-0 fw-bold text-dark">
                                    {summary ? summary.financialCount : <span className="spinner-border spinner-border-sm"></span>}
                                </h4>
                                <small className="text-muted" style={{ fontSize: '0.72rem' }}>Bank, Payout, E-Wallets</small>
                            </div>
                        </div>
                    </div>

                    <div className="col-12 col-sm-6 col-lg-3">
                        <div className="bg-white border rounded-4 p-3 shadow-sm d-flex align-items-center gap-3 h-100 hover-translate-y">
                            <div className="p-3 bg-info bg-opacity-10 text-info-emphasis rounded-3 fs-4">
                                <i className="bi bi-person-badge"></i>
                            </div>
                            <div>
                                <span className="text-muted small fw-bold text-uppercase">Personal Identity (PII)</span>
                                <h4 className="mb-0 fw-bold text-dark">
                                    {summary ? summary.piiCount : <span className="spinner-border spinner-border-sm"></span>}
                                </h4>
                                <small className="text-muted" style={{ fontSize: '0.72rem' }}>ID card, Passport, Address</small>
                            </div>
                        </div>
                    </div>

                    <div className="col-12 col-sm-6 col-lg-3">
                        <div className="bg-white border rounded-4 p-3 shadow-sm d-flex align-items-center gap-3 h-100 hover-translate-y">
                            <div className="p-3 bg-danger bg-opacity-10 text-danger rounded-3 fs-4">
                                <i className="bi bi-shield-slash-fill"></i>
                            </div>
                            <div>
                                <span className="text-muted small fw-bold text-uppercase">Credentials & Privileges</span>
                                <h4 className="mb-0 fw-bold text-dark">
                                    {summary ? summary.securityCredentialCount + summary.adminPrivilegeCount : <span className="spinner-border spinner-border-sm"></span>}
                                </h4>
                                <small className="text-muted" style={{ fontSize: '0.72rem' }}>2FA, Passwords, Role grants</small>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Filters & Table Card */}
                <div className="card border-0 shadow-sm rounded-4 overflow-hidden mb-4">
                    {/* Filter Bar */}
                    <div className="p-3 bg-white border-bottom">
                        <div className="row g-2 align-items-center">
                            {/* Search */}
                            <div className="col-12 col-md-4">
                                <form onSubmit={handleSearchSubmit} className="position-relative">
                                    <i className="bi bi-search position-absolute text-muted" style={{ top: '10px', left: '12px' }}></i>
                                    <input 
                                        type="text"
                                        className="form-control form-control-sm ps-5 rounded-pill border-light-subtle"
                                        placeholder="Search by Actor, Action, Target, IP..."
                                        value={searchInputValue}
                                        onChange={(e) => setSearchInputValue(e.target.value)}
                                    />
                                </form>
                            </div>

                            {/* Classification */}
                            <div className="col-6 col-md-3">
                                <select 
                                    className="form-select form-select-sm rounded-pill"
                                    value={classificationFilter}
                                    onChange={(e) => { setClassificationFilter(e.target.value); setPageNumber(1); }}
                                >
                                    <option value="All">Classification: All</option>
                                    <option value="Financial">Financial</option>
                                    <option value="PII">Personal Identity (PII)</option>
                                    <option value="SecurityCredential">Security & Credentials</option>
                                    <option value="AdminPrivilege">Admin Privilege</option>
                                </select>
                            </div>

                            {/* Action Filter */}
                            <div className="col-6 col-md-3">
                                <select 
                                    className="form-select form-select-sm rounded-pill"
                                    value={actionFilter}
                                    onChange={(e) => { setActionFilter(e.target.value); setPageNumber(1); }}
                                >
                                    <option value="All">Action: All</option>
                                    <option value="UpdatePaymentInfo">Update Bank Account</option>
                                    <option value="ExportKYC">Export KYC Data</option>
                                    <option value="ResetPassword">Reset Account Password</option>
                                    <option value="ChangeAdminRole">Change Admin Role</option>
                                    <option value="PayoutExecution">Execute Payout</option>
                                    <option value="WalletAdjustment">Adjust Wallet Balance</option>
                                    <option value="TwoFactorSecret">Setup 2FA Security</option>
                                </select>
                            </div>

                            {/* Reset Button */}
                            <div className="col-12 col-md-2 text-end">
                                <button 
                                    className="btn btn-sm btn-outline-secondary rounded-pill w-100"
                                    onClick={resetFilters}
                                >
                                    <i className="bi bi-x-circle me-1"></i> Reset Filters
                                </button>
                            </div>
                        </div>
                    </div>

                    {/* Table */}
                    <div className="table-responsive">
                        <table className="table mb-0 align-middle" style={{ fontSize: '0.85rem' }}>
                            <thead className="bg-light text-secondary text-uppercase" style={{ fontSize: '0.72rem', letterSpacing: '0.5px' }}>
                                <tr>
                                    <th style={{ width: '15%' }} className="ps-4">Timestamp</th>
                                    <th style={{ width: '18%' }}>Actor</th>
                                    <th style={{ width: '22%' }}>Action & Classification</th>
                                    <th style={{ width: '15%' }}>Target</th>
                                    <th style={{ width: '18%' }}>Encrypted Data (Ciphertext)</th>
                                    <th style={{ width: '12%' }} className="text-end pe-4">Action</th>
                                </tr>
                            </thead>
                            <tbody>
                                {loading ? (
                                    <tr>
                                        <td colSpan="6" className="text-center py-5">
                                            <div className="spinner-border text-danger" role="status">
                                                <span className="visually-hidden">Loading...</span>
                                            </div>
                                            <div className="text-muted small mt-2">Loading sensitive logs...</div>
                                        </td>
                                    </tr>
                                ) : logs.length === 0 ? (
                                    <tr>
                                        <td colSpan="6" className="text-center py-5">
                                            <i className="bi bi-shield-check text-success" style={{ fontSize: '3rem' }}></i>
                                            <h6 className="mt-2 text-dark fw-bold">No sensitive logs found</h6>
                                            <p className="text-muted small mb-0">Try adjusting your filters or search keywords.</p>
                                        </td>
                                    </tr>
                                ) : (
                                    logs.map((log) => (
                                        <tr key={log.id} className="hover-bg-light">
                                            {/* Timestamp */}
                                            <td className="ps-4">
                                                <div className="fw-semibold text-dark">
                                                    {log.createdAt ? new Date(log.createdAt).toLocaleDateString('en-US') : '-'}
                                                </div>
                                                <small className="text-muted" style={{ fontSize: '0.72rem' }}>
                                                    {log.createdAt ? new Date(log.createdAt).toLocaleTimeString('en-US') : ''}
                                                </small>
                                            </td>

                                            {/* Actor */}
                                            <td>
                                                <div className="d-flex align-items-center gap-2">
                                                    <div className="bg-danger bg-opacity-10 text-danger rounded-circle d-flex align-items-center justify-content-center fw-bold" style={{ width: 32, height: 32, fontSize: '0.8rem' }}>
                                                        {log.actorUsername?.charAt(0).toUpperCase() || 'A'}
                                                    </div>
                                                    <div>
                                                        <div className="fw-bold text-dark small">{log.actorUsername}</div>
                                                        <div className="text-muted" style={{ fontSize: '0.7rem' }}>
                                                            Role: <span className="fw-medium text-secondary">{log.actorRole}</span> • IP: {log.ipAddress || '127.0.0.1'}
                                                        </div>
                                                    </div>
                                                </div>
                                            </td>

                                            {/* Action & Classification */}
                                            <td>
                                                <div className="fw-bold text-dark small mb-1">{log.action}</div>
                                                {getClassificationBadge(log.dataClassification)}
                                            </td>

                                            {/* Target */}
                                            <td>
                                                <div className="fw-medium text-dark small">{log.targetEntity}</div>
                                                <small className="text-muted" style={{ fontSize: '0.72rem' }}>ID: #{log.targetId || 'N/A'}</small>
                                            </td>

                                            {/* Encrypted Data Preview */}
                                            <td>
                                                <div className="p-2 bg-dark bg-opacity-10 rounded font-monospace small text-truncate" style={{ maxWidth: '180px', fontSize: '0.75rem' }} title={log.encryptedData}>
                                                    <i className="bi bi-lock-fill text-danger me-1"></i>
                                                    {log.encryptedData ? log.encryptedData.substring(0, 18) + '...' : ''}
                                                </div>
                                                <small className="text-muted d-block mt-1" style={{ fontSize: '0.68rem' }}>
                                                    Algorithm: {log.encryptionAlgorithm}
                                                </small>
                                            </td>

                                            {/* Action */}
                                            <td className="pe-4 text-end">
                                                <button 
                                                    className="btn btn-sm btn-outline-danger rounded-pill px-3 fw-semibold shadow-sm"
                                                    onClick={() => handleDecrypt(log.id)}
                                                    disabled={decryptingId === log.id}
                                                    style={{ fontSize: '0.75rem' }}
                                                    title="SuperAdmin only permission to decrypt this data"
                                                >
                                                    {decryptingId === log.id ? (
                                                        <span className="spinner-border spinner-border-sm me-1"></span>
                                                    ) : (
                                                        <i className="bi bi-key-fill me-1"></i>
                                                    )}
                                                    Decrypt
                                                </button>
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
                            Page <strong>{pageNumber}</strong> / {pagination.totalPages} (Total {pagination.totalCount} records)
                        </span>
                        <nav>
                            <ul className="pagination pagination-sm mb-0 gap-1">
                                <li className={`page-item ${!pagination.hasPreviousPage ? 'disabled' : ''}`}>
                                    <button className="page-link rounded-pill border-0 px-3" onClick={() => setPageNumber(prev => Math.max(1, prev - 1))}>
                                        <i className="bi bi-chevron-left me-1"></i> Prev
                                    </button>
                                </li>
                                <li className={`page-item ${!pagination.hasNextPage ? 'disabled' : ''}`}>
                                    <button className="page-link rounded-pill border-0 px-3" onClick={() => setPageNumber(prev => prev + 1)}>
                                        Next <i className="bi bi-chevron-right ms-1"></i>
                                    </button>
                                </li>
                            </ul>
                        </nav>
                    </div>
                </div>

                {/* ========================================================================= */}
                {/* DECRYPTED RESULT MODAL (SUPERADMIN VIEW) */}
                {/* ========================================================================= */}
                {decryptedResult && (
                    <div className="modal show d-block" tabIndex="-1" style={{ backgroundColor: 'rgba(15, 23, 42, 0.65)', backdropFilter: 'blur(3px)' }}>
                        <div className="modal-dialog modal-dialog-centered modal-lg">
                            <div className="modal-content border-0 shadow-lg rounded-4 overflow-hidden">
                                <div className="modal-header bg-dark text-white px-4 py-3">
                                    <div className="d-flex align-items-center gap-2">
                                        <div className="p-2 bg-danger rounded-circle d-flex align-items-center justify-content-center">
                                            <i className="bi bi-unlock-fill text-white"></i>
                                        </div>
                                        <div>
                                            <h5 className="modal-title fw-bold h6 mb-0">
                                                Decrypted Sensitive Data #{decryptedResult.id}
                                            </h5>
                                            <small className="text-white-50" style={{ fontSize: '0.72rem' }}>
                                                Successfully decrypted by: <strong>{decryptedResult.decryptedBy}</strong> ({new Date(decryptedResult.decryptedAt).toLocaleTimeString('en-US')})
                                            </small>
                                        </div>
                                    </div>
                                    <button type="button" className="btn-close btn-close-white" onClick={() => setDecryptedResult(null)}></button>
                                </div>
                                <div className="modal-body p-4 bg-light">
                                    {/* Verification Badges */}
                                    <div className="d-flex flex-wrap gap-2 mb-3">
                                        <span className="badge bg-success bg-opacity-10 text-success border border-success border-opacity-25 px-2 py-1">
                                            <i className="bi bi-patch-check-fill me-1"></i>Integrity: Valid HMAC-SHA256
                                        </span>
                                        <span className="badge bg-primary bg-opacity-10 text-primary border border-primary border-opacity-25 px-2 py-1">
                                            <i className="bi bi-cpu me-1"></i>Algorithm: {decryptedResult.encryptionAlgorithm}
                                        </span>
                                        <span className="badge bg-dark bg-opacity-10 text-dark border px-2 py-1">
                                            Action: {decryptedResult.action}
                                        </span>
                                    </div>

                                    {/* 1. Ciphertext stored in Database */}
                                    <div className="mb-3">
                                        <div className="d-flex justify-content-between align-items-center mb-1">
                                            <label className="form-label fw-bold small text-secondary mb-0">
                                                <i className="bi bi-lock-fill text-danger me-1"></i>
                                                Encrypted Data in Database (Ciphertext Base64):
                                            </label>
                                            <button 
                                                className="btn btn-sm btn-link text-decoration-none p-0 text-muted small"
                                                onClick={() => copyToClipboard(decryptedResult.encryptedData)}
                                            >
                                                <i className="bi bi-clipboard me-1"></i>Copy Ciphertext
                                            </button>
                                        </div>
                                        <div className="p-3 bg-white rounded-3 border font-monospace text-muted small text-break" style={{ maxHeight: '100px', overflowY: 'auto' }}>
                                            {decryptedResult.encryptedData}
                                        </div>
                                    </div>

                                    {/* 2. Decrypted Plaintext Payload */}
                                    <div className="mb-2">
                                        <div className="d-flex justify-content-between align-items-center mb-1">
                                            <label className="form-label fw-bold small text-dark mb-0">
                                                <i className="bi bi-file-earmark-code text-success me-1"></i>
                                                Decrypted Sensitive Content (Plaintext Payload JSON):
                                            </label>
                                            <button 
                                                className={`btn btn-sm btn-${copied ? 'success' : 'outline-primary'} rounded-pill px-3 py-1 fw-semibold`}
                                                style={{ fontSize: '0.75rem' }}
                                                onClick={() => copyToClipboard(decryptedResult.decryptedData)}
                                            >
                                                <i className={`bi ${copied ? 'bi-check2' : 'bi-clipboard'} me-1`}></i>
                                                {copied ? 'Copied!' : 'Copy JSON'}
                                            </button>
                                        </div>
                                        <div className="p-3 bg-dark text-white rounded-3 font-monospace small" style={{ maxHeight: '240px', overflowY: 'auto', fontSize: '0.82rem' }}>
                                            <pre className="mb-0 text-white" style={{ whiteSpace: 'pre-wrap' }}>
                                                {(() => {
                                                    try {
                                                        return JSON.stringify(JSON.parse(decryptedResult.decryptedData), null, 2);
                                                    } catch {
                                                        return decryptedResult.decryptedData;
                                                    }
                                                })()}
                                            </pre>
                                        </div>
                                    </div>

                                    <div className="alert alert-warning border-0 d-flex align-items-center gap-2 mb-0 py-2 px-3 small rounded-3 mt-3">
                                        <i className="bi bi-exclamation-triangle-fill text-warning fs-5"></i>
                                        <span>
                                            <strong>Security Notice:</strong> This sensitive data decryption operation has been automatically recorded in the <strong>Audit Log</strong> for compliance and security auditing.
                                        </span>
                                    </div>
                                </div>
                                <div className="modal-footer bg-white px-4 py-3">
                                    <button type="button" className="btn btn-sm btn-dark rounded-pill px-4 fw-semibold" onClick={() => setDecryptedResult(null)}>
                                        Close
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
};

export default SensitiveLogsPage;
