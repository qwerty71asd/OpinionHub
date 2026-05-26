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
                    animation: { duration: 600, easing: 'easeOutCubic' },
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
                    animation: { duration: 600, easing: 'easeOutCubic' },
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

        // Обновляем Chart.js с анимацией
        if (chart) {
            chart.data.datasets[0].data = newChartData;
            if (chart.config && chart.config.type === 'pie') {
                chart.data.datasets[0].backgroundColor = palette.slice(0, newChartData.length);
            }
            chart.update({ duration: 600, easing: 'easeOutCubic' });
        }
    });

    connection.start().then(function () {
        connection.invoke('JoinPollGroup', pollId);
        console.log('Подключились к комнате опроса:', pollId);
    }).catch(function (err) {
        console.error('Ошибка SignalR:', err.toString());
    });
}
