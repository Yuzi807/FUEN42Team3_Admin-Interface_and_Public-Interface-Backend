// wwwroot/js/brand.api.js
// 建議：與上面同理，避免和 CSHTML 的原生版本重覆

let brands = [];

$(document).ready(function () {
    fetchBrands();
    $('#btnAddBrand').on('click', () => openBrandModal());
    $('#itemForm').on('submit', function (e) { e.preventDefault(); handleSaveBrand(); });
    $('#itemImage').on('change', function () {
        const file = this.files?.[0];
        if (!file) return;
        const reader = new FileReader();
        reader.onload = e => $('#itemPreview').attr('src', e.target.result).removeClass('d-none');
        reader.readAsDataURL(file);
    });
});

async function fetchBrands() {
    const res = await fetch('/api/BrandsAPI', { headers: { "Accept": "application/json" } });
    if (!res.ok) { alert('讀取品牌失敗'); return; }
    const raw = await res.json();
    const items = Array.isArray(raw) ? raw : (raw.items ?? raw.data ?? raw.records ?? []);
    brands = items;
    renderBrandTable();
}

function renderBrandTable() {
    const tbody = $('#brandTable tbody');
    tbody.empty();
    brands.forEach((b, i) => {
        const id = b.id ?? b.Id;
        const name = b.brandName ?? b.BrandName ?? b.name ?? '';
        const isActive = (typeof b.isActive === "boolean") ? b.isActive : (b.IsActive ?? false);
        const logo = b.logoUrl ?? b.LogoUrl ?? b.imagePath ?? b.ImagePath ?? '/images/no-image.png';
        const row = `
      <tr class="${isActive ? '' : 'table-secondary'}">
        <td>${i + 1}</td>
        <td><img src="${logo}" style="width:50px;border-radius:4px"/></td>
        <td>${name}</td>
        <td>${isActive ? '啟用' : '停用'}</td>
        <td>
          <button class="btn btn-sm btn-warning me-1" onclick="editBrand(${id})">編輯</button>
          <button class="btn btn-sm btn-danger" onclick="deleteBrand(${id})">刪除</button>
        </td>
      </tr>`;
        tbody.append(row);
    });
}

function openBrandModal() {
    $('#itemType').val('brand');
    $('#itemId').val('');
    $('#itemName').val('');
    $('#itemImage').val('');
    $('#itemPreview').attr('src', '#').addClass('d-none');
    $('#itemModalLabel').text('新增品牌');
    $('#itemModal').modal('show');
}

function editBrand(id) {
    const b = brands.find(x => (x.id ?? x.Id) === id);
    if (!b) return;
    $('#itemType').val('brand');
    $('#itemId').val(b.id ?? b.Id);
    $('#itemName').val(b.brandName ?? b.BrandName ?? b.name ?? '');
    $('#itemPreview').attr('src', (b.logoUrl ?? b.LogoUrl ?? b.imagePath ?? b.ImagePath ?? '/images/no-image.png')).removeClass('d-none');
    $('#itemModalLabel').text('編輯品牌');
    $('#itemModal').modal('show');
}

async function handleSaveBrand() {
    const id = $('#itemId').val();
    const data = {
        brandName: $('#itemName').val(),
        logoUrl: '', // 圖片上傳之後再補
        isActive: true,
        createdBy: 1,
        updatedBy: 1
    };

    let res;
    if (id) {
        res = await fetch(`/api/BrandsAPI/${id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
            body: JSON.stringify(data)
        });
    } else {
        res = await fetch('/api/BrandsAPI', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
            body: JSON.stringify(data)
        });
    }

    if (res.ok) {
        $('#itemModal').modal('hide');
        fetchBrands();
    } else {
        alert('儲存失敗');
    }
}

async function deleteBrand(id) {
    if (!confirm('確定要刪除這個品牌嗎？')) return;
    const res = await fetch(`/api/BrandsAPI/${id}`, { method: 'DELETE' });
    if (res.ok) fetchBrands(); else alert('刪除失敗');
}
