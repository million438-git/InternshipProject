/* =========================================================
   HUCEMS - ACCOUNT JAVASCRIPT
   Login | Register | Profile | Account
   ========================================================= */

document.addEventListener("DOMContentLoaded", function () {

    /* =====================================================
       PASSWORD SHOW / HIDE
       ===================================================== */

    const passwordToggles =
        document.querySelectorAll(".password-toggle");

    passwordToggles.forEach(function (button) {

        button.addEventListener("click", function () {

            const wrapper = button.closest(".password-wrapper");

            if (!wrapper) return;

            const input =
                wrapper.querySelector("input");

            if (!input) return;

            if (input.type === "password") {

                input.type = "text";

                button.innerHTML =
                    '<i class="bi bi-eye-slash"></i>';

                button.setAttribute(
                    "aria-label",
                    "Hide password"
                );

            } else {

                input.type = "password";

                button.innerHTML =
                    '<i class="bi bi-eye"></i>';

                button.setAttribute(
                    "aria-label",
                    "Show password"
                );
            }
        });
    });


    /* =====================================================
       PASSWORD STRENGTH
       ===================================================== */

    const passwordInputs =
        document.querySelectorAll(
            '[data-password-strength]'
        );

    passwordInputs.forEach(function (input) {

        const container =
            input.closest(".account-form-group");

        if (!container) return;

        const strengthBar =
            container.querySelector(
                ".password-strength-fill"
            );

        const strengthText =
            container.querySelector(
                ".password-strength-text"
            );

        if (!strengthBar || !strengthText) return;

        input.addEventListener("input", function () {

            const password = input.value;

            let score = 0;

            if (password.length >= 8)
                score++;

            if (/[A-Z]/.test(password))
                score++;

            if (/[a-z]/.test(password))
                score++;

            if (/[0-9]/.test(password))
                score++;

            if (/[^A-Za-z0-9]/.test(password))
                score++;

            let percentage = score * 20;

            strengthBar.style.width =
                percentage + "%";

            if (password.length === 0) {

                strengthText.textContent = "";
                strengthBar.style.width = "0%";

            } else if (score <= 2) {

                strengthText.textContent =
                    "Weak password";

            } else if (score === 3) {

                strengthText.textContent =
                    "Medium password";

            } else if (score === 4) {

                strengthText.textContent =
                    "Strong password";

            } else {

                strengthText.textContent =
                    "Very strong password";
            }
        });
    });


    /* =====================================================
       CONFIRM PASSWORD
       ===================================================== */

    const confirmPassword =
        document.querySelector(
            '[data-confirm-password]'
        );

    if (confirmPassword) {

        const password =
            document.querySelector(
                '[data-password]'
            );

        const message =
            document.querySelector(
                ".password-match-message"
            );

        function checkPasswordMatch() {

            if (!password) return;

            if (confirmPassword.value === "") {

                confirmPassword.classList.remove(
                    "input-success",
                    "input-error"
                );

                if (message)
                    message.textContent = "";

                return;
            }

            if (
                password.value ===
                confirmPassword.value
            ) {

                confirmPassword.classList.remove(
                    "input-error"
                );

                confirmPassword.classList.add(
                    "input-success"
                );

                if (message) {
                    message.textContent =
                        "Passwords match.";
                    message.style.color =
                        "#198754";
                }

            } else {

                confirmPassword.classList.remove(
                    "input-success"
                );

                confirmPassword.classList.add(
                    "input-error"
                );

                if (message) {
                    message.textContent =
                        "Passwords do not match.";
                    message.style.color =
                        "#dc3545";
                }
            }
        }

        password.addEventListener(
            "input",
            checkPasswordMatch
        );

        confirmPassword.addEventListener(
            "input",
            checkPasswordMatch
        );
    }


    /* =====================================================
       FORM SUBMIT LOADING
       ===================================================== */

    const accountForms =
        document.querySelectorAll(
            ".account-form"
        );

    accountForms.forEach(function (form) {

        form.addEventListener(
            "submit",
            function () {

                const submitButton =
                    form.querySelector(
                        'button[type="submit"]'
                    );

                if (!submitButton)
                    return;

                submitButton.classList.add(
                    "loading"
                );

                submitButton.dataset.originalText =
                    submitButton.innerHTML;

                submitButton.innerHTML =
                    `
                    <span class="account-spinner"></span>
                    <span>Please wait...</span>
                    `;
            }
        );
    });


    /* =====================================================
       DISMISS ALERTS
       ===================================================== */

    const alerts =
        document.querySelectorAll(
            ".account-alert[data-auto-dismiss]"
        );

    alerts.forEach(function (alert) {

        setTimeout(function () {

            alert.style.transition =
                "opacity 0.4s ease";

            alert.style.opacity = "0";

            setTimeout(function () {
                alert.remove();
            }, 400);

        }, 5000);
    });


    /* =====================================================
       PROFILE IMAGE PREVIEW
       ===================================================== */

    const profileInput =
        document.querySelector(
            "#profileImageInput"
        );

    const profilePreview =
        document.querySelector(
            "#profileImagePreview"
        );

    if (profileInput && profilePreview) {

        profileInput.addEventListener(
            "change",
            function (event) {

                const file =
                    event.target.files[0];

                if (!file)
                    return;

                if (!file.type.startsWith("image/")) {

                    alert(
                        "Please select a valid image file."
                    );

                    profileInput.value = "";

                    return;
                }

                const reader =
                    new FileReader();

                reader.onload =
                    function (e) {

                        profilePreview.src =
                            e.target.result;
                    };

                reader.readAsDataURL(file);
            }
        );
    }


    /* =====================================================
       DELETE ACCOUNT CONFIRMATION
       ===================================================== */

    const deleteButtons =
        document.querySelectorAll(
            "[data-delete-account]"
        );

    deleteButtons.forEach(function (button) {

        button.addEventListener(
            "click",
            function (event) {

                const confirmed =
                    confirm(
                        "Are you sure you want to delete your account? This action may not be reversible."
                    );

                if (!confirmed) {
                    event.preventDefault();
                }
            }
        );
    });


    /* =====================================================
       EDIT PROFILE
       ===================================================== */

    const editButton =
        document.querySelector(
            "[data-edit-profile]"
        );

    const profileFields =
        document.querySelectorAll(
            "[data-profile-field]"
        );

    if (editButton) {

        let editing = false;

        editButton.addEventListener(
            "click",
            function () {

                editing = !editing;

                profileFields.forEach(
                    function (field) {

                        field.disabled =
                            !editing;
                    }
                );

                if (editing) {

                    editButton.textContent =
                        "Cancel";

                } else {

                    editButton.textContent =
                        "Edit Profile";
                }
            }
        );
    }


    /* =====================================================
       EMAIL VALIDATION
       ===================================================== */

    const emailInputs =
        document.querySelectorAll(
            'input[type="email"]'
        );

    emailInputs.forEach(function (input) {

        input.addEventListener(
            "blur",
            function () {

                if (
                    input.value &&
                    !isValidEmail(input.value)
                ) {

                    input.classList.add(
                        "input-error"
                    );

                } else {

                    input.classList.remove(
                        "input-error"
                    );
                }
            }
        );
    });


    function isValidEmail(email) {

        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/
            .test(email);
    }


    /* =====================================================
       MOBILE ACCOUNT SIDEBAR
       ===================================================== */

    const sidebarToggle =
        document.querySelector(
            "[data-account-sidebar-toggle]"
        );

    const sidebar =
        document.querySelector(
            ".account-sidebar"
        );

    if (sidebarToggle && sidebar) {

        sidebarToggle.addEventListener(
            "click",
            function () {

                sidebar.classList.toggle(
                    "show"
                );
            }
        );
    }


    /* =====================================================
       ACTIVE ACCOUNT MENU
       ===================================================== */

    const currentPath =
        window.location.pathname
            .toLowerCase();

    const accountLinks =
        document.querySelectorAll(
            ".account-menu a"
        );

    accountLinks.forEach(function (link) {

        const href =
            link.getAttribute("href");

        if (!href)
            return;

        if (
            currentPath ===
            href.toLowerCase()
        ) {

            link.classList.add(
                "active"
            );
        }
    });


    /* =====================================================
       PREVENT DOUBLE CLICK
       ===================================================== */

    const submitButtons =
        document.querySelectorAll(
            ".account-form button[type='submit']"
        );

    submitButtons.forEach(function (button) {

        button.addEventListener(
            "click",
            function () {

                if (
                    button.dataset.clicked ===
                    "true"
                ) {
                    return;
                }

                button.dataset.clicked =
                    "true";

                setTimeout(function () {

                    button.dataset.clicked =
                        "false";

                }, 5000);
            }
        );
    });

});