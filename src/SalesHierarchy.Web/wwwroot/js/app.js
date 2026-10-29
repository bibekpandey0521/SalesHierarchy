// SalesHierarchy — app shell interactions.
(function () {
    'use strict';

    /* ------------------------------------------------------------ sidebar */

    const sidebar = document.getElementById('appSidebar');
    const backdrop = document.getElementById('sidebarBackdrop');

    function setSidebar(open) {
        if (!sidebar || !backdrop) {
            return;
        }

        sidebar.classList.toggle('show', open);
        backdrop.classList.toggle('show', open);
        document.body.classList.toggle('sidebar-open', open);

        document.querySelectorAll('[data-sidebar-open]').forEach(function (trigger) {
            trigger.setAttribute('aria-expanded', open ? 'true' : 'false');
        });
    }

    document.addEventListener('click', function (event) {
        if (event.target.closest('[data-sidebar-open]')) {
            setSidebar(true);
            return;
        }

        if (event.target.closest('[data-sidebar-close]')) {
            setSidebar(false);
            return;
        }

        if (event.target.closest('.app-sidebar .nav-link') && window.matchMedia('(max-width: 1024px)').matches) {
            setSidebar(false);
        }
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            setSidebar(false);
        }
    });

    /* -------------------------------------------------------- search palette */

    const palette = document.getElementById('searchPalette');

    function setPalette(open) {
        if (!palette) {
            return;
        }

        palette.classList.toggle('show', open);
        palette.setAttribute('aria-hidden', open ? 'false' : 'true');
        document.body.classList.toggle('palette-open', open);

        if (open) {
            window.setTimeout(function () {
                const input = palette.querySelector('input[name="q"]');
                if (input) {
                    input.focus();
                    input.select();
                }
            }, 30);
        }
    }

    document.addEventListener('click', function (event) {
        if (event.target.closest('[data-search-open]')) {
            event.preventDefault();
            setPalette(true);
            return;
        }

        if (event.target.closest('[data-search-close]') || event.target === palette) {
            setPalette(false);
        }
    });

    document.addEventListener('keydown', function (event) {
        if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
            if (!palette) {
                return;
            }

            event.preventDefault();
            setPalette(!palette.classList.contains('show'));
        }

        if (event.key === 'Escape') {
            setPalette(false);
        }
    });

    /* --------------------------------------------------------- view switches */

    document.addEventListener('click', function (event) {
        const windowTrigger = event.target.closest('[data-month-window]');
        if (windowTrigger) {
            const group = windowTrigger.closest('.segmented');
            if (group) {
                group.querySelectorAll('[data-month-window]').forEach(function (button) {
                    button.classList.toggle('active', button === windowTrigger);
                    button.setAttribute('aria-pressed', button === windowTrigger ? 'true' : 'false');
                });
            }

            const columns = document.querySelectorAll('.growth-column[data-month-index]');
            const windowSize = parseInt(windowTrigger.getAttribute('data-month-window'), 10) || 6;
            columns.forEach(function (column, index) {
                column.setAttribute('data-hidden', index < columns.length - windowSize ? 'true' : 'false');
            });
            return;
        }

        const trigger = event.target.closest('[data-view-toggle]');
        if (!trigger) {
            return;
        }

        event.preventDefault();
        const group = trigger.closest('.segmented');
        if (group) {
            group.querySelectorAll('[data-view-toggle]').forEach(function (button) {
                button.classList.toggle('active', button === trigger);
                button.setAttribute('aria-pressed', button === trigger ? 'true' : 'false');
            });
        }

        const targetSelector = trigger.getAttribute('data-view-target');
        const mode = trigger.getAttribute('data-view-mode') || 'list';
        const target = targetSelector ? document.querySelector(targetSelector) : null;

        if (target) {
            target.classList.toggle('is-list', mode === 'list');
        }

        const hideSelector = trigger.getAttribute('data-hide-target');
        if (hideSelector) {
            document.querySelectorAll(hideSelector).forEach(function (element) {
                element.hidden = element.id !== targetSelector.replace('#', '');
            });
        }
    });

    /* --------------------------------------------------------- CSV export */

    document.addEventListener('click', function (event) {
        const trigger = event.target.closest('[data-export-table]');
        if (!trigger) {
            return;
        }

        const table = document.querySelector(trigger.getAttribute('data-export-table'));
        if (!table) {
            return;
        }

        const rows = [];
        table.querySelectorAll('tr').forEach(function (row) {
            const cells = [];
            row.querySelectorAll('th, td').forEach(function (cell) {
                const text = (cell.innerText || '').replace(/\s+/g, ' ').trim().replace(/"/g, '""');
                cells.push('"' + text + '"');
            });
            if (cells.length) {
                rows.push(cells.join(','));
            }
        });

        const blob = new Blob([rows.join('\r\n')], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = (trigger.getAttribute('data-export-name') || 'saleshierarchy-report') + '.csv';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(link.href);
    });
})();
