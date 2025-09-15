// WebAssembly polyfill for browsers that don't support BarcodeDetector natively
if (!('BarcodeDetector' in window)) {
    (async () => {
        const { BarcodeDetectorPolyfill } = await import('https://cdn.jsdelivr.net/npm/@undecaf/barcode-detector@0.2.0/dist/index.js');
        window.BarcodeDetector = BarcodeDetectorPolyfill;
    })();
}

// 假設訂單和產品數據，純前端模擬
let orders = [
    {
        id: 1,
        user_id: 101,
        total_amount: 1500,
        status: '待處理',
        order_date: '2025-07-01T10:00:00',
        recipient_name: '張三',
        phone: '0912345678',
        address: '台北市中正區1號',
        payment_method_id: '信用卡',
        delivery_method_id: '宅配',
        shipping_fee: 100,
        used_points: 0,
        details: [
            { product_id: 1, product_name_snapshot: '產品 A', quantity: 2, unit_price_snapshot: 500, discount_amount: 0 },
            { product_id: 2, product_name_snapshot: '產品 B', quantity: 1, unit_price_snapshot: 1000, discount_amount: 100 }
        ],
        gifts: [{ gift_product_id: 3, quantity: 1, gift_rule_id: 1 }]
    },
    {
        id: 2,
        user_id: 102,
        total_amount: 2300,
        status: '已出貨',
        order_date: '2025-07-02T14:30:00',
        recipient_name: '李四',
        phone: '0987654321',
        address: '新北市板橋區2號',
        payment_method_id: 'LINE Pay',
        delivery_method_id: '7-11 取貨',
        shipping_fee: 60,
        used_points: 50,
        details: [
            { product_id: 3, product_name_snapshot: '產品 C', quantity: 3, unit_price_snapshot: 600, discount_amount: 50 }
        ],
        gifts: []
    }
];
let products = [
    { id: 1, name: '產品 A', price: 500 },
    { id: 2, name: '產品 B', price: 1000 },
    { id: 3, name: '產品 C', price: 600 }
];

let itemsPerPage = 10;
let currentPage = 1;

