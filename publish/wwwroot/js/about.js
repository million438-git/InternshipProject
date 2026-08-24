

/*
 * HUCEMS About Page JavaScript
 * Hawassa Unified Campus Event Management System
 */

document.addEventListener("DOMContentLoaded", function () {

    /* ==========================================
       SCROLL REVEAL
       ========================================== */

    const revealElements = document.querySelectorAll(
        ".intro-card, " +
        ".feature-card, " +
        ".user-card, " +
        ".value-item, " +
        ".mission-content, " +
        ".mission-visual, " +
        ".future-content"
    );

    revealElements.forEach(function (element) {
        element.classList.add("about-reveal");
    });

    if ("IntersectionObserver" in window) {

        const revealObserver = new IntersectionObserver(
            function (entries, observer) {

                entries.forEach(function (entry) {

                    if (entry.isIntersecting) {

                        entry.target.classList.add("visible");

                        observer.unobserve(entry.target);
                    }
                });

            },
            {
                threshold: 0.12,
                rootMargin: "0px 0px -40px 0px"
            }
        );

        revealElements.forEach(function (element) {
            revealObserver.observe(element);
        });

    } else {

        revealElements.forEach(function (element) {
            element.classList.add("visible");
        });
    }


    /* ==========================================
       STATISTICS COUNTER
       ========================================== */

    const statNumbers = document.querySelectorAll(".stat-number");

    function animateCounter(element) {

        const target = Number(element.dataset.target);

        if (!Number.isFinite(target)) {
            return;
        }

        const duration = 1200;
        const startTime = performance.now();

        function updateCounter(currentTime) {

            const elapsed = currentTime - startTime;
            const progress = Math.min(elapsed / duration, 1);

            /*
             * Ease-out animation.
             */
            const easedProgress =
                1 - Math.pow(1 - progress, 3);

            const currentValue =
                Math.floor(target * easedProgress);

            element.textContent = currentValue;

            if (progress < 1) {
                requestAnimationFrame(updateCounter);
            } else {
                element.textContent = target;
            }
        }

        requestAnimationFrame(updateCounter);
    }


    if ("IntersectionObserver" in window) {

        const statsSection = document.querySelector(".stats-section");

        if (statsSection) {

            let hasAnimated = false;

            const statsObserver = new IntersectionObserver(
                function (entries, observer) {

                    entries.forEach(function (entry) {

                        if (
                            entry.isIntersecting &&
                            !hasAnimated
                        ) {

                            hasAnimated = true;

                            statNumbers.forEach(function (number) {
                                animateCounter(number);
                            });

                            observer.unobserve(entry.target);
                        }
                    });

                },
                {
                    threshold: 0.35
                }
            );

            statsObserver.observe(statsSection);
        }

    } else {

        statNumbers.forEach(function (number) {
            number.textContent = number.dataset.target;
        });
    }


    /* ==========================================
       SMOOTH SCROLL
       ========================================== */

    document.querySelectorAll(
        '.about-page a[href^="#"]'
    ).forEach(function (link) {

        link.addEventListener("click", function (event) {

            const targetId =
                link.getAttribute("href");

            if (!targetId || targetId === "#") {
                return;
            }

            const target =
                document.querySelector(targetId);

            if (!target) {
                return;
            }

            event.preventDefault();

            target.scrollIntoView({
                behavior: "smooth",
                block: "start"
            });
        });
    });


    /* ==========================================
       CARD HOVER ACCESSIBILITY
       ========================================== */

    const cards = document.querySelectorAll(
        ".intro-card, " +
        ".feature-card, " +
        ".user-card, " +
        ".value-item"
    );

    cards.forEach(function (card) {

        card.addEventListener("mouseenter", function () {
            card.classList.add("card-active");
        });

        card.addEventListener("mouseleave", function () {
            card.classList.remove("card-active");
        });
    });


    /* ==========================================
       PAGE LOAD EFFECT
       ========================================== */

    requestAnimationFrame(function () {

        const heroContent =
            document.querySelector(".about-hero-content");

        if (heroContent) {

            heroContent.style.opacity = "0";
            heroContent.style.transform = "translateY(20px)";

            requestAnimationFrame(function () {

                heroContent.style.transition =
                    "opacity 0.8s ease, transform 0.8s ease";

                heroContent.style.opacity = "1";
                heroContent.style.transform = "translateY(0)";
            });
        }
    });


    /* ==========================================
       CONSOLE INFORMATION
       ========================================== */

    console.log(
        "%cHUCEMS About Page Loaded",
        "font-size: 16px; font-weight: bold;"
    );

    console.log(
        "Hawassa Unified Campus Event Management System"
    );
});

