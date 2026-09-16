import React, { useState, useEffect } from 'react';
import { productService } from '../../services/productService'; // Ensure correct path
import { reviewsService } from '../../services/reviewsService';

export const ProductList = () => {
    const [products, setProducts] = useState([]);
    const [loading, setLoading] = useState(true);

    // --- Report Modal State ---
    const [reportingProductId, setReportingProductId] = useState(null);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [reportData, setReportData] = useState({
        reason: 'Counterfeit Item',
        description: '',
        evidenceFiles: '',
        priority: 'Low'
    });

    // --- Review Modal State ---
    const [reviewingProductId, setReviewingProductId] = useState(null);
    const [reviewData, setReviewData] = useState({
        rating: 5,
        comment: ''
    });

    const parseImageUrl = (imgStr) => {
        if (!imgStr || imgStr === "[]") return '/images/default-product.png';
        try {
            const parsed = JSON.parse(imgStr);
            if (Array.isArray(parsed) && parsed.length > 0) return parsed[0];
            if (Array.isArray(parsed) && parsed.length === 0) return '/images/default-product.png';
            return imgStr;
        } catch (e) {
            return imgStr;
        }
    };

    useEffect(() => {
        const fetchProducts = async () => {
            try {
                setLoading(true);
                // Depending on the actual API, we assume a list fetching function here
                const data = await productService.getAllProducts();
                // If backend returns pagination, remember to change to data.items || data
                const allProducts = data.items || data || [];
                setProducts(allProducts.filter(p => p.status === 'Active')); 
            } catch (error) {
                console.error("Could not load data", error);
            } finally {
                setLoading(false);
            }
        };

        fetchProducts();
    }, []);

    // --- Open Modal Function ---
    const handleOpenReport = (productId) => {
        setReportingProductId(productId);
        // Reset form mỗi khi mở pop-up mới
        setReportData({
            priority: 'Low'
        });
    };

    // --- Close Modal Function ---
    const handleCloseReport = () => {
        setReportingProductId(null);
    };

    const handleOpenReview = (productId) => {
        setReviewingProductId(productId);
        setReviewData({ rating: 5, comment: '' });
    };

    const handleCloseReview = () => {
        setReviewingProductId(null);
    };

    // --- Input Change Handler in Form ---
    const handleInputChange = (e) => {
        const { name, value } = e.target;
        setReportData(prev => ({ ...prev, [name]: value }));
    };

    // --- Report Form Submission Handler ---
    const handleSubmitReport = async (e) => {
        e.preventDefault();
        setIsSubmitting(true);

        try {
            const payload = {
                productId: reportingProductId,
                reporterUserId: 1, // TODO: Replace with actual logged-in user ID from Context/Redux
                reporterType: "User", // Hardcoded per BE requirement
                reason: reportData.reason,
                description: reportData.description,
                // Convert URL string to JSON string array to match C# 'string? EvidenceFiles'
                evidenceFiles: reportData.evidenceFiles ? JSON.stringify([reportData.evidenceFiles]) : null,
                status: "Pending",
                priority: reportData.priority
            };

            // Call API using the service you defined
            await productService.reportProduct(reportingProductId, payload);
            
            alert("Report sent successfully!");
            handleCloseReport(); // Close modal after successful submission
        } catch (error) {
            console.error(error);
            alert("Error sending report. Please try again.");
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleSubmitReview = async (e) => {
        e.preventDefault();
        setIsSubmitting(true);

        try {
            const payload = {
                productId: reviewingProductId,
                reviewerId: 1, // Mock user ID
                rating: parseInt(reviewData.rating),
                comment: reviewData.comment
            };

            await reviewsService.createReview(payload);
            
            alert("Review submitted successfully! It will be moderated if it contains sensitive content.");
            handleCloseReview();
        } catch (error) {
            console.error(error);
            alert("Error submitting review.");
        } finally {
            setIsSubmitting(false);
        }
    };

    if (loading) {
        return <p style={{ padding: 20 }}>Loading products...</p>;
    }

    return (
        <div style={{ padding: 20, fontFamily: 'sans-serif' }}>
            <h2>Product List (Axios Custom)</h2>
            <ul style={{ listStyle: 'none', padding: 0 }}>
                {products.length === 0 ? (
                    <li>No products found.</li>
                ) : (
                    products.map((product) => (
                        <li key={product.id} style={{ padding: '12px', borderBottom: '1px solid #f1f5f9', display: 'flex', gap: '16px', alignItems: 'center', maxWidth: 600 }}>
                            <img 
                                src={parseImageUrl(product.image)} 
                                alt={product.title} 
                                style={{ width: 64, height: 64, borderRadius: 12, objectFit: 'cover', border: '1px solid #e2e8f0' }} 
                                onError={(e) => { e.target.onerror = null; e.target.src = '/images/default-product.png'; }}
                            />
                            <div style={{ flex: 1 }}>
                                <div style={{ fontSize: '15px', fontWeight: 'Bold', color: '#1e293b' }}>{product.title}</div>
                                <div style={{ fontSize: '14px', color: '#64748b', marginTop: '2px' }}>Price: <span style={{ color: '#0d6efd', fontWeight: 'Bold' }}>${product.price}</span></div>
                            </div>
                            <div style={{ display: 'flex', gap: '8px' }}>
                                <button 
                                    onClick={() => handleOpenReview(product.id)}
                                    style={{ background: '#3b82f6', color: 'white', border: 'none', padding: '6px 12px', borderRadius: 4, cursor: 'pointer' }}
                                >
                                    Review
                                </button>
                                <button 
                                    onClick={() => handleOpenReport(product.id)}
                                    style={{ background: '#ef4444', color: 'white', border: 'none', padding: '6px 12px', borderRadius: 4, cursor: 'pointer' }}
                                >
                                    Report
                                </button>
                            </div>
                        </li>
                    ))
                )}
            </ul>

            {/* --- REPORT MODAL --- */}
            {reportingProductId && (
                <div style={{
                    position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
                    backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex',
                    alignItems: 'center', justifyContent: 'center', zIndex: 1000
                }}>
                    <div style={{
                        background: '#fff', padding: '24px', borderRadius: '8px', 
                        width: '100%', maxWidth: '400px', boxShadow: '0 4px 6px rgba(0,0,0,0.1)'
                    }}>
                        <h3 style={{ marginTop: 0 }}>Report product #{reportingProductId}</h3>
                        
                        <form onSubmit={handleSubmitReport} style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                            
                            {/* Lý do báo cáo */}
                            <div>
                                <label style={{ display: 'block', fontSize: '13px', fontWeight: 'bold', marginBottom: '4px' }}>Violation Reason</label>
                                <select 
                                    name="reason" 
                                    value={reportData.reason} 
                                    onChange={handleInputChange}
                                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                                >
                                    <option value="Counterfeit Item">Counterfeit / Fake</option>
                                    <option value="Inappropriate Content">Inappropriate Content</option>
                                    <option value="Prohibited Item">Prohibited Item</option>
                                    <option value="Other">Other</option>
                                </select>
                            </div>

                            {/* Mô tả chi tiết */}
                            <div>
                                <label style={{ display: 'block', fontSize: '13px', fontWeight: 'bold', marginBottom: '4px' }}>Detailed Description</label>
                                <textarea 
                                    name="description" 
                                    value={reportData.description} 
                                    onChange={handleInputChange}
                                    required
                                    rows={3}
                                    placeholder="Please provide more information..."
                                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc', resize: 'vertical' }}
                                />
                            </div>

                            {/* Bằng chứng */}
                            <div>
                                <label style={{ display: 'block', fontSize: '13px', fontWeight: 'bold', marginBottom: '4px' }}>Evidence Link (Optional)</label>
                                <input 
                                    type="text" 
                                    name="evidenceFiles" 
                                    value={reportData.evidenceFiles} 
                                    onChange={handleInputChange}
                                    placeholder="https://imgur.com/your-image"
                                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                                />
                            </div>

                            {/* Mức độ ưu tiên */}
                            <div>
                                <label style={{ display: 'block', fontSize: '13px', fontWeight: 'bold', marginBottom: '4px' }}>Priority</label>
                                <select 
                                    name="priority" 
                                    value={reportData.priority} 
                                    onChange={handleInputChange}
                                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                                >
                                    <option value="Low">Low</option>
                                    <option value="High">High</option>
                                </select>
                                <span style={{ fontSize: '11px', color: '#666' }}>*VeRO (copyright owners) will always be Critical from the system.</span>
                            </div>

                            {/* Nút hành động */}
                            <div style={{ display: 'flex', gap: '10px', marginTop: '10px' }}>
                                <button 
                                    type="button" 
                                    onClick={handleCloseReport} 
                                    style={{ flex: 1, padding: '10px', borderRadius: '4px', border: '1px solid #ccc', background: '#f9fafb', cursor: 'pointer' }}
                                >
                                    Cancel
                                </button>
                                <button 
                                    type="submit" 
                                    disabled={isSubmitting}
                                    style={{ flex: 1, padding: '10px', borderRadius: '4px', border: 'none', background: '#3b82f6', color: '#fff', fontWeight: 'bold', cursor: isSubmitting ? 'not-allowed' : 'pointer' }}
                                >
                                    {isSubmitting ? 'Sending...' : 'Send Report'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* --- REVIEW MODAL --- */}
            {reviewingProductId && (
                <div style={{
                    position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
                    backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex',
                    alignItems: 'center', justifyContent: 'center', zIndex: 1000
                }}>
                    <div style={{
                        background: '#fff', padding: '24px', borderRadius: '12px', 
                        width: '100%', maxWidth: '400px', boxShadow: '0 10px 25px rgba(0,0,0,0.2)'
                    }}>
                        <h3 style={{ marginTop: 0, color: '#1a1a1a' }}>Review product #{reviewingProductId}</h3>
                        
                        <form onSubmit={handleSubmitReview} style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                            <div>
                                <label style={{ display: 'block', fontSize: '14px', fontWeight: 'bold', marginBottom: '8px' }}>Star Rating</label>
                                <div style={{ display: 'flex', gap: '8px' }}>
                                    {[1, 2, 3, 4, 5].map(star => (
                                        <button
                                            key={star}
                                            type="button"
                                            onClick={() => setReviewData(prev => ({ ...prev, rating: star }))}
                                            style={{
                                                background: 'none', border: 'none', fontSize: '24px', 
                                                color: star <= reviewData.rating ? '#ffc107' : '#ddd',
                                                cursor: 'pointer', padding: 0
                                            }}
                                        >
                                            ★
                                        </button>
                                    ))}
                                </div>
                            </div>

                            <div>
                                <label style={{ display: 'block', fontSize: '14px', fontWeight: 'bold', marginBottom: '8px' }}>Your Feedback</label>
                                <textarea 
                                    value={reviewData.comment} 
                                    onChange={(e) => setReviewData(prev => ({ ...prev, comment: e.target.value }))}
                                    required
                                    rows={4}
                                    placeholder="Share your experience..."
                                    style={{ width: '100%', padding: '12px', borderRadius: '8px', border: '1px solid #ddd', fontSize: '14px' }}
                                />
                                {reviewData.comment && ["scam", "fuck", "die"].some(w => reviewData.comment.toLowerCase().includes(w)) && (
                                    <p style={{ color: '#ef4444', fontSize: '12px', marginTop: '4px' }}>
                                        ⚠️ Content contains sensitive words, it will be flagged by the system.
                                    </p>
                                )}
                            </div>

                            <div style={{ display: 'flex', gap: '12px' }}>
                                <button 
                                    type="button" 
                                    onClick={handleCloseReview} 
                                    style={{ flex: 1, padding: '12px', borderRadius: '8px', border: '1px solid #ddd', background: '#fff', cursor: 'pointer', fontWeight: 'bold' }}
                                >
                                    Cancel
                                </button>
                                <button 
                                    type="submit" 
                                    disabled={isSubmitting}
                                    style={{ 
                                        flex: 1, padding: '12px', borderRadius: '8px', border: 'none', 
                                        background: '#3b82f6', color: '#fff', fontWeight: 'bold', 
                                        cursor: isSubmitting ? 'not-allowed' : 'pointer'
                                    }}
                                >
                                    {isSubmitting ? 'Sending...' : 'Submit Review'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}
        </div>
    );
};