// 渲染訂單列表
function renderOrders(ordersToRender) {
    const tbody = document.getElementById('orderTableBody');
    tbody.innerHTML = '';
    const start = (currentPage - 1) * itemsPerPage;
    const end = start + itemsPerPage;
    const paginatedOrders = ordersToRender.slice(start, end);

    paginatedOrders.forEach(order => {
        const row = document.createElement('tr');
        row.innerHTML = `
          <td>${order.id}</td>
          <td>${order.user_id}</td>
          <td>${order.total_amount}</td>
          <td>
            <select class="form-select status-select" data-id="${order.id}">
              <option value="待處理" ${order.status === '待處理' ? 'selected' : ''}>待處理</option>
              <option value="已出貨" ${order.status === '已出貨' ? 'selected' : ''}>已出貨</option>
              <option value="已完成" ${order.status === '已完成' ? 'selected' : ''}>已完成</option>
              <option value="已取消" ${order.status === '已取消' ? 'selected' : ''}>已取消</option>
            </select>
          </td>
          <td>${new Date(order.order_date).toLocaleString('zh-TW')}</td>
          <td>${order.recipient_name}</td>
          <td>${order.phone}</td>
          <td>
            <button class="btn btn-sm btn-outline-primary action-btn details-btn" data-id="${order.id}">詳情</button>
            <button class="btn btn-sm btn-outline-primary action-btn edit-btn" data-id="${order.id}">編輯</button>
            <button class="btn btn-sm btn-outline-danger action-btn delete-btn" data-id="${order.id}">刪除</button>
          </td>
        `;
        tbody.appendChild(row);

        // 訂單詳情（包含訂購內容和贈品）
        const detailsRow = document.createElement('tr');
        detailsRow.className = 'details-row';
        detailsRow.id = `details-${order.id}`;
        detailsRow.innerHTML = `
          <td colspan="8">
            <div class="p-3">
              <h6>訂購內容</h6>
              <table class="table table-sm">
                <thead>
                  <tr>
                    <th>類型</th>
                    <th>名稱</th>
                    <th>數量</th>
                    <th>單價</th>
                    <th>折扣</th>
                  </tr>
                </thead>
                <tbody>
                  ${order.details.map(detail => `
                    <tr>
                      <td>產品</td>
                      <td>${detail.product_name_snapshot}</td>
                      <td>${detail.quantity}</td>
                      <td>${detail.unit_price_snapshot}</td>
                      <td>${detail.discount_amount}</td>
                    </tr>
                  `).join('')}
                  ${order.gifts.map(gift => `
                    <tr>
                      <td>贈品</td>
                      <td>${products.find(p => p.id === gift.gift_product_id)?.name || '未知'}</td>
                      <td>${gift.quantity}</td>
                      <td>-</td>
                      <td>-</td>
                    </tr>
                  `).join('')}
                </tbody>
              </table>
            </div>
          </td>
        `;
        tbody.appendChild(detailsRow);
    });

    // 綁定事件
    document.querySelectorAll('.status-select').forEach(select => {
        select.addEventListener('change', handleStatusChange);
    });
    document.querySelectorAll('.details-btn').forEach(btn => {
        btn.addEventListener('click', handleDetails);
    });
    document.querySelectorAll('.edit-btn').forEach(btn => {
        btn.addEventListener('click', handleEdit);
    });
    document.querySelectorAll('.delete-btn').forEach(btn => {
        btn.addEventListener('click', handleDelete);
    });

    // 更新頁數和總筆數顯示
    const totalPages = Math.ceil(ordersToRender.length / itemsPerPage);
    document.getElementById('pageInfo').textContent = `第 ${currentPage} 頁 / 共 ${totalPages} 頁`;
    document.getElementById('totalItems').textContent = `總計 ${ordersToRender.length} 筆資料`;

    // 控制按鈕狀態
    document.getElementById('firstPage').disabled = currentPage === 1;
    document.getElementById('prevPage').disabled = currentPage === 1;
    document.getElementById('nextPage').disabled = currentPage === totalPages;
    document.getElementById('lastPage').disabled = currentPage === totalPages;
}

// 顯示/隱藏訂單詳情
function handleDetails(e) {
    const orderId = e.target.dataset.id;
    const detailsRow = document.getElementById(`details-${orderId}`);
    detailsRow.style.display = detailsRow.style.display === 'table-row' ? 'none' : 'table-row';
}

// 篩選和搜尋
function filterOrders() {
    const status = document.getElementById('statusFilter').value;
    const searchField = document.getElementById('searchField').value;
    const searchTerm = document.getElementById('searchInput').value.toLowerCase();
    const dateStart = document.getElementById('dateStart').value;
    const dateEnd = document.getElementById('dateEnd').value;

    const filtered = orders.filter(order => {
        const matchesStatus = status === '全部' || order.status === status;
        const matchesSearch = searchTerm ? (
            searchField === 'id' ? order.id.toString().includes(searchTerm) :
                searchField === 'user_id' ? order.user_id.toString().includes(searchTerm) :
                    searchField === 'recipient_name' ? order.recipient_name.toLowerCase().includes(searchTerm) :
                        order.phone.toLowerCase().includes(searchTerm)
        ) : true;
        const matchesDate = dateStart && dateEnd ? (
            new Date(order.order_date) >= new Date(dateStart) &&
            new Date(order.order_date) <= new Date(dateEnd)
        ) : true;
        return matchesStatus && matchesSearch && matchesDate;
    });
    currentPage = 1; // 篩選後回到第一頁
    renderOrders(filtered);
}

