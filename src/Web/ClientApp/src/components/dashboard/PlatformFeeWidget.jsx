import React, { useState, useEffect } from 'react';
import { apiRequest } from '../../services/httpClient';
import { ToastMessage } from '../ToastMessage';

export const PlatformFeeWidget = () => {
    const [fees, setFees] = useState({
        listingFee: 0,
        finalValueFeePercentage: 0
    });
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [toast, setToast] = useState({ message: '', type: 'success' });

    useEffect(() => {
        loadFees();
    }, []);

    const loadFees = async () => {
        try {
            setLoading(true);
            const data = await apiRequest('/api/Financials/fees');
            setFees({
                listingFee: data.listingFee || 0,
                finalValueFeePercentage: data.finalValueFeePercentage || 0
            });
        } catch (error) {
            setToast({ message: 'Failed to load platform fees', type: 'error' });
            console.error(error);
        } finally {
            setLoading(false);
        }
    };

    const handleChange = (e) => {
        const { name, value } = e.target;
        setFees(prev => ({ ...prev, [name]: parseFloat(value) || 0 }));
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        try {
            setSaving(true);
            await apiRequest('/api/Financials/fees', {
                method: 'PUT',
                body: {
                    listingFee: fees.listingFee,
                    finalValueFeePercentage: fees.finalValueFeePercentage
                }
            });
            setToast({ message: 'Platform fees updated successfully', type: 'success' });
        } catch (error) {
            setToast({ message: 'Failed to update platform fees', type: 'error' });
            console.error(error);
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
                <h5 className="fw-bold mb-0">Platform Fees</h5>
                <i className="bi bi-tag text-primary fs-5"></i>
            </div>
            
            {loading ? (
                <div className="text-center py-4">
                    <div className="spinner-border text-primary spinner-border-sm" role="status">
                        <span className="visually-hidden">Loading...</span>
                    </div>
                </div>
            ) : (
                <form onSubmit={handleSubmit} className="d-flex flex-column gap-3">
                    <div className="pe-input-group">
                        <label className="pe-input-label d-flex justify-content-between">
                            <span>Final Value Fee (%)</span>
                            <span className="text-muted small">Percent cut per sale</span>
                        </label>
                        <input 
                            type="number" 
                            step="0.1"
                            min="0"
                            max="100"
                            className="pe-form-control" 
                            name="finalValueFeePercentage" 
                            value={fees.finalValueFeePercentage} 
                            onChange={handleChange} 
                            required 
                        />
                    </div>

                    <div className="pe-input-group">
                        <label className="pe-input-label d-flex justify-content-between">
                            <span>Listing Fee (USD)</span>
                            <span className="text-muted small">Fixed cost per listing</span>
                        </label>
                        <input 
                            type="number" 
                            min="0"
                            step="0.01"
                            className="pe-form-control" 
                            name="listingFee" 
                            value={fees.listingFee} 
                            onChange={handleChange} 
                            required 
                        />
                    </div>

                    <button 
                        type="submit" 
                        className="btn btn-outline-primary rounded-pill mt-2" 
                        disabled={saving}
                    >
                        {saving ? 'Saving...' : 'Save Configuration'}
                    </button>
                    <div className="text-muted" style={{ fontSize: '0.65rem', textAlign: 'center' }}>
                        These fees apply instantly to all new mock transactions.
                    </div>
                </form>
            )}
        </div>
    );
};
