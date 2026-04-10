function initPollDetails(pollId, labels, data) {
  const ctx = document.getElementById('chart');
  if (!ctx) return;

  const chart = new Chart(ctx, {
    type: 'bar',
    data: {
      labels,
      datasets: [{
        label: 'Голоса',
        data,
        backgroundColor: '#0d6efd'
      }]
    }
  });

  const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/polls').build();
  connection.start().then(() => connection.invoke('JoinPoll', pollId));
  connection.on('pollUpdated', () => window.location.reload());

  return chart;
}
