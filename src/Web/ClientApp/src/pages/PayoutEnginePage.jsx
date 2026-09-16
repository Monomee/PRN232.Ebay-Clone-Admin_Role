import React, { useState, useEffect, useCallback } from 'react';
import {
    BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend
} from 'recharts';
import financeService from '../services/financeService';
import { ToastMessage } from "../components/ToastMessage";
import './PayoutEnginePage.css';

// ─── Constants & Helpers ─────────────────────────────────────────────────────
const fmt = (n) => (n != null ? Number(n).toLocaleString("en-US") : 0);
const fmtCurrency = (n) =>
    new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(n ?? 0);

const StatusBadge = ({ status }) => {
    const map = {
        Success: 'badge-success',
        Failed: 'badge-failed',
        Hold: 'badge-hold',
        Processing: 'badge-processing',
        Released: 'badge-released',
    };
    return <span className={`pe-badge ${map[status] || 'badge-default'}`}>{status}</span>;
};

// ─── Modular Components ──────────────────────────────────────────────────────
const GlassCard = ({ children, className = "", stagger = "" }) => (
    <div className={`glass-panel rounded-4 p-4 ${className} animate-fade-in-up ${stagger}`}>
        {children}
    </div>
);

const MetricTile = ({ label, value, icon, gradient, stagger, currency = false }) => (
    <GlassCard className="h-100" stagger={stagger}>
        <div className="metric-card-modern">
            <div className={`icon-box ${gradient} shadow-sm mb-3`}>
                <i className={icon}></i>
            </div>
            <div className="metric-label mb-1">{label}</div>
            <div className="metric-value">{currency ? fmtCurrency(value) : fmt(value)}</div>
        </div>
    </GlassCard>
);

