window.estiblazor = {
    focus(id) { document.getElementById(id)?.focus(); },
    toggleTheme() { const next = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark'; document.documentElement.dataset.theme = next; try { localStorage.setItem('estiblazor.theme', next); } catch {} },
    async copy(text) { try { await navigator.clipboard.writeText(text); return true; } catch { return false; } }
};
try { document.documentElement.dataset.theme = localStorage.getItem('estiblazor.theme') || 'dark'; } catch {}
