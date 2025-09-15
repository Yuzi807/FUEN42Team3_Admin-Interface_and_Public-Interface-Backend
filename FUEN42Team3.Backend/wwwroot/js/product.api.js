// wwwroot/js/product.api.js
// 真正打 API 的版本（GET/POST/PUT/DELETE）
// 表單欄位使用 CategoryId / BrandId / StatusId，與 ProductsAPIController 對齊

console.log("product.api.js loaded v2025-08-19-15:10");

let products = [];      // 快取
let tempImageList = []; // 圖片預覽用
let dt = null;

const API_BASE = "/api/ProductsAPI"; // ProductsAPIController 路徑

// ===== 小工具 =====
const getEl = (sel) => document.querySelector(sel);
const qsa = (sel) => Array.from(document.querySelectorAll(sel));

// 同時支援 PascalCase / camelCase
function pick(obj, ...keys) {
    for (const k of keys) {
        if (obj?.[k] !== undefined && obj?.[k] !== null) return obj[k];
    }
    return undefined;
}

function fmtDate(v) {
    if (!v) return "";
    const d = new Date(v);
    if (isNaN(d)) return String(v).slice(0, 10);
    const mm = String(d.getMonth() + 1).padStart(2, "0");
    const dd = String(d.getDate()).padStart(2, "0");
    return `${d.getFullYear()}-${mm}-${dd}`;
}
function fmtPrice(base, sale) {
    const b = Number(base ?? 0);
    const s = sale == null || sale === "" ? null : Number(sale);
    if (s != null && s > 0 && s < b) {
        return `<span class="text-danger fw-bold">NT$${s}</span> <span class="text-muted text-decoration-line-through">NT$${b}</span>`;
    }
    return `<span class="fw-bold">NT$${b}</span>`;
}
// 轉成 datetime-local 需要的 YYYY-MM-DDTHH:MM (使用台灣時區)
function toDatetimeLocal(v) {
    if (!v) return "";
    const d = new Date(v);
    if (isNaN(d)) return "";
    
    // 將日期轉換為台灣時區 (UTC+8)
    const taiwanTime = new Date(d.getTime() + (8 * 60 * 60 * 1000)); // 加上8小時的毫秒數
    
    const yyyy = taiwanTime.getUTCFullYear();
    const mm = String(taiwanTime.getUTCMonth() + 1).padStart(2, "0");
    const dd = String(taiwanTime.getUTCDate()).padStart(2, "0");
    const hh = String(taiwanTime.getUTCHours()).padStart(2, "0");
    const mi = String(taiwanTime.getUTCMinutes()).padStart(2, "0");
    return `${yyyy}-${mm}-${dd}T${hh}:${mi}`;
}

// 後端回來（GET 列表）→ 前端 VM
function toViewModel(p, i) {
    // 記錄原始數據，協助調試
    console.log("從後端返回的原始數據:", p);
    
    const model = {
        id: pick(p, "id", "Id") ?? (i + 1),
        name: pick(p, "name", "Name", "productName", "ProductName") ?? "",
        sku: pick(p, "sku", "SKU", "Sku") ?? "",

        // 顯示文字（後端 Select 有回傳）
        category: pick(p, "categoryName", "CategoryName") ?? "",
        brand: pick(p, "brandName", "BrandName") ?? "",
        status: pick(p, "statusName", "StatusName") ?? "",

        // 之後編輯要用的 ID（後端也回）
        categoryId: pick(p, "categoryId", "CategoryId") ?? null,
        brandId: pick(p, "brandId", "BrandId") ?? null,
        statusId: pick(p, "statusId", "StatusId") ?? null,

        basePrice: pick(p, "basePrice", "BasePrice", "price", "Price") ?? 0,
        salePrice: pick(p, "specialPrice", "SpecialPrice") ?? null,
        active: Boolean(pick(p, "isActive", "IsActive") ?? true),

        // ★ 規格資料
        quantity: pick(p, "quantity", "Quantity") ?? null,
        minOrderQty: pick(p, "minimumOrderQuantity", "MinimumOrderQuantity") ?? null,
        maxOrderQty: pick(p, "maximumOrderQuantity", "MaximumOrderQuantity") ?? null,
        weight: pick(p, "weight", "Weight") ?? null,
        length: pick(p, "length", "Length") ?? null,
        width: pick(p, "width", "Width") ?? null,
        height: pick(p, "height", "Height") ?? null,
        description: pick(p, "description", "Description") ?? "",

        // 特價期間 / 發售日
        specialPriceStartDate: pick(p, "specialPriceStartDate", "SpecialPriceStartDate") ?? null,
        specialPriceEndDate: pick(p, "specialPriceEndDate", "SpecialPriceEndDate") ?? null,
        estimatedReleaseDate: pick(p, "estimatedReleaseDate", "EstimatedReleaseDate") ?? null,

        // ★ 後端改回傳 Images(string[])；保留舊欄位相容
        images: pick(p, "images", "Images") ?? (pick(p, "Image") ? [pick(p, "Image")] : [])
    };
    
    // 調試日誌
    console.log("轉換後的模型:", model);
    return model;
}