// ─── Main Component ──────────────────────────────────────────────────────────
export default function PayoutEnginePage() {
    const [history, setHistory] = useState([]);
    const [exceptions, setExceptions] = useState([]);
    const [config, setConfig] = useState({ frequency: 'Daily', minimumThreshold: 10, scheduledHourUtc: 2, isEnabled: true });
    const [groupBy, setGroupBy] = useState('day');
    const [runResult, setRunResult] = useState(null);
    const [isRunning, setIsRunning] = useState(false);
    const [isSavingConfig, setIsSavingConfig] = useState(false);
    const [releasingId, setReleasingId] = useState(null);
    const [toast, setToast] = useState({ message: "", type: "success" });
    const [loading, setLoading] = useState(true);

    const showToast = (message, type = 'success') => setToast({ message, type });

    const loadData = useCallback(async () => {
        try {
            const [hist, exc, cfg] = await Promise.all([
                financeService.getPayoutHistory(groupBy),
                financeService.getPayoutExceptions(),
                financeService.getPayoutConfig(),
            ]);
            setHistory(hist || []);
            setExceptions(exc || []);
            if (cfg) setConfig(cfg);
        } catch {
            showToast('Failed to load payout data.', 'error');
        } finally {
            setLoading(false);
        }
    }, [groupBy]);

    useEffect(() => { loadData(); }, [loadData]);

    // ── Stats Calculation ─────────────────────────────────────────────────────
    const totalDisbursed = history.reduce((s, r) => s + (r.totalAmount || 0), 0);
    const totalTx = history.reduce((s, r) => s + (r.count || 0), 0);
    const exFailed = exceptions.filter(e => e.status === 'Failed').length;

    // ── Handlers ──────────────────────────────────────────────────────────────
    // const handleForceRun = async () => {
    //     setIsRunning(true);
    //     setRunResult(null);
    //     try {
    //         const res = await financeService.runPayoutEngine();
    //         setRunResult(res);
    //         showToast(`Run complete: ${res.success} Success | ${res.failed} Failed | ${res.onHold} Hold`, 'success');
    //         await loadData();
    //     } catch {
    //         showToast('Payout engine run failed.', 'error');
    //     } finally {
    //         setIsRunning(false);
    //     }
    // };

    const handleRelease = async (id) => {
        setReleasingId(id);
        try {
            await financeService.releaseHold(id);
            showToast(`Payout #${id} released for retry.`, 'success');
            await loadData();
        } catch {
            showToast('Release failed.', 'error');
        } finally {
            setReleasingId(null);
        }
    };

    const handleSaveConfig = async () => {
        setIsSavingConfig(true);
        try {
            await financeService.updatePayoutConfig(config);
            showToast('Configuration updated.', 'success');
        } catch {
            showToast('Update failed.', 'error');
        } finally {
            setIsSavingConfig(false);
        }
    };

    return (
        <section className="pe-container py-2">
            <ToastMessage
                message={toast.message}
                type={toast.type}
                onClose={() => setToast({ message: "", type: "success" })}
            />

            {/* Header Area */}
            <div className="d-flex justify-content-between align-items-center mb-4 flex-wrap gap-3 p-3">
                <div>
                    <h1 className="pe-header-title">Payout Engine</h1>
                    <div className="pe-header-subtitle">
                        <i className="bi bi-cpu me-2"></i>
                        Automated clearing and disbursement management
                    </div>
                </div>

                {/* <div className="d-flex gap-2">
                    <button
                        className="btn btn-primary shadow-sm"
                        onClick={handleForceRun}
                        disabled={isRunning}
                    >
                        {isRunning ? (
                            <><span className="pe-spinner me-2" /> Processing...</>
                        ) : (
                            <><i className="bi bi-play-circle me-2"></i> Force Engine Run</>
                        )}
                    </button>
                </div> */}
            </div>

            <div className="p-3 bg-light rounded-4">
                {/* Last Run Summary (Conditional) */}
                {runResult && (
                    <GlassCard className="pe-run-result mb-4 stagger-1 border-primary" style={{ borderLeft: '5px solid var(--ebay-blue)' }}>
                        <div className="pe-card-header mb-3">
                            <h3 className="pe-card-title text-primary"><i className="bi bi-activity me-2"></i> Last Run Summary</h3>
                            <span className="text-secondary small">Session ID: {runResult.sessionId.split('-')[0]}...</span>
                        </div>
                        <div className="pe-run-result__grid">
                            <div className="pe-run-stat pe-run-stat--success">
                                <span className="pe-run-num">{runResult.success}</span>
                                <span className="pe-run-label">Success</span>
                            </div>
                            <div className="pe-run-stat pe-run-stat--failed">
                                <span className="pe-run-num">{runResult.failed}</span>
                                <span className="pe-run-label">Failed</span>
                            </div>
                            <div className="pe-run-stat pe-run-stat--disburse">
                                <span className="pe-run-num text-primary">{fmtCurrency(runResult.totalDisbursed)}</span>
                                <span className="pe-run-label">Total Outflow</span>
                            </div>
                        </div>
                    </GlassCard>
                )}

                {/* KPI Metrics Row */}
                <div className="row g-4 mb-4">
                    <div className="col-md-4">
                        <MetricTile
                            label="Total Disbursed"
                            value={totalDisbursed}
                            icon="bi bi-cash-stack"
                            gradient="bg-gradient-blue"
                            stagger="stagger-1"
                            currency
                        />
                    </div>
                    <div className="col-md-4">
                        <MetricTile
                            label="Successful Payouts"
                            value={totalTx}
                            icon="bi bi-check2-all"
                            gradient="bg-gradient-green"
                            stagger="stagger-2"
                        />
                    </div>
                    <div className="col-md-4">
                        <MetricTile
                            label="Unresolved Errors"
                            value={exFailed}
                            icon="bi bi-exclamation-octagon"
                            gradient="bg-gradient-red"
                            stagger="stagger-3"
                        />
                    </div>
                </div>

                <div className="row g-4">
                    {/* History Chart */}
                    <div className="col-lg-8">
                        <GlassCard className="h-100" stagger="stagger-2">
                            <div className="pe-card-header mb-4">
                                <h2 className="pe-card-title"><i className="bi bi-graph-up me-2"></i> Disbursement History</h2>
                                <div className="date-presets-container bg-light p-1 rounded-pill shadow-sm">
                                    <button
                                        className={`date-preset-btn ${groupBy === 'day' ? 'active' : ''}`}
                                        onClick={() => setGroupBy('day')}
                                    >Daily</button>
                                    <button
                                        className={`date-preset-btn ${groupBy === 'month' ? 'active' : ''}`}
                                        onClick={() => setGroupBy('month')}
                                    >Monthly</button>
                                </div>
                            </div>

                            {loading ? (
                                <div className="text-center py-5"><span className="pe-spinner" /></div>
                            ) : history.length === 0 ? (
                                <div className="text-center py-5 text-secondary">No recorded payout history found.</div>
                            ) : (
                                <ResponsiveContainer width="100%" height={300}>
                                    <BarChart data={history}>
                                        <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#eee" />
                                        <XAxis dataKey="label" axisLine={false} tickLine={false} />
                                        <YAxis axisLine={false} tickLine={false} tickFormatter={(v) => `$${v}`} />
                                        <Tooltip
                                            cursor={{ fill: 'rgba(0,0,0,0.02)' }}
                                            contentStyle={{ borderRadius: '12px', border: 'none', boxShadow: '0 8px 32px rgba(0,0,0,0.1)' }}
                                            formatter={(v) => [fmtCurrency(v), 'Disbursed']}
                                        />
                                        <Bar dataKey="totalAmount" fill="#0064d2" radius={[6, 6, 0, 0]} />
                                    </BarChart>
                                </ResponsiveContainer>
                            )}
                        </GlassCard>
                    </div>

                    {/* Configuration Sidebar */}
                    <div className="col-lg-4">
                        <GlassCard className="h-100" stagger="stagger-3">
                            <h2 className="pe-card-title mb-4"><i className="bi bi-gear-wide-connected me-2"></i> Engine Settings</h2>
                            <div className="d-flex flex-column gap-3">
                                <div className="form-check form-switch p-0 d-flex align-items-center justify-content-between mb-2">
                                    <label className="pe-input-label m-0" htmlFor="engineSwitch">Engine Operational</label>
                                    <input
                                        className="form-check-input ms-0 mt-0"
                                        type="checkbox"
                                        id="engineSwitch"
                                        role="switch"
                                        style={{ width: '2.5em', height: '1.25em', cursor: 'pointer' }}
                                        checked={config.isEnabled}
                                        onChange={e => setConfig(c => ({ ...c, isEnabled: e.target.checked }))}
                                    />
                                </div>

                                <div className="pe-input-group">
                                    <label className="pe-input-label">Frequency</label>
                                    <select
                                        className="pe-form-control pe-select"
                                        value={config.frequency}
                                        onChange={e => setConfig(c => ({ ...c, frequency: e.target.value }))}
                                    >
                                        <option value="Daily">Daily Payouts</option>
                                        <option value="Weekly">Weekly Clearing</option>
                                    </select>
                                </div>

                                <div className="pe-input-group">
                                    <label className="pe-input-label">Min Threshold (USD)</label>
                                    <input
                                        type="number"
                                        className="pe-form-control"
                                        value={config.minimumThreshold}
                                        onChange={e => setConfig(c => ({ ...c, minimumThreshold: parseFloat(e.target.value) }))}
                                    />
                                </div>

                                <div className="pe-input-group">
                                    <label className="pe-input-label">Execution Hour (UTC)</label>
                                    <input
                                        type="number"
                                        className="pe-form-control"
                                        min={0} max={23}
                                        value={config.scheduledHourUtc}
                                        onChange={e => setConfig(c => ({ ...c, scheduledHourUtc: parseInt(e.target.value) }))}
                                    />
                                </div>

                                <button
                                    className="btn btn-outline-primary mt-2"
                                    onClick={handleSaveConfig}
                                    disabled={isSavingConfig}
                                >
                                    {isSavingConfig ? 'Saving...' : 'Update Settings'}
                                </button>
                            </div>
                        </GlassCard>
                    </div>

                    {/* Exceptions Table */}
                    <div className="col-12">
                        <GlassCard stagger="stagger-4">
                            <div className="pe-card-header mb-4">
                                <h2 className="pe-card-title"><i className="bi bi-shield-exclamation me-2"></i> Operational Exceptions</h2>
                                <span className="badge bg-soft-red text-red rounded-pill px-3 py-2">{exceptions.length} Items</span>
                            </div>

                            {loading ? (
                                <div className="text-center py-4"><span className="pe-spinner" /></div>
                            ) : exceptions.length === 0 ? (
                                <div className="text-center py-5">
                                    <i className="bi bi-check-circle text-success fs-1 d-block mb-3"></i>
                                    <span className="text-secondary">No critical exceptions found. System healthy.</span>
                                </div>
                            ) : (
                                <div className="pe-table-container table-responsive">
                                    <table className="pe-table">
                                        <thead>
                                            <tr>
                                                <th>Reference</th>
                                                <th>Seller Information</th>
                                                <th>Amount</th>
                                                <th>Status</th>
                                                <th>Reason / Log</th>
                                                <th>Created</th>
                                                <th className="text-end">Action</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {exceptions.map(ex => (
                                                <tr key={ex.id}>
                                                    <td className="text-secondary small">#{ex.id}</td>
                                                    <td>
                                                        <div className="fw-bold">{ex.sellerUsername || 'Unknown'}</div>
                                                        <div className="text-secondary x-small">ID: {ex.sellerId}</div>
                                                    </td>
                                                    <td className="fw-bold text-blue">{fmtCurrency(ex.amount)}</td>
                                                    <td><StatusBadge status={ex.status} /></td>
                                                    <td className="text-secondary x-small" style={{ maxWidth: '250px' }}>
                                                        {ex.errorLog || 'Manual hold applied'}
                                                    </td>
                                                    <td className="text-secondary x-small">
                                                        {new Date(ex.createdAt).toLocaleDateString()}
                                                    </td>
                                                    <td className="text-end">
                                                        {(ex.status === 'Failed') && (
                                                            <button
                                                                className="btn btn-sm btn-outline-success rounded-pill px-3"
                                                                onClick={() => handleRelease(ex.id)}
                                                                disabled={releasingId === ex.id}
                                                            >
                                                                {releasingId === ex.id ? '...' : 'Release'}
                                                            </button>
                                                        )}
                                                    </td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                </div>
                            )}
                        </GlassCard>
                    </div>
                </div>
            </div>

            <style>{`
                .x-small { font-size: 0.75rem; }
                .text-blue { color: var(--ebay-blue); }
                .text-red { color: var(--ebay-danger); }
            `}</style>
        </section>
    );
}
