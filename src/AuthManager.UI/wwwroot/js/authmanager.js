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
    },

    // ── Passkeys (WebAuthn) ──────────────────────────────────────────────────
    // Uses the WebAuthn JSON serialization methods (PublicKeyCredential.parseCreationOptionsFromJSON,
    // .parseRequestOptionsFromJSON, credential.toJSON()) rather than hand-rolled base64url<->ArrayBuffer
    // conversion — supported in current Chrome, Edge, Safari, and Firefox, and it's what the server's
    // options JSON is already shaped for.
    supportsPasskeys() {
        return typeof PublicKeyCredential !== 'undefined'
            && typeof PublicKeyCredential.parseCreationOptionsFromJSON === 'function';
    },
    async registerPasskey(routePrefix) {
        const optsResp = await fetch(`/${routePrefix}/api/passkeys/creation-options`, { credentials: 'include' });
        if (!optsResp.ok) throw new Error('Could not get passkey creation options.');
        const optionsJson = await optsResp.json();
        const publicKey = PublicKeyCredential.parseCreationOptionsFromJSON(optionsJson);
        const credential = await navigator.credentials.create({ publicKey });

        const regResp = await fetch(`/${routePrefix}/api/passkeys/register`, {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ credentialJson: JSON.stringify(credential.toJSON()) })
        });
        if (!regResp.ok) {
            const err = await regResp.json().catch(() => ({}));
            throw new Error(err.error || 'Passkey registration failed.');
        }
    },
    async loginWithPasskey(routePrefix, username) {
        const url = username
            ? `/${routePrefix}/api/passkeys/login/options?username=${encodeURIComponent(username)}`
            : `/${routePrefix}/api/passkeys/login/options`;
        const optsResp = await fetch(url);
        if (!optsResp.ok) throw new Error('Could not get passkey login options.');
        const optionsJson = await optsResp.json();
        const publicKey = PublicKeyCredential.parseRequestOptionsFromJSON(optionsJson);
        const credential = await navigator.credentials.get({ publicKey });

        const loginResp = await fetch(`/${routePrefix}/api/passkeys/login`, {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ credentialJson: JSON.stringify(credential.toJSON()) })
        });
        const result = await loginResp.json().catch(() => ({}));
        return { ok: loginResp.ok, outcome: result.outcome };
    },
    async listPasskeys(routePrefix) {
        const resp = await fetch(`/${routePrefix}/api/passkeys`, { credentials: 'include' });
        if (!resp.ok) return [];
        return await resp.json();
    },
    async removePasskey(routePrefix, credentialId) {
        const resp = await fetch(`/${routePrefix}/api/passkeys/${encodeURIComponent(credentialId)}`, {
            method: 'DELETE',
            credentials: 'include'
        });
        return resp.ok;
    }
};

// Apply saved theme immediately on load to avoid flash
(function () {
    const theme = localStorage.getItem('am-theme');
    if (theme) document.documentElement.setAttribute('data-theme', theme);
})();
