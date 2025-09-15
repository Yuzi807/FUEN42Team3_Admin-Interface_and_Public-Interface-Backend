// wwwroot/js/category.api.js
// 建議：若已在 CategoryList.cshtml 內寫好載入/渲染，就不要再同時載入本檔

let categories = [];

$(document).ready(function () {
    fetchCategories();
    $('#btnSaveCategory').on('click', handleSaveCategory);
});

async function fetchCategories() {
    const res = await fetch('/api/CategoriesAPI', { headers: { "Accept": "application/json" } });
    if (!res.ok) { alert('讀取分類失敗'); return; }
    const raw = await res.json();
    const items = Array.isArray(raw) ? raw : (raw.items ?? raw.data ?? raw.records ?? []);
    categories = items;
    renderCategoryTable();
}

function renderCategoryTable() {
    const tbody = $('#categoryTable tbody');
    tbody.empty();
    categories.forEach((c, i) => {
        const id = c.id ?? c.Id;
        const name = c.categoryName ?? c.CategoryName ?? c.name ?? '';
        const desc = c.description ?? c.Description ?? '';
        const isActive = (typeof c.isActive === "boolean") ? c.isActive : (c.IsActive ?? false);
        const row = `
      <tr>
        <td>${i + 1}</td>
        <td>${name}</td>
        <td>${desc}</td>
        <td>${isActive ? '啟用' : '停用'}</td>
        <td>
          <button class="btn btn-sm btn-warning me-1" onclick="editCategory(${id})">編輯</button>
          <button class="btn btn-sm btn-danger" onclick="deleteCategory(${id})">刪除</button>
        </td>
      </tr>`;
        tbody.append(row);
    });
}

function newCategory() {
    $('#categoryId').val('');
    $('#categoryName').val('');
    $('#categoryDescription').val('');
    $('#categoryIsActive').prop('checked', true);
    $('#categoryModalLabel').text('新增分類');
    $('#categoryModal').modal('show');
}

function editCategory(id) {
    const c = categories.find(x => (x.id ?? x.Id) === id);
    if (!c) return;
    $('#categoryId').val(c.id ?? c.Id);
    $('#categoryName').val(c.categoryName ?? c.CategoryName ?? c.name ?? '');
    $('#categoryDescription').val(c.description ?? c.Description ?? '');
    $('#categoryIsActive').prop('checked', (c.isActive ?? c.IsActive ?? false));
    $('#categoryModalLabel').text('編輯分類');
    $('#categoryModal').modal('show');
}

async function handleSaveCategory() {
    const id = $('#categoryId').val();
    const data = {
        categoryName: $('#categoryName').val(),
        description: $('#categoryDescription').val(),
        isActive: $('#categoryIsActive').prop('checked'),
        createdBy: 1,
        updatedBy: 1
    };

    let res;
    if (id) {
        res = await fetch(`/api/CategoriesAPI/${id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
            body: JSON.stringify(data)
        });
    } else {
        res = await fetch('/api/CategoriesAPI', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
            body: JSON.stringify(data)
        });
    }

    if (res.ok) {
        $('#categoryModal').modal('hide');
        fetchCategories();
    } else {
        alert('儲存失敗');
    }
}

async function deleteCategory(id) {
    if (!confirm('確定要刪除這個分類嗎？')) return;
    const res = await fetch(`/api/CategoriesAPI/${id}`, { method: 'DELETE' });
    if (res.ok) fetchCategories(); else alert('刪除失敗');
}
