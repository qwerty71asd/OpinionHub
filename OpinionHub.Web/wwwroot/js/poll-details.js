function initPollDetails(pollId, labels, initialData) {
    // 1. Инициализируем график Chart.js
    const ctx = document.getElementById('chart');
    let chart = null;

    if (ctx) {
        chart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Голоса',
                    data: initialData,
                    backgroundColor: '#0d6efd'
                }]
            }
        });
    }

    // 2. Настраиваем ОДНО подключение SignalR
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/polls")
        .withAutomaticReconnect() // Автоматически переподключаться при обрыве
        .build();

    // 3. Слушаем новые данные от сервера
    connection.on("updateStats", function (data) {
        console.log("Получены свежие данные:", data);

        // Массив для новых значений графика
        let newChartData = [];

        // Пробегаемся по новым данным и обновляем текст/полоски
        data.stats.forEach((item, index) => {
            const textElement = document.getElementById(`stat-text-${item.id}`);
            const barElement = document.getElementById(`stat-bar-${item.id}`);

            if (textElement && barElement) {
                // Обновляем текст
                textElement.innerText = `${item.count} ( ${item.percent.toFixed(1)}% )`;

                // Плавно обновляем ширину полоски
                barElement.style.width = `${item.percent}%`;
            }

            // Добавляем свежий голос в массив для графика (порядок совпадает)
            newChartData.push(item.count);
        });

        // 4. Плавно анимируем обновление графика Chart.js
        if (chart) {
            chart.data.datasets[0].data = newChartData;
            chart.update();
        }
    });

    // 5. Запускаем соединение и заходим в "комнату"
    connection.start().then(function () {
        // Вызываем правильное имя метода из C# Хаба
        connection.invoke("JoinPollGroup", pollId);
        console.log("Подключились к комнате опроса:", pollId);
    }).catch(function (err) {
        console.error("Ошибка SignalR:", err.toString());
    });
}