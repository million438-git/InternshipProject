/**
 * Hawassa Unified Campus Event Management System
 * Admin Console JavaScript Helper - Full Responsive & Collapsible Sidebar Engine
 */

document.addEventListener('DOMContentLoaded', () => {
    const sidebar = document.getElementById('adminSidebar');
    const toggleBtn = document.getElementById('adminSidebarToggle');
    const closeBtn = document.getElementById('adminCloseSidebar');

    // Create backdrop for mobile if not exists
    let backdrop = document.getElementById('adminSidebarBackdrop');
    if (!backdrop) {
        backdrop = document.createElement('div');
        backdrop.id = 'adminSidebarBackdrop';
        backdrop.className = 'admin-sidebar-backdrop';
        document.body.appendChild(backdrop);
    }

    function isDesktop() {
        return window.innerWidth >= 992;
    }

    // Initialize desktop collapsed state from localStorage
    if (isDesktop() && localStorage.getItem('hucems-admin-sidebar-collapsed') === 'true') {
        document.body.classList.add('sidebar-collapsed');
    }

    function closeMobileSidebar() {
        if (sidebar) sidebar.classList.remove('show');
        if (backdrop) backdrop.classList.remove('active');
        document.body.style.overflow = '';
    }

    function toggleSidebar() {
        if (isDesktop()) {
            // Desktop: toggle collapsed mode (expand to full page)
            const isCollapsed = document.body.classList.toggle('sidebar-collapsed');
            localStorage.setItem('hucems-admin-sidebar-collapsed', isCollapsed ? 'true' : 'false');
        } else {
            // Mobile: slide-in drawer with backdrop
            if (sidebar) {
                const isOpen = sidebar.classList.toggle('show');
                if (backdrop) backdrop.classList.toggle('active', isOpen);
                document.body.style.overflow = isOpen ? 'hidden' : '';
            }
        }
    }

    if (toggleBtn) {
        toggleBtn.addEventListener('click', (e) => {
            e.preventDefault();
            toggleSidebar();
        });
    }

    if (closeBtn) {
        closeBtn.addEventListener('click', (e) => {
            e.preventDefault();
            closeMobileSidebar();
        });
    }

    if (backdrop) {
        backdrop.addEventListener('click', () => {
            closeMobileSidebar();
        });
    }

    // Keyboard shortcut to collapse/expand sidebar: Ctrl+B or Alt+M
    document.addEventListener('keydown', (e) => {
        if ((e.ctrlKey && e.key.toLowerCase() === 'b') || (e.altKey && e.key.toLowerCase() === 'm')) {
            e.preventDefault();
            toggleSidebar();
        }
    });

    // Window Resize cleanup
    window.addEventListener('resize', () => {
        if (isDesktop()) {
            closeMobileSidebar();
            if (localStorage.getItem('hucems-admin-sidebar-collapsed') === 'true') {
                document.body.classList.add('sidebar-collapsed');
            } else {
                document.body.classList.remove('sidebar-collapsed');
            }
        }
    });

    // 2. Table Live Search Filtering
    const searchInputs = document.querySelectorAll('[data-table-filter]');
    searchInputs.forEach(input => {
        const targetTableId = input.getAttribute('data-table-filter');
        const table = document.getElementById(targetTableId);
        if (!table) return;

        input.addEventListener('input', (e) => {
            const term = e.target.value.toLowerCase().trim();
            const rows = table.querySelectorAll('tbody tr');

            rows.forEach(row => {
                const text = row.textContent.toLowerCase();
                if (text.includes(term)) {
                    row.style.display = '';
                } else {
                    row.style.display = 'none';
                }
            });
        });
    });

    // 3. Auto-dismiss alerts after 5 seconds
    setTimeout(() => {
        const alerts = document.querySelectorAll('.alert-dismissible');
        alerts.forEach(alert => {
            const bsAlert = bootstrap.Alert.getOrCreateInstance(alert);
            if (bsAlert) bsAlert.close();
        });
    }, 5000);
});

// CSV Export Helper
function exportTableToCSV(tableId, filename = 'export.csv') {
    const table = document.getElementById(tableId);
    if (!table) return;

    let csv = [];
    const rows = table.querySelectorAll('tr');

    for (let i = 0; i < rows.length; i++) {
        let row = [], cols = rows[i].querySelectorAll('td, th');
        for (let j = 0; j < cols.length - 1; j++) { // exclude last action column
            let data = cols[j].innerText.replace(/(\r\n|\n|\r)/gm, '').replace(/(\s\s+)/gm, ' ');
            data = data.replace(/"/g, '""');
            row.push('"' + data + '"');
        }
        csv.push(row.join(','));
    }

    const csvFile = new Blob([csv.join('\n')], { type: 'text/csv' });
    const downloadLink = document.createElement('a');
    downloadLink.download = filename;
    downloadLink.href = window.URL.createObjectURL(csvFile);
    downloadLink.style.display = 'none';
    document.body.appendChild(downloadLink);
    downloadLink.click();
    document.body.removeChild(downloadLink);
}
