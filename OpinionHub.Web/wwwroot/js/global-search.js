/*
 * Global search overlay. Открывается по клику на #globalSearchBtn или Ctrl/⌘+K.
 * Использует /api/search/suggest для live-подсказок (debounce 200ms + AbortController).
 * Рендерит всё через DOM API (textContent), без innerHTML с user-content — защита от XSS.
 */
(function () {
    const btn = document.getElementById('globalSearchBtn');
    const overlay = document.getElementById('ohSearchOverlay');
    if (!btn || !overlay) return;

    const input = overlay.querySelector('#ohSearchInput');
    const results = overlay.querySelector('#ohSearchResults');
    const footer = overlay.querySelector('#ohSearchFooter');
    const sectionPolls = overlay.querySelector('#ohSearchPolls');
    const sectionUsers = overlay.querySelector('#ohSearchUsers');
    const groupPolls = overlay.querySelector('#ohSearchPollsGroup');
    const groupUsers = overlay.querySelector('#ohSearchUsersGroup');
    const countPolls = overlay.querySelector('#ohSearchPollsCount');
    const countUsers = overlay.querySelector('#ohSearchUsersCount');

    let abortCtrl = null;
    let debounceTimer = null;
    let lastQuery = '';
    let activeIdx = -1;
    let savedOverflow = '';

    function setState(state) { results.dataset.state = state; }

    function open() {
        if (!overlay.hidden) return;
        overlay.hidden = false;
        savedOverflow = document.body.style.overflow;
        document.body.style.overflow = 'hidden';
        // Фокус — после отрисовки, иначе курсор не появится.
        requestAnimationFrame(() => input.focus());
    }

    function close() {
        if (overlay.hidden) return;
        overlay.hidden = true;
        document.body.style.overflow = savedOverflow;
        abortCtrl?.abort();
        clearTimeout(debounceTimer);
    }

    function clearList(el) {
        while (el.firstChild) el.removeChild(el.firstChild);
    }

    function updateFooter(q) {
        footer.href = '/Search?q=' + encodeURIComponent(q);
    }

    function buildItem({ href, title, subtitle, iconImage, iconClass }) {
        const a = document.createElement('a');
        a.href = href;
        a.className = 'oh-search-item';

        const icon = document.createElement('div');
        icon.className = 'oh-search-item__icon';
        if (iconImage) {
            const img = document.createElement('img');
            img.src = iconImage;
            img.alt = '';
            img.loading = 'lazy';
            icon.appendChild(img);
        } else {
            const i = document.createElement('i');
            i.className = 'bi ' + (iconClass || 'bi-search');
            icon.appendChild(i);
        }
        a.appendChild(icon);

        const body = document.createElement('div');
        body.className = 'oh-search-item__body';
        const t = document.createElement('div');
        t.className = 'oh-search-item__title';
        t.textContent = title;
        body.appendChild(t);
        if (subtitle) {
            const s = document.createElement('div');
            s.className = 'oh-search-item__subtitle';
            s.textContent = subtitle;
            body.appendChild(s);
        }
        a.appendChild(body);
        return a;
    }

    function pollAuthorLabel(p) {
        if (p.isAnonymousAuthor) return 'Аноним';
        if (p.authorIsDeleted) return 'Удалённый аккаунт';
        return p.authorUserName ? '@' + p.authorUserName : '';
    }

    function truncate(str, max) {
        if (!str) return '';
        return str.length > max ? str.substring(0, max) + '…' : str;
    }

    function renderResults(data) {
        clearList(groupPolls);
        clearList(groupUsers);

        const polls = data?.polls || [];
        const users = data?.users || [];

        if (!polls.length && !users.length) {
            sectionPolls.hidden = true;
            sectionUsers.hidden = true;
            setState('no-results');
            return;
        }

        if (polls.length) {
            sectionPolls.hidden = false;
            countPolls.textContent = polls.length;
            polls.forEach(p => {
                groupPolls.appendChild(buildItem({
                    href: '/Polls/Details/' + encodeURIComponent(p.id),
                    title: p.title,
                    subtitle: pollAuthorLabel(p),
                    iconImage: p.coverImagePath || null,
                    iconClass: 'bi-bar-chart'
                }));
            });
        } else {
            sectionPolls.hidden = true;
        }

        if (users.length) {
            sectionUsers.hidden = false;
            countUsers.textContent = users.length;
            users.forEach(u => {
                groupUsers.appendChild(buildItem({
                    href: '/Profile/' + encodeURIComponent(u.userName),
                    title: '@' + u.userName,
                    subtitle: truncate(u.bio, 80),
                    iconImage: u.avatarPath || null,
                    iconClass: 'bi-person'
                }));
            });
        } else {
            sectionUsers.hidden = true;
        }

        setState('results');
        activeIdx = -1;
    }

    async function runSearch(q) {
        updateFooter(q);
        if (q.length < 2) {
            setState('empty');
            return;
        }
        setState('loading');
        try {
            abortCtrl?.abort();
            abortCtrl = new AbortController();
            const resp = await fetch('/api/search/suggest?q=' + encodeURIComponent(q), { signal: abortCtrl.signal });
            if (!resp.ok) { setState('no-results'); return; }
            const data = await resp.json();
            renderResults(data);
        } catch (e) {
            if (e.name !== 'AbortError') setState('no-results');
        }
    }

    function getItems() {
        return Array.from(overlay.querySelectorAll('.oh-search-item'));
    }

    function setActive(idx) {
        const items = getItems();
        items.forEach(el => el.classList.remove('is-active'));
        if (items.length === 0) { activeIdx = -1; return; }
        if (idx < 0) idx = items.length - 1;
        if (idx >= items.length) idx = 0;
        activeIdx = idx;
        items[idx].classList.add('is-active');
        items[idx].scrollIntoView({ block: 'nearest' });
    }

    // --- Event wiring ---

    btn.addEventListener('click', open);

    document.addEventListener('keydown', (e) => {
        // Ctrl+K (Win/Linux) и ⌘+K (macOS) — оба открывают.
        const isCmdK = (e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey
            && (e.key === 'k' || e.key === 'K');
        if (isCmdK) {
            e.preventDefault();
            open();
            return;
        }
        if (e.key === 'Escape' && !overlay.hidden) {
            e.preventDefault();
            close();
        }
    });

    overlay.addEventListener('mousedown', (e) => {
        // Клик мимо модала (по backdrop) — закрытие.
        if (e.target === overlay) close();
    });

    input.addEventListener('input', () => {
        const q = (input.value || '').trim();
        lastQuery = q;
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => runSearch(q), 200);
    });

    input.addEventListener('keydown', (e) => {
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            setActive(activeIdx + 1);
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            setActive(activeIdx - 1);
        } else if (e.key === 'Enter') {
            const items = getItems();
            if (activeIdx >= 0 && items[activeIdx]) {
                window.location.href = items[activeIdx].href;
            } else if (lastQuery.length >= 2) {
                window.location.href = '/Search?q=' + encodeURIComponent(lastQuery);
            }
        }
    });
})();
