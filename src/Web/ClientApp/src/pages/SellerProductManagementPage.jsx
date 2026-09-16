import React, { useState, useEffect } from 'react';
import { Container, Table, Button, Badge, Form, Row, Col, Modal, Alert } from 'react-bootstrap';
import { productService } from '../services/productService';
import { useAuth } from '../services/useAuth'; // Hook to get user info

const SellerProductManagementPage = () => {
    const { user } = useAuth(); // Get logged in user info, e.g., { id: 1, role: 'Seller' }
    const [products, setProducts] = useState([]);
    const [loading, setLoading] = useState(false);
    
    // Add test user ID if not logged in for easy debugging
    const sellerId = user?.id || 1; 

    // Add modal state
    const [showAddModal, setShowAddModal] = useState(false);
    const [newProduct, setNewProduct] = useState({ title: '', description: '', price: '', categoryId: 1, images: '', isAuction: false });
    const [addError, setAddError] = useState('');

    useEffect(() => {
        fetchProducts();
    }, [sellerId]);

    const fetchProducts = async () => {
        setLoading(true);
        try {
            const data = await productService.getSellerProducts(sellerId);
            setProducts(data.items || []); // PaginatedList returns .items
        } catch (err) {
            console.error(err);
        } finally {
            setLoading(false);
        }
    };

    const handleAddProduct = async (e) => {
        e.preventDefault();
        setAddError('');
        try {
            await productService.createProduct({
                ...newProduct,
                sellerId: sellerId
            });
            setShowAddModal(false);
            setNewProduct({ title: '', description: '', price: '', categoryId: 1, images: '', isAuction: false });
            fetchProducts();
        } catch (err) {
            setAddError("Failed to add product. Please try again.");
        }
    };

    const getStatusBadge = (status) => {
        switch (status) {
            case 'Active': return <Badge bg="success">Active</Badge>;
            case 'InActive': return <Badge bg="secondary">Inactive</Badge>;
            case 'Hidden': return <Badge bg="danger">Hidden (Violation)</Badge>;
            case 'PendingReview': return <Badge bg="warning">Pending Review</Badge>;
            default: return <Badge bg="info">{status}</Badge>;
        }
    };

    return (
        <Container fluid className="px-5 py-4" style={{ backgroundColor: '#F9FAFB', minHeight: '100vh', fontFamily: "'Inter', sans-serif" }}>
            <div className="d-flex justify-content-between align-items-center mb-4">
                <div>
                    <h2 style={{ fontWeight: 700, color: '#111827' }}>Product Management (Seller)</h2>
                    <p className="text-muted">View, add, and manage your active listings.</p>
                </div>
                <button className="btn btn-dark" style={{ borderRadius: '8px', padding: '10px 20px', fontWeight: 600 }} onClick={() => setShowAddModal(true)}>
                    + Add New Product
                </button>
            </div>

            <div style={{ backgroundColor: 'white', borderRadius: '16px', boxShadow: '0 4px 12px rgba(0,0,0,0.05)', padding: '24px' }}>
                <Table hover responsive style={{ borderCollapse: 'separate', borderSpacing: '0 8px' }}>
                    <thead>
                        <tr style={{ color: '#6B7280', fontSize: '13px', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                            <th style={{ borderBottom: 'none' }}>ID</th>
                            <th style={{ borderBottom: 'none' }}>Product</th>
                            <th style={{ borderBottom: 'none' }}>Price</th>
                            <th style={{ borderBottom: 'none' }}>Status</th>
                            <th style={{ borderBottom: 'none' }}>Notes / Violations</th>
                        </tr>
                    </thead>
                    <tbody>
                        {loading ? (
                            <tr><td colSpan="5" className="text-center py-4">Loading...</td></tr>
                        ) : products.length === 0 ? (
                            <tr><td colSpan="5" className="text-center py-4">No products found.</td></tr>
                        ) : (
                            products.map(p => (
                                <tr key={p.id} style={{ backgroundColor: p.status === 'Hidden' ? '#FEF2F2' : 'white', boxShadow: '0 1px 3px rgba(0,0,0,0.02)', borderRadius: '8px' }}>
                                    <td style={{ verticalAlign: 'middle', borderTop: '1px solid #F3F4F6', borderBottom: '1px solid #F3F4F6' }}>
                                        #{p.id}
                                    </td>
                                    <td style={{ verticalAlign: 'middle', borderTop: '1px solid #F3F4F6', borderBottom: '1px solid #F3F4F6', fontWeight: 500, color: '#111827' }}>
                                        {p.title}
                                    </td>
                                    <td style={{ verticalAlign: 'middle', borderTop: '1px solid #F3F4F6', borderBottom: '1px solid #F3F4F6' }}>
                                        ${p.price?.toFixed(2)}
                                    </td>
                                    <td style={{ verticalAlign: 'middle', borderTop: '1px solid #F3F4F6', borderBottom: '1px solid #F3F4F6' }}>
                                        {getStatusBadge(p.status)}
                                    </td>
                                    <td style={{ verticalAlign: 'middle', borderTop: '1px solid #F3F4F6', borderBottom: '1px solid #F3F4F6', color: '#DC2626', fontSize: '14px' }}>
                                        {p.status === 'Hidden' && "Policy violation: " + (p.violationType || p.moderationNotes || "Inappropriate content/Spam")}
                                    </td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </Table>
            </div>

            {/* Add Product Modal */}
            <Modal show={showAddModal} onHide={() => setShowAddModal(false)} centered>
                <Modal.Header closeButton style={{ borderBottom: 'none' }}>
                    <Modal.Title style={{ fontWeight: 700 }}>Add New Product</Modal.Title>
                </Modal.Header>
                <Form onSubmit={handleAddProduct}>
                    <Modal.Body>
                        {addError && <Alert variant="danger">{addError}</Alert>}
                        <Form.Group className="mb-3">
                            <Form.Label style={{ fontWeight: 600, fontSize: '14px' }}>Product Title (Will be auto-reviewed by AI)</Form.Label>
                            <Form.Control required type="text" placeholder="Enter product name..." value={newProduct.title} onChange={e => setNewProduct({...newProduct, title: e.target.value})} />
                        </Form.Group>
                        <Row>
                            <Col md={6}>
                                <Form.Group className="mb-3">
                                    <Form.Label style={{ fontWeight: 600, fontSize: '14px' }}>Price ($)</Form.Label>
                                    <Form.Control required type="number" step="0.01" min="0" placeholder="0.00" value={newProduct.price} onChange={e => setNewProduct({...newProduct, price: parseFloat(e.target.value)})} />
                                </Form.Group>
                            </Col>
                            <Col md={6}>
                                <Form.Group className="mb-3">
                                    <Form.Label style={{ fontWeight: 600, fontSize: '14px' }}>Category ID</Form.Label>
                                    <Form.Control required type="number" min="1" value={newProduct.categoryId} onChange={e => setNewProduct({...newProduct, categoryId: parseInt(e.target.value)})} />
                                </Form.Group>
                            </Col>
                        </Row>
                        <Form.Group className="mb-3">
                            <Form.Label style={{ fontWeight: 600, fontSize: '14px' }}>Additional Description</Form.Label>
                            <Form.Control as="textarea" rows={3} placeholder="Product description" value={newProduct.description} onChange={e => setNewProduct({...newProduct, description: e.target.value})} />
                        </Form.Group>
                    </Modal.Body>
                    <Modal.Footer style={{ borderTop: 'none' }}>
                        <Button variant="light" onClick={() => setShowAddModal(false)}>Cancel</Button>
                        <Button variant="dark" type="submit" style={{ borderRadius: '8px', fontWeight: 600 }}>Create New Product</Button>
                    </Modal.Footer>
                </Form>
            </Modal>
        </Container>
    );
};

export default SellerProductManagementPage;
