document.addEventListener("DOMContentLoaded", function () {

    /*
     * MOBILE SIDEBAR
     */

    const sidebarToggle = document.getElementById("sidebarToggle");
    const sidebar = document.querySelector(".dashboard-sidebar");

    if (sidebarToggle && sidebar) {
        sidebarToggle.addEventListener("click", function () {
            sidebar.classList.toggle("open");
        });
    }


    /*
     * SEARCH
     */

    const searchInput = document.getElementById("dashboardSearch");

    if (searchInput) {

        searchInput.addEventListener("input", function () {

            const searchValue = this.value.toLowerCase();

            const eventItems =
                document.querySelectorAll(".dashboard-event-item");

            eventItems.forEach(function (item) {

                const text = item.textContent.toLowerCase();

                if (text.includes(searchValue)) {
                    item.style.display = "";
                } else {
                    item.style.display = "none";
                }

            });

        });

    }


    /*
     * NOTIFICATION BUTTON
     */

    const notificationButton =
        document.getElementById("notificationButton");

    if (notificationButton) {

        notificationButton.addEventListener("click", function () {

            const notificationList =
                document.querySelector(".notification-list");

            if (notificationList) {

                notificationList.scrollIntoView({
                    behavior: "smooth",
                    block: "center"
                });

            }

        });

    }


    /*
     * MARK NOTIFICATIONS AS READ
     */

    const markNotificationsRead =
        document.getElementById("markNotificationsRead");

    if (markNotificationsRead) {

        markNotificationsRead.addEventListener("click", function () {

            const unreadNotifications =
                document.querySelectorAll(
                    ".dashboard-notification.unread"
                );

            unreadNotifications.forEach(function (notification) {
                notification.classList.remove("unread");
            });

            const notificationCount =
                document.querySelector(".notification-count");

            if (notificationCount) {
                notificationCount.textContent = "0";
                notificationCount.style.display = "none";
            }

        });

    }


    /*
     * MINI CALENDAR
     */

    const calendarDays =
        document.getElementById("calendarDays");

    const calendarMonth =
        document.getElementById("calendarMonth");

    const previousMonth =
        document.getElementById("previousMonth");

    const nextMonth =
        document.getElementById("nextMonth");


    let currentDate = new Date();


    function generateCalendar(date) {

        if (!calendarDays || !calendarMonth) {
            return;
        }

        calendarDays.innerHTML = "";


        const year = date.getFullYear();
        const month = date.getMonth();


        const firstDay =
            new Date(year, month, 1);

        const lastDay =
            new Date(year, month + 1, 0);


        const firstWeekday =
            (firstDay.getDay() + 6) % 7;


        const daysInMonth =
            lastDay.getDate();


        calendarMonth.textContent =
            date.toLocaleDateString(
                "en-US",
                {
                    month: "long",
                    year: "numeric"
                }
            );


        for (let i = 0; i < firstWeekday; i++) {

            const emptyDay =
                document.createElement("span");

            calendarDays.appendChild(emptyDay);

        }


        for (let day = 1; day <= daysInMonth; day++) {

            const dayElement =
                document.createElement("span");

            dayElement.textContent = day;


            const today = new Date();


            if (
                day === today.getDate() &&
                month === today.getMonth() &&
                year === today.getFullYear()
            ) {

                dayElement.classList.add("today");

            }


            calendarDays.appendChild(dayElement);

        }

    }


    if (previousMonth) {

        previousMonth.addEventListener("click", function () {

            currentDate.setMonth(
                currentDate.getMonth() - 1
            );

            generateCalendar(currentDate);

        });

    }


    if (nextMonth) {

        nextMonth.addEventListener("click", function () {

            currentDate.setMonth(
                currentDate.getMonth() + 1
            );

            generateCalendar(currentDate);

        });

    }


    generateCalendar(currentDate);

});