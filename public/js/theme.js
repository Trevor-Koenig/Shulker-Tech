(function () {
    const STORAGE_KEY = 'shulker-theme';
    const root = document.documentElement;
    const saved = localStorage.getItem(STORAGE_KEY);
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;

    const initial = saved ?? (prefersDark ? 'dark' : 'light');
    root.setAttribute('data-theme', initial);

    document.addEventListener('DOMContentLoaded', function () {
        const btn = document.getElementById('theme-toggle');
        if (!btn) return;

        updateIcon(btn, root.getAttribute('data-theme'));

        btn.addEventListener('click', function () {
            const current = root.getAttribute('data-theme');
            const next = current === 'dark' ? 'light' : 'dark';
            root.setAttribute('data-theme', next);
            localStorage.setItem(STORAGE_KEY, next);
            updateIcon(btn, next);
        });
    });

    function updateIcon(btn, theme) {
        btn.textContent = theme === 'dark' ? '☀' : '☾';
        btn.setAttribute('aria-label', theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme');
    }
}());
