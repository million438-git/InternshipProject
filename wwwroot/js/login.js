/**
 * HUCEMS Authentication Portal Interactivity Script
 */
document.addEventListener("DOMContentLoaded", function () {
    const passwordInput = document.getElementById("password");
    const toggleBtn = document.getElementById("togglePasswordBtn");
    const toggleIcon = document.getElementById("togglePasswordIcon");
    const form = document.getElementById("loginForm");
    const loginBtn = document.getElementById("loginButton");
    const btnText = document.getElementById("btnText");
    const btnSpinner = document.getElementById("btnSpinner");

    // Accessible Password Visibility Toggle
    if (toggleBtn && passwordInput && toggleIcon) {
        toggleBtn.addEventListener("click", function () {
            const isPassword = passwordInput.type === "password";
            passwordInput.type = isPassword ? "text" : "password";
            toggleIcon.className = isPassword ? "bi bi-eye-slash" : "bi bi-eye";
            toggleBtn.setAttribute("aria-label", isPassword ? "Hide password" : "Show password");
            toggleBtn.setAttribute("title", isPassword ? "Hide password" : "Show password");
        });
    }

    // Accessible Form Submission Feedback
    if (form && loginBtn) {
        form.addEventListener("submit", function (e) {
            const emailInput = document.getElementById("email");
            if (!emailInput || !passwordInput) return;

            // Simple validation check before disabling button
            if (emailInput.value.trim() === "" || passwordInput.value.trim() === "") {
                return;
            }

            loginBtn.disabled = true;
            if (btnText) btnText.textContent = "Signing In...";
            if (btnSpinner) btnSpinner.classList.remove("d-none");
        });
    }
});