import React, { useState, useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../services/useAuth';
import { getActiveBroadcasts } from '../services/broadcastService';
import './Ebay.css';

const EbayHeader = () => {
    const { user } = useAuth();
    const [broadcasts, setBroadcasts] = useState([]);
    const [showNotifications, setShowNotifications] = useState(false);
    const notificationRef = useRef(null);

    useEffect(() => {
        if (user) {
            getActiveBroadcasts()
                .then(data => setBroadcasts(data || []))
                .catch(err => console.error("Failed to fetch broadcasts:", err));
        }
    }, [user]);

    // Close dropdown when clicking outside
    useEffect(() => {
        const handleClickOutside = (event) => {
            if (notificationRef.current && !notificationRef.current.contains(event.target)) {
                setShowNotifications(false);
            }
        };
        document.addEventListener('mousedown', handleClickOutside);
        return () => document.removeEventListener('mousedown', handleClickOutside);
    }, []);

    const handleSearchSubmit = (event) => {
        event.preventDefault();
    };

    const toggleNotifications = () => {
        setShowNotifications(!showNotifications);
    };

    return (
        <header className="ebay-header" role="banner">
            <style>
                {`
                .ebay-notification-dropdown {
                    position: absolute;
                    top: 100%;
                    right: 0;
                    width: 350px;
                    background: white;
                    border: 1px solid #e5e5e5;
                    border-radius: 8px;
                    box-shadow: 0 4px 12px rgba(0,0,0,0.15);
                    z-index: 1000;
                    margin-top: 8px;
                    max-height: 480px;
                    overflow-y: auto;
                }
                .ebay-notification-header {
                    padding: 16px;
                    border-bottom: 1px solid #e5e5e5;
                    font-weight: bold;
                    font-size: 16px;
                    background: #f8f8f8;
                    border-radius: 8px 8px 0 0;
                }
                .ebay-notification-item {
                    padding: 16px;
                    border-bottom: 1px solid #f0f0f0;
                    background: #fff;
                    transition: background 0.2s;
                }
                .ebay-notification-item:hover {
                    background: #f5f8fa;
                }
                .ebay-notification-item:last-child {
                    border-bottom: none;
                }
                .ebay-notification-title {
                    font-weight: 600;
                    margin-bottom: 6px;
                    color: #111;
                    font-size: 14px;
                }
                .ebay-notification-content {
                    color: #555;
                    font-size: 13px;
                    line-height: 1.4;
                    margin-bottom: 8px;
                }
                .ebay-notification-meta {
                    display: flex;
                    justify-content: space-between;
                    font-size: 11px;
                    color: #888;
                }
                .ebay-notification-badge {
                    position: absolute;
                    top: -2px;
                    right: -2px;
                    background: #e53238;
                    color: white;
                    border-radius: 50%;
                    width: 16px;
                    height: 16px;
                    font-size: 10px;
                    font-weight: bold;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    border: 2px solid white;
                }
                `}
            </style>
            <div className="ebay-container">
                <div className="ebay-header-utility" aria-label="Global utility links">
                    <div className="ebay-utility-left">
                        {user ? (
                            <span>
                                Hi! <Link to="/Account/Manage">{user.username || user.email}</Link>
                            </span>
                        ) : (
                            <span>
                                Hi! <Link to="/login">Sign in</Link> or <Link to="#">register</Link>
                            </span>
                        )}
                        <Link to="#">Deals</Link>
                        <Link to="#">Brand Outlet</Link>
                        <Link to="#">Gift Cards</Link>
                        <Link to="#">Help & Contact</Link>
                    </div>
                    <div className="ebay-utility-right">
                        <Link to="#">Ship to</Link>
                        <Link to="#">Sell</Link>
                        <Link to="#">Watchlist <i className="bi bi-chevron-down" aria-hidden="true" /></Link>
                        <Link to="#">My eBay <i className="bi bi-chevron-down" aria-hidden="true" /></Link>
                        
                        <div style={{ position: 'relative' }} ref={notificationRef}>
                            <button 
                                type="button" 
                                className="ebay-utility-icon" 
                                aria-label="Notifications"
                                onClick={toggleNotifications}
                                style={{ position: 'relative' }}
                            >
                                <i className="bi bi-bell" aria-hidden="true" />
                                {user && broadcasts.length > 0 && (
                                    <span className="ebay-notification-badge">
                                        {broadcasts.length > 9 ? '9+' : broadcasts.length}
                                    </span>
                                )}
                            </button>

                            {showNotifications && (
                                <div className="ebay-notification-dropdown">
                                    <div className="ebay-notification-header">
                                        Notifications
                                    </div>
                                    {broadcasts.length > 0 ? (
                                        broadcasts.map((br) => (
                                            <div key={br.id} className="ebay-notification-item">
                                                <div className="ebay-notification-title">{br.title}</div>
                                                <div className="ebay-notification-content">{br.content}</div>
                                                <div className="ebay-notification-meta">
                                                    <span>{br.type || 'In-App'}</span>
                                                    <span>{new Date(br.sentAt || br.createdAt).toLocaleDateString()}</span>
                                                </div>
                                            </div>
                                        ))
                                    ) : (
                                        <div style={{ padding: '24px', textAlign: 'center', color: '#777' }}>
                                            <i className="bi bi-bell-slash" style={{ fontSize: '24px', marginBottom: '8px', display: 'block' }}></i>
                                            {user ? "No new notifications." : "Please sign in to view notifications."}
                                        </div>
                                    )}
                                </div>
                            )}
                        </div>
                        <button type="button" className="ebay-utility-icon" aria-label="Cart">
                            <i className="bi bi-cart" aria-hidden="true" />
                        </button>
                    </div>
                </div>

                <div className="ebay-header-main">
                    <div className="ebay-header-brand">
                        <Link to="/home" className="ebay-logo" aria-label="eBay Home">
                            <span style={{ color: '#e53238' }}>e</span>
                            <span style={{ color: '#0064d2' }}>b</span>
                            <span style={{ color: '#f5af02' }}>a</span>
                            <span style={{ color: '#86b817' }}>y</span>
                        </Link>

                        <button type="button" className="ebay-shop-by-category" aria-haspopup="menu">
                            Shop by category <i className="bi bi-chevron-down" aria-hidden="true" />
                        </button>
                    </div>

                    <form className="ebay-search-row" role="search" onSubmit={handleSearchSubmit}>
                        <label className="ebay-visually-hidden" htmlFor="ebay-search-input">
                            Search for anything
                        </label>
                        <div className="ebay-search-field">
                            <i className="bi bi-search" aria-hidden="true" />
                            <input
                                id="ebay-search-input"
                                type="search"
                                placeholder="Search for anything"
                                autoComplete="off"
                            />
                            <button type="button" className="ebay-search-camera" aria-label="Search with camera">
                                <i className="bi bi-camera" />
                            </button>
                            <span className="ebay-search-divider" aria-hidden="true" />
                            <div className="ebay-category-select-wrapper">
                                <select className="ebay-category-select" aria-label="Select category" defaultValue="all">
                                    <option value="all">All Categories</option>
                                    <option value="antiques">Antiques</option>
                                    <option value="art">Art</option>
                                </select>
                                <i className="bi bi-chevron-down" aria-hidden="true" />
                            </div>
                        </div>

                        <button type="submit" className="ebay-search-btn">
                            Search
                        </button>
                        <button type="button" className="ebay-advanced-search">
                            Advanced
                        </button>
                    </form>
                </div>

                <nav className="ebay-main-nav" aria-label="Primary">
                    <ul>
                        <li><Link to="#">Saved</Link></li>
                        <li><Link to="#">Electronics</Link></li>
                        <li><Link to="#">Motors</Link></li>
                        <li><Link to="#">Fashion</Link></li>
                        <li><Link to="#">Collectibles and Art</Link></li>
                        <li><Link to="#">Sports</Link></li>
                        <li><Link to="#">Health & Beauty</Link></li>
                        <li><Link to="#">Industrial equipment</Link></li>
                        <li><Link to="#">Home & Garden</Link></li>
                        <li><Link to="#">Deals</Link></li>
                        <li><Link to="#">Sell</Link></li>
                    </ul>
                </nav>
            </div>
        </header>
    );
};

export default EbayHeader;