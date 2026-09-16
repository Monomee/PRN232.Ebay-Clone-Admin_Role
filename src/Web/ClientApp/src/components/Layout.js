import React, { useState, useEffect } from 'react';
import { NavMenu } from './NavMenu';
import { AdminNotifications } from './AdminNotifications';

export const Layout = ({ children }) => {
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
      <AdminNotifications />
      
      <main className="main-content">
        {/* ── Floated Toggle Button (Only when sidebar is closed) ── */}
        {!sidebarOpen && (
          <button 
            className="btn shadow-sm d-flex align-items-center justify-content-center position-fixed" 
            onClick={toggleSidebar}
            style={{ 
              top: '20px', 
              left: '20px', 
              zIndex: 1100, 
              width: '45px', 
              height: '45px', 
              borderRadius: '12px',
              backgroundColor: '#fff',
              border: '1px solid var(--ebay-border)',
              color: 'var(--ebay-black)'
            }}
          >
            <i className="bi bi-list fs-4"></i>
          </button>
        )}
        {children}
      </main>

      {/* ── Sidebar Overlay (Mobile Only) ── */}
      <div className={`sidebar-overlay ${sidebarOpen ? 'show' : ''}`} onClick={closeSidebar}></div>
    </div>
  );
};