// 前端 VM → 後端 DTO（與 Controller 對齊）
function toApiPayload(vm) {
    // 將空字串轉為 null 的輔助函數
    const nullIfEmpty = (val) => val === "" ? null : val;
    
    // 將字串轉為數字的輔助函數（空字串轉為 null）
    const numberOrNull = (val) => {
        if (val === "" || val === null || val === undefined) return null;
        const num = Number(val);
        return isNaN(num) ? null : num;
    };
    
    const payload = {
        ProductName: vm.name,
        CategoryId: Number(vm.categoryId),
        BrandId: Number(vm.brandId),
        StatusId: Number(vm.statusId),
        Description: vm.description || "",
        BasePrice: Number(vm.basePrice || 0),
        SpecialPrice: numberOrNull(vm.salePrice),
        IsActive: !!vm.active,
        
        // 日期時間
        SpecialPriceStartDate: nullIfEmpty(vm.specialPriceStartDate),
        SpecialPriceEndDate: nullIfEmpty(vm.specialPriceEndDate),
        EstimatedReleaseDate: nullIfEmpty(vm.estimatedReleaseDate),
        
        // 規格資料
        Quantity: numberOrNull(vm.quantity),
        MinimumOrderQuantity: numberOrNull(vm.minOrderQty),
        MaximumOrderQuantity: numberOrNull(vm.maxOrderQty),
        Weight: numberOrNull(vm.weight),
        Length: numberOrNull(vm.length),
        Width: numberOrNull(vm.width),
        Height: numberOrNull(vm.height)
    };
    
    console.log("送出的資料：", payload);
    return payload;
}

