import React, { useState } from 'react';
import { useNavigate, Link, useLocation } from 'react-router-dom';
import { NavItem, NavLink } from 'reactstrap';
import { useAuth } from '../context/AuthContext';
import { canAccess } from '../utils/access';
import './NavMenu.css';

const SidebarLink = ({ to, icon, children }) => {
  const location = useLocation();
  const isActive = location.pathname === to || (to !== '/' && location.pathname.startsWith(to));
  
  return (
    <NavItem>
      <NavLink 
        tag={Link} 
        className={`sidebar-link ${isActive ? 'active' : ''}`} 
        to={to}
      >
        <i className={icon} aria-hidden="true"></i>
        <span>{children}</span>
      </NavLink>
    </NavItem>
  );
};

export const NavMenu = ({ isOpen, toggleSidebar, closeSidebar }) => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  // Same roles/permissions as the routes in AppRoutes.js
  const operationLinks = [
    ['/users', 'bi bi-people', 'Users', 'ManageUsers'],
    ['/products', 'bi bi-box-seam', 'Products', 'ManageProducts'],
    ['/orders', 'bi bi-cart', 'Orders', 'ManageOrders'],
    ['/review-monitoring', 'bi bi-chat-left-text', 'Reviews', 'ModerateReviews'],
    ['/disputes', 'bi bi-shield-exclamation', 'Disputes', 'ManageDisputes'],
    ['/return-requests', 'bi bi-arrow-return-left', 'Return Requests', 'ManageOrders'],
    ['/sellers', 'bi bi-person-badge', 'Sellers Overview', 'ManageUsers'],
    ['/broadcasts', 'bi bi-megaphone', 'Broadcasts', 'ManageBroadcasts'],
  ].filter(([, , , permission]) => canAccess(user, ['SuperAdmin', 'Support'], permission));

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  return (
    <>
      <nav className={`sidebar ${isOpen ? 'show' : 'collapsed'}`} aria-label="Main navigation">
        <div className="sidebar-header d-flex align-items-center justify-content-between px-3">
          <Link to="/" className="text-decoration-none d-flex align-items-center" onClick={closeSidebar}>
            <span className="brand-mark"><span className="e">e</span><span className="b">b</span><span className="a">a</span><span className="y">y</span></span>
            <span className="ms-2 badge rounded-pill text-bg-light border fw-semibold" style={{ fontSize: '0.7rem', letterSpacing: 0 }}>{user?.role === 'Seller' ? 'Seller' : 'Admin'}</span>
          </Link>
          <button 
            type="button"
            className="btn btn-link text-secondary p-0 border-0 fs-5 d-flex align-items-center justify-content-center" 
            onClick={toggleSidebar}
            title="Collapse sidebar"
            aria-label="Collapse sidebar"
          >
            <i className="bi bi-layout-sidebar-inset" aria-hidden="true"></i>
          </button>
        </div>
        <div className="sidebar-menu">
          <ul className="navbar-nav w-100" onClick={() => { if(window.innerWidth < 992) closeSidebar(); }}>
            {user?.role !== 'Seller' && <SidebarLink to="/dashboard" icon="bi bi-grid-1x2">Dashboard</SidebarLink>}
            
            {user?.role === 'Seller' && (
              <>
                <li className="sidebar-section">Store</li>
                <SidebarLink to="/seller-products" icon="bi bi-box-seam">Your products</SidebarLink>
                <SidebarLink to="/seller-bank-account" icon="bi bi-bank">Payout bank account</SidebarLink>
              </>
            )}

            {operationLinks.length > 0 && <li className="sidebar-section">Operations</li>}
            {operationLinks.map(([to, icon, label]) => <SidebarLink key={to} to={to} icon={icon}>{label}</SidebarLink>)}



            {(user?.role === 'SuperAdmin' || user?.roles?.includes('SuperAdmin')) && (
              <>
                <li className="sidebar-section">System</li>
                <SidebarLink to="/payout-engine" icon="bi bi-lightning-charge">Payout Engine</SidebarLink>


                <SidebarLink to="/audit-logs" icon="bi bi-journal-text">Audit Logs</SidebarLink>
                <SidebarLink to="/sensitive-logs" icon="bi bi-shield-lock">Sensitive Logs</SidebarLink>
                <SidebarLink to="/admin-roles" icon="bi bi-gear">Admin Roles</SidebarLink>
                <SidebarLink to="/active-connections" icon="bi bi-hdd-network">Active Connections</SidebarLink>
              </>
            )}
          </ul>
        </div>
        <div className="sidebar-footer mt-auto border-top pt-2 px-3 pb-3">
          <ul className="navbar-nav w-100">
            {user && (
              <li className="nav-item d-flex align-items-center mb-1">
                <div 
                  className="rounded-circle text-white d-flex justify-content-center align-items-center shadow-sm"
                  style={{ width: '36px', height: '36px', fontSize: '0.95rem', fontWeight: 600, backgroundColor: 'var(--ebay-blue)', flexShrink: 0 }}
                  aria-hidden="true"
                >
                  {user.email ? user.email.charAt(0).toUpperCase() : <i className="bi bi-person"></i>}
                </div>
                <div className="ms-3 overflow-hidden w-100">
                  <div className="fw-semibold text-truncate text-dark" title={user.fullName || user.userName || user.email || (user?.role === 'Seller' ? 'Seller' : 'Admin')} style={{ fontSize: '0.9rem' }}>
                    {user.fullName || user.userName || (user?.role === 'Seller' ? 'Seller' : 'Admin')}
                  </div>
                  <div className="text-secondary text-truncate" title={user.email} style={{ fontSize: '0.8rem' }}>
                    {user.email}
                  </div>
                </div>
              </li>
            )}
            
            <li className="nav-item mt-2">
              <button 
                type="button"
                className="btn btn-link sidebar-link border-0 w-100 text-start p-0 bg-transparent text-danger fw-medium" 
                onClick={handleLogout}
                style={{ cursor: 'pointer', textDecoration: 'none' }}
              >
                <i className="bi bi-box-arrow-right" aria-hidden="true"></i>
                <span>Log out</span>
              </button>
            </li>
          </ul>
        </div>
      </nav>
    </>
  );
};