// 處理狀態更改
function handleStatusChange(e) {
    const orderId = e.target.dataset.id;
    const newStatus = e.target.value;
    const order = orders.find(o => o.id == orderId);
    if (order) {
        order.status = newStatus;
        filterOrders();
    }
}

// 處理編輯
function handleEdit(e) {
    const orderId = e.target.dataset.id;
    const order = orders.find(o => o.id == orderId);
    const modal = new bootstrap.Modal(document.getElementById('orderModal'));
    document.getElementById('orderModalLabel').textContent = '編輯訂單';
    document.getElementById('userId').value = order.user_id;
    document.getElementById('totalAmount').value = order.total_amount;
    document.getElementById('status').value = order.status;
    document.getElementById('paymentMethod').value = order.payment_method_id;
    document.getElementById('deliveryMethod').value = order.delivery_method_id;
    document.getElementById('shippingFee').value = order.shipping_fee;
    document.getElementById('usedPoints').value = order.used_points;
    document.getElementById('recipientName').value = order.recipient_name;
    document.getElementById('address').value = order.address;
    document.getElementById('phone').value = order.phone;
    document.getElementById('submitOrder').dataset.id = orderId;

    // 載入訂購內容
    const orderItems = document.getElementById('orderItems');
    orderItems.innerHTML = '';
    order.details.forEach(detail => {
        addOrderItem(detail.product_id, detail.quantity, detail.unit_price_snapshot);
    });

    // 載入贈品
    const giftItems = document.getElementById('giftItems');
    giftItems.innerHTML = '';
    order.gifts.forEach(gift => {
        addGiftItem(gift.gift_product_id, gift.quantity);
    });

    // 確保至少有一個空的訂購內容和贈品輸入框
    if (orderItems.children.length === 0 || !orderItems.querySelector('.order-item')) {
        addOrderItem();
    }
    if (giftItems.children.length === 0 || !giftItems.querySelector('.gift-item')) {
        addGiftItem();
    }

    modal.show();
    bindRemoveItemEvents();
    bindRemoveGiftEvents();
}

// 新增訂購項目
function addOrderItem(productId = '', quantity = '', unitPrice = '') {
    const orderItems = document.getElementById('orderItems');
    const item = document.createElement('div');
    item.className = 'order-item mb-2';
    item.innerHTML = `
        <div class="row">
          <div class="col-md-4">
            <select class="form-select product-select" required>
              <option value="">選擇產品</option>
              ${products.map(p => `<option value="${p.id}" ${p.id === productId ? 'selected' : ''}>${p.name}</option>`).join('')}
            </select>
          </div>
          <div class="col-md-3">
            <input type="number" class="form-control quantity" placeholder="數量" value="${quantity}" min="1" required>
          </div>
          <div class="col-md-3">
            <input type="number" class="form-control unit-price" placeholder="單價" value="${unitPrice}" min="0" required>
          </div>
          <div class="col-md-2">
            <button type="button" class="btn btn-sm btn-danger remove-item">移除</button>
          </div>
        </div>
      `;
    orderItems.appendChild(item);
    bindRemoveItemEvents();
}

document.getElementById('addItem').addEventListener('click', () => {
    addOrderItem();
});

// 新增贈品項目
function addGiftItem(giftProductId = '', quantity = '') {
    const giftItems = document.getElementById('giftItems');
    const item = document.createElement('div');
    item.className = 'gift-item mb-2';
    item.innerHTML = `
        <div class="row">
          <div class="col-md-4">
            <select class="form-select gift-select">
              <option value="">選擇贈品</option>
              ${products.map(p => `<option value="${p.id}" ${p.id === giftProductId ? 'selected' : ''}>${p.name}</option>`).join('')}
            </select>
          </div>
          <div class="col-md-3">
            <input type="number" class="form-control gift-quantity" placeholder="數量" value="${quantity}" min="1">
          </div>
          <div class="col-md-2">
            <button type="button" class="btn btn-sm btn-danger remove-gift">移除</button>
          </div>
        </div>
      `;
    giftItems.appendChild(item);
    bindRemoveGiftEvents();
}