// ===== API =====
async function apiGetList() {
    const res = await fetch(API_BASE, { headers: { Accept: "application/json" } });
    if (!res.ok) throw new Error(`GET ${res.status}`);
    const data = await res.json();
    return data.map(toViewModel);
}
async function apiCreate(vm) {
    const payload = toApiPayload(vm);
    console.log("新增商品發送數據:", payload);
    
    const res = await fetch(API_BASE, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(payload)
    });
    if (!res.ok) {
        const t = await res.text().catch(() => "");
        throw new Error(`POST ${res.status} ${t}`);
    }
    const created = await res.json();
    console.log("新增商品後端返回:", created);
    return toViewModel(created, 0);
}
async function apiUpdate(id, vm) {
    const payload = toApiPayload(vm);
    console.log("更新商品發送數據:", payload);
    
    const res = await fetch(`${API_BASE}/${id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(payload)
    });
    if (!res.ok) {
        const t = await res.text().catch(() => "");
        throw new Error(`PUT ${res.status} ${t}`);
    }
    const updated = await res.json();   // 後端已回傳更新後的資料
    console.log("更新商品後端返回:", updated);
    return toViewModel(updated, 0);
}
async function apiDelete(id) {
    const res = await fetch(`${API_BASE}/${id}`, { method: "DELETE" });
    if (!res.ok) throw new Error(`DELETE ${res.status}`);
}

// ===== DataTables =====
function initDataTable() {
    dt = $("#productTable").DataTable({
        dom: 't',           // 仍然只顯示表格，不顯示 DataTables 內建搜尋框
        paging: false,
        searching: true,    // ← 一定要 true，否則任何 .search() 都不會作用
        language: {
            search: "搜尋：",
            paginate: { previous: "上一頁", next: "下一頁" },
            zeroRecords: "找不到資料"
        },
        columnDefs: [{ targets: -1, orderable: false }],
        order: [[0, "desc"]]
    });

    $("#productTable tbody")
        .on("click", ".btn-edit", onEditClick)
        .on("click", ".btn-delete", onDeleteClick);
}

function renderTable() {
    const rows = products.map(p => ([
        p.id ?? "",
        p.name ?? "",
        p.category ?? "",
        p.brand ?? "",
        p.status ?? "",
        fmtPrice(p.basePrice, p.salePrice),
        p.active ? "是" : "否",
        fmtDate(p.estimatedReleaseDate), // ★ 這裡會把 API 回的日期轉 YYYY-MM-DD 顯示
        `
      <button class="btn btn-sm btn-info btn-edit" data-id="${p.id}">編輯</button>
      <button class="btn btn-sm btn-danger btn-delete" data-id="${p.id}">刪除</button>
    `
    ]));
    if (!dt) initDataTable();
    dt.clear();
    dt.rows.add(rows).draw();
}

// ===== 編輯彈窗 =====
function onEditClick() {
    const id = $(this).data("id");
    const p = products.find(x => String(x.id) === String(id));
    if (!p) {
        console.error(`找不到ID為 ${id} 的產品`);
        return;
    }
    
    console.log("正在編輯產品:", p); // 偵錯用
    const f = $("#productForm")[0];

    f.reset();
    
    // 清除舊的臨時圖片列表
    tempImageList = [];
    
    // 使用 ProductImageUploader 的 reset 函數重置上傳器狀態
    if (window.ProductImageUploader && typeof window.ProductImageUploader.reset === 'function') {
        window.ProductImageUploader.reset();
        console.log('已重置圖片上傳器狀態');
    } else {
        // 如果找不到重置函數，則手動清空預覽區域
        $("#imagePreviewList").empty();
        console.warn('無法使用 ProductImageUploader.reset 函數，已手動清空預覽區域');
    }

    // 基本資料
    f.ProductID && (f.ProductID.value = p.id ?? "");
    f.ProductName && (f.ProductName.value = p.name ?? "");
    f.SKU && (f.SKU.value = p.sku ?? "");
    f.BasePrice && (f.BasePrice.value = p.basePrice ?? 0);
    f.SalePrice && (f.SalePrice.value = p.salePrice ?? "");
    f.IsActive && (f.IsActive.value = p.active ? "1" : "0");
    f.Description && (f.Description.value = p.description ?? "");

    // 分類/品牌/狀態
    if (f.CategoryId) f.CategoryId.value = p.categoryId ?? "";
    if (f.BrandId) f.BrandId.value = p.brandId ?? "";
    if (f.StatusId) f.StatusId.value = p.statusId ?? "";

    // ★ 最小/最大量、重量、尺寸
    if (f.MinimumOrderQuantity) {
        f.MinimumOrderQuantity.value = p.minOrderQty ?? "";
        console.log("最小訂購量:", p.minOrderQty);
    }
    if (f.MaximumOrderQuantity) {
        f.MaximumOrderQuantity.value = p.maxOrderQty ?? "";
        console.log("最大訂購量:", p.maxOrderQty);
    }
    if (f.Quantity) {
        f.Quantity.value = p.quantity ?? "";
        console.log("庫存數量:", p.quantity);
    }
    if (f.Weight) {
        f.Weight.value = p.weight ?? "";
        console.log("重量:", p.weight);
    }
    if (f.Length) {
        f.Length.value = p.length ?? "";
        console.log("長度:", p.length);
    }
    if (f.Width) {
        f.Width.value = p.width ?? "";
        console.log("寬度:", p.width);
    }
    if (f.Height) {
        f.Height.value = p.height ?? "";
        console.log("高度:", p.height);
    }

    // 特價期間 - 格式化為 datetime-local 格式
    const startInput = getEl("input[name='SpecialPriceStartDate']");
    const endInput = getEl("input[name='SpecialPriceEndDate']");
    if (startInput) {
        startInput.value = toDatetimeLocal(p.specialPriceStartDate);
        console.log("特價開始時間:", p.specialPriceStartDate, "格式化後:", toDatetimeLocal(p.specialPriceStartDate));
    }
    if (endInput) {
        endInput.value = toDatetimeLocal(p.specialPriceEndDate);
        console.log("特價結束時間:", p.specialPriceEndDate, "格式化後:", toDatetimeLocal(p.specialPriceEndDate));
    }

    // 發售日 - 格式化為 YYYY-MM-DD 格式
    const est = getEl("input[name='EstimatedReleaseDate']");
    if (est) {
        est.value = fmtDate(p.estimatedReleaseDate);
        console.log("預計發售日:", p.estimatedReleaseDate, "格式化後:", fmtDate(p.estimatedReleaseDate));
    }

    // ★ 既有圖片預覽（主圖在前）
    if (Array.isArray(p.images) && p.images.length) {
        console.log("產品圖片:", p.images);
        // 使用新的圖片上傳器初始化圖片
        if (window.ProductImageUploader) {
            window.ProductImageUploader.initImages(p.images);
        } else {
            console.error("ProductImageUploader 模組未載入");
        }
    } else {
        // 清空圖片預覽區
        $("#imagePreviewList").empty();
        if (window.ProductImageUploader) {
            window.ProductImageUploader.initImages([]);
        }
    }

    bootstrap.Modal.getOrCreateInstance("#productModal").show();
}


async function onDeleteClick() {
    const id = $(this).data("id");
    if (!confirm("確定要刪除此商品嗎？")) return;
    try {
        await apiDelete(id);
        products = products.filter(x => String(x.id) !== String(id));
        renderTable();
    } catch (err) {
        console.error(err);
        alert("刪除失敗");
    }
}

// ===== 新增 =====
$(document).on("click", "#btnAdd", () => {
    console.log('點擊新增商品按鈕');
    // 重置表單
    const f = $("#productForm")[0];
    f.reset();
    // 確保產品ID為空
    if (f.ProductID) {
        f.ProductID.value = "";
    }
    
    // 清除舊的臨時圖片列表
    tempImageList = [];
    
    // 使用 ProductImageUploader 的 reset 函數重置上傳器狀態
    if (window.ProductImageUploader && typeof window.ProductImageUploader.reset === 'function') {
        window.ProductImageUploader.reset();
        console.log('已重置圖片上傳器狀態');
    } else {
        // 如果找不到重置函數，則手動清空預覽區域
        $("#imagePreviewList").empty();
        console.warn('無法使用 ProductImageUploader.reset 函數，已手動清空預覽區域');
    }
    
    // 顯示 Modal
    bootstrap.Modal.getOrCreateInstance("#productModal").show();
});

// 圖片上傳處理已移至 product-image-uploader.js
// 初始化圖片上傳器
$(document).ready(function() {
    if (window.ProductImageUploader) {
        window.ProductImageUploader.init("input[name='ProductImages']");
    } else {
        console.error("ProductImageUploader 模組未載入");
    }
});

// 取同名 input 中第一個有值的
function getFirstValueByName(name) {
    for (const el of qsa(`input[name='${name}']`)) {
        if (el && el.value && el.value.trim() !== "") return el.value;
    }
    return "";
}
// 將 YYYY-MM-DD 或 datetime-local → ISO（或 null）
const toIsoOrNull = (s) => {
    if (!s) return null;
    const d = new Date(s);
    return isNaN(d) ? null : d.toISOString();
};

// ===== 送出（Create / Update）=====
$(document).on("submit", "#productForm", async function (e) {
    e.preventDefault();

    // —— 取值（id 或 name 任一可）——
    const val = (idOrName) => {
        const byId = getEl(`#${idOrName}`);
        if (byId && byId.value != null) return byId.value;
        const byName = getEl(`[name='${idOrName}']`);
        return byName ? byName.value : "";
    };

    const id = val("ProductID");
    const name = (val("ProductName") || "").trim();
    const categoryId = Number(val("CategoryId") || 0);
    const brandId = Number(val("BrandId") || 0);
    const statusId = Number(val("StatusId") || 0);
    const base = Number(val("BasePrice") || 0);

    const saleRaw = (val("SalePrice") || "").trim();
    const sale = saleRaw === "" ? null : Number(saleRaw);
    const normalizedSale =
        (sale != null && isFinite(sale) && sale > 0 && sale < base)
            ? sale
            : (saleRaw === "" ? null : sale);

    // datetime-local（可能有兩個同名）
    const specialStartISO = toIsoOrNull(getFirstValueByName("SpecialPriceStartDate"));
    const specialEndISO = toIsoOrNull(getFirstValueByName("SpecialPriceEndDate"));
    const estDateISO = toIsoOrNull(val("EstimatedReleaseDate"));

    const isActive = (val("IsActive") === "1");
    const description = (val("Description") || "").trim();

    // 規則：填任一日期就必須有特價
    if ((!normalizedSale && (specialStartISO || specialEndISO))) {
        alert("若填特價期間，必須同時填特惠價");
        return;
    }
    // 兩個都有填才檢查先後
    if (specialStartISO && specialEndISO) {
        if (new Date(specialStartISO) >= new Date(specialEndISO)) {
            alert("特惠開始時間必須早於結束時間");
            return;
        }
    }

    const vm = {
        id,
        name,
        categoryId,
        brandId,
        statusId,
        basePrice: base,
        salePrice: normalizedSale,
        active: isActive,
        description,
        
        // 規格資料 - 直接取值，不進行轉換（在 toApiPayload 中處理）
        quantity: val("Quantity"),
        minOrderQty: val("MinimumOrderQuantity"),
        maxOrderQty: val("MaximumOrderQuantity"),
        weight: val("Weight"),
        length: val("Length"),
        width: val("Width"),
        height: val("Height"),
        
        // 日期時間 - 特價期間相關邏輯修改
        specialPriceStartDate: specialStartISO,
        specialPriceEndDate: specialEndISO,
        estimatedReleaseDate: estDateISO
    };
    
    console.log("表單收集的數據:", vm);

    console.log("VM that will be sent:", vm);

    if (!vm.name) { alert("請輸入商品名稱"); return; }
    if (!vm.categoryId || !vm.brandId || !vm.statusId) {
        alert("請選擇 分類/品牌/狀態（需為ID）"); return;
    }

    try {
        let productId = vm.id;
        
        if (!productId) {
            // 新增產品
            const created = await apiCreate(vm);
            products.push(created);
            productId = created.id;
        } else {
            // 更新產品
            const updated = await apiUpdate(vm.id, vm);
            const idx = products.findIndex(x => String(x.id) === String(vm.id));
            if (idx >= 0) products[idx] = updated;
        }
        
        // 上傳圖片（如果有）
        if (window.ProductImageUploader && window.ProductImageUploader.hasUnsaved() && productId) {
            await window.ProductImageUploader.upload(productId);
        }
        
        renderTable();
        dt.order([0, "desc"]).draw(false);
        bootstrap.Modal.getInstance(getEl("#productModal")).hide();
    } catch (err) {
        console.error(err);
        alert("儲存失敗\n" + (err?.message || ""));
    }
});

// ===== 初始化 =====
$(async function () {
    initDataTable();
    try {
        products = await apiGetList();
        renderTable();
        dt.order([0, "desc"]).draw(false);
    } catch (err) {
        console.error(err);
        alert("無法載入商品資料");
    }

    $("#btnSearch").on("click", () => dt.search($("#keyword").val() || "").draw());
    $("#pageSize").on("change", function () { dt.page.len(Number(this.value || 10)).draw(); });
});

// 監聽表單送出（按 Enter 或按下 type=submit 的搜尋鈕）
$(document).on("submit", "#productQueryForm", function (e) {
    e.preventDefault(); // 阻止預設送出、避免整頁重載
    const kw = $("#keyword").val() || "";
    if (dt) dt.search(kw).draw();
});

// （可留可刪）保留原本 click 也沒關係，兩邊都會觸發同樣的搜尋
$("#btnSearch").on("click", () => {
    const kw = $("#keyword").val() || "";
    if (dt) dt.search(kw).draw();
});

// 小保險：有些情況下按 Enter 可能不會觸發 submit，就手動觸發一次
$("#keyword").on("keydown", function (e) {
    if (e.key === "Enter") {
        e.preventDefault();
        $("#productQueryForm").trigger("submit");
    }
});
