"use strict";

/* =========================================================
   HUCEMS CAMPUS CALENDAR (DATABASE PERSISTENCE ENGINE)
   ========================================================= */

// Live Database Event Store (Populated via /api/events/calendar)
let campusEvents = [];

/* =========================================================
   STATE
   ========================================================= */

let currentDate = new Date();
let selectedDate = new Date();
let filteredEvents = [];
let isLoadingEvents = false;

/* =========================================================
   DOM ELEMENTS
   ========================================================= */

const calendarGrid = document.getElementById("calendarGrid");
const currentMonth = document.getElementById("currentMonth");
const previousMonth = document.getElementById("previousMonth");
const nextMonth = document.getElementById("nextMonth");
const todayButton = document.getElementById("todayButton");
const eventSearch = document.getElementById("eventSearch");
const categoryFilters = document.querySelectorAll(".category-filter input");
const upcomingEvents = document.getElementById("upcomingEvents");
const selectedDayTitle = document.getElementById("selectedDayTitle");
const selectedEventCount = document.getElementById("selectedEventCount");
const selectedDayEvents = document.getElementById("selectedDayEvents");

/* =========================================================
   MODAL ELEMENTS
   ========================================================= */

const eventModal = document.getElementById("eventModal");
const modalOverlay = document.getElementById("modalOverlay");
const closeModal = document.getElementById("closeModal");
const closeModalButton = document.getElementById("closeModalButton");
const modalCategory = document.getElementById("modalCategory");
const modalTitle = document.getElementById("modalTitle");
const modalDate = document.getElementById("modalDate");
const modalTime = document.getElementById("modalTime");
const modalLocation = document.getElementById("modalLocation");
const modalOrganizer = document.getElementById("modalOrganizer");
const modalDescription = document.getElementById("modalDescription");
const viewEventButton = document.getElementById("viewEventButton");
let activeModalEvent = null;

/* =========================================================
   MONTH NAMES
   ========================================================= */

const months = [
    "January", "February", "March", "April", "May", "June",
    "July", "August", "September", "October", "November", "December"
];

const shortMonths = [
    "Jan", "Feb", "Mar", "Apr", "May", "Jun",
    "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
];

/* =========================================================
   ASYNC DATA FETCHER (API INTEGRATION)
   ========================================================= */

async function fetchCalendarEvents() {
    isLoadingEvents = true;
    showLoadingState();

    try {
        const response = await fetch('/api/events/calendar');
        if (!response.ok) {
            // Fallback to general list endpoint if calendar feed is unavailable
            const fallbackRes = await fetch('/api/events?pageSize=100');
            if (!fallbackRes.ok) throw new Error(`API error: ${fallbackRes.status}`);
            const fallbackJson = await fallbackRes.json();
            processEventPayload(fallbackJson.data || []);
            return;
        }

        const resData = await response.json();
        const rawItems = resData && resData.data ? resData.data : [];
        processEventPayload(rawItems);
    } catch (err) {
        console.warn("HUCEMS Calendar: Could not load live events from API.", err);
        showErrorState("Could not retrieve events from the campus server. Please refresh or try again later.");
    } finally {
        isLoadingEvents = false;
        applyFilters();
    }
}

function processEventPayload(rawEvents) {
    campusEvents = (rawEvents || []).map(function (e) {
        const startDate = new Date(e.start || e.startAt || Date.now());
        const endDate = e.end || e.endAt ? new Date(e.end || e.endAt) : null;

        const yyyy = startDate.getFullYear();
        const mm = String(startDate.getMonth() + 1).padStart(2, '0');
        const dd = String(startDate.getDate()).padStart(2, '0');
        const dateStr = `${yyyy}-${mm}-${dd}`;

        let timeStr = startDate.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        if (endDate) {
            const endTimeStr = endDate.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
            timeStr = `${timeStr} - ${endTimeStr}`;
        }

        const rawCat = (e.category || "academic").toLowerCase().trim();
        const validCategories = ["academic", "technology", "social", "sports", "career", "cultural"];
        const normalizedCategory = validCategories.includes(rawCat) ? rawCat : "academic";

        return {
            id: e.id,
            slug: e.slug || `event-${e.id}`,
            title: e.title || "Campus Event",
            date: dateStr,
            rawStartDate: startDate,
            rawEndDate: endDate,
            time: timeStr,
            location: e.location || e.venue || "Main Campus, Hawassa University",
            organizer: e.organizer || "Hawassa Campus Administration",
            category: normalizedCategory,
            eventMode: e.eventMode || "IN_PERSON",
            description: e.description || e.shortDescription || "No detailed description provided."
        };
    });

    filteredEvents = [...campusEvents];
}

function showLoadingState() {
    if (selectedDayEvents) {
        selectedDayEvents.innerHTML = `
            <div class="no-events text-center py-4">
                <div class="spinner-border text-primary mb-2" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
                <p class="text-muted small mb-0">Synchronizing campus events calendar...</p>
            </div>
        `;
    }
}

