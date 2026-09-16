import React, { useEffect, useState } from 'react';
import PropTypes from 'prop-types';

const AUTO_INTERVAL = 6000;

const HeroCarousel = ({ slides = [], autoPlay = true }) => {
    const safeSlides = Array.isArray(slides) ? slides : [];
    const [activeIndex, setActiveIndex] = useState(0);
    const [isPaused, setIsPaused] = useState(false);
    const totalSlides = safeSlides.length;

    useEffect(() => {
        if (!autoPlay || isPaused || totalSlides <= 1) {
            return undefined;
        }

        const id = setInterval(() => {
            setActiveIndex((prev) => (prev + 1) % totalSlides);
        }, AUTO_INTERVAL);

        return () => clearInterval(id);
    }, [autoPlay, isPaused, totalSlides]);

    useEffect(() => {
        if (activeIndex > totalSlides - 1) {
            setActiveIndex(0);
        }
    }, [activeIndex, totalSlides]);

    if (totalSlides === 0) {
        return null;
    }

    const moveTo = (nextIndex) => {
        setActiveIndex((nextIndex + totalSlides) % totalSlides);
    };

    const isPlaying = autoPlay && !isPaused;

    const renderStandardPanel = (slide) => (
        <>
            <div className="ebay-hero-carousel__text">
                {slide.kicker && <p className="ebay-hero-carousel__eyebrow">{slide.kicker}</p>}
                <h2 className="ebay-hero-carousel__title">{slide.title}</h2>
                {slide.subtitle && <p className="ebay-hero-carousel__subtitle">{slide.subtitle}</p>}
                {slide.ctaLabel && slide.ctaHref && (
                    <a href={slide.ctaHref} target="_blank" rel="noreferrer" className="ebay-hero-carousel__cta">
                        {slide.ctaLabel}
                        <i className="bi bi-arrow-right" aria-hidden="true" />
                    </a>
                )}
                {slide.termsLabel && (
                    <p className="ebay-hero-carousel__terms">
                        {slide.termsHref ? (
                            <a href={slide.termsHref} target="_blank" rel="noreferrer">
                                {slide.termsLabel}
                            </a>
                        ) : (
                            slide.termsLabel
                        )}
                    </p>
                )}
            </div>

            <div className="ebay-hero-carousel__media">
                {slide.image && (
                    <img src={slide.image} alt={slide.imageAlt || slide.title} loading="lazy" />
                )}
            </div>
        </>
    );

    const renderPhoneBanner = (slide) => (
        <div className="ebay-phone-banner">
            <div className="ebay-phone-banner__content">
                {slide.kicker && <p className="ebay-phone-banner__eyebrow">{slide.kicker}</p>}
                <h2 className="ebay-phone-banner__title">{slide.title}</h2>
                {slide.subtitle && <p className="ebay-phone-banner__subtitle">{slide.subtitle}</p>}
                {slide.ctaLabel && slide.ctaHref && (
                    <a
                        href={slide.ctaHref}
                        target="_self"
                        rel="noreferrer"
                        className="ebay-phone-banner__button"
                    >
                        {slide.ctaLabel}
                    </a>
                )}
            </div>

            <ul className="ebay-phone-banner__brands" aria-label="Featured phone brands">
                {slide.tiles.map((tile) => (
                    <li key={tile.id} className="ebay-phone-banner__brands-item">
                        <a href={tile.href} target="_self" rel="noreferrer" className="ebay-phone-banner__brand-card">
                            <div className="ebay-phone-banner__brand-media">
                                <img src={tile.image} alt={tile.imageAlt || tile.label} loading="lazy" />
                            </div>
                            <span className="ebay-phone-banner__brand-label">
                                {tile.label}
                                <i className="bi bi-chevron-right" aria-hidden="true" />
                            </span>
                        </a>
                    </li>
                ))}
            </ul>
        </div>
    );

    return (
        <section className="ebay-hero-carousel" aria-roledescription="carousel" aria-label="Featured promotions">
            <span className="ebay-visually-hidden" aria-live="polite">
                Slide {activeIndex + 1} of {totalSlides}
            </span>

            <div className="ebay-hero-carousel__viewport">
                <ul
                    className="ebay-hero-carousel__list"
                    style={{ transform: `translateX(-${activeIndex * 100}%)` }}
                >
                    {safeSlides.map((slide, index) => {
                        const hasTiles = Array.isArray(slide.tiles) && slide.tiles.length > 0;
                        const panelStyle = {
                            background: slide.background || '#f5f5f5',
                            color: slide.foreground || '#111111'
                        };
                        const panelClassName = hasTiles
                            ? 'ebay-hero-carousel__panel ebay-hero-carousel__panel--featured-tiles'
                            : 'ebay-hero-carousel__panel';

                        return (
                            <li
                                key={slide.id}
                                className="ebay-hero-carousel__slide"
                                role="group"
                                aria-roledescription="slide"
                                aria-label={`${index + 1} of ${totalSlides}`}
                                aria-hidden={index !== activeIndex}
                            >
                                <div className={panelClassName} style={panelStyle}>
                                    {hasTiles ? renderPhoneBanner(slide) : renderStandardPanel(slide)}
                                </div>
                            </li>
                        );
                    })}
                </ul>
            </div>

            {totalSlides > 1 && (
                <div className="ebay-hero-carousel__controls" aria-label="Carousel controls">
                    <div className="ebay-hero-carousel__dots">
                        {safeSlides.map((slide, index) => (
                            <button
                                key={slide.id}
                                type="button"
                                className="ebay-hero-carousel__dot"
                                aria-label={`Go to slide ${index + 1}`}
                                aria-current={activeIndex === index}
                                onClick={() => moveTo(index)}
                            />
                        ))}
                    </div>

                    <div className="ebay-hero-carousel__control-group" aria-label="Slide navigation">
                        <button
                            type="button"
                            className="ebay-hero-carousel__control"
                            onClick={() => moveTo(activeIndex - 1)}
                            aria-label="Previous slide"
                        >
                            <i className="bi bi-chevron-left text-black" aria-hidden="true" />
                        </button>

                        <button
                            type="button"
                            className="ebay-hero-carousel__control"
                            onClick={() => moveTo(activeIndex + 1)}
                            aria-label="Next slide"
                        >
                            <i className="bi bi-chevron-right text-black" aria-hidden="true" />
                        </button>

                        {autoPlay && (
                            <button
                                type="button"
                                className="ebay-hero-carousel__control ebay-hero-carousel__control--playback"
                                aria-label={isPlaying ? 'Pause carousel' : 'Play carousel'}
                                aria-pressed={!isPlaying}
                                onClick={() => setIsPaused((prev) => !prev)}
                            >
                                <i
                                    className={`bi ${isPlaying ? 'bi-pause-fill' : 'bi-play-fill'} text-black`}
                                    aria-hidden="true"
                                />
                            </button>
                        )}
                    </div>
                </div>
            )}
        </section>
    );
};

const tileShape = PropTypes.shape({
    id: PropTypes.oneOfType([PropTypes.string, PropTypes.number]).isRequired,
    label: PropTypes.string.isRequired,
    href: PropTypes.string.isRequired,
    image: PropTypes.string.isRequired,
    imageAlt: PropTypes.string
});

const slideShape = PropTypes.shape({
    id: PropTypes.oneOfType([PropTypes.string, PropTypes.number]).isRequired,
    title: PropTypes.string.isRequired,
    subtitle: PropTypes.string,
    kicker: PropTypes.string,
    ctaLabel: PropTypes.string,
    ctaHref: PropTypes.string,
    termsLabel: PropTypes.string,
    termsHref: PropTypes.string,
    image: PropTypes.string,
    imageAlt: PropTypes.string,
    background: PropTypes.string,
    foreground: PropTypes.string,
    tiles: PropTypes.arrayOf(tileShape)
});

HeroCarousel.propTypes = {
    slides: PropTypes.arrayOf(slideShape),
    autoPlay: PropTypes.bool
};

export default HeroCarousel;