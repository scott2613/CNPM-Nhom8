(() => {
    const countFormat = new Intl.NumberFormat('vi-VN');
    const compact = new Intl.NumberFormat('vi-VN', { notation: 'compact' });
    const percent = new Intl.NumberFormat('vi-VN', { style: 'percent', maximumFractionDigits: 1 });
    const date = new Intl.DateTimeFormat('vi-VN', {
        timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: 'short'
    });
    const tooltip = document.createElement('div');
    tooltip.className = 'chart-detail-tooltip';
    tooltip.hidden = true;
    tooltip.setAttribute('aria-hidden', 'true');
    document.body.append(tooltip);
    let anchor;

    function hideTooltip() {
        anchor = undefined;
        tooltip.hidden = true;
    }

    window.addEventListener('resize', hideTooltip);
    window.addEventListener('scroll', hideTooltip, true);
    document.addEventListener('pointerdown', event => {
        if (anchor && !anchor.contains(event.target)) hideTooltip();
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') hideTooltip();
    });

    function comparison(current, previous) {
        if (previous === current) return 'Không đổi (0%)';
        if (previous === 0) return 'Có lượt mới · kỳ trước 0 lượt';
        const difference = (current - previous) / previous;
        return (difference > 0 ? 'Tăng ' : 'Giảm ') + percent.format(Math.abs(difference));
    }

    function showTooltip(button, lines, trendClass) {
        anchor = button;
        tooltip.replaceChildren(...lines.map((line, index) => {
            const text = document.createElement(index === 0 ? 'strong' : 'span');
            if (index === lines.length - 1) text.className = trendClass;
            text.textContent = line;
            return text;
        }));
        tooltip.hidden = false;
        const bounds = button.getBoundingClientRect();
        const width = tooltip.offsetWidth;
        const height = tooltip.offsetHeight;
        const left = Math.max(8, Math.min(bounds.left + (bounds.width - width) / 2, window.innerWidth - width - 8));
        const preferredTop = bounds.top - height - 8;
        const top = preferredTop >= 8 ? preferredTop : Math.min(bounds.top + 8, window.innerHeight - height - 8);
        tooltip.style.left = left + 'px';
        tooltip.style.top = Math.max(top, 8) + 'px';
    }

    function column({ heading, count, previous, max, currentPeriod, previousPeriod, color }) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'chart-column ' + color;
        button.style.setProperty('--chart-height', (max ? 85 * count / max : 0) + '%');
        const lines = [
            heading,
            'Kỳ hiện tại: ' + currentPeriod + ' · ' + countFormat.format(count) + ' lượt',
            'Kỳ trước: ' + previousPeriod + ' · ' + countFormat.format(previous) + ' lượt',
            comparison(count, previous)
        ];
        button.setAttribute('aria-label', lines.join('. '));
        const value = document.createElement('span');
        value.className = 'chart-column-value';
        value.textContent = compact.format(count);
        const fill = document.createElement('span');
        fill.className = 'chart-column-fill';
        button.append(value, fill);
        const trendClass = count === previous ? '' : count > previous ? 'text-success' : 'text-danger';
        const show = () => showTooltip(button, lines, trendClass);
        button.addEventListener('mouseenter', show);
        button.addEventListener('mouseleave', hideTooltip);
        button.addEventListener('focus', show);
        button.addEventListener('blur', hideTooltip);
        button.addEventListener('click', show);
        return button;
    }

    function rangePicker(group, onChange) {
        const buttons = Array.from(group.querySelectorAll('button[data-range]'));
        const summary = group.querySelector('summary');
        const label = group.querySelector('[data-range-label]');
        const name = summary.getAttribute('aria-label');
        let active = buttons.find(button => button.getAttribute('aria-pressed') === 'true');
        summary.setAttribute('aria-label', name + ' · ' + label.textContent);
        for (const button of buttons) {
            button.addEventListener('click', () => {
                group.open = false;
                summary.focus();
                if (button === active) return;
                active.setAttribute('aria-pressed', 'false');
                active = button;
                active.setAttribute('aria-pressed', 'true');
                label.textContent = active.textContent;
                summary.setAttribute('aria-label', name + ' · ' + label.textContent);
                onChange();
            });
        }
        document.addEventListener('pointerdown', event => {
            if (group.open && !group.contains(event.target)) group.open = false;
        });
        group.addEventListener('keydown', event => {
            if (group.open && event.key === 'Escape') {
                group.open = false;
                summary.focus();
            }
        });
        return () => active.dataset.range;
    }

    window.dashboardCharts = {
        column, rangePicker, hideTooltip, countFormat,
        period: (from, to) => date.format(new Date(from)) + ' – ' + date.format(new Date(to)),
        validCount: value => Number.isSafeInteger(value) && value >= 0
    };
})();
