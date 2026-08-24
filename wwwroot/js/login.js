/**
 * HUCEMS Login Interactivity Script
 */
document.addEventListener("DOMContentLoaded", function () {
    const passwordInput = document.getElementById("password");
    const toggleBtn = document.getElementById("togglePasswordBtn");
    const toggleIcon = document.getElementById("togglePasswordIcon");
    const form = document.getElementById("loginForm");
    const loginBtn = document.getElementById("loginButton");
    const btnText = document.getElementById("btnText");
    const btnArrow = document.getElementById("btnArrow");
    const btnSpinner = document.getElementById("btnSpinner");

    // Toggle Password Visibility
    if (toggleBtn && passwordInput && toggleIcon) {
        toggleBtn.addEventListener("click", function () {
            const isPassword = passwordInput.type === "password";
            passwordInput.type = isPassword ? "text" : "password";
            toggleIcon.className = isPassword ? "bi bi-eye-slash" : "bi bi-eye";
        });
    }

    // Submit Animation
    if (form) {
        form.addEventListener("submit", function () {
            if (loginBtn) {
                loginBtn.disabled = true;
                if (btnText) btnText.textContent = "Authenticating with HU Portal...";
                if (btnArrow) btnArrow.classList.add("d-none");
                if (btnSpinner) btnSpinner.classList.remove("d-none");
            }
        });
    }
});