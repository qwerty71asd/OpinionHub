(function () {
    'use strict';

    const MAX_BYTES = 5 * 1024 * 1024;
    const ACCEPTED_MIME = /^image\/(jpeg|png|webp|gif)$/i;

    // SignalR-id текущего соединения. Заполняется в bindSignalR. Шлётся в каждый POST
    // (коммент/лайк) как X-SignalR-Connection-Id — сервер исключает инициатора из
    // broadcast'а через Clients.GroupExcept, чтобы избежать дублей у отправителя.
    let signalrConnectionId = null;

    function getToken() {
        const el = document.getElementById('af-token');
        return el ? el.value : '';
    }

    function bumpCounter(delta) {
        const el = document.getElementById('comments-total');
        if (!el) return;
        const m = el.textContent.match(/\((\d+)\)/);
        const next = (m ? parseInt(m[1], 10) : 0) + delta;
        el.textContent = '(' + next + ')';
    }

    function removeEmptyPlaceholder() {
        const empty = document.getElementById('comments-empty');
        if (empty) empty.remove();
    }

    function autoGrow(textarea) {
        textarea.style.height = 'auto';
        textarea.style.height = Math.min(textarea.scrollHeight, 144) + 'px'; // 9rem ~= 144px при 16px корне
    }

    function bindAutoGrow(textarea) {
        if (!textarea || textarea.dataset.autogrow === 'on') return;
        textarea.dataset.autogrow = 'on';
        autoGrow(textarea);
        textarea.addEventListener('input', () => autoGrow(textarea));
    }

    // Enter — отправить, Shift+Enter — перенос строки.
    function bindEnterSubmit(textarea, form) {
        if (!textarea || textarea.dataset.entersubmit === 'on') return;
        textarea.dataset.entersubmit = 'on';
        textarea.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) {
                e.preventDefault();
                form.requestSubmit();
            }
        });
    }

    // Унифицированно подсовываем файл в input и триггерим change — единая точка для file picker, paste и DnD.
    function setFile(fileInput, file) {
        if (!fileInput || !file) return;
        try {
            const dt = new DataTransfer();
            dt.items.add(file);
            fileInput.files = dt.files;
        } catch {
            // Safari < 14.1: fallback — не получится записать FileList, но можно сразу обработать как событие.
        }
        fileInput.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function bindAttach(form) {
        if (form.dataset.attachBound === 'on') return;
        form.dataset.attachBound = 'on';

        const fileInput = form.querySelector('.oh-composer-file');
        const attachBtn = form.querySelector('.oh-composer-attach');
        const preview = form.querySelector('.oh-composer-preview');
        if (!fileInput || !attachBtn || !preview) return;

        const previewImg = preview.querySelector('img');
        const previewName = preview.querySelector('.filename');
        const removeBtn = preview.querySelector('.remove-btn');

        attachBtn.addEventListener('click', () => fileInput.click());

        fileInput.addEventListener('change', function () {
            const file = fileInput.files && fileInput.files[0];
            if (!file) {
                hidePreview();
                return;
            }
            if (!ACCEPTED_MIME.test(file.type)) {
                alert('Можно прикрепить только изображение (JPG, PNG, WebP, GIF).');
                fileInput.value = '';
                hidePreview();
                return;
            }
            if (file.size > MAX_BYTES) {
                alert('Изображение должно быть не больше 5 МБ.');
                fileInput.value = '';
                hidePreview();
                return;
            }
            const reader = new FileReader();
            reader.onload = e => { previewImg.src = e.target.result; };
            reader.readAsDataURL(file);
            previewName.textContent = file.name;
            preview.classList.remove('d-none');
            attachBtn.classList.add('has-file');
        });

        removeBtn.addEventListener('click', function () {
            fileInput.value = '';
            hidePreview();
        });

        function hidePreview() {
            preview.classList.add('d-none');
            previewImg.removeAttribute('src');
            previewName.textContent = '';
            attachBtn.classList.remove('has-file');
        }
    }

    function bindPaste(form) {
        if (form.dataset.pasteBound === 'on') return;
        form.dataset.pasteBound = 'on';

        const textarea = form.querySelector('.oh-composer-input');
        const fileInput = form.querySelector('.oh-composer-file');
        if (!textarea || !fileInput) return;

        textarea.addEventListener('paste', function (e) {
            const items = (e.clipboardData && e.clipboardData.items) || [];
            for (const it of items) {
                if (it.kind === 'file' && /^image\//.test(it.type)) {
                    const file = it.getAsFile();
                    if (!file) continue;
                    e.preventDefault();
                    // Дадим файлу адекватное имя — у paste-blob его обычно нет (image.png).
                    const named = file.name ? file : new File([file], 'pasted-' + Date.now() + '.png', { type: file.type });
                    setFile(fileInput, named);
                    return;
                }
            }
        });
    }

    function bindDnD(form) {
        if (form.dataset.dndBound === 'on') return;
        form.dataset.dndBound = 'on';

        const pill = form.querySelector('.oh-composer-pill');
        const fileInput = form.querySelector('.oh-composer-file');
        if (!pill || !fileInput) return;

        const stop = (e) => { e.preventDefault(); e.stopPropagation(); };
        ['dragenter', 'dragover'].forEach(t => {
            pill.addEventListener(t, function (e) {
                if (!e.dataTransfer || !Array.from(e.dataTransfer.types || []).includes('Files')) return;
                stop(e);
                pill.classList.add('is-dragover');
            });
        });
        ['dragleave', 'dragend'].forEach(t => {
            pill.addEventListener(t, () => pill.classList.remove('is-dragover'));
        });
        pill.addEventListener('drop', function (e) {
            if (!e.dataTransfer || !e.dataTransfer.files || !e.dataTransfer.files.length) return;
            stop(e);
            pill.classList.remove('is-dragover');
            setFile(fileInput, e.dataTransfer.files[0]);
        });
    }

    function bindComposer(form) {
        const textarea = form.querySelector('.oh-composer-input');
        if (textarea) {
            bindAutoGrow(textarea);
            bindEnterSubmit(textarea, form);
        }
        bindAttach(form);
        bindPaste(form);
        bindDnD(form);
    }

    // Lightbox: одна модалка на всю секцию комментариев.
    let lightboxApi = null;
    function ensureLightbox() {
        if (lightboxApi) return lightboxApi;
        const modalEl = document.getElementById('comment-image-modal');
        if (!modalEl || typeof bootstrap === 'undefined') return null;
        const imgEl = modalEl.querySelector('img');
        const modal = new bootstrap.Modal(modalEl);
        modalEl.addEventListener('hidden.bs.modal', () => { imgEl.removeAttribute('src'); });
        lightboxApi = {
            show(src, alt) {
                imgEl.src = src;
                imgEl.alt = alt || '';
                modal.show();
            }
        };
        return lightboxApi;
    }

    function bindLightbox(scope) {
        scope.addEventListener('click', function (e) {
            // Клик и по уже сохранённым картинкам в комментах, и по превью в композере.
            const img = e.target.closest('.comment-image, .oh-composer-preview img');
            if (!img) return;
            const lb = ensureLightbox();
            if (!lb || !img.src) return;
            lb.show(img.src, img.alt);
        });
    }

    async function postComment(pollId, text, parentCommentId, file) {
        const fd = new FormData();
        fd.append('text', text);
        if (parentCommentId) fd.append('parentCommentId', parentCommentId);
        if (file) fd.append('image', file);

        const resp = await fetch('/api/polls/' + encodeURIComponent(pollId) + '/comments', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': getToken(),
                'X-SignalR-Connection-Id': signalrConnectionId || '',
                'Accept': 'text/html'
            },
            credentials: 'same-origin',
            body: fd
        });
        if (!resp.ok) {
            let message = 'HTTP ' + resp.status;
            try {
                const ct = (resp.headers.get('Content-Type') || '').toLowerCase();
                if (ct.includes('application/json')) {
                    const j = await resp.json();
                    if (j && j.message) message = j.message;
                } else if (ct.includes('text/html')) {
                    // Сервер вернул HTML-страницу ошибки (DeveloperExceptionPage и т.п.) —
                    // в alert ей не место. Текст уходит в консоль, юзеру — короткое сообщение.
                    const html = await resp.text();
                    console.error('Server returned HTML error page:', html);
                    if (resp.status === 401) {
                        message = 'Сессия истекла. Перезагрузите страницу и войдите снова.';
                    } else if (resp.status >= 500) {
                        message = 'Ошибка сервера (' + resp.status + '). Похоже, сервер работает с устаревшей версией кода — попробуйте перезапустить приложение или перезагрузить страницу (Ctrl+F5).';
                    } else {
                        message = 'Ошибка ' + resp.status + '. Подробности в консоли.';
                    }
                } else {
                    const t = await resp.text();
                    if (t) message = t.substring(0, 300);
                }
            } catch (parseErr) {
                console.error('Failed to parse error response', parseErr);
            }
            throw new Error(message);
        }
        return await resp.text();
    }

    function resetComposer(form) {
        const textarea = form.querySelector('.oh-composer-input');
        const fileInput = form.querySelector('.oh-composer-file');
        const preview = form.querySelector('.oh-composer-preview');
        const attachBtn = form.querySelector('.oh-composer-attach');
        if (textarea) { textarea.value = ''; autoGrow(textarea); }
        if (fileInput) fileInput.value = '';
        if (preview) {
            preview.classList.add('d-none');
            const img = preview.querySelector('img');
            const name = preview.querySelector('.filename');
            if (img) img.removeAttribute('src');
            if (name) name.textContent = '';
        }
        if (attachBtn) attachBtn.classList.remove('has-file');
    }

    function buildPayload(form) {
        const textarea = form.querySelector('.oh-composer-input');
        const fileInput = form.querySelector('.oh-composer-file');
        return {
            text: (textarea?.value || '').trim(),
            file: (fileInput && fileInput.files && fileInput.files[0]) || null,
        };
    }

    // Русская плюрализация для счётчика «N ответов» на кнопке раскрытия.
    function pluralize(n) {
        const mod10 = n % 10, mod100 = n % 100;
        if (mod10 === 1 && mod100 !== 11) return 'ответ';
        if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return 'ответа';
        return 'ответов';
    }

    // Управляет видимостью и текстом ДВУХ кнопок секции: «Показать ещё N» (toggle)
    // и «Скрыть» (collapse). data-shown/data-total хранятся на toggle.
    function updateToggleLabel(section, shown, total) {
        const showBtn = section.querySelector(':scope > .comment-replies-actions > .comment-replies-toggle');
        const hideBtn = section.querySelector(':scope > .comment-replies-actions > .comment-replies-collapse');
        if (!showBtn || !hideBtn) return;
        showBtn.dataset.shown = String(shown);
        showBtn.dataset.total = String(total);

        if (total === 0) {
            showBtn.classList.add('d-none');
            hideBtn.classList.add('d-none');
            return;
        }
        if (shown === 0) {
            showBtn.classList.remove('d-none');
            showBtn.querySelector('.label').textContent = 'Показать ' + total + ' ' + pluralize(total);
            hideBtn.classList.add('d-none');
        } else if (shown >= total) {
            showBtn.classList.add('d-none');
            hideBtn.classList.remove('d-none');
        } else {
            showBtn.classList.remove('d-none');
            const left = total - shown;
            showBtn.querySelector('.label').textContent = 'Показать ещё ' + left + ' ' + pluralize(left);
            hideBtn.classList.remove('d-none');
        }
    }

    function collapseReplies(section) {
        const list = section.querySelector(':scope > .replies-list');
        const items = list ? list.querySelectorAll(':scope > .reply-item') : [];
        const total = items.length;
        items.forEach(el => {
            el.classList.add('d-none');
            el.classList.remove('reply-enter');
            el.style.animationDelay = '';
        });
        updateToggleLabel(section, 0, total);
    }

    function expandNextBatch(section) {
        const list = section.querySelector(':scope > .replies-list');
        const items = list ? list.querySelectorAll(':scope > .reply-item') : [];
        const showBtn = section.querySelector(':scope > .comment-replies-actions > .comment-replies-toggle');
        const batch = parseInt(section.dataset.batch, 10) || 3;
        let shown = parseInt(showBtn?.dataset.shown, 10) || 0;
        const total = items.length;
        if (shown >= total) return;

        const next = Math.min(batch, total - shown);
        for (let i = shown; i < shown + next; i++) {
            const el = items[i];
            el.classList.remove('d-none');
            el.classList.remove('reply-enter');
            void el.offsetWidth;
            el.style.animationDelay = ((i - shown) * 80) + 'ms';
            el.classList.add('reply-enter');
        }
        shown += next;
        updateToggleLabel(section, shown, total);
    }

    // TikTok-стиль батч-раскрытие: «Показать ещё N» раскрывает следующие 3,
    // «Скрыть» сворачивает всё. Обе кнопки делегированы по корню списка.
    function bindRepliesToggle(root) {
        root.addEventListener('click', function (e) {
            const showBtn = e.target.closest('.comment-replies-toggle');
            const hideBtn = e.target.closest('.comment-replies-collapse');
            if (!showBtn && !hideBtn) return;
            const section = (showBtn || hideBtn).closest('.comment-replies');
            if (!section) return;
            if (showBtn) {
                expandNextBatch(section);
            } else {
                collapseReplies(section);
            }
        });
    }

    function bindLifecycle(root) {
        // Лайки комментариев (делегирование).
        root.addEventListener('click', async function (e) {
            const btn = e.target.closest('.comment-like-btn');
            if (!btn) return;

            const id = btn.dataset.commentId;
            const liked = btn.dataset.liked === 'true';
            btn.disabled = true;
            try {
                const resp = await fetch('/api/likes/comments/' + encodeURIComponent(id), {
                    method: liked ? 'DELETE' : 'POST',
                    headers: {
                        'RequestVerificationToken': getToken(),
                        'X-SignalR-Connection-Id': signalrConnectionId || '',
                        'Accept': 'application/json'
                    },
                    credentials: 'same-origin'
                });
                if (!resp.ok) { console.error('Like error', resp.status); return; }
                const data = await resp.json();
                btn.dataset.liked = data.liked ? 'true' : 'false';
                btn.setAttribute('aria-pressed', data.liked ? 'true' : 'false');
                btn.querySelector('.comment-like-count').textContent = data.count;
                const icon = btn.querySelector('i');
                if (data.liked) {
                    btn.classList.remove('btn-outline-danger');
                    btn.classList.add('btn-danger');
                    icon.classList.remove('bi-heart');
                    icon.classList.add('bi-heart-fill');
                } else {
                    btn.classList.remove('btn-danger');
                    btn.classList.add('btn-outline-danger');
                    icon.classList.remove('bi-heart-fill');
                    icon.classList.add('bi-heart');
                }
            } catch (err) {
                console.error('Like network error', err);
            } finally {
                btn.disabled = false;
            }
        });

        // Тоггл формы ответа + биндинг её композера при первом раскрытии.
        // Если кликали на reply-item (а не на root), пред-заполняем textarea «@username »
        // — UX как в TikTok, чтобы видно было кому отвечают.
        root.addEventListener('click', function (e) {
            const btn = e.target.closest('.comment-reply-toggle');
            if (!btn) return;
            const wrap = document.getElementById('reply-form-' + btn.dataset.commentId);
            if (!wrap) return;
            const willOpen = wrap.classList.contains('d-none');
            wrap.classList.toggle('d-none');
            const form = wrap.querySelector('form.comment-reply-form');
            if (form) bindComposer(form);
            if (willOpen) {
                const ta = wrap.querySelector('.oh-composer-input');
                if (ta) {
                    // Prefill только если форма пустая И мы отвечаем на reply (не на root).
                    const isReply = btn.closest('.reply-item') !== null;
                    const targetUser = btn.dataset.authorUsername;
                    if (isReply && targetUser && !ta.value.trim()) {
                        ta.value = '@' + targetUser + ' ';
                    }
                    ta.focus();
                    const len = ta.value.length;
                    try { ta.setSelectionRange(len, len); } catch { /* ignore */ }
                }
            }
        });

        // Сабмит ответа. Независимо от того, на какой уровень отвечаем (root или
        // reply на reply), сервер возвращает HTML одиночного .reply-item, а мы
        // вставляем его в .replies-list корня треда. Корень — ближайший .comment-node
        // от формы; .reply-item не вложен в .comment-node, поэтому closest всегда даёт root.
        root.addEventListener('submit', async function (e) {
            const form = e.target.closest('.comment-reply-form');
            if (!form) return;
            e.preventDefault();

            const pollId = root.dataset.pollId;
            const parentId = form.dataset.parentId;
            const { text, file } = buildPayload(form);
            if (!text && !file) return;

            const submitBtn = form.querySelector('button[type="submit"]');
            if (submitBtn) submitBtn.disabled = true;
            try {
                const html = await postComment(pollId, text, parentId, file);
                const rootNode = form.closest('.comment-node');
                const section = rootNode?.querySelector(':scope > .comment-replies');
                const list = section?.querySelector(':scope > .replies-list');
                if (list) {
                    list.insertAdjacentHTML('beforeend', html);
                    // Свежевставленный reply сразу показываем — пользователь только что его
                    // отправил, прятать его за «Показать ещё» было бы дико.
                    const newItem = list.lastElementChild;
                    if (newItem && newItem.classList.contains('reply-item')) {
                        newItem.classList.remove('d-none');
                        void newItem.offsetWidth;
                        newItem.classList.add('reply-enter');
                    }
                    // Обновляем счётчики на обеих кнопках: total+1, shown+1 (новый виден).
                    const toggleBtn = section.querySelector(':scope > .comment-replies-actions > .comment-replies-toggle');
                    if (toggleBtn) {
                        const total = (parseInt(toggleBtn.dataset.total, 10) || 0) + 1;
                        const shown = (parseInt(toggleBtn.dataset.shown, 10) || 0) + 1;
                        updateToggleLabel(section, shown, total);
                    }
                }
                resetComposer(form);
                form.closest('.comment-reply-form-wrap')?.classList.add('d-none');
                bumpCounter(1);
            } catch (err) {
                console.error('Reply error', err);
                alert('Не удалось отправить ответ: ' + err.message);
            } finally {
                if (submitBtn) submitBtn.disabled = false;
            }
        });
    }

    // Root-композер свёрнут на загрузке: видна только аватарка + строка-плейсхолдер.
    // Фокус разворачивает, blur пустого без прикреплённого превью — сворачивает обратно.
    // Reply-формы отсекаются гардом attachScope !== 'root'.
    function bindCollapse(form) {
        if (form.dataset.attachScope !== 'root') return;
        const textarea = form.querySelector('.oh-composer-input');
        const preview = form.querySelector('.oh-composer-preview');
        const pill = form.querySelector('.oh-composer-pill');
        const attachBtn = form.querySelector('.oh-composer-attach');

        if (!textarea) return;

        // Разворачиваем при фокусе
        textarea.addEventListener('focus', () => form.classList.remove('is-collapsed'));

        textarea.addEventListener('blur', (e) => {
            // Если фокус перешел на другой элемент внутри этой же формы (например, на кнопку отправки)
            if (e.relatedTarget && form.contains(e.relatedTarget)) return;

            // Задержка решает проблемы с гонкой событий и системными диалогами
            setTimeout(() => {
                const hasText = textarea.value.trim().length > 0;
                const hasPreview = preview && !preview.classList.contains('d-none');
                const isAttaching = form.dataset.isAttaching === 'true';

                // Сворачиваем форму только если она пустая и мы не находимся в процессе прикрепления файла
                if (!hasText && !hasPreview && !isAttaching && !form.contains(document.activeElement)) {
                    form.classList.add('is-collapsed');
                }
            }, 150);
        });

        // Улучшение UX: клик по пустой области "пилюли" фокусирует текстовое поле, сохраняя обводку
        if (pill) {
            pill.addEventListener('mousedown', (e) => {
                if (e.target === pill) {
                    e.preventDefault(); // Предотвращаем потерю фокуса
                    textarea.focus();
                }
            });
        }

        // Фикс проблемы со скрепкой: предотвращаем blur при клике
        if (attachBtn) {
            attachBtn.addEventListener('mousedown', (e) => {
                e.preventDefault(); // Поле ввода не теряет фокус (сохраняется CSS-выделение)
                form.dataset.isAttaching = 'true'; // Устанавливаем флаг, что сейчас откроется системное окно
            });
        }

        // Возврат из диалогового окна ОС (выбрали файл или нажали Отмена)
        window.addEventListener('focus', () => {
            if (form.dataset.isAttaching === 'true') {
                setTimeout(() => {
                    delete form.dataset.isAttaching;
                    const hasText = textarea.value.trim().length > 0;
                    const hasPreview = preview && !preview.classList.contains('d-none');
                    // Если пользователь ничего не выбрал и поле пустое — сворачиваем
                    if (!hasText && !hasPreview && !form.contains(document.activeElement)) {
                        form.classList.add('is-collapsed');
                    }
                }, 300); // Даем время на срабатывание события change у input[type="file"]
            }
        });
    }

    function bindRootForm(form, pollId, list) {
        bindComposer(form);
        bindCollapse(form);
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const { text, file } = buildPayload(form);
            if (!text && !file) return;

            const submitBtn = form.querySelector('button[type="submit"]');
            if (submitBtn) submitBtn.disabled = true;
            try {
                const html = await postComment(pollId, text, null, file);
                removeEmptyPlaceholder();
                list.insertAdjacentHTML('afterbegin', html);
                resetComposer(form);
                bumpCounter(1);
            } catch (err) {
                console.error('Comment error', err);
                alert('Не удалось отправить комментарий: ' + err.message);
            } finally {
                if (submitBtn) submitBtn.disabled = false;
            }
        });
    }

    // SignalR-handler: вешается на переданное соединение из poll-details.js. Чужие
    // комменты прилетают через 'commentCreated' и вставляются в DOM. Свои комменты
    // отфильтровываются на сервере через GroupExcept по X-SignalR-Connection-Id;
    // дедупликация по data-comment-id остаётся как fallback (если SignalR ещё не
    // подключился к моменту POST'а или клиент со старым JS).
    function bindSignalR(connection, pollId, list) {
        if (!connection) return;

        // Запоминаем connection id для отправки в HTTP-заголовке. На реконнект id
        // меняется — обновляем.
        signalrConnectionId = connection.connectionId || null;
        if (typeof connection.onreconnected === 'function') {
            connection.onreconnected(function (id) { signalrConnectionId = id || null; });
        }
        if (typeof connection.onclose === 'function') {
            connection.onclose(function () { signalrConnectionId = null; });
        }

        // Лайки комментариев — broadcast от LikesController. Перезаписываем только
        // счётчик у нужной кнопки, состояние «лайкнул» — локальное у каждого зрителя.
        connection.on('commentLikeUpdated', function (data) {
            if (!data || !data.commentId) return;
            const btn = list.querySelector('.comment-like-btn[data-comment-id="' + data.commentId + '"]');
            if (!btn) return;
            const countSpan = btn.querySelector('.comment-like-count');
            if (countSpan && typeof data.count === 'number') {
                countSpan.textContent = data.count;
            }
        });

        connection.on('commentCreated', function (payload) {
            if (!payload || !payload.commentId) return;
            // Уже на странице (свой коммент через AJAX или старый из изначального HTML).
            if (document.querySelector('[data-comment-id="' + payload.commentId + '"]')) return;

            if (payload.isReply) {
                const rootNode = list.querySelector('.comment-node[data-comment-id="' + payload.rootCommentId + '"]');
                if (!rootNode) return; // root скрыт фильтром или не в DOM
                const section = rootNode.querySelector(':scope > .comment-replies');
                const replyList = section?.querySelector(':scope > .replies-list');
                if (!replyList) return;
                replyList.insertAdjacentHTML('beforeend', payload.html);
                // Чужой ответ не раскрываем — только увеличиваем total на toggle-кнопке.
                // Читатель сам нажмёт «Показать ещё», когда захочет.
                const showBtn = section.querySelector(':scope > .comment-replies-actions > .comment-replies-toggle');
                const total = (parseInt(showBtn?.dataset.total, 10) || 0) + 1;
                const shown = parseInt(showBtn?.dataset.shown, 10) || 0;
                updateToggleLabel(section, shown, total);
            } else {
                removeEmptyPlaceholder();
                list.insertAdjacentHTML('afterbegin', payload.html);
            }
            bumpCounter(1);
        });
    }

    window.initPollComments = function (pollId, connection) {
        const list = document.getElementById('comments-list');
        if (!list) return;
        list.dataset.pollId = pollId;

        const rootForm = document.getElementById('comment-root-form');
        if (rootForm) bindRootForm(rootForm, pollId, list);

        bindLifecycle(list);
        bindRepliesToggle(list);
        bindSignalR(connection, pollId, list);

        // Делегирование для lightbox — одно на всю секцию комментариев (вместе с превью корневой формы).
        const section = list.closest('.card-body') || document.body;
        bindLightbox(section);
    };
})();
