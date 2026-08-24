document.addEventListener("DOMContentLoaded", function () {

    // =====================================================
    // EVENT SEARCH
    // =====================================================

    const searchInput =
        document.querySelector(".events-search input");

    const eventCards =
        document.querySelectorAll(".event-card");


    if (searchInput && eventCards.length > 0) {

        searchInput.addEventListener("input", function () {

            const search =
                this.value.toLowerCase().trim();

            eventCards.forEach(function (card) {

                const text =
                    card.textContent.toLowerCase();

                if (text.includes(search)) {

                    card.style.display = "";

                } else {

                    card.style.display = "grid";

                    card.style.display = "none";

                }

            });

        });

    }


    // =====================================================
    // CHARACTER COUNTER
    // =====================================================

    const description =
        document.querySelector(
            "#Description"
        );

    if (description) {

        const maxLength =
            description.getAttribute("maxlength");

        if (maxLength) {

            const counter =
                document.createElement("small");

            counter.className =
                "event-character-counter";

            description.parentNode.appendChild(counter);


            function updateCounter() {

                counter.textContent =
                    `${description.value.length}/${maxLength} characters`;

            }


            description.addEventListener(
                "input",
                updateCounter
            );

            updateCounter();

        }

    }


    // =====================================================
    // DELETE CONFIRMATION
    // =====================================================

    const deleteForms =
        document.querySelectorAll(
            ".event-delete-form"
        );


    deleteForms.forEach(function (form) {

        form.addEventListener(
            "submit",
            function (event) {

                const confirmed =
                    confirm(
                        "Are you sure you want to delete this event?"
                    );

                if (!confirmed) {

                    event.preventDefault();

                }

            }
        );

    });


    // =====================================================
    // AUTOMATIC DISMISS ALERT
    // =====================================================

    const alert =
        document.querySelector(".events-alert");


    if (alert) {

        setTimeout(function () {

            alert.style.opacity = "0";

            alert.style.transform =
                "translateY(-5px)";

            alert.style.transition =
                "opacity .3s ease, transform .3s ease";


            setTimeout(function () {

                alert.remove();

            }, 350);

        }, 5000);

    }

});