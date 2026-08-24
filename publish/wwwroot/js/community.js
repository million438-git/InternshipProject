document.addEventListener("DOMContentLoaded", function () {

    console.log("Community system loaded.");

    const searchInput =
        document.querySelector(
            'input[name="query"]'
        );

    if (searchInput) {

        searchInput.addEventListener(
            "input",
            function () {

                console.log(
                    "Searching:",
                    searchInput.value
                );

            }
        );

    }

});