document.getElementById('addGift').addEventListener('click', () => {
    addGiftItem();
});

// 綁定移除訂購項目事件
function bindRemoveItemEvents() {
    document.querySelectorAll('.remove-item').forEach(btn => {
        btn.removeEventListener('click', handleRemoveItem);
        btn.addEventListener('click', handleRemoveItem);
    });
}

function handleRemoveItem(e) {
    e.target.closest('.order-item').remove();
}

// 綁定移除贈品項目事件
function bindRemoveGiftEvents() {
    document.querySelectorAll('.remove-gift').forEach(btn => {
        btn.removeEventListener('click', handleRemoveGift);
        btn.addEventListener('click', handleRemoveGift);
    });
}

function handleRemoveGift(e) {
    e.target.closest('.gift-item').remove();
}

// 處理刪除
function handleDelete(e) {
    if (confirm('確定要刪除此訂單？')) {
        const orderId = e.target.dataset.id;
        const index = orders.findIndex(o => o.id == orderId);
        if (index !== -1) {
            orders.splice(index, 1);
            filterOrders();
        }
    }
}

// 處理表單提交
document.getElementById('submitOrder').addEventListener('click', () => {
    const form = document.getElementById('orderForm');
    if (form.checkValidity()) {
        const orderData = {
            id: orders.length > 0 ? Math.max(...orders.map(o => o.id)) + 1 : 1,
            user_id: document.getElementById('userId').value,
            total_amount: document.getElementById('totalAmount').value,
            status: document.getElementById('status').value,
            order_date: new Date().toISOString(),
            payment_method_id: document.getElementById('paymentMethod').value,
            delivery_method_id: document.getElementById('deliveryMethod').value,
            shipping_fee: document.getElementById('shippingFee').value,
            used_points: document.getElementById('usedPoints').value,
            recipient_name: document.getElementById('recipientName').value,
            address: document.getElementById('address').value,
            phone: document.getElementById('phone').value,
            details: Array.from(document.querySelectorAll('.order-item')).map(item => ({
                product_id: item.querySelector('.product-select').value,
                product_name_snapshot: products.find(p => p.id == item.querySelector('.product-select').value)?.name || '',
                quantity: item.querySelector('.quantity').value,
                unit_price_snapshot: item.querySelector('.unit-price').value,
                discount_amount: 0
            })).filter(item => item.product_id),
            gifts: Array.from(document.querySelectorAll('.gift-item')).map(item => ({
                gift_product_id: item.querySelector('.gift-select').value,
                quantity: item.querySelector('.gift-quantity').value || 0,
                gift_rule_id: 1
            })).filter(gift => gift.gift_product_id && gift.quantity > 0)
        };
        try {
            const orderId = document.getElementById('submitOrder').dataset.id;
            if (orderId) {
                const order = orders.find(o => o.id == orderId);
                Object.assign(order, orderData);
                delete order.id;
            } else {
                orders.push(orderData);
            }
            filterOrders();
            bootstrap.Modal.getInstance(document.getElementById('orderModal')).hide();
            form.reset();
            document.getElementById('orderItems').innerHTML = '';
            document.getElementById('giftItems').innerHTML = '';
            addOrderItem();
            addGiftItem();
            document.getElementById('orderModalLabel').textContent = '新增訂單';
            delete document.getElementById('submitOrder').dataset.id;
        } catch (error) {
            console.error('Error saving order:', error);
        }
    } else {
        form.reportValidity();
    }
});

