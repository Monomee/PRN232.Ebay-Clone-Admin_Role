import React, { useState, useEffect } from 'react';
import { NavMenu } from './NavMenu';
import { AdminNotifications } from './AdminNotifications';

export const Layout = ({ children, title }) => {
  const [sidebarOpen, setSidebarOpen] = useState(window.innerWidth > 992);

  // Handle auto-close on mobile when navigating (could be added to NavMenu instead)
  const toggleSidebar = () => setSidebarOpen(!sidebarOpen);
  const closeSidebar = () => setSidebarOpen(false);

  // Re-check width on resize
  useEffect(() => {
    const handleResize = () => {
      if (window.innerWidth > 992) {
        setSidebarOpen(true);
      } else {
        setSidebarOpen(false);
      }
    };
    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, []);

  return (
    <div className={`app-container ${sidebarOpen ? 'sidebar-open' : 'sidebar-closed'}`}>
      <NavMenu isOpen={sidebarOpen} toggleSidebar={toggleSidebar} closeSidebar={closeSidebar} />

      <main className="main-content">
        {/* Top bar: menu button (when the sidebar is hidden), page title, notifications on the right */}
        <header className="app-topbar">
          {!sidebarOpen && (
            <>
              <button type="button" className="btn-icon" onClick={toggleSidebar} aria-label="Open navigation menu">
                <i className="bi bi-list fs-5" aria-hidden="true"></i>
              </button>
              <span className="brand-mark d-none d-sm-inline" aria-hidden="true">
                <span className="e">e</span><span className="b">b</span><span className="a">a</span><span className="y">y</span>
              </span>
            </>
          )}
          {title && <span className="app-topbar-title text-truncate">{title}</span>}
          <div className="ms-auto">
            <AdminNotifications />
          </div>
        </header>
        {children}
      </main>

      {/* ── Sidebar Overlay (Mobile Only) ── */}
      <div className={`sidebar-overlay ${sidebarOpen ? 'show' : ''}`} onClick={closeSidebar} aria-hidden="true"></div>
    </div>
  );
};
