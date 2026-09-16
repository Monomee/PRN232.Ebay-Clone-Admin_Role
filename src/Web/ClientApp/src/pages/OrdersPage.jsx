import React, { useState, useEffect } from 'react';
import orderService from '../services/orderService';
import { 
  Card, CardBody, CardHeader, Table, Badge, Button, 
  Input, InputGroup, InputGroupText, Pagination, PaginationItem, PaginationLink,
  Modal, ModalHeader, ModalBody, ModalFooter, Spinner, Row, Col,
  UncontrolledDropdown, DropdownToggle, DropdownMenu, DropdownItem
} from 'reactstrap';

const OrdersPage = () => {
  const [orders, setOrders] = useState([]);
  const [loading, setLoading] = useState(true);
  const [totalCount, setTotalCount] = useState(0);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageNumberSize] = useState(10); // Changed to state to support selection
  const setPageSize = (size) => { setPageNumberSize(size); setPageNumber(1); }; // Helper to reset page
  const [status, setStatus] = useState('All');
  const [search, setSearch] = useState('');
  
  // Stats
  const [stats, setStats] = useState({
    totalRevenue: 0,
    activeOrders: 0,
    completedOrders: 0
  });

  // Detail Modal State
  const [selectedOrder, setSelectedOrder] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);

  useEffect(() => {
    fetchOrders();
  }, [pageNumber, pageSize, status]);

  const fetchOrders = async () => {
    setLoading(true);
    try {
      const data = await orderService.getOrders({
        pageNumber,
        pageSize,
        status,
        search
      });
      setOrders(data.items);
      setTotalCount(data.totalCount);
      
      // Calculate simple stats from current page for demo (ideally these come from a dedicated API)
      const revenue = data.items.reduce((acc, curr) => acc + (curr.totalPrice || 0), 0);
      const active = data.items.filter(o => o.status === 'Pending' || o.status === 'Shipped').length;
      const completed = data.items.filter(o => o.status === 'Delivered').length;
      
      setStats({
        totalRevenue: revenue,
        activeOrders: active,
        completedOrders: completed
      });
    } catch (error) {
      console.error("Error fetching orders:", error);
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = (e) => {
    if (e.key === 'Enter') {
      setPageNumber(1);
      fetchOrders();
    }
  };

  const handleViewDetails = async (id) => {
    setModalOpen(true);
    setDetailLoading(true);
    try {
      const data = await orderService.getOrderById(id);
      setSelectedOrder(data);
    } catch (error) {
      console.error("Error fetching order details:", error);
    } finally {
      setDetailLoading(false);
    }
  };

  const toggleModal = () => setModalOpen(!modalOpen);
  
  const resetFilters = () => {
    setSearch('');
    setStatus('All');
    setPageNumber(1);
  };

  const getStatusUI = (status) => {
    switch (status) {
      case 'Pending': 
        return { color: 'warning', icon: 'bi-clock-history', label: 'Pending' };
      case 'Shipped': 
        return { color: 'primary', icon: 'bi-truck', label: 'Shipped' };
      case 'Delivered': 
        return { color: 'success', icon: 'bi-check2-circle', label: 'Delivered' };
      case 'Canceled': 
        return { color: 'danger', icon: 'bi-x-circle', label: 'Canceled' };
      case 'Completed': 
        return { color: 'info', icon: 'bi-flag', label: 'Completed' };
      default: 
        return { color: 'secondary', icon: 'bi-question-circle', label: status };
    }
  };

  const getStatusBadge = (status) => {
    const ui = getStatusUI(status);
    return (
      <Badge color={ui.color} pill className="d-inline-flex align-items-center gap-1 px-2 py-1">
        <i className={`bi ${ui.icon}`}></i>
        <span>{ui.label}</span>
      </Badge>
    );
  };

  const formatRelativeTime = (dateString) => {
    const date = new Date(dateString);
    const now = new Date();
    const diffInSeconds = Math.floor((now - date) / 1000);
    
    if (diffInSeconds < 60) return 'Just now';
    if (diffInSeconds < 3600) return `${Math.floor(diffInSeconds / 60)}m ago`;
    if (diffInSeconds < 86400) return `${Math.floor(diffInSeconds / 3600)}h ago`;
    if (diffInSeconds < 604800) return `${Math.floor(diffInSeconds / 86400)}d ago`;
    
    return date.toLocaleDateString();
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  const ProgressStep = ({ label, icon, active, completed }) => (
    <div className={`d-flex flex-column align-items-center position-relative flex-grow-1`}>
      <div 
        className={`rounded-circle d-flex align-items-center justify-content-center mb-2 z-1 transition-all shadow-sm`}
        style={{ 
          width: '40px', 
          height: '40px', 
          backgroundColor: completed ? '#198754' : active ? '#0d6efd' : '#e9ecef',
          color: (completed || active) ? 'white' : '#adb5bd',
          border: active ? '3px solid #cfe2ff' : 'none'
        }}
      >
        <i className={`bi ${completed ? 'bi-check-lg' : icon}`}></i>
      </div>
      <span className={`small fw-bold ${active ? 'text-primary' : completed ? 'text-success' : 'text-muted'}`}>{label}</span>
    </div>
  );

  return (
    <div className="container-fluid py-4" style={{ background: '#ffffff', minHeight: '100vh' }}>
      <div className="d-flex flex-column align-items-center mb-5 text-center">
        <h2 className="fw-bold mb-0 text-dark" style={{ letterSpacing: '-1px' }}>Order Management</h2>
        <p className="text-secondary mb-0 mt-2" style={{ fontSize: '0.95rem', maxWidth: '600px' }}>
          Monitor and fulfill platform transactions with an overview of all customer activities.
        </p>
      </div>

      {/* --- Visual Stats Widgets --- */}
      <Row className="g-3 mb-4">
        <Col md="4">
          <Card className="border-0 shadow-sm rounded-4 h-100">
            <CardBody className="d-flex align-items-center p-3">
              <div className="bg-success bg-opacity-10 p-3 rounded-4 me-3 text-success">
                <i className="bi bi-wallet2 h3 mb-0"></i>
              </div>
              <div>
                <p className="text-secondary small fw-bold text-uppercase mb-0" style={{ letterSpacing: '1px' }}>Total Revenue</p>
                <h4 className="fw-bold mb-0">${stats.totalRevenue.toLocaleString(undefined, { minimumFractionDigits: 2 })}</h4>
              </div>
            </CardBody>
          </Card>
        </Col>
        <Col md="4">
          <Card className="border-0 shadow-sm rounded-4 h-100">
            <CardBody className="d-flex align-items-center p-3">
              <div className="bg-primary bg-opacity-10 p-3 rounded-4 me-3 text-primary">
                <i className="bi bi-box-seam h3 mb-0"></i>
              </div>
              <div>
                <p className="text-secondary small fw-bold text-uppercase mb-0" style={{ letterSpacing: '1px' }}>Active Orders</p>
                <h4 className="fw-bold mb-0">{stats.activeOrders} <span className="small text-muted fw-normal">unfulfilled</span></h4>
              </div>
            </CardBody>
          </Card>
        </Col>
        <Col md="4">
          <Card className="border-0 shadow-sm rounded-4 h-100">
            <CardBody className="d-flex align-items-center p-3">
              <div className="bg-info bg-opacity-10 p-3 rounded-4 me-3 text-info">
                <i className="bi bi-check-all h3 mb-0"></i>
              </div>
              <div>
                <p className="text-secondary small fw-bold text-uppercase mb-0" style={{ letterSpacing: '1px' }}>Success Rate</p>
                <h4 className="fw-bold mb-0">100% <span className="small text-muted fw-normal">fulfillment</span></h4>
              </div>
            </CardBody>
          </Card>
        </Col>
      </Row>

      <Card className="shadow-sm border-0 rounded-4 overflow-hidden mb-4">
        <CardBody className="p-0">
          <div className="p-3 bg-white d-flex flex-wrap justify-content-between align-items-center gap-3 border-bottom">
            <div>
              <UncontrolledDropdown size="sm">
                <DropdownToggle caret color="white" className="fw-bold px-3 rounded-pill border shadow-sm">
                  <i className="bi bi-funnel me-2"></i>
                  {status === 'All' ? 'Showing All Orders' : `Filtering: ${status}`}
                </DropdownToggle>
                <DropdownMenu className="border-0 shadow-lg rounded-3 mt-2">
                  {['All', 'Pending', 'Shipped', 'Delivered', 'Canceled'].map(s => (
                    <DropdownItem 
                      key={s} 
                      active={status === s}
                      onClick={() => { setStatus(s); setPageNumber(1); }}
                      className="py-2"
                    >
                      {s}
                    </DropdownItem>
                  ))}
                </DropdownMenu>
              </UncontrolledDropdown>
            </div>
            
            {(status !== 'All' || search) && (
              <div className="me-auto">
                <Button 
                  color="link" 
                  className="text-secondary text-decoration-none small fw-bold d-flex align-items-center gap-1 border rounded-pill px-3 bg-light bg-opacity-50"
                  onClick={resetFilters}
                  style={{ fontSize: '0.8rem' }}
                >
                  <i className="bi bi-x-circle text-danger"></i>
                  Clear Filters
                </Button>
              </div>
            )}
            <div style={{ minWidth: '350px' }}>
              <InputGroup className="shadow-sm rounded-pill overflow-hidden border">
                <InputGroupText className="bg-white border-0 ps-3"><i className="bi bi-search text-secondary"></i></InputGroupText>
                <Input 
                  className="border-0 shadow-none"
                  placeholder="Order ID, User or Item..." 
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  onKeyDown={handleSearch}
                  style={{ fontSize: '0.9rem' }}
                />
              </InputGroup>
            </div>
          </div>
          
          <Table responsive className="mb-0 align-middle pe-table">
            <thead className="bg-light text-secondary small text-uppercase fw-bold">
              <tr>
                <th className="ps-4">Order ID</th>
                <th>Purchased At</th>
                <th>Customer</th>
                <th>Total Value</th>
                <th>Workflow</th>
                <th className="text-end pe-4">Detail</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="6" className="text-center py-5">
                    <Spinner color="primary" size="sm" />
                    <p className="mt-2 mb-0 text-secondary small">Syncing orders...</p>
                  </td>
                </tr>
              ) : orders.length === 0 ? (
                <tr>
                  <td colSpan="6" className="text-center py-5">
                    <i className="bi bi-inbox h1 text-light d-block mb-3"></i>
                    <p className="text-secondary">No records found for the selected view.</p>
                  </td>
                </tr>
              ) : (
                orders.map(order => (
                  <tr key={order.id} className="transition-all">
                    <td className="ps-4 fw-bold text-primary">#{order.id}</td>
                    <td className="text-secondary small">{formatRelativeTime(order.orderDate)}</td>
                    <td>
                      <div className="d-flex align-items-center gap-2">
                        <div className="bg-light rounded-circle d-flex align-items-center justify-content-center fw-bold text-secondary" style={{ width: 32, height: 32, fontSize: '0.8rem' }}>
                          {order.buyerName?.charAt(0) || 'U'}
                        </div>
                        <span className="fw-semibold">{order.buyerName}</span>
                      </div>
                    </td>
                    <td>
                      <div className="fw-bold text-dark">${order.totalPrice?.toFixed(2)}</div>
                      <div className="x-small text-muted">{order.itemCount} item(s)</div>
                    </td>
                    <td>{getStatusBadge(order.status)}</td>
                    <td className="text-end pe-4">
                      <Button color="light" size="sm" className="rounded-circle" title="View Details" onClick={() => handleViewDetails(order.id)}>
                        <i className="bi bi-eye"></i>
                      </Button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </Table>

          {/* Sellers-style Pagination Controls */}
          {!loading && orders.length > 0 && (
            <div className="p-3 bg-white border-top d-flex justify-content-between align-items-center flex-wrap gap-2">
              <div className="d-flex align-items-center gap-2">
                <select
                  className="form-select border-0 bg-light rounded-pill py-1 ps-3 pe-5 text-muted small"
                  style={{
                    width: 'auto',
                    minWidth: '70px',
                    fontSize: '0.8rem',
                    cursor: 'pointer'
                  }}
                  value={pageSize}
                  onChange={(e) => setPageSize(Number(e.target.value))}
                >
                  <option value={10}>10</option>
                  <option value={20}>20</option>
                  <option value={50}>50</option>
                </select>
                <span className="text-muted small">
                  Showing {((pageNumber - 1) * pageSize) + 1} to {Math.min(pageNumber * pageSize, totalCount)} of {totalCount}
                </span>
              </div>
              <div className="d-flex gap-2">
                <Button
                  color="outline-secondary"
                  size="sm"
                  disabled={pageNumber <= 1}
                  onClick={() => setPageNumber(p => p - 1)}
                  className="rounded-pill px-3 border-0 bg-light text-dark fw-bold"
                  style={{ fontSize: '0.8rem' }}
                >
                  <i className="bi bi-chevron-left me-1"></i> Previous
                </Button>
                <div className="align-self-center px-3 small border-start border-end fw-bold text-secondary">
                  Page <strong>{pageNumber}</strong> of {totalPages || 1}
                </div>
                <Button
                  color="outline-secondary"
                  size="sm"
                  disabled={pageNumber >= totalPages}
                  onClick={() => setPageNumber(p => p + 1)}
                  className="rounded-pill px-3 border-0 bg-light text-dark fw-bold"
                  style={{ fontSize: '0.8rem' }}
                >
                  Next <i className="bi bi-chevron-right ms-1"></i>
                </Button>
              </div>
            </div>
          )}
        </CardBody>
      </Card>

      {/* --- Visual Order Detail Modal --- */}
      <Modal isOpen={modalOpen} toggle={toggleModal} size="lg" centered className="border-0">
        <ModalHeader toggle={toggleModal} className="border-0 pb-0">
          <div className="d-flex align-items-center gap-2">
             <i className="bi bi-receipt h4 mb-0 text-primary"></i>
             <span className="fw-bold">Order Summary</span>
             {selectedOrder && <Badge color="primary" className="rounded-pill px-3 py-2 ms-2 shadow-sm">#{selectedOrder.id}</Badge>}
          </div>
        </ModalHeader>
        <ModalBody className="pt-3">
          {detailLoading ? (
            <div className="text-center py-5">
              <Spinner color="primary" />
            </div>
          ) : selectedOrder && (
            <>
              {/* --- Order Progress Stepper --- */}
              <div className="px-4 py-4 mb-4 bg-white shadow-sm border rounded-4 d-flex justify-content-between align-items-center position-relative">
                <div className="position-absolute border-top w-75" style={{ top: '44px', left: '12%', zIndex: 0, borderColor: '#dee2e6' }}></div>
                <ProgressStep label="Placed" icon="bi-bag" completed={true} />
                <ProgressStep label="Shipped" icon="bi-truck" 
                  active={selectedOrder.status === 'Shipped'} 
                  completed={['Delivered', 'Completed'].includes(selectedOrder.status)} 
                />
                <ProgressStep label="Delivered" icon="bi-house-check" 
                  active={selectedOrder.status === 'Delivered'} 
                  completed={selectedOrder.status === 'Completed'} 
                />
                <ProgressStep label="Finalized" icon="bi-award" 
                  active={selectedOrder.status === 'Completed'} 
                  completed={false} 
                />
              </div>

              <Row className="mb-4 g-4">
                <Col md="6">
                  <div className="h-100 p-3 bg-light rounded-4 border">
                    <p className="mb-2 text-secondary small fw-bold text-uppercase"><i className="bi bi-person me-2"></i>Buyer Information</p>
                    <div className="d-flex align-items-center gap-3">
                      <div className="bg-primary text-white rounded-circle d-flex align-items-center justify-content-center fw-bold" style={{ width: 44, height: 44 }}>
                        {selectedOrder.buyerName?.charAt(0)}
                      </div>
                      <div>
                        <p className="mb-0 fw-bold">{selectedOrder.buyerName}</p>
                        <p className="mb-0 text-secondary small">{selectedOrder.buyerEmail}</p>
                      </div>
                    </div>
                  </div>
                </Col>
                <Col md="6">
                   <div className="h-100 p-3 bg-light rounded-4 border">
                    <p className="mb-2 text-secondary small fw-bold text-uppercase"><i className="bi bi-geo-alt me-2"></i>Shipping Destination</p>
                    <p className="mb-0 fw-semibold">{selectedOrder.shippingAddress || 'Standard Ground Shipping'}</p>
                    <p className="mb-0 small text-secondary">Est. Delivery: {new Date(new Date(selectedOrder.orderDate).getTime() + 7*24*60*60*1000).toLocaleDateString()}</p>
                  </div>
                </Col>
              </Row>

              <div className="mb-2 d-flex justify-content-between align-items-end">
                <p className="fw-bold mb-0">Manifest Items</p>
                <span className="badge bg-light text-dark border">{selectedOrder.items.length} unique SKU(s)</span>
              </div>
              <Table responsive borderless className="align-middle mb-0 pe-table--detail">
                <thead className="small text-uppercase text-secondary border-bottom">
                   <tr>
                     <th>Product</th>
                     <th className="text-center">Qty</th>
                     <th className="text-end">Unit</th>
                     <th className="text-end">Total</th>
                   </tr>
                </thead>
                <tbody>
                   {selectedOrder.items.map(item => (
                     <tr key={item.id} className="border-bottom-light">
                        <td>
                          <div className="fw-bold text-dark">{item.productName}</div>
                          <div className="x-small text-muted">Sold by: {item.sellerName}</div>
                        </td>
                        <td className="text-center"><span className="badge bg-white text-dark border px-2">{item.quantity}</span></td>
                        <td className="text-end text-secondary small">${item.unitPrice?.toFixed(2)}</td>
                        <td className="text-end fw-bold">${(item.unitPrice * item.quantity).toFixed(2)}</td>
                     </tr>
                   ))}
                </tbody>
                <tfoot>
                   <tr className="border-top">
                     <td colSpan="3" className="text-end text-secondary pt-3">Final Transaction Value:</td>
                     <td className="text-end pt-3 h5 fw-bold text-primary">${selectedOrder.totalPrice?.toFixed(2)}</td>
                   </tr>
                </tfoot>
              </Table>
            </>
          )}
        </ModalBody>
      </Modal>

      <style>{`
        .pe-table tbody tr {
          border-bottom: 1px solid #f1f3f5;
        }
        .transition-all { transition: all 0.2s ease-in-out; }
        .x-small { font-size: 0.75rem; }
        .border-bottom-light { border-bottom: 1px solid #f1f3f5; }
        .z-1 { z-index: 1; }
      `}</style>
    </div>
  );
};

export default OrdersPage;
