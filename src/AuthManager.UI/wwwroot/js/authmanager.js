// DotNetAuthManager — Client-side utilities
window.authManager = {
    getTheme() {
        return localStorage.getItem('am-theme');
    },
    setTheme(theme) {
        localStorage.setItem('am-theme', theme);
        document.documentElement.setAttribute('data-theme', theme);
    },
    clearTheme() {
        localStorage.removeItem('am-theme');
        document.documentElement.removeAttribute('data-theme');
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
    },
    downloadFile(fileName, base64Content, mimeType) {
        const link = document.createElement('a');
        link.href = `data:${mimeType};base64,${base64Content}`;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }
};

// Apply saved theme immediately on load to avoid flash
(function () {
    const theme = localStorage.getItem('am-theme');
    if (theme) document.documentElement.setAttribute('data-theme', theme);
})();
