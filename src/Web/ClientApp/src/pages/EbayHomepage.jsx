import React, { useRef, useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import EbayHeader from '../components/EbayHeader';
import EbayFooter from '../components/EbayFooter';
import HeroCarousel from '../components/HeroCarousel';
import {
    dailyDeals,
    heroSlides,
    techDestinations,
    loyaltyBanner,
    motorsBanner,
    colorBanner
} from '../data/ebayMockData';
import '../components/Ebay.css';
import { productService } from '../services/productService';

const EbayHomepage = () => {
    const techCarouselRef = useRef(null);
    const featuredCarouselRef = useRef(null);
    const trendingCarouselRef = useRef(null);

    const [products, setProducts] = useState([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const fetchProducts = async () => {
            try {
                setLoading(true);
                const data = await productService.getAllProducts();
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

    const parseImageUrl = (imgStr) => {
        if (!imgStr || imgStr === "[]" || imgStr === "null") return '/images/default-product.png';
        try {
            // Trường hợp parse được JSON (mảng ảnh)
            const parsed = JSON.parse(imgStr);
            if (Array.isArray(parsed) && parsed.length > 0) return parsed[0];
            if (Array.isArray(parsed) && parsed.length === 0) return '/images/default-product.png';
            return imgStr;
        } catch (e) {
            // Trường hợp là string URL thuần túy
            return imgStr;
        }
    };

    const formatPrice = (price) => {
        return price ? `$${price.toFixed(2)}` : 'N/A';
    };

    const backendFeaturedDeals = products.slice(0, 15);
    const backendTrending = products.slice(15, 30);

    const handleTechScroll = (direction) => {
        if (!techCarouselRef.current) {
            return;
        }

        const scrollAmount = techCarouselRef.current.clientWidth * 0.8;
        techCarouselRef.current.scrollBy({
            left: direction === 'next' ? scrollAmount : -scrollAmount,
            behavior: 'smooth'
        });
    };

    const handleFeaturedScroll = (direction) => {
        if (!featuredCarouselRef.current) {
            return;
        }

        const scrollAmount = featuredCarouselRef.current.clientWidth * 0.85;
        featuredCarouselRef.current.scrollBy({
            left: direction === 'next' ? scrollAmount : -scrollAmount,
            behavior: 'smooth'
        });
    };

    const handleTrendingScroll = (direction) => {
        if (!trendingCarouselRef.current) {
            return;
        }

        const scrollAmount = trendingCarouselRef.current.clientWidth * 0.82;
        trendingCarouselRef.current.scrollBy({
            left: direction === 'next' ? scrollAmount : -scrollAmount,
            behavior: 'smooth'
        });
    };

    return (
        <div className="ebay-container">
            <EbayHeader />
            <HeroCarousel slides={heroSlides} />

            <main>
                <section
                    className="ebay-tech-highlights"
                    aria-labelledby="ebay-tech-highlights-title"
                >
                    <div className="ebay-tech-highlights-header">
                        <h2 id="ebay-tech-highlights-title">The future in your hands</h2>
                        <p>
                            Handpicked destinations for the devices and components powering your next breakthrough.
                        </p>
                    </div>

                    <div
                        className="ebay-tech-carousel"
                        role="group"
                        aria-roledescription="Carousel"
                        aria-label="Popular tech destinations"
                    >
                        <div className="ebay-tech-carousel__viewport" ref={techCarouselRef}>
                            <ul className="ebay-tech-carousel__list" role="list">
                                {techDestinations.map((destination) => (
                                    <li key={destination.id} className="ebay-tech-card">
                                        <a
                                            className="ebay-tech-card__link"
                                            href="#"
                                        >
                                            <span className="ebay-tech-card__media">
                                                <img src={destination.image} alt={destination.alt} loading="lazy" />
                                                <span className="ebay-tech-card__overlay" aria-hidden="true" />
                                            </span>
                                            <span className="ebay-tech-card__label">{destination.label}</span>
                                        </a>
                                    </li>
                                ))}
                            </ul>
                        </div>

                        <div className="ebay-tech-carousel__controls">
                            <button
                                type="button"
                                className="ebay-tech-carousel__control"
                                aria-label="Scroll carousel left"
                                onClick={() => handleTechScroll('prev')}
                            >
                                <i className="bi bi-chevron-left" aria-hidden="true" />
                            </button>
                            <button
                                type="button"
                                className="ebay-tech-carousel__control"
                                aria-label="Scroll carousel right"
                                onClick={() => handleTechScroll('next')}
                            >
                                <i className="bi bi-chevron-right" aria-hidden="true" />
                            </button>
                        </div>
                    </div>
                </section>

                {loyaltyBanner && (
                    <section
                        className="ebay-loyalty-banner"
                        aria-labelledby="ebay-loyalty-banner-title"
                    >
                        <div className="ebay-loyalty-banner__content">
                            <h2 id="ebay-loyalty-banner-title">
                                <a
                                    href="#"
                                    className="ebay-loyalty-banner__heading-link"
                                >
                                    {loyaltyBanner.title}
                                </a>
                            </h2>

                            <div className="ebay-loyalty-banner__body">
                                <p>{loyaltyBanner.description}</p>

                                <a
                                    href="#"
                                    className="ebay-loyalty-banner__cta ebay-loyalty-banner__cta--mobile"
                                >
                                    {loyaltyBanner.ctaLabel}
                                </a>

                                <span className="ebay-loyalty-banner__terms">
                                    <a href="#">
                                        {loyaltyBanner.termsLabel}
                                    </a>
                                </span>
                            </div>
                        </div>

                        <a
                            href="#"
                            className="ebay-loyalty-banner__cta ebay-loyalty-banner__cta--desktop"
                        >
                            {loyaltyBanner.ctaLabel}
                        </a>
                    </section>
                )}

                {motorsBanner && (
                    <aside
                        className="ebay-motors-banner"
                        aria-labelledby="ebay-motors-banner-title"
                        style={{
                            '--ebay-motors-bg': motorsBanner.background,
                            '--ebay-motors-fg': motorsBanner.foreground
                        }}
                    >
                        <div className="ebay-motors-banner__grid">
                            <div className="ebay-motors-banner__body">
                                <h2 id="ebay-motors-banner-title">{motorsBanner.title}</h2>
                                <p>{motorsBanner.description}</p>
                                <a
                                    className="ebay-motors-banner__cta"
                                    href="#"
                                >
                                    {motorsBanner.ctaLabel}
                                </a>
                                <p className="ebay-motors-banner__terms">
                                    <a href="#">
                                        {motorsBanner.termsLabel}
                                    </a>
                                </p>
                            </div>

                            <div className="ebay-motors-banner__media">
                                <img
                                    src={motorsBanner.image}
                                    alt={motorsBanner.imageAlt}
                                    loading="lazy"
                                />
                            </div>
                        </div>
                    </aside>
                )}

                {backendFeaturedDeals.length > 0 && (
                    <aside
                        className="ebay-featured-deals"
                        aria-labelledby="ebay-featured-deals-title"
                        data-viewport='{"trackableId":"01KK5E6Y9EBQRDPZ9PB0VH9RFE"}'
                    >
                        <div className="ebay-featured-deals__header">
                            <div>
                                <h2 id="ebay-featured-deals-title">Today's Deals</h2>
                                <p>All With Free Shipping</p>
                            </div>
                            <div className="ebay-featured-deals__spacer" aria-hidden="true" />
                        </div>

                        <div
                            className="ebay-featured-deals__carousel"
                            role="group"
                            aria-roledescription="Carousel"
                            aria-label="Featured deals carousel"
                        >
                            <button
                                type="button"
                                className="ebay-featured-deals__control"
                                aria-label="Previous featured deal"
                                onClick={() => handleFeaturedScroll('prev')}
                            >
                                <i className="bi bi-chevron-left" aria-hidden="true" />
                            </button>

                            <div className="ebay-featured-deals__viewport" ref={featuredCarouselRef}>
                                <ul className="ebay-featured-deals__list" role="list">
                                    {backendFeaturedDeals.map((deal) => (
                                        <li key={deal.id} className="ebay-featured-card">
                                            <article className="ebay-featured-card__article">
                                                <div className="ebay-featured-card__media">
                                                    <button
                                                        type="button"
                                                        className="ebay-featured-card__watchlist"
                                                        aria-label={`Add ${deal.title} to Watchlist`}
                                                    >
                                                        <i className="bi bi-heart" aria-hidden="true" />
                                                    </button>
                                                    <Link
                                                        className="ebay-featured-card__image-link"
                                                        to={`/product/${deal.id}`}
                                                    >
                                                        <img 
                                                            src={parseImageUrl(deal.images || deal.image)} 
                                                            alt={deal.title} 
                                                            loading="lazy" 
                                                            onError={(e) => { e.target.onerror = null; e.target.src = '/images/default-product.png'; }}
                                                        />
                                                        <span className="ebay-featured-card__scrim" aria-hidden="true" />
                                                    </Link>
                                                </div>

                                                <Link
                                                    className="ebay-featured-card__info"
                                                    to={`/product/${deal.id}`}
                                                >
                                                    <span className="ebay-featured-card__title">{deal.title}</span>

                                                    <div className="ebay-featured-card__pricing">
                                                        <span className="ebay-featured-card__price">{formatPrice(deal.price)}</span>
                                                    </div>

                                                    <span className="ebay-visually-hidden">
                                                        Opens in a new window or tab
                                                    </span>
                                                </Link>
                                            </article>
                                        </li>
                                    ))}
                                </ul>
                            </div>

                            <button
                                type="button"
                                className="ebay-featured-deals__control"
                                aria-label="Next featured deal"
                                onClick={() => handleFeaturedScroll('next')}
                            >
                                <i className="bi bi-chevron-right" aria-hidden="true" />
                            </button>
                        </div>
                    </aside>
                )}

                {backendTrending.length > 0 && (
                    <aside
                        className="ebay-trending"
                        aria-labelledby="ebay-trending-title"
                        data-viewport='{"trackableId":"01KK5E6Y7PXRDANT6HCZAQH1FH"}'
                        data-trackablemoduleid="4776"
                    >
                        <div className="ebay-trending__header">
                            <h2 id="ebay-trending-title">Trending on eBay</h2>
                        </div>

                        <div
                            className="ebay-trending__carousel"
                            role="group"
                            aria-roledescription="Carousel"
                            aria-label="Trending destinations carousel"
                        >
                            <button
                                type="button"
                                className="ebay-trending__control"
                                aria-label="Previous trending destination"
                                onClick={() => handleTrendingScroll('prev')}
                            >
                                <i className="bi bi-chevron-left" aria-hidden="true" />
                            </button>

                            <div className="ebay-trending__viewport" ref={trendingCarouselRef}>
                                <ul className="ebay-trending__list" role="list">
                                    {backendTrending.map((destination) => (
                                        <li key={destination.id} className="ebay-trending-card">
                                            <Link
                                                className="ebay-trending-card__link"
                                                to={`/product/${destination.id}`}
                                            >
                                                <span className="ebay-trending-card__media">
                                                    <img 
                                                        src={parseImageUrl(destination.images || destination.image)} 
                                                        alt={destination.title} 
                                                        loading="lazy" 
                                                        onError={(e) => { e.target.onerror = null; e.target.src = '/images/default-product.png'; }}
                                                    />
                                                    <span className="ebay-trending-card__scrim" aria-hidden="true" />
                                                </span>
                                                <span className="ebay-trending-card__label">{destination.title}</span>
                                            </Link>
                                        </li>
                                    ))}
                                </ul>
                            </div>

                            <button
                                type="button"
                                className="ebay-trending__control"
                                aria-label="Next trending destination"
                                onClick={() => handleTrendingScroll('next')}
                            >
                                <i className="bi bi-chevron-right" aria-hidden="true" />
                            </button>
                        </div>
                    </aside>
                )}

                {colorBanner && (
                    <aside
                        className="ebay-color-banner"
                        aria-labelledby="ebay-color-banner-title"
                        data-viewport={`{"trackableId":"${colorBanner.trackableId}"}`}
                        data-trackable-id={colorBanner.trackableId}
                        style={{
                            '--ebay-color-banner-bg': colorBanner.background,
                            '--ebay-color-banner-fg': colorBanner.foreground
                        }}
                    >
                        <div className="ebay-color-banner__grid">
                            <div className="ebay-color-banner__body">
                                <div className="ebay-color-banner__spacer" aria-hidden="true" />
                                <h2 id="ebay-color-banner-title">{colorBanner.title}</h2>
                                <p>{colorBanner.description}</p>
                                <a
                                    className="ebay-color-banner__cta"
                                    href="#"
                                    target="_self"
                                    rel="noreferrer"
                                >
                                    {colorBanner.ctaLabel}
                                </a>
                                <div className="ebay-color-banner__spacer" aria-hidden="true" />
                                <p className="ebay-color-banner__terms">
                                    <a href="#" target="_self" rel="noreferrer">
                                        {colorBanner.termsLabel}
                                    </a>
                                </p>
                            </div>

                            <div className="ebay-color-banner__media" role="img" aria-label={colorBanner.imageAlt}>
                                <img src={colorBanner.image} alt={colorBanner.imageAlt} loading="lazy" />
                            </div>
                        </div>
                    </aside>
                )}
            </main>
            <EbayFooter />
        </div>
    );
};

export default EbayHomepage;