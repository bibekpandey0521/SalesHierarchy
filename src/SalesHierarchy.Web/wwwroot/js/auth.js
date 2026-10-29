// SalesHierarchy — authentication form interactions.
(function () {
    'use strict';

    document.querySelectorAll('[data-password-toggle]').forEach(function (button) {
        const input = document.getElementById(button.getAttribute('data-password-toggle'));
        if (!input) {
            return;
        }

        button.addEventListener('click', function () {
            const show = input.getAttribute('type') === 'password';
            input.setAttribute('type', show ? 'text' : 'password');
            button.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
            button.setAttribute('aria-pressed', show ? 'true' : 'false');

            const icon = button.querySelector('i');
            if (icon) {
                icon.classList.toggle('bi-eye', !show);
                icon.classList.toggle('bi-eye-slash', show);
            }
        });
    });
})();
