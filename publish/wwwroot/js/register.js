document.addEventListener("DOMContentLoaded", function () {

    const form = document.getElementById("registerForm");
    const password = document.getElementById("password");
    const confirmPassword =
        document.getElementById("confirmPassword");

    const error =
        document.getElementById("passwordError");

    const button =
        document.getElementById("registerButton");


    function checkPasswords() {

        if (confirmPassword.value === "") {

            error.textContent = "";
            return false;

        }

        if (password.value !== confirmPassword.value) {

            error.textContent =
                "Passwords do not match.";

            return false;
        }

        error.textContent = "";

        return true;
    }


    confirmPassword.addEventListener(
        "input",
        checkPasswords
    );


    form.addEventListener("submit", function (event) {

        if (!checkPasswords()) {

            event.preventDefault();

            confirmPassword.focus();

            return;
        }

        button.disabled = true;
        button.textContent = "Creating Account...";

    });

});