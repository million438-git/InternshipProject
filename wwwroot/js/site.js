/* =========================================================
   HAWASSA UNIFIED CAMPUS EVENT MANAGEMENT SYSTEM
   GLOBAL WEBSITE JAVASCRIPT
   ========================================================= */

document.addEventListener("DOMContentLoaded", function () {

    console.log(
        "Hawassa Unified Campus Event Management System loaded."
    );


    /* =====================================================
       CURRENT YEAR
       ===================================================== */

    const yearElements =
        document.querySelectorAll(".current-year");

    yearElements.forEach(function (element) {

        element.textContent =
            new Date().getFullYear();

    });


    /* =====================================================
       ACTIVE NAVIGATION
       ===================================================== */

    const currentPath =
        window.location.pathname.toLowerCase();

    const navLinks =
        document.querySelectorAll(".navbar-nav .nav-link");

    navLinks.forEach(function (link) {

        const href =
            link.getAttribute("href");

        if (!href || href === "#") {
            return;
        }

        const linkPath =
            new URL(
                href,
                window.location.origin
            ).pathname.toLowerCase();

        if (
            linkPath !== "/" &&
            currentPath.startsWith(linkPath)
        ) {
            link.classList.add("active");
        }

    });


    /* =====================================================
       AUTO-HIDE ALERTS
       ===================================================== */

    const alerts =
        document.querySelectorAll(
            ".alert[data-auto-hide]"
        );

    alerts.forEach(function (alert) {

        setTimeout(function () {

            alert.style.transition =
                "opacity 0.4s ease";

            alert.style.opacity = "0";

            setTimeout(function () {

                alert.remove();

            }, 400);

        }, 4000);

    });


    /* =====================================================
       DELETE CONFIRMATION
       ===================================================== */

    const deleteButtons =
        document.querySelectorAll(
            ".delete-confirm"
        );

    deleteButtons.forEach(function (button) {

        button.addEventListener(
            "click",
            function (event) {

                const confirmed =
                    window.confirm(
                        "Are you sure you want to delete this item?"
                    );

                if (!confirmed) {

                    event.preventDefault();

                }

            }
        );

    });


    /* =====================================================
       PASSWORD SHOW / HIDE
       ===================================================== */

    const passwordButtons =
        document.querySelectorAll(
            ".password-toggle"
        );

    passwordButtons.forEach(function (button) {

        button.addEventListener(
            "click",
            function () {

                const target =
                    button.getAttribute(
                        "data-target"
                    );

                if (!target) {
                    return;
                }

                const input =
                    document.querySelector(target);

                if (!input) {
                    return;
                }

                if (
                    input.type === "password"
                ) {

                    input.type = "text";

                    button.textContent =
                        "Hide";

                } else {

                    input.type = "password";

                    button.textContent =
                        "Show";

                }

            }
        );

    });


    /* =====================================================
       MOBILE NAVBAR
       ===================================================== */

    const mobileNavLinks =
        document.querySelectorAll(
            ".navbar-collapse .nav-link"
        );

    mobileNavLinks.forEach(function (link) {

        link.addEventListener(
            "click",
            function () {

                const navbar =
                    document.querySelector(
                        ".navbar-collapse"
                    );

                const toggle =
                    document.querySelector(
                        ".navbar-toggler"
                    );

                if (
                    navbar &&
                    toggle &&
                    navbar.classList.contains("show")
                ) {

                    toggle.click();

                }

            }
        );

    });


    /* =====================================================
       BACK TO TOP BUTTON
       ===================================================== */

    const backToTop =
        document.getElementById(
            "backToTop"
        );

    if (backToTop) {

        window.addEventListener(
            "scroll",
            function () {

                if (window.scrollY > 400) {

                    backToTop.style.display =
                        "block";

                } else {

                    backToTop.style.display =
                        "none";

                }

            }
        );


        backToTop.addEventListener(
            "click",
            function () {

                window.scrollTo({
                    top: 0,
                    behavior: "smooth"
                });

            }
        );

    }


    /* =====================================================
       SEARCH CLEAR BUTTON
       ===================================================== */

    const searchClearButtons =
        document.querySelectorAll(
            ".search-clear"
        );

    searchClearButtons.forEach(function (button) {

        button.addEventListener(
            "click",
            function () {

                const target =
                    button.getAttribute(
                        "data-target"
                    );

                const input =
                    document.querySelector(
                        target
                    );

                if (input) {

                    input.value = "";

                    input.focus();

                }

            }
        );

    });


    /* =====================================================
       DISABLE BUTTON AFTER FORM SUBMIT
       ===================================================== */

    const forms =
        document.querySelectorAll(
            "form[data-disable-submit]"
        );

    forms.forEach(function (form) {

        form.addEventListener(
            "submit",
            function () {

                const submitButton =
                    form.querySelector(
                        'button[type="submit"]'
                    );

                if (!submitButton) {
                    return;
                }

                submitButton.disabled =
                    true;

                submitButton.dataset.originalText =
                    submitButton.textContent;

                submitButton.textContent =
                    "Please wait...";

            }
        );

    });


    /* =====================================================
       GLOBAL TOOLTIP INITIALIZATION
       Bootstrap 5
       ===================================================== */

    if (
        typeof bootstrap !== "undefined"
    ) {

        const tooltipElements =
            document.querySelectorAll(
                '[data-bs-toggle="tooltip"]'
            );

        tooltipElements.forEach(
            function (element) {

                new bootstrap.Tooltip(
                    element
                );

            }
        );

    }


    /* =====================================================
       GLOBAL THEME CONTROLLER (DARK / LIGHT MODE)
       ===================================================== */

    function getPreferredTheme() {
        const storedTheme = localStorage.getItem("hucems-theme");
        if (storedTheme) {
            return storedTheme;
        }
        return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches
            ? "dark"
            : "light";
    }

    function applyTheme(theme) {
        document.documentElement.setAttribute("data-theme", theme);
        if (theme === "dark") {
            document.documentElement.classList.add("dark-theme");
        } else {
            document.documentElement.classList.remove("dark-theme");
        }
        localStorage.setItem("hucems-theme", theme);

        const buttons = document.querySelectorAll(".theme-toggle-btn");
        buttons.forEach(function (btn) {
            const label = theme === "dark" ? "Switch to Light Mode" : "Switch to Dark Mode";
            btn.setAttribute("title", label);
            btn.setAttribute("aria-label", label);
        });

        // Trigger custom event for third-party widgets like charts/calendars
        window.dispatchEvent(new CustomEvent("hucems-theme-changed", { detail: { theme: theme } }));
    }

    window.toggleTheme = function () {
        const currentTheme = document.documentElement.getAttribute("data-theme") || "light";
        const nextTheme = currentTheme === "dark" ? "light" : "dark";
        applyTheme(nextTheme);
    };

    window.setTheme = function (theme) {
        applyTheme(theme);
    };

    // Initialize theme
    const activeTheme = getPreferredTheme();
    applyTheme(activeTheme);

    // Bind all theme toggle buttons
    const themeButtons = document.querySelectorAll(".theme-toggle-btn");
    themeButtons.forEach(function (btn) {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            window.toggleTheme();
        });
    });

    // Listen for OS color scheme change if not manually set
    if (window.matchMedia) {
        window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", function (e) {
            if (!localStorage.getItem("hucems-theme")) {
                applyTheme(e.matches ? "dark" : "light");
            }
        });
    }

    /* =====================================================
       DYNAMIC VIEWPORT SAFE DROPDOWN POSITIONING GUARD
       ===================================================== */
    function adjustDropdownPosition(dropdown) {
        if (!dropdown || window.innerWidth < 992) return;
        
        // Reset any inline overrides first to calculate natural layout
        dropdown.style.removeProperty('right');
        dropdown.style.removeProperty('left');

        var rect = dropdown.getBoundingClientRect();
        var windowWidth = document.documentElement.clientWidth || window.innerWidth;
        var padding = 12; // Safety margin in pixels

        if (rect.right > windowWidth - padding) {
            var diff = rect.right - (windowWidth - padding);
            dropdown.style.setProperty('left', 'auto', 'important');
            dropdown.style.setProperty('right', '0px', 'important');
        }
        
        if (rect.left < padding) {
            dropdown.style.setProperty('left', '0px', 'important');
            dropdown.style.setProperty('right', 'auto', 'important');
        }
    }

    document.addEventListener('shown.bs.dropdown', function (event) {
        var toggle = event.target;
        var menu = toggle.nextElementSibling || toggle.parentElement.querySelector('.dropdown-menu');
        if (menu) {
            adjustDropdownPosition(menu);
        }
    });

    /* =====================================================
       GLOBAL CONFIRMATION DIALOG (data-confirm)
       ===================================================== */
    var pendingConfirmForm = null;

    document.addEventListener("submit", function (e) {
        var form = e.target;
        var confirmMsg = form.getAttribute("data-confirm");
        if (confirmMsg && !form.dataset.confirmed) {
            e.preventDefault();
            e.stopPropagation();

            var confirmTitle = form.getAttribute("data-confirm-title") || "Confirm Action";
            var confirmBtnText = form.getAttribute("data-confirm-btn") || "Confirm";
            var isDanger = form.getAttribute("data-confirm-danger") !== "false";

            var modalEl = document.getElementById("hucemsConfirmModal");
            if (!modalEl) {
                if (window.confirm(confirmMsg)) {
                    form.dataset.confirmed = "true";
                    form.submit();
                }
                return;
            }

            pendingConfirmForm = form;

            var modalTitleEl = modalEl.querySelector(".modal-title");
            var modalBodyEl = modalEl.querySelector(".modal-body-content");
            var confirmBtn = modalEl.querySelector("#hucemsConfirmModalActionBtn");

            if (modalTitleEl) modalTitleEl.textContent = confirmTitle;
            if (modalBodyEl) modalBodyEl.textContent = confirmMsg;
            if (confirmBtn) {
                confirmBtn.textContent = confirmBtnText;
                confirmBtn.className = isDanger ? "btn btn-danger px-3 shadow-sm fw-semibold" : "btn btn-primary px-3 shadow-sm fw-semibold";
            }

            var modalInstance = bootstrap.Modal.getOrCreateInstance(modalEl);
            modalInstance.show();
        }
    });

    var confirmModalActionBtn = document.getElementById("hucemsConfirmModalActionBtn");
    if (confirmModalActionBtn) {
        confirmModalActionBtn.addEventListener("click", function () {
            if (pendingConfirmForm) {
                var form = pendingConfirmForm;
                pendingConfirmForm = null;
                var modalEl = document.getElementById("hucemsConfirmModal");
                if (modalEl) {
                    var modalInstance = bootstrap.Modal.getInstance(modalEl);
                    if (modalInstance) modalInstance.hide();
                }
                form.dataset.confirmed = "true";
                form.submit();
            }
        });
    }

    /* =====================================================
       AUTOMATIC SUBMIT BUTTON SPINNER & DEBOUNCE
       ===================================================== */
    document.addEventListener("submit", function (e) {
        var form = e.target;
        if (form.getAttribute("data-no-spinner") === "true") return;
        if (form.getAttribute("data-confirm") && !form.dataset.confirmed) return;

        var submitBtn = form.querySelector("button[type='submit']:not(.no-spin)");
        if (submitBtn && !submitBtn.disabled) {
            setTimeout(function () {
                submitBtn.disabled = true;
                var originalHtml = submitBtn.innerHTML;
                submitBtn.setAttribute("data-original-html", originalHtml);
                submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span> Processing...';
            }, 10);
        }
    });

});