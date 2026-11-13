(() => {
    const chart = document.getElementById('gateChart');
    const range = document.getElementById('gateChartRange');
    const refresh = document.getElementById('gateChartRefresh');
    const status = document.getElementById('gateChartStatus');
    const data = document.getElementById('gateChartData');
    const summary = document.getElementById('gateChartSummary');
    const bars = document.getElementById('gateChartBars');
    const { column, rangePicker, hideTooltip, countFormat, period, validCount } = window.dashboardCharts;
    const selectedRange = rangePicker(range, load);
    let pending;

    const hourLabel = hour => `${String(hour).padStart(2, '0')}:00`;
    const hourRange = hour => `${hourLabel(hour)} – ${hourLabel(hour + 1)}`;

    function render(report) {
        const hours = report?.hours;
        if (!Array.isArray(hours) || hours.length !== 24 || hours.some((row, hour) =>
            !row || row.hour !== hour || !validCount(row.count) || !validCount(row.previousCount))) {
            throw new Error('API trả số liệu theo giờ không hợp lệ.');
        }
        const currentPeriod = period(report.fromUtc, report.toUtc);
        const previousPeriod = period(report.previousFromUtc, report.previousToUtc);
        const total = hours.reduce((sum, row) => sum + row.count, 0);
        const peak = hours.reduce((best, row) => row.count > best.count ? row : best);
        summary.textContent = total
            ? `${currentPeriod} · ${countFormat.format(total)} lượt vào cổng · Cao điểm ${hourRange(peak.hour)} (${countFormat.format(peak.count)} lượt)`
            : `${currentPeriod} · Chưa ghi nhận lượt vào cổng trong khoảng này.`;

        const items = hours.map(({ hour, count, previousCount }) => {
            const item = document.createElement('li');
            item.className = 'gate-chart-bar';
            const label = document.createElement('span');
            label.className = 'chart-axis-label';
            label.setAttribute('aria-hidden', 'true');
            label.textContent = hourLabel(hour);
            item.append(column({
                heading: hourRange(hour) + ' · Lượt vào cổng',
                count, previous: previousCount, max: peak.count, currentPeriod, previousPeriod,
                color: 'chart-column-blue'
            }), label);
            return item;
        });
        bars.replaceChildren(...items);
        data.hidden = false;
    }

    async function load() {
        hideTooltip();
        pending?.abort();
        const request = new AbortController();
        pending = request;
        status.textContent = 'Đang tải thống kê…';
        status.hidden = false;
        data.hidden = true;
        chart.setAttribute('aria-busy', 'true');
        const url = new URL(chart.dataset.url, location.href);
        url.searchParams.set('range', selectedRange());
        try {
            const report = await adminJson(url.href, { signal: request.signal, cache: 'no-store' });
            if (request.signal.aborted) return;
            render(report);
            status.hidden = true;
        } catch (error) {
            if (request.signal.aborted) return;
            status.textContent = `Không tải được biểu đồ. ${error instanceof TypeError ? 'Hãy kiểm tra kết nối và thử lại.' : error.message}`;
        } finally {
            if (pending === request) {
                pending = undefined;
                chart.removeAttribute('aria-busy');
            }
        }
    }

    refresh.addEventListener('click', load);
    load();
})();
