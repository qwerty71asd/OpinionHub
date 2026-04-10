(function () {
  const btn = document.getElementById('themeToggle');
  if (!btn) return;

  function getTheme() {
    return document.documentElement.getAttribute('data-bs-theme') || 'light';
  }

  function setTheme(theme) {
    document.documentElement.setAttribute('data-bs-theme', theme);
    try { localStorage.setItem('oh-theme', theme); } catch { }
    const icon = btn.querySelector('i');
    if (icon) {
      icon.className = theme === 'dark' ? 'bi bi-sun' : 'bi bi-moon-stars';
    }
  }

  // set initial icon
  setTheme(getTheme());

  btn.addEventListener('click', () => {
    const next = getTheme() === 'dark' ? 'light' : 'dark';
    setTheme(next);
  });
})();
