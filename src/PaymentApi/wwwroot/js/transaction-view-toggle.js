(() => {
    const buttons = document.querySelectorAll("[data-view]");
    const cardView = document.querySelector(".transaction-list");
    const tableView = document.querySelector(".transaction-table-view");

    if (!buttons.length || !cardView || !tableView) return;

    buttons.forEach(button => button.addEventListener("click", () => {
        const showTable = button.dataset.view === "table";

        cardView.hidden = showTable;
        tableView.hidden = !showTable;
        buttons.forEach(option => {
            const selected = option === button;
            option.classList.toggle("is-selected", selected);
            option.setAttribute("aria-pressed", selected.toString());
        });
    }));
})();