// 處理條碼掃描
let barcodeDetector, video, stream;
async function startBarcodeScanner() {
    try {
        const video = document.querySelector('#barcodeScanner video');
        if (!('BarcodeDetector' in window)) {
            throw new Error('BarcodeDetector API 不支援，請使用支援的瀏覽器（如 Chrome、Edge 或 Firefox）。');
        }

        const formats = ['ean_13', 'ean_8', 'upc_a', 'upc_e', 'code_128'];
        barcodeDetector = new BarcodeDetector({ formats });

        stream = await navigator.mediaDevices.getUserMedia({
            video: { facingMode: 'environment' }
        });
        video.srcObject = stream;
        document.getElementById('barcodeStatusText').textContent = '掃描中...';

        const scanLoop = async () => {
            if (video.readyState === video.HAVE_ENOUGH_DATA) {
                const barcodes = await barcodeDetector.detect(video);
                if (barcodes.length > 0) {
                    const barcode = barcodes[0].rawValue;
                    const order = orders.find(o => o.id.toString() === barcode);
                    if (order) {
                        document.getElementById('barcodeStatusText').textContent = `掃描到訂單 ${barcode}，請選擇狀態並提交。`;
                        document.getElementById('submitBarcode').dataset.barcode = barcode;
                    } else {
                        document.getElementById('barcodeStatusText').textContent = `無效條碼 ${barcode}，請重試。`;
                        delete document.getElementById('submitBarcode').dataset.barcode;
                    }
                }
            }
            if (document.getElementById('barcodeModal').classList.contains('show')) {
                requestAnimationFrame(scanLoop);
            }
        };
        scanLoop();
    } catch (error) {
        document.getElementById('barcodeStatusText').textContent = `掃描錯誤: ${error.message}`;
        console.error('Barcode scanning error:', error);
    }
}

document.getElementById('barcodeModal').addEventListener('shown.bs.modal', startBarcodeScanner);
document.getElementById('barcodeModal').addEventListener('hidden.bs.modal', () => {
    if (stream) {
        stream.getTracks().forEach(track => track.stop());
        video.srcObject = null;
        document.getElementById('barcodeStatusText').textContent = '對準條碼進行掃描...';
        delete document.getElementById('submitBarcode').dataset.barcode;
    }
});

document.getElementById('submitBarcode').addEventListener('click', () => {
    const barcode = document.getElementById('submitBarcode').dataset.barcode;
    const status = document.getElementById('barcodeStatus').value;
    if (barcode) {
        const order = orders.find(o => o.id.toString() === barcode);
        if (order) {
            order.status = status;
            alert(`訂單 ${barcode} 狀態已更新為 ${status}`);
            filterOrders();
        }
        bootstrap.Modal.getInstance(document.getElementById('barcodeModal')).hide();
    } else {
        alert('請先掃描有效的條碼');
    }
});

// 處理分頁
document.getElementById('firstPage').addEventListener('click', () => {
    currentPage = 1;
    filterOrders();
});

document.getElementById('prevPage').addEventListener('click', () => {
    if (currentPage > 1) {
        currentPage--;
        filterOrders();
    }
});

document.getElementById('nextPage').addEventListener('click', () => {
    const totalPages = Math.ceil(orders.length / itemsPerPage);
    if (currentPage < totalPages) {
        currentPage++;
        filterOrders();
    }
});

document.getElementById('lastPage').addEventListener('click', () => {
    const totalPages = Math.ceil(orders.length / itemsPerPage);
    currentPage = totalPages;
    filterOrders();
});

// 處理每頁顯示數量
document.getElementById('itemsPerPage').addEventListener('change', (e) => {
    itemsPerPage = parseInt(e.target.value);
    currentPage = 1;
    filterOrders();
});

// 初始化
document.getElementById('statusFilter').addEventListener('change', filterOrders);
document.getElementById('searchField').addEventListener('change', filterOrders);
document.getElementById('searchInput').addEventListener('input', filterOrders);
document.getElementById('dateStart').addEventListener('change', filterOrders);
document.getElementById('dateEnd').addEventListener('change', filterOrders);
renderOrders(orders);
addOrderItem();
addGiftItem();