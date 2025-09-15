// 模擬資料庫數據
let giftRules = [
    {
        id: 1,
        name: "夏季促銷",
        condition_type: "amount",
        condition_value: 1000,
        gift_product_id: 101,
        gift_quantity: 1,
        gift_image: "https://via.placeholder.com/40",
        start_date: "2025-06-01T00:00",
        end_date: "2025-08-31T23:59",
        created_at: "2025-05-01T10:00",
        is_deleted: false,
        is_active: true
    },
    {
        id: 2,
        name: "雙11活動",
        condition_type: "quantity",
        condition_value: 5,
        gift_product_id: 102,
        gift_quantity: 2,
        gift_image: "https://via.placeholder.com/40",
        start_date: "2025-11-01T00:00",
        end_date: "2025-11-11T23:59",
        created_at: "2025-10-01T10:00",
        is_deleted: false,
        is_active: true
    }
];

let currentPage = 1;
let perPage = 10;

// 格式化日期
const formatDate = (dateStr) => {
    if (!dateStr) return "-";
    const date = new Date(dateStr);
    return date.toLocaleString('zh-TW', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
    });
};

// 獲取狀態
const getStatus = (rule) => {
    if (rule.is_deleted) return { text: "已刪除", class: "status-deleted" };
    if (!rule.is_active) return { text: "已下架", class: "status-deleted" };
    const now = new Date();
    const start = rule.start_date ? new Date(rule.start_date) : null;
    const end = rule.end_date ? new Date(rule.end_date) : null;
    if (end && now > end) return { text: "已過期", class: "status-expired" };
    if (start && now < start) return { text: "已排程", class: "status-scheduled" };
    return { text: "上架中", class: "status-active" };
};

// 填充表格
const populateTable = (rules) => {
    const tbody = document.getElementById("giftRulesTableBody");
    tbody.innerHTML = "";
    const start = (currentPage - 1) * perPage;
    const end = start + perPage;
    const paginatedRules = rules.slice(start, end);

    paginatedRules.forEach(rule => {
        const status = getStatus(rule);
        const row = document.createElement("tr");
        row.innerHTML = `
                    <td>${rule.id}</td>
                    <td>${rule.name}</td>
                    <td>${rule.condition_type === 'amount' ? '訂單金額' : '訂單數量'}</td>
                    <td>${rule.condition_value}</td>
                    <td>${rule.gift_product_id}</td>
                    <td>${rule.gift_quantity}</td>
                    <td>${rule.gift_image ? `<img src="${rule.gift_image}" class="thumbnail" alt="贈品圖片">` : '-'}</td>
                    <td>${formatDate(rule.start_date)}</td>
                    <td>${formatDate(rule.end_date)}</td>
                    <td><span class="${status.class}">${status.text}</span></td>
                    <td>
                        <div class="action-buttons">
                            <button class="btn btn-sm btn-primary edit-btn" data-id="${rule.id}" ${rule.is_deleted ? 'disabled' : ''}>編輯</button>
                            <button class="btn btn-sm ${rule.is_active ? 'btn-toggle-off' : 'btn-toggle'} toggle-btn" data-id="${rule.id}" ${rule.is_deleted ? 'disabled' : ''}>
                                ${rule.is_active ? '下架' : '上架'}
                            </button>
                            <button class="btn btn-sm btn-danger delete-btn" data-id="${rule.id}" ${rule.is_deleted ? 'disabled' : ''}>刪除</button>
                        </div>
                    </td>
                `;
        tbody.appendChild(row);
    });

    // 更新總計資料筆數與分頁資訊
    document.getElementById("totalRecords").textContent = rules.length;
    const totalPages = Math.ceil(rules.length / perPage);
    document.getElementById("currentPageDisplay").textContent = currentPage;
    document.querySelector(".pagination .page-item:first-child a").classList.toggle("disabled", currentPage === 1);
    document.querySelector(".pagination .page-item:nth-child(2) a").classList.toggle("disabled", currentPage === 1);
    document.querySelector(".pagination .page-item:nth-child(4) a").classList.toggle("disabled", currentPage === totalPages);
    document.querySelector(".pagination .page-item:last-child a").classList.toggle("disabled", currentPage === totalPages);
};

// 篩選與搜索規則
const filterRules = () => {
    const status = document.getElementById("statusFilter").value;
    const searchField = document.getElementById("searchField").value;
    const searchKeyword = document.getElementById("searchKeyword").value.toLowerCase();
    const startDateRange = document.getElementById("startDateRange").value;
    const endDateRange = document.getElementById("endDateRange").value;

    let filteredRules = giftRules;

    // 關鍵字搜索
    if (searchKeyword) {
        filteredRules = filteredRules.filter(rule => {
            const value = rule[searchField].toString().toLowerCase();
            return value.includes(searchKeyword);
        });
    }

    // 時間範圍篩選
    if (startDateRange || endDateRange) {
        filteredRules = filteredRules.filter(rule => {
            const ruleStart = rule.start_date ? new Date(rule.start_date) : null;
            const ruleEnd = rule.end_date ? new Date(rule.end_date) : null;
            const filterStart = startDateRange ? new Date(startDateRange) : null;
            const filterEnd = endDateRange ? new Date(endDateRange) : null;

            if (filterStart && filterEnd) {
                return ruleStart >= filterStart && ruleEnd <= filterEnd;
            } else if (filterStart) {
                return ruleStart >= filterStart;
            } else if (filterEnd) {
                return ruleEnd <= filterEnd;
            }
            return true;
        });
    }

    // 狀態篩選
    if (status) {
        filteredRules = filteredRules.filter(rule => {
            if (status === "deleted") return rule.is_deleted;
            if (status === "expired") {
                const end = rule.end_date ? new Date(rule.end_date) : null;
                return !rule.is_deleted && end && new Date() > end;
            }
            if (status === "active") {
                const start = rule.start_date ? new Date(rule.start_date) : null;
                const end = rule.end_date ? new Date(rule.end_date) : null;
                const now = new Date();
                return !rule.is_deleted && rule.is_active && (!start || now >= start) && (!end || now <= end);
            }
            if (status === "scheduled") {
                const start = rule.start_date ? new Date(rule.start_date) : null;
                return !rule.is_deleted && start && new Date() < start;
            }
            return true;
        });
    }

    currentPage = 1; // 重置到第一頁
    populateTable(filteredRules);
};

