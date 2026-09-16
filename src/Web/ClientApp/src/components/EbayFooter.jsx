import React from 'react';
import './Ebay.css';

const footerSections = [
    {
        id: 'buy',
        title: 'Buy',
        links: [
            { label: 'Registration', href: '#' },
            { label: 'Bidding & buying help', href: '#' },
            { label: 'Stores', href: '#' },
            { label: 'Creator Collections', href: '#' },
            { label: 'eBay for Charity', href: '#' },
            { label: 'Charity Shop', href: '#' },
            { label: 'Seasonal Sales and events', href: '#' },
            { label: 'eBay Gift Cards', href: '#' }
        ]
    },
    {
        id: 'sell',
        title: 'Sell',
        links: [
            { label: 'Start selling', href: '#' },
            { label: 'How to sell', href: '#' },
            { label: 'Business sellers', href: '#' },
            { label: 'Affiliates', href: '#' }
        ]
    },
    {
        id: 'tools',
        title: 'Tools & apps',
        links: [
            { label: 'Developers', href: '#' },
            { label: 'Security center', href: '#' },
            { label: 'Site map', href: '#' }
        ]
    },
    {
        id: 'about',
        title: 'About eBay',
        links: [
            { label: 'Company info', href: '#' },
            { label: 'News', href: '#' },
            { label: 'Deferred Prosecution Agreement with District of Massachusetts', href: '#' },
            { label: 'Investors', href: '#' },
            { label: 'Careers', href: '#' },
            { label: 'Diversity & Inclusion', href: '#' },
            { label: 'Global Impact', href: '#' },
            { label: 'Government relations', href: '#' },
            { label: 'Advertise with us', href: '#' },
            { label: 'Policies', href: '#' },
            { label: 'Verified Rights Owner (VeRO) Program', href: '#' },
            { label: 'eCI Licenses', href: '#' },
            { label: 'Product Safety Tips', href: '#' }
        ]
    },
    {
        id: 'help',
        title: 'Help & Contact',
        links: [
            { label: 'Seller Center', href: '#' },
            { label: 'Contact Us', href: '#' },
            { label: 'eBay Returns', href: '#' },
            { label: 'eBay Money Back Guarantee', href: '#' }
        ]
    },
    {
        id: 'community',
        title: 'Community',
        links: [
            { label: 'Announcements', href: '#' },
            { label: 'eBay Community', href: '#' },
            { label: 'eBay for Business Podcast', href: '#' }
        ]
    }
];

const ebayCompanies = [{ label: 'TCGplayer', href: '#' }];

const socialLinks = [
    { id: 'facebook', label: 'Facebook', href: '#', icon: 'bi bi-facebook' },
    { id: 'twitter', label: 'X (Twitter)', href: '#', icon: 'bi bi-twitter-x' }
];

const legalLinks = [
    { label: 'Accessibility', href: '#' },
    { label: 'User Agreement', href: '#' },
    { label: 'Privacy', href: '#' },
    { label: 'Consumer Health Data', href: '#' },
    { label: 'Payments Terms of Use', href: '#' },
    { label: 'Cookies', href: '#' },
    { label: 'CA Privacy Notice', href: '#' },
    { label: 'Your Privacy Choices', href: '#' },
    { label: 'AdChoice', href: '#' }
];

const footerSectionMap = footerSections.reduce((acc, section) => {
    acc[section.id] = section;
    return acc;
}, {});

const FooterLinkSection = ({ section }) => {
    if (!section) {
        return null;
    }

    return (
        <section className="ebay-footer__section" aria-labelledby={`ebay-footer-${section.id}`}>
            <h3 id={`ebay-footer-${section.id}`}>{section.title}</h3>
            <ul role="list">
                {section.links.map((link) => (
                    <li key={link.label}>
                        <a href={link.href}>{link.label}</a>
                    </li>
                ))}
            </ul>
        </section>
    );
};

const EbayFooter = () => {
    const currentYear = new Date().getFullYear();

    return (
        <footer className="ebay-footer" aria-labelledby="ebay-footer-heading">
            <h2 id="ebay-footer-heading" className="ebay-visually-hidden">
                Explore additional eBay resources
            </h2>

            <div className="ebay-footer__grid">
                <div className="ebay-footer__column" data-footer-column="buy">
                    <FooterLinkSection section={footerSectionMap.buy} />
                </div>

                <div className="ebay-footer__column" data-footer-column="sell-tools">
                    <FooterLinkSection section={footerSectionMap.sell} />
                    <FooterLinkSection section={footerSectionMap.tools} />
                </div>

                <div className="ebay-footer__column ebay-footer__column--meta" data-footer-column="companies">
                    <section
                        className="ebay-footer__section ebay-footer__section--meta"
                        aria-labelledby="ebay-footer-companies"
                    >
                        <h3 id="ebay-footer-companies">eBay companies</h3>
                        <ul role="list">
                            {ebayCompanies.map((company) => (
                                <li key={company.label}>
                                    <a href={company.href}>{company.label}</a>
                                </li>
                            ))}
                        </ul>

                        <div>
                            <h3 id="ebay-footer-connect">Stay connected</h3>
                            <ul className="ebay-footer__social" role="list" aria-labelledby="ebay-footer-connect">
                                {socialLinks.map((social) => (
                                    <li key={social.id}>
                                        <a
                                            className="ebay-footer__social-link"
                                            href={social.href}
                                        >
                                            <i className={social.icon} aria-hidden="true" />
                                            <span>{social.label}</span>
                                        </a>
                                    </li>
                                ))}
                            </ul>
                        </div>
                    </section>
                </div>

                <div className="ebay-footer__column" data-footer-column="about">
                    <FooterLinkSection section={footerSectionMap.about} />
                </div>

                <div className="ebay-footer__column ebay-footer__column--support" data-footer-column="support">
                    <FooterLinkSection section={footerSectionMap.help} />
                    <FooterLinkSection section={footerSectionMap.community} />
                    <section
                        className="ebay-footer__section ebay-footer__section--locale"
                        aria-labelledby="ebay-footer-sites"
                    >
                        <h3 id="ebay-footer-sites">eBay Sites</h3>
                        <button
                            type="button"
                            className="ebay-footer__locale-selector"
                            aria-label="Change eBay site location"
                        >
                            <span className="ebay-footer__locale-flag" role="img" aria-label="United States">
                                🇺🇸
                            </span>
                            <span className="ebay-footer__locale-label">United States</span>
                            <i className="bi bi-chevron-down" aria-hidden="true" />
                        </button>
                    </section>
                </div>
            </div>

            <div className="ebay-footer__legal">
                <p>
                    Copyright © 1995-
                    {currentYear} eBay Inc. All Rights Reserved.
                </p>
                <nav aria-label="Legal and privacy links">
                    <ul className="ebay-footer__legal-links" role="list">
                        {legalLinks.map((link) => (
                            <li key={link.label}>
                                <a href={link.href}>{link.label}</a>
                            </li>
                        ))}
                    </ul>
                </nav>
            </div>
        </footer>
    );
};

export default EbayFooter;