function showErrorState(message) {
    if (selectedDayEvents) {
        selectedDayEvents.innerHTML = `
            <div class="no-events text-center py-4">
                <i class="bi bi-exclamation-triangle text-warning fs-3 mb-2"></i>
                <strong>Failed to load events</strong>
                <span class="text-muted small d-block">${escapeHtml(message)}</span>
            </div>
        `;
    }
}

/* =========================================================
   INITIALIZATION
   ========================================================= */

document.addEventListener("DOMContentLoaded", function () {
    setupListeners();
    renderCalendar();
    renderSelectedDay();
    renderUpcomingEvents();
    fetchCalendarEvents();
});

/* =========================================================
   EVENT LISTENERS
   ========================================================= */

function setupListeners() {
    if (previousMonth) {
        previousMonth.addEventListener("click", function () {
            currentDate.setMonth(currentDate.getMonth() - 1);
            renderCalendar();
        });
    }

    if (nextMonth) {
        nextMonth.addEventListener("click", function () {
            currentDate.setMonth(currentDate.getMonth() + 1);
            renderCalendar();
        });
    }

    if (todayButton) {
        todayButton.addEventListener("click", function () {
            const today = new Date();
            currentDate = new Date(today);
            selectedDate = new Date(today);
            renderCalendar();
            renderSelectedDay();
        });
    }

    if (eventSearch) {
        eventSearch.addEventListener("input", applyFilters);
    }

    categoryFilters.forEach(function (filter) {
        filter.addEventListener("change", applyFilters);
    });

    if (closeModal) closeModal.addEventListener("click", closeEventModal);
    if (closeModalButton) closeModalButton.addEventListener("click", closeEventModal);
    if (modalOverlay) modalOverlay.addEventListener("click", closeEventModal);

    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape") closeEventModal();
    });

    if (viewEventButton) {
        viewEventButton.addEventListener("click", function () {
            if (activeModalEvent && activeModalEvent.id) {
                window.location.href = `/Events/Details/${activeModalEvent.id}`;
            }
        });
    }
}

/* =========================================================
   CALENDAR GRID RENDERING
   ========================================================= */

function renderCalendar() {
    if (!calendarGrid) return;
    calendarGrid.innerHTML = "";

    const year = currentDate.getFullYear();
    const month = currentDate.getMonth();

    if (currentMonth) {
        currentMonth.textContent = `${months[month]} ${year}`;
    }

    const firstDay = new Date(year, month, 1).getDay();
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const daysInPreviousMonth = new Date(year, month, 0).getDate();

    // Previous month padding
    for (let i = firstDay - 1; i >= 0; i--) {
        const date = new Date(year, month - 1, daysInPreviousMonth - i);
        createDay(date, true);
    }

    // Current month days
    for (let day = 1; day <= daysInMonth; day++) {
        const date = new Date(year, month, day);
        createDay(date, false);
    }

    // Next month padding
    const cells = calendarGrid.children.length;
    const remaining = cells % 7 === 0 ? 0 : 7 - (cells % 7);

    for (let day = 1; day <= remaining; day++) {
        const date = new Date(year, month + 1, day);
        createDay(date, true);
    }
}

function createDay(date, otherMonth) {
    const day = document.createElement("div");
    day.className = "calendar-day";

    if (otherMonth) day.classList.add("other-month");
    if (sameDate(date, new Date())) day.classList.add("today");
    if (sameDate(date, selectedDate)) day.classList.add("selected");

    const number = document.createElement("div");
    number.className = "calendar-day-number";
    number.textContent = date.getDate();
    day.appendChild(number);

    const dayEvents = getEventsForDate(date);

    if (dayEvents.length > 0) {
        const eventContainer = document.createElement("div");
        eventContainer.className = "calendar-events";

        const max = window.innerWidth <= 520 ? 1 : 3;

        dayEvents.slice(0, max).forEach(function (evt) {
            const eventElement = document.createElement("div");
            eventElement.className = `calendar-event ${evt.category}`;
            eventElement.textContent = evt.title;
            eventElement.title = `${evt.title} (${evt.time})`;

            eventElement.addEventListener("click", function (e) {
                e.stopPropagation();
                openEventModal(evt);
            });

            eventContainer.appendChild(eventElement);
        });

        if (dayEvents.length > max) {
            const more = document.createElement("div");
            more.className = "more-events";
            more.textContent = `+${dayEvents.length - max} more`;
            eventContainer.appendChild(more);
        }

        day.appendChild(eventContainer);
    }

    day.addEventListener("click", function () {
        selectedDate = new Date(date);
        renderCalendar();
        renderSelectedDay();
    });

    calendarGrid.appendChild(day);
}

function getEventsForDate(date) {
    const dateString = formatDate(date);
    return filteredEvents.filter(function (event) {
        return event.date === dateString;
    });
}

function formatDate(date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
}

function sameDate(first, second) {
    return (
        first.getFullYear() === second.getFullYear() &&
        first.getMonth() === second.getMonth() &&
        first.getDate() === second.getDate()
    );
}

/* =========================================================
   SELECTED DAY PANEL
   ========================================================= */

