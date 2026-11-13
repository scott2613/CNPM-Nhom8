(() => {
    const pagers = new WeakMap();

    function attach(table) {
        const body = table.tBodies[0];
        const nav = document.createElement("nav");
        nav.className = "admin-pager px-3 py-2";
        nav.setAttribute("aria-label", "Phân trang " + table.dataset.paginate);
        const status = document.createElement("span");
        status.className = "small text-secondary";
        status.setAttribute("aria-live", "polite");
        const size = document.createElement("select");
        size.className = "form-select form-select-sm w-auto";
        size.setAttribute("aria-label", "Số dòng mỗi trang");
        [10, 25, 50].forEach(n => size.add(new Option(n + " dòng", n)));
        const links = document.createElement("ul");
        links.className = "pagination pagination-sm mb-0";
        nav.append(status, links, size);
        table.after(nav);

        let page = 1;
        let lastControls = "";

        function pageLink(label, description, target, pages, current = false) {
            const item = document.createElement("li");
            const disabled = target < 1 || target > pages;
            item.className = "page-item" + (current ? " active" : disabled ? " disabled" : "");
            const button = document.createElement("button");
            button.type = "button";
            button.className = "page-link";
            button.textContent = label;
            button.disabled = disabled;
            button.setAttribute("aria-label", description);
            if (current) button.setAttribute("aria-current", "page");
            button.addEventListener("click", () => {
                page = target;
                render();
                links.querySelector('[aria-current="page"]')?.focus();
            });
            item.append(button);
            return item;
        }

        function renderControls(pages) {
            const controls = page + "/" + pages;
            if (controls === lastControls) return;
            lastControls = controls;
            const numbers = pages <= 7 ? Array.from({ length: pages }, (_, i) => i + 1)
                : page <= 3 ? [1, 2, 3, 4, null, pages]
                : page >= pages - 2 ? [1, null, pages - 3, pages - 2, pages - 1, pages]
                : [1, null, page - 1, page, page + 1, null, pages];
            links.replaceChildren(
                pageLink("‹", "Trang trước", page - 1, pages),
                ...numbers.map(number => {
                    if (number !== null) return pageLink(number, "Trang " + number, number, pages, number === page);
                    const gap = document.createElement("li");
                    const mark = document.createElement("span");
                    gap.className = "page-item disabled";
                    mark.className = "page-link";
                    mark.textContent = "…";
                    gap.setAttribute("aria-hidden", "true");
                    gap.append(mark);
                    return gap;
                }),
                pageLink("›", "Trang sau", page + 1, pages)
            );
        }

        function render(reset = false) {
            if (reset) page = 1;
            const rows = Array.from(body.rows).filter(row => row.cells.length > 1);
            const matches = rows.filter(row => row.style.display !== "none");
            const pageSize = Number(size.value);
            const pages = Math.max(1, Math.ceil(matches.length / pageSize));
            page = Math.max(1, Math.min(page, pages));
            const start = (page - 1) * pageSize;
            const visible = new Set(matches.slice(start, start + pageSize));
            for (const row of rows) row.classList.toggle("d-none", !visible.has(row));
            status.textContent = matches.length
                ? `${start + 1}–${Math.min(start + pageSize, matches.length)} / ${matches.length} · Trang ${page}/${pages}`
                : "Không có dòng phù hợp";
            renderControls(pages);
        }

        size.addEventListener("change", () => render(true));
        new MutationObserver(() => render()).observe(body, { childList: true });
        pagers.set(table, render);
        render();
    }

    window.refreshAdminPagination = (id, reset = false) =>
        pagers.get(document.getElementById(id))?.(reset);

    document.addEventListener("DOMContentLoaded", () => {
        document.querySelectorAll("table[data-paginate]").forEach(attach);
    });
})();
