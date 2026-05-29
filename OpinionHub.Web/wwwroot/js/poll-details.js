function initPollDetails(pollId, labels, initialData) {
    const canvas = document.getElementById('chart');
    let chart = null;

    // Палитра — вписывается в общий стиль сайта
    const palette = [
        '#0d6efd', '#6610f2', '#6f42c1', '#d63384', '#fd7e14',
        '#198754', '#20c997', '#0dcaf0', '#ffc107', '#6c757d'
    ];

    function applyActiveTab(type) {
        const tabBar = document.getElementById('tab-bar');
        const tabPie = document.getElementById('tab-pie');
        if (!tabBar || !tabPie) return;
        tabBar.classList.toggle('active', type === 'bar');
        tabPie.classList.toggle('active', type === 'pie');
    }

    function createChart(type, labelsArr, dataArr) {
        if (!canvas) return null;
        // Уничтожаем предыдущий экземпляр, если есть
        if (chart) {
            try { chart.destroy(); } catch (e) { }
        }

        // Единая конфигурация анимаций. easeOutQuart мягче чем cubic — нет "рывка" в конце.
        // animations.numbers — анимирует числовые значения (длина бара, угол сектора) с тем же темпом.
        const smoothAnimation = {
            animation: {
                duration: 900,
                easing: 'easeOutQuart'
            },
            animations: {
                numbers: { duration: 900, easing: 'easeOutQuart' },
                colors: { duration: 300, easing: 'linear' }
            },
            transitions: {
                active: { animation: { duration: 200 } }
            }
        };

        if (type === 'pie') {
            chart = new Chart(canvas, {
                type: 'pie',
                data: {
                    labels: labelsArr,
                    datasets: [{
                        data: dataArr,
                        backgroundColor: palette.slice(0, labelsArr.length),
                        hoverOffset: 8,
                        borderColor: 'transparent',
                        borderWidth: 0
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    ...smoothAnimation,
                    plugins: { legend: { position: 'bottom' } }
                }
            });
        } else {
            chart = new Chart(canvas, {
                type: 'bar',
                data: {
                    labels: labelsArr,
                    datasets: [{
                        label: 'Голоса',
                        data: dataArr,
                        backgroundColor: palette.slice(0, labelsArr.length),
                        borderRadius: 6
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    ...smoothAnimation,
                    scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
                    plugins: { legend: { display: false } }
                }
            });
        }

        return chart;
    }

    // Чтение выбранного типа из localStorage (по pollId)
    const storageKey = 'oh-chart-type-' + pollId;
    let savedType = null;
    try { savedType = localStorage.getItem(storageKey); } catch { savedType = null; }
    const initialType = savedType || 'bar';
    applyActiveTab(initialType);
    chart = createChart(initialType, labels, initialData);
    let currentChartType = initialType;

    // Обработчики вкладок
    const tabBar = document.getElementById('tab-bar');
    const tabPie = document.getElementById('tab-pie');

    function onTabClick(e) {
        const type = e.currentTarget.getAttribute('data-chart');
        if (type === currentChartType) return; // если клик по уже активной вкладке — ничего не делаем
        currentChartType = type;
        try { localStorage.setItem(storageKey, type); } catch { }
        applyActiveTab(type);
        const currentData = chart && chart.data && chart.data.datasets && chart.data.datasets[0] ? chart.data.datasets[0].data : initialData;
        chart = createChart(type, labels, currentData);
    }

    if (tabBar) tabBar.addEventListener('click', onTabClick);
    if (tabPie) tabPie.addEventListener('click', onTabClick);

    // SignalR подключение
    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/polls')
        .withAutomaticReconnect()
        .build();

    // Экспортируем connectionId наружу — inline-скрипт в Details.cshtml использует
    // его для X-SignalR-Connection-Id-заголовка в fetch'е лайка опроса, чтобы сервер
    // исключил инициатора из broadcast'а через Clients.GroupExcept.
    window.getSignalrConnectionId = function () {
        return connection.connectionId || null;
    };

    // Лайки опроса — broadcast от LikesController после успешного POST/DELETE.
    // Перезаписываем только счётчик; собственное состояние «лайкнул» — локальное
    // у каждого зрителя, не передаётся через SignalR.
    connection.on('pollLikeUpdated', function (data) {
        const countEl = document.getElementById('poll-like-count');
        if (countEl && data && typeof data.count === 'number') {
            countEl.textContent = data.count;
        }
    });

    connection.on('updateStats', function (data) {
        // Обновляем прогресс-бары и текст
        let newChartData = [];
        data.stats.forEach((item) => {
            const textElement = document.getElementById(`stat-text-${item.id}`);
            const barElement = document.getElementById(`stat-bar-${item.id}`);

            if (textElement && barElement) {
                textElement.innerText = `${item.count} ( ${item.percent.toFixed(1)}% )`;
                barElement.style.width = `${item.percent}%`;
            }

            newChartData.push(item.count);
        });

        // Обновляем Chart.js. В v4 chart.update(mode) принимает строку режима, а не объект —
        // настройки анимации читаются из options.animation, которую мы выставили при createChart.
        // Раньше мы передавали объект, который игнорировался, и анимация шла дефолтная (короткая).
        if (chart) {
            chart.data.datasets[0].data = newChartData;
            if (chart.config && chart.config.type === 'pie') {
                chart.data.datasets[0].backgroundColor = palette.slice(0, newChartData.length);
            }
            chart.update();
        }
    });

    connection.start().then(function () {
        connection.invoke('JoinPollGroup', pollId);
        console.log('Подключились к комнате опроса:', pollId);
        // Поднимаем секцию комментариев на том же соединении — отдельный WebSocket
        // для комментов не нужен. initPollComments сам подпишется на 'commentCreated'.
        if (typeof window.initPollComments === 'function') {
            window.initPollComments(pollId, connection);
        }
    }).catch(function (err) {
        console.error('Ошибка SignalR:', err.toString());
        // Если SignalR не поднялся — комменты всё равно должны работать (хотя без real-time).
        if (typeof window.initPollComments === 'function') {
            window.initPollComments(pollId, null);
        }
    });
}
