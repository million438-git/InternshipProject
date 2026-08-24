document.addEventListener("DOMContentLoaded", function () {

    /* =========================================
       SCROLL REVEAL
       ========================================= */

    const revealElements =
        document.querySelectorAll(".reveal");

    const revealObserver =
        new IntersectionObserver(
            function (entries, observer) {

                entries.forEach(function (entry) {

                    if (entry.isIntersecting) {

                        entry.target.classList.add("visible");

                        observer.unobserve(entry.target);
                    }

                });

            },
            {
                threshold: 0.12
            }
        );


    revealElements.forEach(function (element) {

        revealObserver.observe(element);

    });


    /* =========================================
       STATISTICS COUNTERS
       ========================================= */

    const counters =
        document.querySelectorAll(".stat-number");


    const counterObserver =
        new IntersectionObserver(
            function (entries, observer) {

                entries.forEach(function (entry) {

                    if (!entry.isIntersecting) {
                        return;
                    }

                    const counter = entry.target;

                    const target =
                        parseInt(counter.dataset.target);

                    let current = 0;

                    const duration = 1500;

                    const step =
                        target / (duration / 20);


                    const timer =
                        setInterval(function () {

                            current += step;

                            if (current >= target) {

                                current = target;

                                clearInterval(timer);

                            }

                            counter.textContent =
                                Math.floor(current) + "+";

                        }, 20);


                    observer.unobserve(counter);

                });

            },
            {
                threshold: 0.5
            }
        );


    counters.forEach(function (counter) {

        counterObserver.observe(counter);

    });


    /* =========================================
       PARALLAX EFFECT
       ========================================= */

    const celebration =
        document.querySelector(".event-celebration");


    window.addEventListener(
        "scroll",
        function () {

            if (!celebration) {
                return;
            }

            if (window.innerWidth <= 650) {
                return;
            }

            const rect =
                celebration.getBoundingClientRect();

            const windowHeight =
                window.innerHeight;


            if (
                rect.top < windowHeight &&
                rect.bottom > 0
            ) {

                const progress =
                    (windowHeight - rect.top) /
                    (windowHeight + rect.height);

                const position =
                    50 + (progress - .5) * 10;

                celebration.style.backgroundPosition =
                    `center ${position}%`;

            }

        },
        {
            passive: true
        }
    );


    /* =========================================
       HERO BUTTON FEEDBACK
       ========================================= */

    const buttons =
        document.querySelectorAll(".hero-btn");


    buttons.forEach(function (button) {

        button.addEventListener(
            "mouseenter",
            function () {

                this.style.transform =
                    "translateY(-3px)";

            }
        );


        button.addEventListener(
            "mouseleave",
            function () {

                this.style.transform =
                    "translateY(0)";

            }
        );

    });


    /* =========================================
       PREVENT BROKEN IMAGE EXPERIENCE
       ========================================= */

    const body =
        document.body;


    body.classList.add("home-loaded");

});