const feedConnection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/polls")
    .withAutomaticReconnect()
    .build();

feedConnection.on("ReceiveNewPoll", function (poll) {
    if (document.getElementById('poll-card-' + poll.id)) return;

    console.log("Новый опрос прилетел!", poll); // Проверим в консоли (F12)

    const container = document.getElementById("poll-feed-container");
    if (!container) return;

    // Скрываем заглушку "Пока нет опросов", если она есть
    const noPollsMsg = document.getElementById("no-polls-msg");
    if (noPollsMsg) noPollsMsg.remove();

    // ВНИМАНИЕ: Сверяем имена полей (id, title, author).
    // SignalR по умолчанию превращает заглавные буквы C# в маленькие в JS.
    // Корневой контейнер — без bootstrap-grid (col-*), потому что #poll-feed-container
    // использует CSS Columns (.poll-masonry). Классы карточки должны совпадать с
    // серверным шаблоном Home/Index.cshtml, иначе масштаб ломается.
    const safeTitle = String(poll.title ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const safeAuthor = String(poll.author ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const html = `
        <div class="card border-0 shadow-sm oh-card oh-card--compact is-hoverable overflow-hidden"
             id="poll-card-${poll.id}"
             style="opacity: 0; transform: translateY(20px); transition: all 0.5s ease;">
            <div class="card-body d-flex flex-column">
                <h5 class="card-title fw-bold mb-2">${safeTitle}</h5>
                <div class="mt-auto">
                    <div class="d-flex align-items-center mb-2 small text-secondary">
                        <i class="bi bi-person-circle me-2"></i>
                        <span>Автор: ${safeAuthor}</span>
                    </div>
                    <a href="/Polls/Details/${poll.id}" class="btn btn-outline-primary btn-sm w-100 fw-semibold">
                        Голосовать
                    </a>
                </div>
            </div>
        </div>`;

    container.insertAdjacentHTML('afterbegin', html);

    // Плавный вход
    setTimeout(() => {
        const card = document.getElementById(`poll-card-${poll.id}`);
        if (card) {
            card.style.opacity = "1";
            card.style.transform = "translateY(0)";
        }
    }, 50);
});

// Удаление (оставляем как есть, раз оно работает)
feedConnection.on("RemovePoll", function (pollId) {
    const card = document.getElementById(`poll-card-${pollId}`);
    if (card) {
        card.style.opacity = "0";
        card.style.transform = "scale(0.9)";
        setTimeout(() => card.remove(), 500);
    }
});

feedConnection.start().catch(err => console.error(err.toString()));