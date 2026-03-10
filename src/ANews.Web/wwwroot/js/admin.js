/**
 * AgenteNews Admin JS
 * - Chart.js cost charts
 * - Admin helpers
 */

// Auto-scroll log feed to bottom
window.adminScrollToBottom = function(elementId) {
    const el = document.getElementById(elementId);
    if (el) {
        el.scrollTop = el.scrollHeight;
    }
};

// Render cost chart
window.adminRenderCostChart = function(providerCosts) {
    const canvas = document.getElementById('costChart');
    if (!canvas || typeof Chart === 'undefined') return;

    // Destroy previous instance
    const existing = Chart.getChart(canvas);
    if (existing) existing.destroy();

    if (!providerCosts || providerCosts.length === 0) return;

    new Chart(canvas, {
        type: 'doughnut',
        data: {
            labels: providerCosts.map(p => p.name),
            datasets: [{
                data: providerCosts.map(p => parseFloat(p.cost) || 0),
                backgroundColor: providerCosts.map(p => p.color + 'cc'),
                borderColor: providerCosts.map(p => p.color),
                borderWidth: 2
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    callbacks: {
                        label: (ctx) => ` ${ctx.label}: $${ctx.parsed.toFixed(4)}`
                    }
                }
            },
            cutout: '65%'
        }
    });
};

// Render cost line chart (daily trend)
window.adminRenderCostTrend = function(data, canvasId) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || !data || typeof Chart === 'undefined') return;

    const existing = Chart.getChart(canvas);
    if (existing) existing.destroy();

    new Chart(canvas, {
        type: 'line',
        data: {
            labels: data.map(d => d.date),
            datasets: [{
                label: 'Coste ($)',
                data: data.map(d => d.cost),
                borderColor: '#4a90e2',
                backgroundColor: 'rgba(74,144,226,0.1)',
                tension: 0.4,
                fill: true,
                pointBackgroundColor: '#4a90e2'
            }]
        },
        options: {
            responsive: true,
            plugins: { legend: { display: false } },
            scales: {
                x: {
                    ticks: { color: '#8892a4', font: { size: 11 } },
                    grid: { color: '#1e2a3a' }
                },
                y: {
                    ticks: {
                        color: '#8892a4', font: { size: 11 },
                        callback: v => '$' + v.toFixed(4)
                    },
                    grid: { color: '#1e2a3a' }
                }
            }
        }
    });
};

// Copy to clipboard
window.copyToClipboard = function(text) {
    navigator.clipboard.writeText(text).then(() => {
        // Brief visual feedback
        const toast = document.createElement('div');
        toast.textContent = 'Copiado';
        toast.style.cssText = 'position:fixed;bottom:20px;right:20px;background:#4a90e2;color:white;padding:8px 16px;border-radius:6px;font-size:13px;z-index:9999';
        document.body.appendChild(toast);
        setTimeout(() => toast.remove(), 2000);
    });
};

// Confirm dialog
window.confirmDialog = function(message) {
    return confirm(message);
};

// Download a text file from Blazor
window.downloadTextFile = function(filename, content) {
    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = filename;
    document.body.appendChild(a); a.click();
    document.body.removeChild(a); URL.revokeObjectURL(url);
};
