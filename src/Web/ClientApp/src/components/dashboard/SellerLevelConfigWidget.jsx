import React, { useState, useEffect } from 'react';
import { apiRequest } from '../../services/httpClient';
import { ToastMessage } from '../ToastMessage';

export const SellerLevelConfigWidget = () => {
    const [criteria, setCriteria] = useState({
        topRatedMinTransactions: 100,
        topRatedMinSales: 1000,
        topRatedMinDays: 90,
        topRatedMaxUnresolvedCases: 2,
        topRatedMaxDefectRate: 0.005,
        topRatedMaxLateRate: 0.03,
        aboveStandardMinDays: 30,
        aboveStandardMaxDefectRate: 0.02,
        aboveStandardMaxUnresolvedCases: 2,
        aboveStandardMaxUnresolvedRate: 0.003
    });
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [toast, setToast] = useState({ message: '', type: 'success' });

    useEffect(() => {
        loadCriteria();
    }, []);

    const loadCriteria = async () => {
        try {
            setLoading(true);
            const data = await apiRequest('/api/Users/seller-level-criteria');
            setCriteria(data);
        } catch (error) {
            setToast({ message: 'Failed to load seller level criteria', type: 'error' });
        } finally {
            setLoading(false);
        }
    };

    const handleChange = (e) => {
        const { name, value } = e.target;
        if (name === 'nextEvaluationDate') {
            setCriteria(prev => ({ ...prev, [name]: value }));
            return;
        }
        const val = name.includes('Rate') ? parseFloat(value) : (name.includes('Sales') ? parseFloat(value) : parseInt(value));
        setCriteria(prev => ({ ...prev, [name]: isNaN(val) ? 0 : val }));
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        try {
            setSaving(true);
            await apiRequest('/api/Users/seller-level-criteria', {
                method: 'PUT',
                body: criteria
            });
            setToast({ message: 'Seller level criteria updated successfully', type: 'success' });
        } catch (error) {
            setToast({ message: 'Failed to update criteria', type: 'error' });
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="glass-panel rounded-4 p-4 h-100 animate-fade-in-up">
            <ToastMessage 
                message={toast.message} 
                type={toast.type} 
                onClose={() => setToast({ message: '', type: 'success' })} 
            />
            <div className="d-flex justify-content-between align-items-center mb-4">
                <h5 className="fw-bold mb-0">Seller Level Criteria</h5>
                <i className="bi bi-trophy text-warning fs-5"></i>
            </div>
            
            {loading ? (
                <div className="text-center py-4">
                    <div className="spinner-border text-primary spinner-border-sm" role="status">
                        <span className="visually-hidden">Loading...</span>
                    </div>
                </div>
            ) : (
                <form onSubmit={handleSubmit} className="d-flex flex-column gap-3">
                    <div className="row g-3">
                        <div className="col-12"><small className="text-primary fw-bold uppercase">Top Rated Rules</small></div>
                        
                        <div className="col-6">
                            <label className="pe-input-label small mb-1">Min Orders</label>
                            <input type="number" className="pe-form-control pe-form-control-sm" name="topRatedMinTransactions" value={criteria.topRatedMinTransactions} onChange={handleChange} required />
                        </div>
                        <div className="col-6">
                            <label className="pe-input-label small mb-1">Min Days</label>
                            <input type="number" className="pe-form-control pe-form-control-sm" name="topRatedMinDays" value={criteria.topRatedMinDays} onChange={handleChange} required />
                        </div>
                        <div className="col-6">
                            <label className="pe-input-label small mb-1">Defect Max (%)</label>
                            <input type="number" step="0.001" className="pe-form-control pe-form-control-sm" name="topRatedMaxDefectRate" value={criteria.topRatedMaxDefectRate} onChange={handleChange} required />
                        </div>
                        <div className="col-6">
                            <label className="pe-input-label small mb-1">Late Max (%)</label>
                            <input type="number" step="0.001" className="pe-form-control pe-form-control-sm" name="topRatedMaxLateRate" value={criteria.topRatedMaxLateRate} onChange={handleChange} required />
                        </div>

                        <div className="col-12 mt-3"><small className="text-primary fw-bold uppercase">Above Standard Rules</small></div>
                        <div className="col-4">
                            <label className="pe-input-label small mb-1">Min Days</label>
                            <input type="number" className="pe-form-control pe-form-control-sm" name="aboveStandardMinDays" value={criteria.aboveStandardMinDays} onChange={handleChange} required />
                        </div>
                        <div className="col-4">
                            <label className="pe-input-label small mb-1">Defect Max (%)</label>
                            <input type="number" step="0.001" className="pe-form-control pe-form-control-sm" name="aboveStandardMaxDefectRate" value={criteria.aboveStandardMaxDefectRate} onChange={handleChange} required />
                        </div>
                        <div className="col-4">
                            <label className="pe-input-label small mb-1">Max Cases</label>
                            <input type="number" className="pe-form-control pe-form-control-sm" name="aboveStandardMaxUnresolvedCases" value={criteria.aboveStandardMaxUnresolvedCases} onChange={handleChange} required />
                        </div>
                    </div>

                    <button 
                        type="submit" 
                        className="btn btn-primary rounded-pill mt-2 w-100" 
                        disabled={saving}
                    >
                        {saving ? 'Saving...' : 'Update Criteria'}
                    </button>
                    <div style={{ fontSize: '0.65rem', textAlign: 'center', color: '#6c757d' }} className="mt-1">
                        Changing criteria affects the next evaluation cycle. Demo overrides are moved to the Mock page.
                    </div>
                </form>
            )}
        </div>
    );
};
