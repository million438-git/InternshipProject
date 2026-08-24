document.addEventListener("DOMContentLoaded", function () {

    const form =
        document.getElementById("forgotPasswordForm");

    const email =
        document.getElementById("forgotEmail");

    const button =
        document.getElementById("forgotButton");


    form.addEventListener("submit", function (event) {

        const value = email.value.trim();

        if (value === "") {

            event.preventDefault();

            email.focus();

            return;
        }

        button.disabled = true;
        button.textContent = "Sending...";

    });

});