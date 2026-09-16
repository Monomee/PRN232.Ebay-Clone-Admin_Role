import React, { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import financeService from '../services/financeService';
import { LoadingIndicator } from '../components/LoadingIndicator';
import { ToastMessage } from '../components/ToastMessage';

const OrderCountdown = ({ estSettlementDate }) => {
    const [timeLeft, setTimeLeft] = useState('');
    const [progress, setProgress] = useState(100);

    useEffect(() => {
        if (!estSettlementDate) {
            setTimeLeft('Pending');
            return;
        }
        
        let target = new Date(estSettlementDate);
        if(typeof estSettlementDate === 'string' && !estSettlementDate.endsWith('Z')) {
             target = new Date(estSettlementDate + 'Z');
        }

        const calc = () => {
            const now = new Date();
            const diff = target - now;
            if (diff <= 0) {
                setTimeLeft('Ready to Settle');
                setProgress(100);
                return;
            }
            const MathFloor = Math.floor;
            const days = MathFloor(diff / (1000 * 60 * 60 * 24));
            const hours = MathFloor((diff / (1000 * 60 * 60)) % 24);
            const mins = MathFloor((diff / 1000 / 60) % 60);
            const secs = MathFloor((diff / 1000) % 60);

            let str = '';
            if (days > 0) str += `${days}d `;
            str += `${hours.toString().padStart(2, '0')}h ${mins.toString().padStart(2, '0')}m ${secs.toString().padStart(2, '0')}s`;
            setTimeLeft(str);

            const maxMs = 21 * 24 * 60 * 60 * 1000;
            const percent = Math.min(100, Math.max(5, 100 - (diff / maxMs * 100)));
            setProgress(percent);
        };
        calc();
        const t = setInterval(calc, 1000);
        return () => clearInterval(t);
    }, [estSettlementDate]);

    if (timeLeft === 'Ready to Settle') return <span className="badge bg-success">Ready to Settle</span>;
    if (timeLeft === 'Pending') return <span className="text-muted">Pending</span>;

    return (
        <div className="d-flex flex-column align-items-center" style={{ minWidth: '130px' }}>
            <span className="fw-bold text-warning font-monospace small">{timeLeft}</span>
            <div className="progress w-100 mt-1" style={{height: '4px'}}>
                <div className="progress-bar bg-warning" role="progressbar" style={{width: `${progress}%`}}></div>
            </div>
        </div>
    );
};

export const SellerPendingFundsPage = () => {
    const { sellerId } = useParams();
    const [orders, setOrders] = useState([]);
    const [loading, setLoading] = useState(true);
    const [toast, setToast] = useState({ message: '', type: 'success' });

    useEffect(() => {
        loadPendingFunds();
    }, [sellerId]);

    const loadPendingFunds = async () => {
        try {
            setLoading(true);
            const data = await financeService.getSellerPendingFunds(sellerId);
            setOrders(data);
        } catch (error) {
            setToast({ message: 'Failed to load pending funds detail', type: 'error' });
        } finally {
            setLoading(false);
        }
    };

    const formatCurrency = (amount) => {
        return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount);
    };

    const formatDate = (dateString) => {
        if (!dateString) return '-';
        return new Date(dateString).toLocaleDateString('vi-VN', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit'
        });
    };

    return (
        <div className="container-fluid py-4">
            <ToastMessage 
                message={toast.message} 
                type={toast.type} 
                onClose={() => setToast({ message: '', type: 'success' })} 
            />

            <nav aria-label="breadcrumb" className="mb-4">
                <ol className="breadcrumb">
                    <li className="breadcrumb-item"><Link to="/sellers">Sellers</Link></li>
                    <li className="breadcrumb-item active" aria-current="page">Pending Funds Detail</li>
                </ol>
            </nav>

            <div className="d-flex justify-content-between align-items-center mb-4">
                <h1 className="h3">Pending Funds Detail</h1>
                <div className="text-muted">Seller ID: {sellerId}</div>
            </div>

            <div className="card shadow-sm border-0">
                <div className="card-header bg-white py-3">
                    <h5 className="card-title mb-0">Orders Awaiting Settlement</h5>
                </div>
                <div className="table-responsive">
                    <table className="table table-hover align-middle mb-0">
                        <thead className="table-light">
                            <tr>
                                <th>Order ID</th>
                                <th>Order Date</th>
                                <th className="text-end">Seller Earnings</th>
                                <th>Status</th>
                                <th>Est. Settlement</th>
                                <th className="text-center">Days Remaining</th>
                            </tr>
                        </thead>
                        <tbody>
                            {loading ? (
                                <tr><td colSpan="6" className="text-center py-5"><LoadingIndicator text="Fetching details..." /></td></tr>
                            ) : orders.length === 0 ? (
                                <tr><td colSpan="6" className="text-center py-5 text-muted">No pending funds for this seller.</td></tr>
                            ) : orders.map(order => (
                                <tr key={order.orderId}>
                                    <td><span className="fw-bold text-primary">#{order.orderId}</span></td>
                                    <td>{formatDate(order.orderDate)}</td>
                                    <td className="text-end fw-bold">{formatCurrency(order.sellerEarnings)}</td>
                                    <td>
                                        <span className="badge bg-info text-dark">
                                            {order.status}
                                        </span>
                                    </td>
                                    <td>{formatDate(order.estimatedSettlementDate)}</td>
                                    <td className="text-center">
                                        <OrderCountdown estSettlementDate={order.estimatedSettlementDate} />
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
                <div className="card-footer bg-white py-3 text-end">
                    <div className="h5 mb-0">
                        Total Pending: <span className="text-warning fw-bold">
                            {formatCurrency(orders.reduce((sum, o) => sum + o.sellerEarnings, 0))}
                        </span>
                    </div>
                </div>
            </div>

            <div className="mt-4">
                <div className="alert alert-info border-0 shadow-sm d-flex align-items-center">
                    <i className="fas fa-info-circle me-3 fs-4"></i>
                    <div>
                        <strong>Note:</strong> Funds are automatically moved to available balance when the settlement date is reached and a settlement trigger is executed. 
                        Top-rated sellers have 0 days hold, while others may have 3-21 days depending on performance.
                    </div>
                </div>
            </div>
        </div>
    );
};
