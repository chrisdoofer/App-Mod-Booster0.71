// site.js — Expense Management custom JavaScript

// ── Auto-dismiss success alerts after 5 seconds ──────────────────────
document.addEventListener('DOMContentLoaded', function () {
    const successAlerts = document.querySelectorAll('.alert-success');
    successAlerts.forEach(function (alert) {
        setTimeout(function () {
            const bsAlert = bootstrap.Alert.getOrCreateInstance(alert);
            if (bsAlert) bsAlert.close();
        }, 5000);
    });
});

// ── Confirm dangerous actions (delete buttons) ────────────────────────
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('[data-confirm]').forEach(function (el) {
        el.addEventListener('click', function (e) {
            if (!confirm(el.dataset.confirm)) {
                e.preventDefault();
            }
        });
    });
});

// ── Highlight active nav link ─────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {
    const currentPath = window.location.pathname.toLowerCase();
    document.querySelectorAll('.navbar .nav-link').forEach(function (link) {
        const href = link.getAttribute('href');
        if (href && currentPath === href.toLowerCase()) {
            link.classList.add('active');
            link.setAttribute('aria-current', 'page');
        }
    });
});
