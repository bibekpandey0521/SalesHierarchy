// Site-wide interaction helpers.

(function () {
    const sidebar = document.getElementById('appSidebar');
    const backdrop = document.getElementById('sidebarBackdrop');

    if (!sidebar || !backdrop) {
        return;
    }

    function setSidebar(open) {
        sidebar.classList.toggle('show', open);
        backdrop.classList.toggle('show', open);
        document.body.classList.toggle('sidebar-open', open);

        const trigger = document.querySelector('[data-sidebar-open]');
        if (trigger) {
            trigger.setAttribute('aria-expanded', open ? 'true' : 'false');
        }
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

        // Navigating on a small screen should dismiss the off-canvas menu.
        if (event.target.closest('.app-sidebar .nav-link') && window.matchMedia('(max-width: 992px)').matches) {
            setSidebar(false);
        }
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            setSidebar(false);
        }
    });
})();