// 更改頁面
const changePage = (page) => {
    const totalPages = Math.ceil(giftRules.length / perPage);
    if (page >= 1 && page <= totalPages) {
        currentPage = page;
        filterRules();
    }
};

// 更改每頁顯示筆數
const changePerPage = () => {
    perPage = parseInt(document.getElementById("perPage").value);
    currentPage = 1; // 重置到第一頁
    filterRules();
};

// 圖片上傳預覽
const previewImage = (file) => {
    const preview = document.getElementById("imagePreview");
    if (file) {
        const reader = new FileReader();
        reader.onload = (e) => {
            preview.src = e.target.result;
            preview.classList.remove("d-none");
        };
        reader.readAsDataURL(file);
    } else {
        preview.src = "";
        preview.classList.add("d-none");
    }
};

// 儲存規則
const saveRule = (e) => {
    e.preventDefault();
    const startDate = new Date(document.getElementById("startDate").value);
    const endDate = new Date(document.getElementById("endDate").value);
    if (endDate <= startDate) {
        alert("活動結束時間必須晚於開始時間");
        return;
    }
    const giftImageInput = document.getElementById("giftImage");
    const giftImage = giftImageInput.files[0] ? URL.createObjectURL(giftImageInput.files[0]) : document.getElementById("imagePreview").src;
    const rule = {
        id: document.getElementById("ruleId").value || giftRules.length + 1,
        name: document.getElementById("name").value,
        condition_type: document.getElementById("conditionType").value,
        condition_value: parseFloat(document.getElementById("conditionValue").value),
        gift_product_id: parseInt(document.getElementById("giftProductId").value),
        gift_quantity: parseInt(document.getElementById("giftQuantity").value),
        gift_image: giftImage || null,
        start_date: document.getElementById("startDate").value,
        end_date: document.getElementById("endDate").value,
        created_at: new Date().toISOString(),
        is_deleted: false,
        is_active: true
    };

    if (document.getElementById("ruleId").value) {
        const index = giftRules.findIndex(r => r.id == rule.id);
        giftRules[index] = rule;
    } else {
        giftRules.push(rule);
    }

    populateTable(giftRules);
    bootstrap.Modal.getInstance(document.getElementById("giftRuleModal")).hide();
    document.getElementById("giftRuleForm").reset();
    document.getElementById("ruleId").value = "";
    document.getElementById("imagePreview").src = "";
    document.getElementById("imagePreview").classList.add("d-none");
    document.getElementById("giftRuleModalLabel").textContent = "新增贈品規則";
};

// 編輯規則
const editRule = (id) => {
    const rule = giftRules.find(r => r.id == id);
    document.getElementById("ruleId").value = rule.id;
    document.getElementById("name").value = rule.name;
    document.getElementById("conditionType").value = rule.condition_type;
    document.getElementById("conditionValue").value = rule.condition_value;
    document.getElementById("giftProductId").value = rule.gift_product_id;
    document.getElementById("giftQuantity").value = rule.gift_quantity;
    document.getElementById("giftImage").value = "";
    const preview = document.getElementById("imagePreview");
    if (rule.gift_image) {
        preview.src = rule.gift_image;
        preview.classList.remove("d-none");
    } else {
        preview.src = "";
        preview.classList.add("d-none");
    }
    document.getElementById("startDate").value = rule.start_date ? rule.start_date.slice(0, 16) : "";
    document.getElementById("endDate").value = rule.end_date ? rule.end_date.slice(0, 16) : "";
    document.getElementById("giftRuleModalLabel").textContent = "編輯贈品規則";
    bootstrap.Modal.getOrCreateInstance(document.getElementById("giftRuleModal")).show();
};

// 上下架規則
const toggleRule = (id) => {
    const rule = giftRules.find(r => r.id == id);
    rule.is_active = !rule.is_active;
    rule.updated_at = new Date().toISOString();
    filterRules();
};

// 刪除規則
const deleteRule = (id) => {
    if (confirm("確定要刪除此贈品規則嗎？")) {
        const rule = giftRules.find(r => r.id == id);
        rule.is_deleted = true;
        rule.is_active = false;
        rule.deleted_at = new Date().toISOString();
        filterRules();
    }
};

// 事件監聽
document.getElementById("giftRuleForm").addEventListener("submit", saveRule);
document.getElementById("statusFilter").addEventListener("change", filterRules);
document.getElementById("startDateRange").addEventListener("change", filterRules);
document.getElementById("endDateRange").addEventListener("change", filterRules);
document.getElementById("perPage").addEventListener("change", changePerPage);
document.getElementById("giftImage").addEventListener("change", (e) => {
    previewImage(e.target.files[0]);
});
document.addEventListener("click", (e) => {
    if (e.target.classList.contains("edit-btn")) {
        editRule(parseInt(e.target.dataset.id));
    } else if (e.target.classList.contains("delete-btn")) {
        deleteRule(parseInt(e.target.dataset.id));
    } else if (e.target.classList.contains("toggle-btn")) {
        toggleRule(parseInt(e.target.dataset.id));
    }
});

// 初始化表格
populateTable(giftRules);