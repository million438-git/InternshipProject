document.addEventListener("DOMContentLoaded", function () {

    const form =
        document.getElementById("resetPasswordForm");

    const password =
        document.getElementById("newPassword");

    const confirmPassword =
        document.getElementById("confirmNewPassword");

    const error =
        document.getElementById("resetError");

    const button =
        document.getElementById("resetButton");


    form.addEventListener("submit", function (event) {

        error.textContent = "";

        if (password.value !== confirmPassword.value) {

            event.preventDefault();

            error.textContent =
                "Passwords do not match.";

            confirmPassword.focus();

            return;
        }

        button.disabled = true;

        button.textContent =
            "Resetting Password...";

    });

});