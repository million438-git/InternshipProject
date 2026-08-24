document.addEventListener("DOMContentLoaded", function () {

    const password = document.getElementById("password");
    const showPassword = document.getElementById("showPassword");
    const form = document.getElementById("loginForm");
    const button = document.getElementById("loginButton");

    if (showPassword && password) {

        showPassword.addEventListener("change", function () {

            password.type =
                this.checked ? "text" : "password";

        });

    }

    if (form) {

        form.addEventListener("submit", function () {

            if (button) {

                button.disabled = true;
                button.textContent = "Signing in...";

            }

        });

    }

});