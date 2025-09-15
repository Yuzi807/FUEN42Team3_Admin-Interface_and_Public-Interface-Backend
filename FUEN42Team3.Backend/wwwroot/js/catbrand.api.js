/*  catbrand.api.js  (with toggle status)
    - 分類/品牌共用：載入列表、搜尋、分頁、建立/修改、刪除、狀態切換
    - Category 無圖片；Brand 若頁面有圖片欄位則可上傳 Logo（沒有也不會壞）
*/
(() => {
    // ===== API 路由（可由頁面裡的 CB_API 覆寫；否則用預設）=====
    const API = {
        categoriesList: (window.CB_API?.categoriesUrl) || "/api/Categories/paged",
        brandsList: (window.CB_API?.brandsUrl) || "/api/Brands/paged",
        categoriesBase: (window.CB_API?.categoriesBaseUrl) || "/api/Categories",
        brandsBase: (window.CB_API?.brandsBaseUrl) || "/api/Brands",
    };

    // ===== DOM 小工具 =====
    const $ = (sel, root = document) => root.querySelector(sel);
    const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

    // ===== 狀態 =====
    const state = {
        category: { page: 1, pageSize: 10, keyword: "", items: [] },
        brand: { page: 1, pageSize: 10, keyword: "", items: [] }
    };

    // ===== 通用：分頁 + 回傳形狀容錯 =====
    function normalizeResult(raw, pageSize) {
        if (Array.isArray(raw)) return { items: raw, totalPages: 1, totalCount: raw.length };
        const items = raw.items ?? raw.records ?? raw.data ?? [];
        const totalPages = raw.totalPages ?? (raw.totalCount ? Math.ceil(Number(raw.totalCount) / Number(pageSize || 10)) : 1);
        const totalCount = raw.totalCount ?? (Array.isArray(items) ? items.length : 0);
        return { items, totalPages, totalCount };
    }

    function buildPagination(containerEl, page, totalPages, onChange) {
        if (!containerEl) return;
        containerEl.innerHTML = "";
        totalPages = Math.max(1, Number(totalPages || 1));
        page = Math.min(Math.max(1, Number(page || 1)), totalPages);

        const ul = document.createElement("ul");
        ul.className = "pagination";

        const addItem = (text, targetPage, disabled = false, active = false) => {
            const li = document.createElement("li");
            li.className = "page-item" + (disabled ? " disabled" : "") + (active ? " active" : "");
            const btn = document.createElement("button");
            btn.type = "button";
            btn.className = "page-link";
            btn.textContent = text;
            if (!disabled && !active) btn.addEventListener("click", () => onChange(targetPage));
            li.appendChild(btn);
            ul.appendChild(li);
        };

        addItem("«", 1, page === 1);
        addItem("‹", page - 1, page === 1);
        const start = Math.max(1, page - 2);
        const end = Math.min(totalPages, page + 2);
        for (let p = start; p <= end; p++) addItem(String(p), p, false, p === page);
        addItem("›", page + 1, page === totalPages);
        addItem("»", totalPages, page === totalPages);

        containerEl.appendChild(ul);
    }

    // ===== 欄位對齊 =====
    const pickCategory = x => ({
        id: x.id ?? x.Id,
        name: x.categoryName ?? x.CategoryName ?? x.name ?? "",
        isActive: (typeof x.isActive === "boolean") ? x.isActive : (x.IsActive ?? x.enabled ?? false)
    });

    const pickBrand = x => ({
        id: x.id ?? x.Id,
        name: x.brandName ?? x.BrandName ?? x.name ?? "",
        isActive: (typeof x.isActive === "boolean") ? x.isActive : (x.IsActive ?? x.enabled ?? false),
        logo: x.logoUrl ?? x.LogoUrl ?? "/uploads/band/no-image.png"
    });

    // ===== 渲染 =====
    function statusBtnHtml(kind, x) {
        const color = x.isActive ? "btn-success" : "btn-danger";
        const text = x.isActive ? "啟用中" : "已停用";
        return `<button class="btn btn-sm ${color}" data-type="${kind}" data-action="toggle" data-id="${x.id}">${text}</button>`;
    }

    function renderCategoryTable(items) {
        const tbody = $("#categoryTable tbody");
        if (!tbody) return;
        tbody.innerHTML = "";
        items.forEach((raw, idx) => {
            const x = pickCategory(raw);
            const tr = document.createElement("tr");
            tr.innerHTML = `
        <td>${idx + 1 + (state.category.page - 1) * state.category.pageSize}</td>
        <td>${x.name}</td>
        <td>${statusBtnHtml("category", x)}</td>
        <td>
          <button class="btn btn-sm btn-outline-primary me-1" data-type="category" data-action="edit" data-id="${x.id}">編輯</button>
          <button class="btn btn-sm btn-outline-danger" data-type="category" data-action="del" data-id="${x.id}">刪除</button>
        </td>`;
            tbody.appendChild(tr);
        });
    }

    function renderBrandTable(items) {
        const tbody = $("#brandTable tbody");
        if (!tbody) return;
        tbody.innerHTML = "";
        items.forEach((raw, idx) => {
            const x = pickBrand(raw);
            const tr = document.createElement("tr");
            tr.innerHTML = `
        <td>${idx + 1 + (state.brand.page - 1) * state.brand.pageSize}</td>
        <td><img src="${x.logo}" class="img-thumbnail" style="max-height:60px" /></td>
        <td>${x.name}</td>
        <td>${statusBtnHtml("brand", x)}</td>
        <td>
          <button class="btn btn-sm btn-outline-primary me-1" data-type="brand" data-action="edit" data-id="${x.id}">編輯</button>
          <button class="btn btn-sm btn-outline-danger" data-type="brand" data-action="del" data-id="${x.id}">刪除</button>
        </td>`;
            tbody.appendChild(tr);
        });
    }

    // ===== 載入列表 =====
    async function loadList(kind) {
        const isCategory = (kind === "category");
        const s = isCategory ? state.category : state.brand;
        const listUrl = isCategory ? API.categoriesList : API.brandsList;

        const q = new URLSearchParams({
            page: s.page,
            pageSize: s.pageSize,
            keyword: s.keyword || ""
        });

        const res = await fetch(`${listUrl}?${q.toString()}`, { headers: { "Accept": "application/json" } });
        if (!res.ok) { alert(`${isCategory ? '分類' : '品牌'}載入失敗 (${res.status})`); return; }
        const raw = await res.json();
        const { items, totalPages } = normalizeResult(raw, s.pageSize);

        s.items = items;
        if (isCategory) {
            renderCategoryTable(items);
            buildPagination($("#categoryPagination"), s.page, totalPages, (p) => { s.page = p; loadList("category"); });
        } else {
            renderBrandTable(items);
            buildPagination($("#brandPagination"), s.page, totalPages, (p) => { s.page = p; loadList("brand"); });
        }
    }

    // ===== Modal =====
    function openItemModal(kind, model = null) {
        $("#itemType").value = kind;
        $("#itemId").value = model?.id ?? "";
        $("#itemName").value = model?.name ?? "";

        const imgInput = $("#itemImage");
        if (imgInput) imgInput.value = "";

        const imgGroup = imgInput ? imgInput.closest(".mb-3") : null;
        if (imgGroup) {
            if (kind === "category") imgGroup.classList.add("d-none");
            else imgGroup.classList.remove("d-none");
        }

        const preview = $("#itemPreview");
        if (preview) {
            if (kind === "brand" && model?.logo) {
                preview.src = model.logo;
                preview.classList.remove("d-none");
            } else {
                preview.src = "#";
                preview.classList.add("d-none");
            }
        }

        $("#itemModalLabel").textContent = (model ? "編輯" : "新增") + (kind === "category" ? "分類" : "品牌");
        new bootstrap.Modal(document.getElementById("itemModal")).show();
    }

    // ===== 新增 / 修改 =====
    async function submitItem(kind) {
        const isCategory = (kind === "category");
        const baseUrl = isCategory ? API.categoriesBase : API.brandsBase;

        const id = $("#itemId").value.trim();
        const name = $("#itemName").value.trim();
        const imgEl = $("#itemImage");
        const file = imgEl?.files?.[0] || null;

        if (!name) { alert("請輸入名稱"); return; }

        const fd = new FormData();
        if (isCategory) fd.append("CategoryName", name);
        else fd.append("BrandName", name);

        fd.append("IsActive", "true");
        fd.append("CreatedBy", "1");
        fd.append("UpdatedBy", "1");

        if (!isCategory && file) fd.append("imageFile", file);

        const method = id ? "PUT" : "POST";
        const url = id ? `${baseUrl}/${id}` : baseUrl;

        const res = await fetch(url, { method, body: fd, headers: { "Accept": "application/json" } });
        if (!res.ok) { alert(`儲存失敗 (${res.status})`); return; }

        bootstrap.Modal.getInstance(document.getElementById("itemModal"))?.hide();
        if (isCategory) loadList("category"); else loadList("brand");
    }

    // ===== 切換啟用/停用 =====
    async function toggleItem(kind, id, toActive) {
        const isCategory = (kind === "category");
        const baseUrl = isCategory ? API.categoriesBase : API.brandsBase;

        // 從快取抓目前的名稱，因為很多後端 PUT 需要名稱一起帶
        const raw = (isCategory ? state.category.items : state.brand.items)
            .find(x => (x.id ?? x.Id) === id);
        const picked = isCategory ? pickCategory(raw || {}) : pickBrand(raw || {});
        if (!picked?.id) { alert("找不到要切換的項目"); return; }

        const fd = new FormData();
        if (isCategory) fd.append("CategoryName", picked.name);
        else fd.append("BrandName", picked.name);
        fd.append("IsActive", String(!!toActive));
        fd.append("UpdatedBy", "1");

        const res = await fetch(`${baseUrl}/${id}`, { method: "PUT", body: fd, headers: { "Accept": "application/json" } });
        if (!res.ok) { alert(`狀態切換失敗 (${res.status})`); return; }

        // 成功後重新載入目前分頁
        if (isCategory) await loadList("category"); else await loadList("brand");
    }

    // ===== 刪除 =====
    async function deleteItem(kind, id) {
        const isCategory = (kind === "category");
        const baseUrl = isCategory ? API.categoriesBase : API.brandsBase;
        if (!confirm(`確定刪除這個${isCategory ? '分類' : '品牌'}？`)) return;

        const res = await fetch(`${baseUrl}/${id}`, { method: "DELETE" });
        if (!res.ok) { alert(`刪除失敗 (${res.status})`); return; }
        if (isCategory) loadList("category"); else loadList("brand");
    }

    // ===== 綁定 =====
    document.addEventListener("DOMContentLoaded", () => {
        // 初始 pageSize
        state.category.pageSize = Number($("#categoryPageSize")?.value || 10);
        state.brand.pageSize = Number($("#brandPageSize")?.value || 10);

        // 先載入分類
        loadList("category").catch(console.error);

        // 分類 搜尋/每頁
        $("#btnCategorySearch")?.addEventListener("click", () => {
            state.category.keyword = $("#categoryKeyword").value.trim();
            state.category.page = 1;
            loadList("category");
        });
        $("#categoryKeyword")?.addEventListener("keydown", (e) => {
            if (e.key === "Enter") { e.preventDefault(); $("#btnCategorySearch").click(); }
        });
        $("#categoryPageSize")?.addEventListener("change", () => {
            state.category.pageSize = Number($("#categoryPageSize").value || 10);
            state.category.page = 1;
            loadList("category");
        });

        // 品牌 搜尋/每頁
        $("#btnBrandSearch")?.addEventListener("click", () => {
            state.brand.keyword = $("#brandKeyword").value.trim();
            state.brand.page = 1;
            loadList("brand");
        });
        $("#brandKeyword")?.addEventListener("keydown", (e) => {
            if (e.key === "Enter") { e.preventDefault(); $("#btnBrandSearch").click(); }
        });
        $("#brandPageSize")?.addEventListener("change", () => {
            state.brand.pageSize = Number($("#brandPageSize").value || 10);
            state.brand.page = 1;
            loadList("brand");
        });

        // 切到品牌分頁時載入
        document.getElementById("brand-tab")?.addEventListener("shown.bs.tab", () => {
            loadList("brand");
        });

        // 新增按鈕
        $("#btnAddCategory")?.addEventListener("click", () => openItemModal("category"));
        $("#btnAddBrand")?.addEventListener("click", () => openItemModal("brand"));

        // 表格事件代理（分類）
        $("#categoryTable")?.addEventListener("click", (e) => {
            const btn = e.target.closest("button[data-action]");
            if (!btn) return;
            const id = Number(btn.getAttribute("data-id"));
            const act = btn.getAttribute("data-action");
            if (act === "edit") {
                const raw = state.category.items.find(x => (x.id ?? x.Id) === id);
                const m = pickCategory(raw || {});
                openItemModal("category", m);
            } else if (act === "del") {
                deleteItem("category", id);
            } else if (act === "toggle") {
                const raw = state.category.items.find(x => (x.id ?? x.Id) === id);
                const curr = pickCategory(raw || {});
                toggleItem("category", id, !curr.isActive);
            }
        });

        // 表格事件代理（品牌）
        $("#brandTable")?.addEventListener("click", (e) => {
            const btn = e.target.closest("button[data-action]");
            if (!btn) return;
            const id = Number(btn.getAttribute("data-id"));
            const act = btn.getAttribute("data-action");
            if (act === "edit") {
                const raw = state.brand.items.find(x => (x.id ?? x.Id) === id);
                const m = pickBrand(raw || {});
                openItemModal("brand", m);
            } else if (act === "del") {
                deleteItem("brand", id);
            } else if (act === "toggle") {
                const raw = state.brand.items.find(x => (x.id ?? x.Id) === id);
                const curr = pickBrand(raw || {});
                toggleItem("brand", id, !curr.isActive);
            }
        });

        // 圖片預覽（欄位存在才綁）
        const imgEl = $("#itemImage");
        if (imgEl) {
            imgEl.addEventListener("change", (e) => {
                const file = e.target.files?.[0];
                const img = $("#itemPreview");
                if (!img) return;
                if (!file) { img.classList.add("d-none"); img.src = "#"; return; }
                const reader = new FileReader();
                reader.onload = () => { img.src = reader.result; img.classList.remove("d-none"); };
                reader.readAsDataURL(file);
            });
        }

        // Modal 送出
        $("#itemForm")?.addEventListener("submit", (e) => {
            e.preventDefault();
            const kind = $("#itemType").value; // category | brand
            submitItem(kind).catch(console.error);
        });
    });
})();
