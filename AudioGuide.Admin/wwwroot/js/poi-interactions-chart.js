(() => {
    const chart = document.getElementById('poiChart');
    const range = document.getElementById('poiChartRange');
    const locationFilter = document.getElementById('poiChartLocation');
    const status = document.getElementById('poiChartStatus');
    const data = document.getElementById('poiChartData');
    const period = document.getElementById('poiChartPeriod');
    const summary = document.getElementById('poiChartSummary');
    const bars = document.getElementById('poiChartBars');
    const { column, rangePicker, hideTooltip, countFormat, period: formatPeriod, validCount } = window.dashboardCharts;
    const selectedRange = rangePicker(range, load);
    let locations = [];
    let currentPeriod, previousPeriod, pending;

    function render() {
        hideTooltip();
        const selected = locationFilter.value;
        const rows = selected ? locations.filter(row => row.code === selected) : locations;
        const max = locations.reduce((value, row) => Math.max(value, row.viewCount, row.playCount), 0);
        const views = rows.reduce((sum, row) => sum + row.viewCount, 0);
        const plays = rows.reduce((sum, row) => sum + row.playCount, 0);
        summary.textContent = rows.length
            ? `${countFormat.format(views)} lượt mở điểm · ${countFormat.format(plays)} lượt bắt đầu nghe`
            : 'Chưa có địa điểm trong danh mục.';
        bars.replaceChildren(...rows.map(row => {
            const item = document.createElement('li');
            item.className = 'poi-chart-group';
            const columns = document.createElement('div');
            columns.className = 'poi-chart-columns';
            columns.append(
                column({ heading: row.title + ' · Mở điểm', count: row.viewCount,
                    previous: row.previousViewCount, max, currentPeriod, previousPeriod, color: 'chart-column-blue' }),
                column({ heading: row.title + ' · Bắt đầu nghe', count: row.playCount,
                    previous: row.previousPlayCount, max, currentPeriod, previousPeriod, color: 'chart-column-green' })
            );
            const title = document.createElement('span');
            title.className = 'chart-axis-label poi-chart-label';
            title.textContent = row.title + ' · ' + row.code;
            item.append(columns, title);
            return item;
        }));
    }

    async function load() {
        hideTooltip();
        pending?.abort();
        const request = new AbortController();
        pending = request;
        status.textContent = 'Đang tải thống kê…';
        status.hidden = false;
        data.hidden = true;
        locationFilter.disabled = true;
        chart.setAttribute('aria-busy', 'true');
        const url = new URL(chart.dataset.url, location.href);
        url.searchParams.set('range', selectedRange());
        try {
            const report = await adminJson(url.href, { signal: request.signal, cache: 'no-store' });
            if (request.signal.aborted) return;
            if (!Array.isArray(report?.locations) || report.locations.some(row =>
                !row || typeof row.code !== 'string' || typeof row.title !== 'string' ||
                ![row.viewCount, row.playCount, row.previousViewCount, row.previousPlayCount].every(validCount))) {
                throw new Error('API trả số liệu địa điểm không hợp lệ.');
            }
            currentPeriod = formatPeriod(report.fromUtc, report.toUtc);
            previousPeriod = formatPeriod(report.previousFromUtc, report.previousToUtc);
            period.textContent = currentPeriod;
            const selected = locationFilter.value;
            locations = report.locations;
            locationFilter.replaceChildren(new Option('Tất cả địa điểm', ''),
                ...locations.map(row => new Option(row.title + ' · ' + row.code, row.code)));
            locationFilter.value = locations.some(row => row.code === selected) ? selected : '';
            locationFilter.disabled = !locations.length;
            render();
            status.hidden = true;
            data.hidden = false;
        } catch (error) {
            if (request.signal.aborted) return;
            status.textContent = 'Không tải được biểu đồ địa điểm. ' +
                (error instanceof TypeError ? 'Hãy kiểm tra kết nối và thử lại.' : error.message);
        } finally {
            if (pending === request) {
                pending = undefined;
                chart.removeAttribute('aria-busy');
            }
        }
    }

    locationFilter.addEventListener('change', render);
    document.getElementById('poiChartRefresh').addEventListener('click', load);
    load();
})();
