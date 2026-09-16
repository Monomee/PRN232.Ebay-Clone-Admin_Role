import React, { useState } from 'react';
import { useNavigate, Link, useLocation } from 'react-router-dom';
import { NavItem, NavLink } from 'reactstrap';
import { useAuth } from '../context/AuthContext';
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
        <i className={icon}></i>
        <span>{children}</span>
      </NavLink>
    </NavItem>
  );
};

export const NavMenu = ({ isOpen, toggleSidebar, closeSidebar }) => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  return (
    <>
      <nav className={`sidebar shadow-sm ${isOpen ? 'show' : 'collapsed'}`}>
        <div className="sidebar-header d-flex align-items-center justify-content-between px-3">
          <Link to="/" className="text-decoration-none d-flex align-items-center" onClick={closeSidebar}>
            <span className="fw-bold" style={{ color: '#e53238' }}>e</span>
            <span className="fw-bold" style={{ color: '#0064d2' }}>b</span>
            <span className="fw-bold" style={{ color: '#f5af02' }}>a</span>
            <span className="fw-bold" style={{ color: '#86b817' }}>y</span>
            <span className="ms-1 text-dark small fw-semibold">{user?.role === 'Seller' ? 'Seller' : 'Admin'}</span>
          </Link>
          <button 
            className="btn btn-link text-dark p-0 border-0 fs-4 d-flex align-items-center justify-content-center" 
            onClick={toggleSidebar}
            title="Toggle Sidebar"
          >
            <i className="bi bi-list"></i>
          </button>
        </div>
        <div className="sidebar-menu">
          <ul className="navbar-nav w-100" onClick={() => { if(window.innerWidth < 992) closeSidebar(); }}>
            {user?.role !== 'Seller' && <SidebarLink to="/dashboard" icon="bi bi-speedometer2">Dashboard</SidebarLink>}
            
            {user?.role === 'Seller' && (
              <SidebarLink to="/seller-products" icon="bi bi-box-seam">Your products</SidebarLink>
            )}

            {(user?.role === 'SuperAdmin' || user?.role === 'Support' || user?.roles?.includes('SuperAdmin') || user?.roles?.includes('Support')) && (
              <>
                <SidebarLink to="/users" icon="bi bi-people">Users</SidebarLink>
                <SidebarLink to="/products" icon="bi bi-box-seam">Products</SidebarLink>
                <SidebarLink to="/orders" icon="bi bi-cart">Orders</SidebarLink>
                <SidebarLink to="/review-monitoring" icon="bi bi-chat-left-text">Reviews</SidebarLink>

                <SidebarLink to="/disputes" icon="bi bi-shield-exclamation">Disputes</SidebarLink>
                <SidebarLink to="/return-requests" icon="bi bi-cart">Return Requests</SidebarLink>
              </>
            )}



            {(user?.role === 'SuperAdmin' || user?.roles?.includes('SuperAdmin')) && (
              <>
                <SidebarLink to="/sellers" icon="bi bi-person-badge">Sellers Overview</SidebarLink>
                <SidebarLink to="/payout-engine" icon="bi bi-lightning-charge">Payout Engine</SidebarLink>


                <SidebarLink to="/audit-logs" icon="bi bi-bar-chart-line">Audit Logs</SidebarLink>
                <SidebarLink to="/broadcasts" icon="bi bi-megaphone">Broadcasts</SidebarLink>
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
                  style={{ width: '36px', height: '36px', fontSize: '1rem', backgroundColor: '#0064d2', flexShrink: 0 }}
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
                className="btn btn-link sidebar-link border-0 w-100 text-start p-0 bg-transparent text-danger fw-medium" 
                onClick={handleLogout}
                style={{ cursor: 'pointer', textDecoration: 'none' }}
              >
                <i className="bi bi-box-arrow-right"></i>
                <span className="ms-2">Logout</span>
              </button>
            </li>
          </ul>
        </div>
      </nav>
    </>
  );
};