function renderSelectedDay() {
    if (!selectedDayTitle || !selectedEventCount || !selectedDayEvents) return;

    const dayEvents = getEventsForDate(selectedDate);

    selectedDayTitle.textContent = selectedDate.toLocaleDateString("en-US", {
        weekday: "long",
        month: "long",
        day: "numeric",
        year: "numeric"
    });

    selectedEventCount.textContent = `${dayEvents.length} ${dayEvents.length === 1 ? "Event" : "Events"}`;
    selectedDayEvents.innerHTML = "";

    if (dayEvents.length === 0) {
        selectedDayEvents.innerHTML = `
            <div class="no-events">
                <i class="bi bi-calendar-x"></i>
                <strong>No events scheduled</strong>
                <span>There are no campus events for this day.</span>
            </div>
        `;
        return;
    }

    dayEvents.forEach(function (evt) {
        const card = document.createElement("div");
        card.className = "day-event";

        card.innerHTML = `
            <div class="day-event-time">${escapeHtml(evt.time)}</div>
            <div class="day-event-info">
                <h3>${escapeHtml(evt.title)}</h3>
                <p><i class="bi bi-geo-alt me-1"></i>${escapeHtml(evt.location)}</p>
            </div>
            <span class="day-event-category ${evt.category}">${escapeHtml(evt.category)}</span>
        `;

        card.addEventListener("click", function () {
            openEventModal(evt);
        });

        selectedDayEvents.appendChild(card);
    });
}

/* =========================================================
   UPCOMING EVENTS PANEL
   ========================================================= */

function renderUpcomingEvents() {
    if (!upcomingEvents) return;
    upcomingEvents.innerHTML = "";

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const upcoming = filteredEvents
        .filter(function (evt) {
            const d = new Date(evt.date + "T00:00:00");
            return d >= today;
        })
        .sort(function (a, b) {
            return new Date(a.date) - new Date(b.date);
        })
        .slice(0, 6);

    if (upcoming.length === 0) {
        upcomingEvents.innerHTML = `
            <div class="no-events">
                <i class="bi bi-calendar-x"></i>
                <span>No upcoming events registered</span>
            </div>
        `;
        return;
    }

    upcoming.forEach(function (evt) {
        const date = new Date(evt.date + "T00:00:00");

        const item = document.createElement("div");
        item.className = "upcoming-event";

        item.innerHTML = `
            <div class="upcoming-date">
                <strong>${date.getDate()}</strong>
                <span>${shortMonths[date.getMonth()]}</span>
            </div>
            <div class="upcoming-info">
                <strong>${escapeHtml(evt.title)}</strong>
                <span>${escapeHtml(evt.time)}</span>
            </div>
        `;

        item.addEventListener("click", function () {
            openEventModal(evt);
        });

        upcomingEvents.appendChild(item);
    });
}

/* =========================================================
   SEARCH & CATEGORY FILTERS
   ========================================================= */

function applyFilters() {
    const search = eventSearch ? eventSearch.value.trim().toLowerCase() : "";

    const selectedCategories = Array.from(categoryFilters)
        .filter(f => f.checked)
        .map(f => f.value.toLowerCase());

    filteredEvents = campusEvents.filter(function (evt) {
        const matchesSearch = !search ||
            evt.title.toLowerCase().includes(search) ||
            evt.description.toLowerCase().includes(search) ||
            evt.location.toLowerCase().includes(search) ||
            evt.organizer.toLowerCase().includes(search);

        const matchesCategory = selectedCategories.includes(evt.category);

        return matchesSearch && matchesCategory;
    });

    renderCalendar();
    renderSelectedDay();
    renderUpcomingEvents();
}

/* =========================================================
   MODAL CONTROLLER
   ========================================================= */

function openEventModal(evt) {
    if (!eventModal) return;
    activeModalEvent = evt;

    if (modalCategory) {
        modalCategory.textContent = evt.category;
        modalCategory.className = `modal-category ${evt.category}`;
    }

    if (modalTitle) modalTitle.textContent = evt.title;

    if (modalDate) {
        const date = new Date(evt.date + "T00:00:00");
        modalDate.textContent = date.toLocaleDateString("en-US", {
            weekday: "long",
            month: "long",
            day: "numeric",
            year: "numeric"
        });
    }

    if (modalTime) modalTime.textContent = evt.time;
    if (modalLocation) modalLocation.textContent = evt.location;
    if (modalOrganizer) modalOrganizer.textContent = evt.organizer;
    if (modalDescription) modalDescription.textContent = evt.description;

    eventModal.classList.add("show");
    eventModal.setAttribute("aria-hidden", "false");
    document.body.style.overflow = "hidden";
}

function closeEventModal() {
    if (!eventModal) return;
    activeModalEvent = null;
    eventModal.classList.remove("show");
    eventModal.setAttribute("aria-hidden", "true");
    document.body.style.overflow = "";
}

/* =========================================================
   UTILITIES
   ========================================================= */

function escapeHtml(value) {
    return String(value || "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}