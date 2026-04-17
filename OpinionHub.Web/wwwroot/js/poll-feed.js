const feedConnection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/polls")
    .withAutomaticReconnect()
    .build();

feedConnection.on("ReceiveNewPoll", function (poll) {
    console.log("Новый опрос прилетел!", poll); // Проверим в консоли (F12)

    const container = document.getElementById("poll-feed-container");
    if (!container) return;

    // Скрываем заглушку "Пока нет опросов", если она есть
    const noPollsMsg = document.getElementById("no-polls-msg");
    if (noPollsMsg) noPollsMsg.remove();

    // ВНИМАНИЕ: Сверяем имена полей (id, title, author). 
    // SignalR по умолчанию превращает заглавные буквы C# в маленькие в JS.
    const html = `
        <div class="col-md-6 col-lg-4" id="poll-card-${poll.id}" style="opacity: 0; transform: translateY(20px); transition: all 0.5s ease;">
            <div class="card h-100 shadow-sm border-0 oh-card border-top border-primary border-4">
                <div class="card-body p-4 d-flex flex-column">
                    <h5 class="card-title fw-bold mb-3 text-truncate">${poll.title}</h5>
                    <div class="mt-auto">
                        <div class="d-flex align-items-center mb-3 text-secondary small">
                            <i class="bi bi-person-circle me-2"></i>
                            <span>Автор: ${poll.author}</span>
                        </div>
                        <a href="/Polls/Details/${poll.id}" class="btn btn-outline-primary w-100 fw-semibold">
                            Голосовать
                        </a>
                    </div>
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