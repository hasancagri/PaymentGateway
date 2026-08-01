// Jenerik filtrelenebilir + sayfalanabilir tablo modülü. Sayfadaki her `[data-filterable]` kökü
// için bağımsız çalışır. Kök içinde beklenenler:
//   - <table> (satırlarında data-* eksen öznitelikleri; select[data-filter] value'su ile eşleşir)
//   - select[data-filter="<eksen>"]  → satırdaki data-<eksen> ile filtreler (birlikte daraltır)
//   - [data-role="prev"|"next"|"page-info"]  → 20'li sayfalama kontrolleri (opsiyonel)
//   - [data-role="fill-rate"] + [data-role="fill-empty"]  → görünen (açık sayfa) boş hücreleri
//     girilen oranla doldur (opsiyonel; yalnız düzenlenebilir gridlerde)
// Backend'e dokunmaz; yalnız DOM görünürlüğü ve boş rate input'larını düzenler. Tüm <input>'lar
// DOM'da kalır — kalıcılık normal form gönderimiyle olur.
(function () {
    "use strict";

    var PAGE_SIZE = 20;

    function init(root) {
        var table = root.querySelector("table");
        if (!table) return;

        var rows = Array.prototype.slice.call(table.querySelectorAll("tbody tr"));
        var filterSelects = Array.prototype.slice.call(root.querySelectorAll("select[data-filter]"));

        var prevBtn = root.querySelector('[data-role="prev"]');
        var nextBtn = root.querySelector('[data-role="next"]');
        var pageInfo = root.querySelector('[data-role="page-info"]');
        var fillRate = root.querySelector('[data-role="fill-rate"]');
        var fillBtn = root.querySelector('[data-role="fill-empty"]');

        var currentPage = 1;

        function rowMatchesFilters(row) {
            for (var i = 0; i < filterSelects.length; i++) {
                var sel = filterSelects[i];
                var chosen = sel.value;
                if (!chosen) continue; // "hepsi"
                var attr = sel.getAttribute("data-filter"); // brand/type/region/inst
                if (row.getAttribute("data-" + attr) !== chosen) return false;
            }
            return true;
        }

        function render() {
            var matched = rows.filter(rowMatchesFilters);
            var totalPages = Math.max(1, Math.ceil(matched.length / PAGE_SIZE));
            if (currentPage > totalPages) currentPage = totalPages;
            if (currentPage < 1) currentPage = 1;

            var start = (currentPage - 1) * PAGE_SIZE;
            var end = start + PAGE_SIZE;

            rows.forEach(function (row) { row.style.display = "none"; });
            matched.slice(start, end).forEach(function (row) { row.style.display = ""; });

            if (pageInfo) pageInfo.textContent = "Sayfa " + currentPage + " / " + totalPages +
                " (" + matched.length + " kayıt)";
            if (prevBtn) prevBtn.disabled = currentPage <= 1;
            if (nextBtn) nextBtn.disabled = currentPage >= totalPages;
        }

        filterSelects.forEach(function (sel) {
            sel.addEventListener("change", function () {
                currentPage = 1; // filtre değişince başa dön
                render();
            });
        });

        if (prevBtn) prevBtn.addEventListener("click", function () {
            if (currentPage > 1) { currentPage--; render(); }
        });
        if (nextBtn) nextBtn.addEventListener("click", function () {
            currentPage++; render();
        });

        // Opsiyonel: görünen (açık sayfa + filtre) boş hücreleri doldur.
        if (fillBtn && fillRate) {
            fillBtn.addEventListener("click", function () {
                var raw = fillRate.value.trim();
                if (raw === "") { fillRate.focus(); return; }
                rows.forEach(function (row) {
                    if (row.style.display === "none") return; // yalnız açık sayfadaki görünenler
                    var input = row.querySelector("input.rate");
                    if (!input) return;
                    if (input.value.trim() === "") {
                        input.value = raw;
                        row.classList.remove("missing");
                    }
                });
            });
        }

        render();
    }

    Array.prototype.slice.call(document.querySelectorAll("[data-filterable]")).forEach(init);
})();