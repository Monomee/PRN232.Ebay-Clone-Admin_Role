import React, { useState, useEffect, useCallback } from 'react';
import { productService } from '../services/productService'; // Ensure this path is correct for your project
import { categoryService } from '../services/categoryService';



// Substituted by .status-indicator from PayoutEnginePage.css

// ─── Review Modal ─────────────────────────────────────────────────────────────
const ReviewModal = ({ product, onClose, onResolve }) => {
    // decision will now store a number (1, 2, or 3) instead of a string
    const [decision, setDecision] = useState(null);
    const [note, setNote] = useState('');
    const [loading, setLoading] = useState(false);
    const [details, setDetails] = useState(null);

    useEffect(() => {
        const fetchDetails = async () => {
            try {
                // Assuming you have a getViolationDetails function
                const data = await productService.getViolationDetails(product.id);
                setDetails(data);
            } catch (e) {
                console.error('Error loading violation details:', e);
            }
        };
        fetchDetails();
    }, [product.id]);

    const handleSubmit = async () => {
        if (!decision) return;
        setLoading(true);
        try {
            // FIXED HERE: Payload matches 100% with C# Backend Record
            const payload = {
                productId: product.id,
                action: decision,       // Send number 1 (Delete), 2 (Hide), or 3 (Reject report)
                adminNote: note,        // Change key from 'note' to 'adminNote'
                violationType: "Other"   // Can be hardcoded for now or add a dropdown later
            };

            // Call real API to send resolution decision
            await productService.resolveViolation(product.id, payload);

            // Notify parent component to update the UI list
            onResolve(product.id, decision);
            onClose();
        } catch (e) {
            console.error('Error sending resolution decision:', e);
            alert('An error occurred during processing. Please try again.');
        } finally {
            setLoading(false);
        }
    };

    const ACTION_BUTTONS = [
        { id: 1, label: 'Delete & Warn', color: '#991B1B', border: '#EF4444', bg: '#FEF2F2', activeBg: '#FCA5A5' },
        { id: 2, label: 'Hide Product', color: '#92400E', border: '#F59E0B', bg: '#FFFBEB', activeBg: '#FDE68A' },
        { id: 3, label: 'Ignore Report', color: '#065F46', border: '#10B981', bg: '#ECFDF5', activeBg: '#A7F3D0' },
        { id: 4, label: 'Restore Product', color: '#065F46', border: '#10B981', bg: '#ECFDF5', activeBg: '#A7F3D0' }
    ];

    return (
        <div style={{
            position: 'fixed', inset: 0, background: 'rgba(15,23,42,0.6)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            zIndex: 1000, backdropFilter: 'blur(4px)', padding: 16,
        }}>
            <div style={{
                background: '#fff', borderRadius: 16, width: '100%', maxWidth: 520,
                maxHeight: '90vh', display: 'flex', flexDirection: 'column',
                boxShadow: '0 25px 60px rgba(0,0,0,0.25)', overflow: 'hidden',
                animation: 'slideUp 0.25s ease',
            }}>
                {/* Header */}
                <div className="p-3 bg-primary text-white d-flex justify-content-between align-items-center flex-shrink-0">
                    <div>
                        <p className="text-white text-opacity-75 small mb-0 fw-bold" style={{ fontSize: '0.65rem', letterSpacing: '0.5px' }}>MODERATION CASE</p>
                        <h6 className="mb-0 fw-bold">
                            #{product.id} · {product.title}
                        </h6>
                    </div>
                    <button onClick={onClose} className="btn-close btn-close-white shadow-none" style={{ fontSize: '0.8rem' }}></button>
                </div>

                <div style={{ padding: '20px 24px', overflowY: 'auto', flex: 1 }}>
                    {/* Product Info */}
                    <div style={{ display: 'flex', gap: 12, marginBottom: 20, padding: 14, background: '#F8FAFC', borderRadius: 10, border: '1px solid #E2E8F0' }}>
                        <img 
                            src={product.image || '/images/default-product.png'} 
                            alt="" 
                            style={{ width: 52, height: 52, borderRadius: 8, objectFit: 'cover' }} 
                            onError={(e) => { e.target.onerror = null; e.target.src = '/images/default-product.png'; }}
                        />
                        <div>
                            <p style={{ margin: 0, fontWeight: 600, color: '#1E293B', fontSize: 14 }}>{product.title}</p>
                            <p style={{ margin: '3px 0 0', fontSize: 12, color: '#64748B' }}>Shop: <b>{product.shopName || 'Unknown'}</b></p>
                        </div>
                    </div>

                    {/* Violation Details */}
                    {details ? (
                        <div style={{ marginBottom: 20 }}>
                            <p style={{ fontSize: 12, fontWeight: 700, color: '#94A3B8', textTransform: 'uppercase', letterSpacing: 1, margin: '0 0 8px' }}>
                                Reports List ({details?.reports?.length || 0})
                            </p>
                            <div className="bg-light border rounded-3 p-3 overflow-auto" style={{ maxHeight: '200px' }}>
                                {details?.reports && details.reports.length > 0 ? (
                                    <div className="d-flex flex-column gap-2">
                                        {details.reports.map((r, i) => (
                                            <div key={r.reportId || i} className="bg-white border rounded-3 p-3 shadow-sm">
                                                <div className="d-flex justify-content-between align-items-start mb-2">
                                                    <span className="badge bg-primary bg-opacity-10 text-primary small">{r.reporterType}</span>
                                                    <span className="text-muted small">#{r.reportId}</span>
                                                </div>
                                                <div className="fw-bold text-dark mb-1">{r.reason}</div>
                                                <div className="text-secondary small mb-2">
                                                    <i className="bi bi-clock me-1"></i> {new Date(r.createdAt).toLocaleString()}
                                                </div>
                                                {r.proofDocumentUrl && (
                                                    <a href={r.proofDocumentUrl} target="_blank" rel="noopener noreferrer" className="btn btn-sm btn-outline-primary py-0 px-2 rounded-pill">
                                                        <i className="bi bi-paperclip me-1"></i>View Proof
                                                    </a>
                                                )}
                                            </div>
                                        ))}
                                    </div>
                                ) : (
                                    <p className="text-muted text-center py-3 mb-0">No detailed report data found.</p>
                                )}
                            </div>
                        </div>
                    ) : (
                        <div style={{ marginBottom: 20, textAlign: 'center', color: '#64748B', fontSize: 13, padding: '20px 0' }}>
                            <div className="spinner-border spinner-border-sm text-primary me-2"></div>
                            Loading violation details...
                        </div>
                    )}

                    {/* Decision Buttons */}
                    <p style={{ fontSize: 12, fontWeight: 700, color: '#94A3B8', textTransform: 'uppercase', letterSpacing: 1, margin: '0 0 8px' }}>Resolution Decision</p>
                    <div style={{ display: 'flex', gap: 10, marginBottom: 16 }}>
                        {ACTION_BUTTONS.map(btn => (
                            <button
                                key={btn.id}
                                onClick={() => setDecision(btn.id)}
                                style={{
                                    flex: 1, padding: '10px 4px', borderRadius: 8, border: '2px solid',
                                    cursor: 'pointer', fontSize: 12, fontWeight: 600, transition: 'all 0.15s',
                                    borderColor: decision === btn.id ? btn.border : '#E2E8F0',
                                    background: decision === btn.id ? btn.activeBg : '#fff',
                                    color: decision === btn.id ? btn.color : '#64748B',
                                }}
                            >
                                {btn.label}
                            </button>
                        ))}
                    </div>

                    <textarea
                        value={note}
                        onChange={e => setNote(e.target.value)}
                        placeholder="Admin Notes (Required if delete/hide)..."
                        rows={3}
                        style={{ width: '100%', borderRadius: 8, border: '1.5px solid #E2E8F0', padding: '10px 12px', fontSize: 13, resize: 'vertical', outline: 'none', boxSizing: 'border-box', fontFamily: 'inherit', color: '#1E293B' }}
                    />

                    {/* Footer Actions */}
                    <div style={{ display: 'flex', gap: 10, marginTop: 16 }}>
                        <button onClick={onClose} style={{ flex: 1, padding: '11px 0', borderRadius: 8, border: '1.5px solid #E2E8F0', background: '#fff', color: '#64748B', cursor: 'pointer', fontWeight: 600 }}>Cancel</button>
                        <button onClick={handleSubmit} disabled={!decision || loading} style={{
                            flex: 2, padding: '11px 0', borderRadius: 8, border: 'none', cursor: decision ? 'pointer' : 'not-allowed',
                            background: decision ? 'linear-gradient(135deg,#2563eb,#1d4ed8)' : '#CBD5E1',
                            color: '#fff', fontWeight: 700, fontSize: 14, transition: 'all 0.2s',
                        }}>
                            {loading ? 'Processing...' : 'Confirm Decision'}
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
};

// ─── Main Component ───────────────────────────────────────────────────────────
export const ProductModerationPage = () => {
    const [activeTab, setActiveTab] = useState('Reported');
    const [products, setProducts] = useState([]);
    const [categories, setCategories] = useState([]);
    const [loading, setLoading] = useState(true);
    const [categoryFilter, setCategoryFilter] = useState('');
    const [shopFilter, setShopFilter] = useState('All');
    const [search, setSearch] = useState('');
    const [selectedProduct, setSelectedProduct] = useState(null);
    const [page, setPage] = useState(1);
    const [totalItems, setTotalItems] = useState(0); // Added state to manage total item count for pagination
    const PER_PAGE = 10;
    const totalPages = Math.ceil(totalItems / PER_PAGE) || 1;
    const fetchProducts = useCallback(async () => {
        setLoading(true);
        try {
            // Option 3: Run both APIs in parallel
            // Promise.all takes an array of Promises and returns corresponding results
            const [productData, categoriesData] = await Promise.all([
                productService.getManagedProducts({
                    pageNumber: page,
                    pageSize: PER_PAGE,
                    tab: activeTab
                }),
                categoryService.getAllCategories()
            ]);

            // Process Products data
            // Assuming API returns { items: [], totalCount: 0 } or just an array []
            const items = productData.items || productData;
            const total = productData.totalCount || (productData.items ? productData.items.length : productData.length);

            setProducts(items);
            setTotalItems(total);

            // Process Categories data
            setCategories(categoriesData);

            console.log('Data loaded successfully:', { items, total });
        } catch (err) {
            console.error('Cannot load data:', err);
            // Bạn có thể set một thông báo lỗi tại đây để hiển thị lên UI
        } finally {
            setLoading(false);
        }
    }, [activeTab, page]);

    useEffect(() => { fetchProducts(); }, [fetchProducts]);

    const handleResolve = (id, decision) => {
        // Update UI based on decision
        setProducts(prev => prev.map(p =>
            p.id === id ? { ...p, status: decision === 'Approve' ? 'Resolved' : decision === 'Remove' ? 'Rejected' : 'Resolved' } : p
        ));

        // Optional: You can also call fetchProducts() here for 100% synchronization with the server
        // fetchProducts(); 
    };

    const isFiltering = categoryFilter !== '' || shopFilter !== 'All' || search !== '';
    // If filtering via API, this client-side filter can be skipped or left if you fetch everything
    const filtered = products.filter(p => {
        // 1. Check Category (Note: check if p.category exists)
        const matchCat = categoryFilter === '' || p.categoryName === categoryFilter;
        // 2. Check Shop
        const matchShop = shopFilter === 'All' || shopFilter.includes('All') || p.shopName === shopFilter;

        // 3. Check Search (Use optional chaining ?. to avoid error if title is null)
        const matchSearch = search === '' ||
            p.title?.toLowerCase().includes(search.toLowerCase()) ||
            String(p.id).includes(search);

        // 4. IMPORTANT: Skip checking p.status === 'Pending Review' 
        // because the Server already filtered by activeTab for you.
        return matchCat && matchShop && matchSearch;
    });

    const pendingCount = products.filter(p => p.status === 'Pending Review').length;
    const displayTotalItems = isFiltering ? filtered.length : totalItems;
    const filteredTotalPages = Math.ceil(displayTotalItems / PER_PAGE) || 1;

    // Decide which product array to display in the table
    const displayProducts = isFiltering ? filtered : products;
    return (
        <>
            <style>{`
        @import url('https://fonts.googleapis.com/css2?family=Sora:wght@400;500;600;700&family=Inter:wght@400;500;600&display=swap');
        @keyframes slideUp { from { transform: translateY(20px); opacity: 0; } to { transform: translateY(0); opacity: 1; } }
        @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
        @keyframes shimmer { 0%{background-position:-400px 0} 100%{background-position:400px 0} }
        .skeleton { background: linear-gradient(90deg,#f0f4f8 25%,#e2e8f0 50%,#f0f4f8 75%); background-size: 400px; animation: shimmer 1.4s infinite; border-radius: 6px; }
        .row-hover:hover { background: #F0F7FF !important; }
        .btn-review:hover { background: #1d4ed8 !important; transform: translateY(-1px); box-shadow: 0 4px 12px rgba(37,99,235,0.3); }
        select:focus, input:focus { outline: 2px solid #2563eb; outline-offset: 1px; }
        ::-webkit-scrollbar { width: 6px; } ::-webkit-scrollbar-track { background: #f1f5f9; } ::-webkit-scrollbar-thumb { background: #CBD5E1; border-radius: 3px; }
      `}</style>

            <div style={{ minHeight: '100vh', background: '#ffffff', fontFamily: "'Inter', sans-serif", padding: '28px 20px' }}>
                <div className="container-fluid" style={{ maxWidth: 1200 }}>

                    {/* ── Page Header (Standardized) ── */}
                    <div className="mb-5">
                        <div className="d-flex flex-column align-items-center text-center">
                            <div className="d-flex align-items-center gap-3">
                                <h1 className="h2 fw-bold mb-0 text-dark" style={{ letterSpacing: '-1px' }}>Product Moderation</h1>
                                <div className="p-2 bg-primary bg-opacity-10 text-primary rounded-circle d-flex align-items-center justify-content-center" style={{ width: '40px', height: '40px' }}>
                                    <i className="bi bi-shield-check fs-5"></i>
                                </div>
                            </div>
                            <p className="text-secondary mb-0 mt-2" style={{ maxWidth: '600px', fontSize: '0.95rem', lineHeight: '1.4' }}>
                                Monitor, review, and resolve reported products to maintain platform integrity and safety.
                            </p>
                        </div>
                    </div>

                    {/* ── Quick Stats Grid (Unified Blue) ── */}
                    <div className="d-flex flex-wrap justify-content-center gap-3 w-100 mx-auto mb-4" style={{ maxWidth: '1100px' }}>
                        <div className="bg-white border-0 shadow-sm rounded-4 px-4 py-3 d-flex align-items-center gap-3 flex-grow-1 stats-card-primary" style={{ minWidth: '180px' }}>
                            <div className="p-2 bg-primary bg-opacity-10 text-primary rounded-3">
                                <i className="bi bi-box-seam fs-5"></i>
                            </div>
                            <div>
                                <div className="text-secondary small fw-medium text-uppercase ls-1" style={{ fontSize: '0.65rem' }}>Total Scanned</div>
                                <div className="h5 mb-0 fw-bold text-dark">{totalItems}</div>
                            </div>
                        </div>

                        <div className="bg-white border-0 shadow-sm rounded-4 px-4 py-3 d-flex align-items-center gap-3 flex-grow-1 stats-card-primary" style={{ minWidth: '180px' }}>
                            <div className="p-2 bg-primary bg-opacity-10 text-primary rounded-3">
                                <i className="bi bi-flag-fill fs-5"></i>
                            </div>
                            <div>
                                <div className="text-secondary small fw-medium text-uppercase ls-1" style={{ fontSize: '0.65rem' }}>Reported</div>
                                <div className="h5 mb-0 fw-bold text-dark">{pendingCount}</div>
                            </div>
                        </div>

                        <div className="bg-white border-0 shadow-sm rounded-4 px-4 py-3 d-flex align-items-center gap-3 flex-grow-1 stats-card-primary" style={{ minWidth: '220px' }}>
                            <div className="p-2 bg-primary bg-opacity-10 text-primary rounded-3">
                                <i className="bi bi-check-circle-fill fs-5"></i>
                            </div>
                            <div>
                                <div className="text-secondary small fw-medium text-uppercase ls-1" style={{ fontSize: '0.65rem' }}>Resolved</div>
                                <div className="h5 mb-0 fw-bold text-dark">{products.filter(p => p.status === 'Resolved').length}</div>
                            </div>
                        </div>

                        <div className="bg-white border-0 shadow-sm rounded-4 px-4 py-3 d-flex align-items-center gap-3 flex-grow-1 stats-card-primary" style={{ minWidth: '220px' }}>
                            <div className="p-2 bg-primary bg-opacity-10 text-primary rounded-3">
                                <i className="bi bi-x-octagon-fill fs-5"></i>
                            </div>
                            <div>
                                <div className="text-secondary small fw-medium text-uppercase ls-1" style={{ fontSize: '0.65rem' }}>Rejected</div>
                                <div className="h5 mb-0 fw-bold text-dark">{products.filter(p => p.status === 'Rejected').length}</div>
                            </div>
                        </div>
                    </div>

                    {/* ── Main Content Card ── */}
                    <div className="bg-white rounded-4 shadow-sm border-0 overflow-hidden">
                        
                        {/* ── Toolbar: Search & Filters ── */}
                        <div className="p-4 bg-white border-bottom">
                            <div className="d-flex flex-wrap align-items-center justify-content-between gap-3">
                                
                                {/* Tabs as Button Group */}
                                <div className="btn-group p-1 bg-light rounded-pill">
                                    {['All Products', 'Reported Products'].map((tab, i) => {
                                        const key = i === 0 ? 'All' : 'Reported';
                                        const active = activeTab === key;
                                        return (
                                            <button 
                                                key={tab} 
                                                onClick={() => { setActiveTab(key); setPage(1); }}
                                                className={`btn btn-sm rounded-pill px-4 py-2 border-0 fw-bold transition-all ${active ? 'bg-white text-primary shadow-sm' : 'text-secondary'}`}
                                            >
                                                {tab}
                                                {i === 1 && pendingCount > 0 && <span className="badge bg-danger ms-2">{pendingCount}</span>}
                                            </button>
                                        );
                                    })}
                                </div>

                                <div className="d-flex flex-wrap align-items-center gap-3 flex-grow-1 justify-content-md-end">
                                    {/* Search */}
                                    <div className="position-relative" style={{ minWidth: '280px' }}>
                                        <i className="bi bi-search position-absolute top-50 start-0 translate-middle-y ms-3 text-muted"></i>
                                        <input 
                                            className="form-control ps-5 rounded-pill border-0 bg-light shadow-none"
                                            value={search} 
                                            onChange={e => setSearch(e.target.value)} 
                                            placeholder="Search product ID or name..."
                                            style={{ height: '42px' }}
                                        />
                                    </div>

                                    {/* Category Filter */}
                                    <select
                                        className="form-select rounded-pill border-0 bg-light shadow-none ps-3 pe-5"
                                        style={{ width: 'auto', minWidth: '200px', height: '42px', cursor: 'pointer' }}
                                        value={categoryFilter}
                                        onChange={(e) => { setCategoryFilter(e.target.value); setPage(1); }}
                                    >
                                        <option value="">All Categories</option>
                                        {categories.map((c) => (
                                            <option key={c.tagId || c.id} value={c.tagName || c.name}>{c.tagName || c.name}</option>
                                        ))}
                                    </select>

                                    <button 
                                        className="btn btn-light rounded-circle shadow-sm d-flex align-items-center justify-content-center"
                                        onClick={() => { setSearch(""); setCategoryFilter(""); fetchProducts(); }}
                                        style={{ width: '42px', height: '42px' }}
                                        title="Reset filters"
                                    >
                                        <i className="bi bi-arrow-clockwise text-primary"></i>
                                    </button>
                                </div>
                            </div>
                        </div>

                        {/* ── Products Table ── */}
                        <div className="table-responsive">
                            <table className="table table-hover align-middle pe-table mb-0">
                                <thead>
                                    <tr>
                                        <th className="ps-4">
                                            <i className="bi bi-hash header-icon"></i>ID
                                        </th>
                                        <th>
                                            <i className="bi bi-box-seam header-icon"></i>Product info
                                        </th>
                                        <th>
                                            <i className="bi bi-shop header-icon"></i>Shop
                                        </th>
                                        <th>
                                            <i className="bi bi-flag header-icon"></i>Reason
                                        </th>
                                        <th>
                                            <i className="bi bi-circle-fill header-icon" style={{ fontSize: '0.6em' }}></i>Status
                                        </th>
                                        <th className="text-center">Review</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {loading ? (
                                        Array(PER_PAGE).fill(0).map((_, i) => (
                                            <tr key={i}>
                                                <td colSpan="6" className="py-4 text-center">
                                                    <div className="spinner-border spinner-border-sm text-primary me-2"></div>
                                                    <span className="text-muted small">Loading...</span>
                                                </td>
                                            </tr>
                                        ))
                                    ) : displayProducts.length === 0 ? (
                                        <tr>
                                            <td colSpan="6" className="text-center py-5">
                                                <div className="text-muted fs-1 mb-3">📭</div>
                                                <h5 className="text-secondary">No products found</h5>
                                                <p className="text-muted small">Try adjusting your filters or search terms</p>
                                            </td>
                                        </tr>
                                    ) : (
                                        displayProducts.map((product) => (
                                            <tr key={product.id}>
                                                <td className="ps-4">
                                                    <span className="text-muted text-monospace" style={{ fontSize: '0.8rem' }}>#{product.id}</span>
                                                </td>
                                                <td>
                                                    <div className="d-flex align-items-center gap-3">
                                                        <img 
                                                            src={product.image || '/images/default-product.png'} 
                                                            alt="" 
                                                            className="rounded-3 border shadow-sm" 
                                                            style={{ width: '42px', height: '42px', objectFit: 'cover' }} 
                                                            onError={(e) => { e.target.onerror = null; e.target.src = '/images/default-product.png'; }}
                                                        />
                                                        <div className="fw-bold text-primary cursor-pointer text-decoration-hover">{product.title}</div>
                                                    </div>
                                                </td>
                                                <td>
                                                    <div className="small fw-medium text-dark">{product.shopName}</div>
                                                </td>
                                                <td style={{ maxWidth: '200px' }}>
                                                    <div className="small text-truncate" title={product.reason}>{product.reason}</div>
                                                </td>
                                                <td>
                                                    <div className="status-indicator">
                                                        <span className={`status-dot status-dot--${product.status.toLowerCase().replace(' ', '-')}`}></span>
                                                        <span className="small fw-bold text-dark">{product.status}</span>
                                                    </div>
                                                </td>
                                                <td className="text-center">
                                                    {product.reportReasons.length > 0 ? (
                                                        <button 
                                                            className="btn btn-primary btn-sm px-3 rounded-pill shadow-sm fw-bold"
                                                            onClick={() => setSelectedProduct(product)}
                                                        >
                                                            Review
                                                        </button>
                                                    ) : (
                                                        <span className="text-success small fw-bold">
                                                            <i className="bi bi-check-circle-fill me-1"></i>Report Handled
                                                        </span>
                                                    )}
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
                                Showing <strong>{displayTotalItems === 0 ? 0 : (page - 1) * PER_PAGE + 1}</strong> to <strong>{Math.min(page * PER_PAGE, displayTotalItems)}</strong> of {displayTotalItems}
                            </span>
                            {filteredTotalPages > 1 && (
                                <nav>
                                    <ul className="pagination pagination-sm mb-0 gap-1">
                                        {Array.from({ length: filteredTotalPages }, (_, index) => index + 1).map(p => (
                                            <li key={p} className={`page-item ${page === p ? 'active' : ''}`}>
                                                <button className="page-link rounded-circle border-0 fw-bold px-3 py-2" onClick={() => setPage(p)}>
                                                    {p}
                                                </button>
                                            </li>
                                        ))}
                                    </ul>
                                </nav>
                            )}
                        </div>
                    </div>
                </div>
            </div>

            {/* Review Modal */}
            {selectedProduct && (
                <ReviewModal product={selectedProduct} onClose={() => setSelectedProduct(null)} onResolve={handleResolve} />
            )}
        </>
    );
};

export default ProductModerationPage;