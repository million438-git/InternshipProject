/**
 * Hawassa Unified Campus Event Management System
 * International Enterprise Admin Console Engine
 * Features: Responsive Mini/Expanded Collapsible Sidebar, Tooltips, Table Search & Shortcuts
 */

document.addEventListener('DOMContentLoaded', () => {
    const sidebar = document.getElementById('adminSidebar');
    const toggleBtn = document.getElementById('adminSidebarToggle');
    const closeBtn = document.getElementById('adminCloseSidebar');
    const searchInput = document.getElementById('adminGlobalSearch');

    // Initialize Bootstrap 5 Tooltips for sidebar items
    let tooltipTriggerList = [].slice.call(document.querySelectorAll('.admin-sidebar [data-bs-toggle="tooltip"]'));
    let tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl, {
            trigger: 'hover',
            boundary: 'clippingParents'
        });
    });

    // Create backdrop for mobile drawer if not exists
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

    function updateToggleButtonState(isMini) {
        if (!toggleBtn) return;
        if (isDesktop()) {
            toggleBtn.setAttribute('title', isMini ? 'Expand Sidebar (Ctrl+B)' : 'Collapse Sidebar (Ctrl+B)');
            toggleBtn.setAttribute('aria-expanded', isMini ? 'false' : 'true');
        } else {
            const isOpen = sidebar && sidebar.classList.contains('show');
            toggleBtn.setAttribute('title', isOpen ? 'Close Menu' : 'Open Sidebar Menu');
            toggleBtn.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
        }
    }

    // Synchronize initial state
    const savedMiniState = localStorage.getItem('hucems-admin-sidebar-mini');
    if (isDesktop()) {
        if (savedMiniState === 'true') {
            document.documentElement.classList.add('sidebar-mini');
            document.body.classList.add('sidebar-mini');
            updateToggleButtonState(true);
        } else {
            document.documentElement.classList.remove('sidebar-mini');
            document.body.classList.remove('sidebar-mini');
            updateToggleButtonState(false);
        }
    } else {
        document.documentElement.classList.remove('sidebar-mini');
        document.body.classList.remove('sidebar-mini');
        updateToggleButtonState(false);
    }

    function closeMobileSidebar() {
        if (sidebar) sidebar.classList.remove('show');
        if (backdrop) backdrop.classList.remove('active');
        document.body.style.overflow = '';
        updateToggleButtonState(false);
    }

    function openMobileSidebar() {
        if (sidebar) sidebar.classList.add('show');
        if (backdrop) backdrop.classList.add('active');
        document.body.style.overflow = 'hidden';
        updateToggleButtonState(false);
    }

    function toggleSidebar() {
        if (isDesktop()) {
            // Desktop: toggle compact mini-sidebar mode
            const isCurrentlyMini = document.documentElement.classList.contains('sidebar-mini') || document.body.classList.contains('sidebar-mini');
            const newMini = !isCurrentlyMini;

            document.documentElement.classList.toggle('sidebar-mini', newMini);
            document.body.classList.toggle('sidebar-mini', newMini);
            localStorage.setItem('hucems-admin-sidebar-mini', newMini ? 'true' : 'false');
            updateToggleButtonState(newMini);

            // Re-sync tooltip instances
            tooltipList.forEach(t => t.hide());
        } else {
            // Mobile: toggle slide-in offcanvas drawer
            if (sidebar && sidebar.classList.contains('show')) {
                closeMobileSidebar();
            } else {
                openMobileSidebar();
            }
        }
    }

    // Automatically expand parent submenu containing active item
    const activeSubmenuItem = document.querySelector('.admin-submenu .admin-submenu-item.active');
    if (activeSubmenuItem) {
        const parentSubmenu = activeSubmenuItem.closest('.admin-submenu');
        if (parentSubmenu) {
            parentSubmenu.classList.add('show');
            const toggleTrigger = document.querySelector(`[data-bs-target="#${parentSubmenu.id}"]`);
            if (toggleTrigger) {
                toggleTrigger.setAttribute('aria-expanded', 'true');
                toggleTrigger.classList.add('active');
            }
        }
    }

    // Submenu toggle clicks in mini-mode automatically expand the sidebar
    const submenuToggles = document.querySelectorAll('.admin-nav-toggle');
    submenuToggles.forEach(toggle => {
        toggle.addEventListener('click', () => {
            if (isDesktop() && (document.documentElement.classList.contains('sidebar-mini') || document.body.classList.contains('sidebar-mini'))) {
                document.documentElement.classList.remove('sidebar-mini');
                document.body.classList.remove('sidebar-mini');
                localStorage.setItem('hucems-admin-sidebar-mini', 'false');
                updateToggleButtonState(false);
            }
        });
    });

    if (toggleBtn) {
        toggleBtn.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
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

    // Keyboard Shortcuts:
    // 1. Ctrl+B or Alt+M: Toggle Sidebar
    // 2. Ctrl+K: Focus Global Search
    // 3. Escape: Close mobile sidebar
    document.addEventListener('keydown', (e) => {
        if ((e.ctrlKey && e.key.toLowerCase() === 'b') || (e.altKey && e.key.toLowerCase() === 'm')) {
            e.preventDefault();
            toggleSidebar();
        } else if (e.ctrlKey && e.key.toLowerCase() === 'k') {
            if (searchInput) {
                e.preventDefault();
                searchInput.focus();
                searchInput.select();
            }
        } else if (e.key === 'Escape') {
            if (!isDesktop() && sidebar && sidebar.classList.contains('show')) {
                closeMobileSidebar();
            }
        }
    });

    // Window Resize cleanup
    window.addEventListener('resize', () => {
        if (isDesktop()) {
            closeMobileSidebar();
            const isMini = localStorage.getItem('hucems-admin-sidebar-mini') === 'true';
            document.documentElement.classList.toggle('sidebar-mini', isMini);
            document.body.classList.toggle('sidebar-mini', isMini);
            updateToggleButtonState(isMini);
        } else {
            document.documentElement.classList.remove('sidebar-mini');
            document.body.classList.remove('sidebar-mini');
            updateToggleButtonState(false);
        }
    });

    // Global Search redirect on Enter
    if (searchInput) {
        searchInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && searchInput.value.trim().length > 0) {
                window.location.href = `/Admin/Events?search=${encodeURIComponent(searchInput.value.trim())}`;
            }
        });
    }

    // Table Live Search Filtering
    const tableFilterInputs = document.querySelectorAll('[data-table-filter]');
    tableFilterInputs.forEach(input => {
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

    // Auto-dismiss flash alerts after 5 seconds
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
        for (let j = 0; j < cols.length - 1; j++) {
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
