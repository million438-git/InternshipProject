/**
 * HUCEMS Verified Registration Interactivity Script
 */
document.addEventListener("DOMContentLoaded", function () {
    const roleCards = document.querySelectorAll(".role-picker-card");
    const roleBadge = document.getElementById("roleDescriptionBadge");
    const studentGroup = document.getElementById("studentIdGroup");
    const employeeGroup = document.getElementById("employeeIdGroup");
    const orgGroup = document.getElementById("orgGroup");
    const studentIdInput = document.getElementById("studentId");
    const employeeIdInput = document.getElementById("employeeId");
    const orgNameInput = document.getElementById("organizationName");

    const passwordInput = document.getElementById("password");
    const confirmPasswordInput = document.getElementById("confirmPassword");
    const toggleBtn = document.getElementById("toggleRegPasswordBtn");
    const toggleIcon = document.getElementById("toggleRegPasswordIcon");
    const strengthBar = document.getElementById("strengthBar");
    const strengthLabel = document.getElementById("strengthLabel");
    const matchLabel = document.getElementById("matchLabel");

    const form = document.getElementById("registerForm");
    const regBtn = document.getElementById("registerButton");
    const regBtnText = document.getElementById("regBtnText");
    const regBtnArrow = document.getElementById("regBtnArrow");
    const regBtnSpinner = document.getElementById("regBtnSpinner");

    // =========================================================
    // 1. DYNAMIC ROLE SWITCHER
    // =========================================================
    const roleDescriptions = {
        Student: "Student Account — Events & RSVPs",
        Faculty: "Faculty Account — Academic Seminars",
        Staff: "Staff Account — Campus Operations",
        Organization: "Student Club / Org — Host Activities"
    };

    function updateRole(selectedRole) {
        roleCards.forEach(card => {
            const radio = card.querySelector('input[type="radio"]');
            if (radio && radio.value === selectedRole) {
                card.classList.add("active");
                radio.checked = true;
            } else {
                card.classList.remove("active");
            }
        });

        if (roleBadge) {
            roleBadge.textContent = roleDescriptions[selectedRole] || selectedRole;
        }

        // Hide all conditional groups
        if (studentGroup) studentGroup.classList.add("d-none");
        if (employeeGroup) employeeGroup.classList.add("d-none");
        if (orgGroup) orgGroup.classList.add("d-none");

        // Clear required states
        if (studentIdInput) studentIdInput.required = false;
        if (employeeIdInput) employeeIdInput.required = false;
        if (orgNameInput) orgNameInput.required = false;

        // Show matching role fields
        if (selectedRole === "Student") {
            if (studentGroup) studentGroup.classList.remove("d-none");
            if (studentIdInput) studentIdInput.required = true;
        } else if (selectedRole === "Faculty" || selectedRole === "Staff") {
            if (employeeGroup) employeeGroup.classList.remove("d-none");
            if (employeeIdInput) employeeIdInput.required = true;
        } else if (selectedRole === "Organization") {
            if (orgGroup) orgGroup.classList.remove("d-none");
            if (orgNameInput) orgNameInput.required = true;
        }
    }

    roleCards.forEach(card => {
        card.addEventListener("click", function () {
            const radio = this.querySelector('input[type="radio"]');
            if (radio) {
                updateRole(radio.value);
            }
        });
    });

    // Initialize with default checked role
    const defaultRadio = document.querySelector('.role-picker-card input[type="radio"]:checked');
    if (defaultRadio) {
        updateRole(defaultRadio.value);
    }

    // =========================================================
    // 2. TOGGLE PASSWORD VISIBILITY
    // =========================================================
    if (toggleBtn && passwordInput && toggleIcon) {
        toggleBtn.addEventListener("click", function () {
            const isPassword = passwordInput.type === "password";
            passwordInput.type = isPassword ? "text" : "password";
            toggleIcon.className = isPassword ? "bi bi-eye-slash" : "bi bi-eye";
        });
    }

    // =========================================================
    // 3. DYNAMIC PASSWORD STRENGTH METER
    // =========================================================
    function evaluatePasswordStrength(val) {
        if (!val || val.length === 0) {
            return { score: 0, text: "Password strength: Enter password", color: "#e2e8f0", width: "0%" };
        }
        if (val.length < 8) {
            return { score: 1, text: "Too Short (Min 8 characters)", color: "#ef4444", width: "25%" };
        }

        let score = 1;
        if (/[a-z]/.test(val) && /[A-Z]/.test(val)) score++;
        if (/\d/.test(val)) score++;
        if (/[^a-zA-Z0-9]/.test(val)) score++;

        if (score <= 1) {
            return { score: 1, text: "Weak Password", color: "#ef4444", width: "35%" };
        } else if (score === 2) {
            return { score: 2, text: "Fair Password (add numbers/symbols)", color: "#f59e0b", width: "60%" };
        } else if (score === 3) {
            return { score: 3, text: "Good Password", color: "#3b82f6", width: "80%" };
        } else {
            return { score: 4, text: "Strong & Secure 🔒", color: "#10b981", width: "100%" };
        }
    }

    if (passwordInput && strengthBar && strengthLabel) {
        passwordInput.addEventListener("input", function () {
            const res = evaluatePasswordStrength(this.value);
            strengthBar.style.width = res.width;
            strengthBar.style.backgroundColor = res.color;
            strengthLabel.textContent = res.text;
            strengthLabel.style.color = res.color === "#e2e8f0" ? "#64748b" : res.color;

            checkPasswordMatch();
        });
    }

    // =========================================================
    // 4. PASSWORD MATCH VALIDATION
    // =========================================================
    function checkPasswordMatch() {
        if (!confirmPasswordInput || !matchLabel) return true;

        const pass = passwordInput ? passwordInput.value : "";
        const confirm = confirmPasswordInput.value;

        if (!confirm) {
            matchLabel.classList.add("d-none");
            matchLabel.textContent = "";
            return true;
        }

        matchLabel.classList.remove("d-none");
        if (pass === confirm) {
            matchLabel.innerHTML = '<span class="text-success fw-bold"><i class="bi bi-check-circle-fill"></i> Passwords Match</span>';
            return true;
        } else {
            matchLabel.innerHTML = '<span class="text-danger fw-bold"><i class="bi bi-x-circle-fill"></i> Passwords Do Not Match</span>';
            return false;
        }
    }

    if (confirmPasswordInput) {
        confirmPasswordInput.addEventListener("input", checkPasswordMatch);
    }

    // =========================================================
    // 5. SUBMIT ANIMATION & FINAL VALIDATION
    // =========================================================
    if (form) {
        form.addEventListener("submit", function (e) {
            if (!checkPasswordMatch()) {
                e.preventDefault();
                confirmPasswordInput.focus();
                return;
            }

            if (regBtn) {
                regBtn.disabled = true;
                if (regBtnText) regBtnText.textContent = "Verifying & Creating Account...";
                if (regBtnArrow) regBtnArrow.classList.add("d-none");
                if (regBtnSpinner) regBtnSpinner.classList.remove("d-none");
            }
        });
    }
});