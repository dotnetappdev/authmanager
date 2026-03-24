// DotNetAuthManager — Client-side utilities
window.authManager = {
    getTheme() {
        return localStorage.getItem('am-theme');
    },
    setTheme(theme) {
        localStorage.setItem('am-theme', theme);
        document.documentElement.setAttribute('data-theme', theme);
    },
    prefersDarkMode() {
        return window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
    },
    async copyToClipboard(text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            // Fallback
            const el = document.createElement('textarea');
            el.value = text;
            el.style.position = 'fixed';
            el.style.opacity = '0';
            document.body.appendChild(el);
            el.select();
            document.execCommand('copy');
            document.body.removeChild(el);
            return true;
        }
    },
    scrollToBottom(elementId) {
        const el = document.getElementById(elementId);
        if (el) el.scrollTop = el.scrollHeight;
    }
};

// Apply saved theme immediately on load to avoid flash
(function () {
    const theme = localStorage.getItem('am-theme');
    if (theme) document.documentElement.setAttribute('data-theme', theme);
})();
