// product-image-uploader.js
// 用於處理商品圖片上傳、設定主圖與刪除圖片的模組

const ProductImageUploader = (function() {
    // API 基礎路徑
    const API_BASE = "/api/ProductImages";

    // 儲存實際檔案對象
    let imageFiles = [];
    // 已上傳到服務器的圖片資訊
    let uploadedImages = [];

    /**
     * 初始化已上傳圖片清單（用於編輯產品時）
     * @param {Array} images 產品已有圖片的路徑列表
     */
    function initUploadedImages(images = []) {
        console.log('初始化上傳圖片清單:', images);
        
        // 清空現有圖片列表
        uploadedImages = [];
        
        // 檢查傳入的參數類型
        if (!Array.isArray(images)) {
            console.warn('初始化圖片清單時收到非陣列資料:', images);
            return;
        }
        
        if (images.length === 0) {
            console.log('沒有初始圖片資料');
            return;
        }
        
        // 如果是複雜物件陣列（如API返回的完整圖片資訊）
        if (typeof images[0] === 'object' && images[0] !== null) {
            images.forEach(img => {
                // 檢查必要屬性是否存在
                const imagePath = img.imagePath || img.path;
                const isMainImage = img.isMainImage || img.isMain || false;
                const id = img.id || null;
                
                if (imagePath) {
                    // 檢查是否已經存在相同路徑的圖片
                    const exists = uploadedImages.some(u => u.path === imagePath);
                    if (!exists) {
                        uploadedImages.push({
                            id: id,
                            path: imagePath,
                            isMain: isMainImage
                        });
                    }
                }
            });
        } else {
            // 簡單的路徑字串陣列
            // 去除重複的圖片路徑
            const uniquePaths = [...new Set(images)];
            
            // 重新初始化圖片列表
            uniquePaths.forEach((path, index) => {
                uploadedImages.push({
                    id: null,  // 初始化時我們可能不知道 ID，會在後續請求中取得
                    path,
                    isMain: index === 0  // 假設第一張是主圖
                });
            });
        }
        
        console.log('初始化後的圖片列表:', uploadedImages);
        renderImagePreviews();
    }

    /**
     * 顯示圖片預覽
     */
    function renderImagePreviews() {
        const previewContainer = document.getElementById('imagePreviewList');
        if (!previewContainer) return;
        
        previewContainer.innerHTML = '';
        
        // 顯示已上傳的圖片
        uploadedImages.forEach((img, idx) => {
            const preview = document.createElement('div');
            preview.className = 'position-relative d-inline-block me-2 mb-2';
            preview.innerHTML = `
                <img src="${img.path}" style="max-width:120px;max-height:120px;border-radius:4px;border:${img.isMain ? '2px solid #28a745' : '1px solid #ccc'};object-fit:cover;">
                <div class="btn-group position-absolute top-0 end-0">
                    ${!img.isMain ? `<button class="btn btn-sm btn-success set-main-btn" data-index="${idx}" style="padding:2px 6px;" title="設為主圖">✓</button>` : ''}
                    <button class="btn btn-sm btn-danger delete-img-btn" data-index="${idx}" style="padding:2px 6px;" title="刪除圖片">×</button>
                </div>
                ${img.isMain ? '<span class="badge bg-success position-absolute bottom-0 start-0">主圖</span>' : ''}
            `;
            previewContainer.appendChild(preview);
        });
        
        // 顯示未上傳的本地圖片（臨時預覽）
        imageFiles.forEach((file, idx) => {
            const reader = new FileReader();
            reader.onload = (e) => {
                const preview = document.createElement('div');
                preview.className = 'position-relative d-inline-block me-2 mb-2';
                preview.innerHTML = `
                    <img src="${e.target.result}" style="max-width:120px;max-height:120px;border-radius:4px;border:1px solid #ccc;object-fit:cover;">
                    <button class="btn btn-sm btn-danger position-absolute top-0 end-0 remove-local-btn" data-index="${idx}" style="padding:2px 6px;" title="移除">×</button>
                    <span class="badge bg-warning position-absolute bottom-0 start-0">新選擇</span>
                `;
                previewContainer.appendChild(preview);
            };
            reader.readAsDataURL(file);
        });
        
        // 綁定事件處理器
        setTimeout(bindEventHandlers, 100);
    }
    
    /**
     * 綁定圖片操作事件
     */
    function bindEventHandlers() {
        // 設為主圖按鈕
        document.querySelectorAll('.set-main-btn').forEach(btn => {
            btn.addEventListener('click', function(e) {
                e.preventDefault();
                const idx = parseInt(this.getAttribute('data-index'));
                setMainImage(idx);
            });
        });
        
        // 刪除已上傳圖片按鈕
        document.querySelectorAll('.delete-img-btn').forEach(btn => {
            btn.addEventListener('click', function(e) {
                e.preventDefault();
                const idx = parseInt(this.getAttribute('data-index'));
                deleteImage(idx);
            });
        });
        
        // 移除本地圖片按鈕
        document.querySelectorAll('.remove-local-btn').forEach(btn => {
            btn.addEventListener('click', function(e) {
                e.preventDefault();
                const idx = parseInt(this.getAttribute('data-index'));
                removeLocalImage(idx);
            });
        });
    }
    
    /**
     * 處理圖片選擇
     * @param {Event} e 事件對象
     */
    function handleImageSelect(e) {
        console.log('圖片選擇事件觸發');
        const files = e.target.files;
        if (!files || !files.length) {
            console.log('沒有選擇任何檔案');
            return;
        }
        
        console.log(`選擇了 ${files.length} 個檔案`);
        
        // 將 FileList 轉為陣列並儲存
        const newFiles = [];
        const errors = [];
        
        for (let i = 0; i < files.length; i++) {
            const file = files[i];
            
            // 檢查檔案大小 (限制為 5MB)
            if (file.size > 5 * 1024 * 1024) {
                errors.push(`檔案 ${file.name} 超過 5MB 大小限制`);
                continue;
            }
            
            // 檢查副檔名
            const extension = file.name.split('.').pop().toLowerCase();
            if (!['jpg', 'jpeg', 'png', 'gif', 'webp'].includes(extension)) {
                errors.push(`檔案 ${file.name} 格式不支援，請選擇 jpg、jpeg、png、gif 或 webp 格式`);
                continue;
            }
            
            // 檢查是否為重複檔案 (用檔名和大小比較)
            const isDuplicate = imageFiles.some(existingFile => 
                existingFile.name === file.name && existingFile.size === file.size
            );
            
            if (isDuplicate) {
                console.log(`檔案 ${file.name} 已選擇過，略過`);
                continue;
            }
            
            newFiles.push(file);
        }
        
        // 添加有效文件到文件列表
        if (newFiles.length > 0) {
            imageFiles.push(...newFiles);
            console.log(`成功添加 ${newFiles.length} 個新檔案，目前共有 ${imageFiles.length} 個檔案待上傳`);
        }
        
        // 如果有錯誤，顯示錯誤訊息
        if (errors.length > 0) {
            alert(errors.join('\n'));
        }
        
        // 清空 input 值，允許重複選擇相同文件
        e.target.value = '';
        
        // 更新預覽
        renderImagePreviews();
    }
    
    /**
     * 移除本地圖片（還未上傳的）
     * @param {number} index 圖片索引
     */
    function removeLocalImage(index) {
        if (index >= 0 && index < imageFiles.length) {
            imageFiles.splice(index, 1);
            renderImagePreviews();
        }
    }
    
    /**
     * 設定主圖
     * @param {number} index 圖片索引
     */
    async function setMainImage(index) {
        try {
            const image = uploadedImages[index];
            if (!image || !image.id) {
                alert('無法設定主圖：圖片ID不存在');
                return;
            }
            
            // 發送 API 請求設為主圖
            const response = await fetch(`${API_BASE}/set-main/${image.id}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' }
            });
            
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`設定主圖失敗：${response.status} ${errorText}`);
            }
            
            const result = await response.json();
            
            // 更新本地數據
            uploadedImages.forEach(img => img.isMain = false);
            uploadedImages[index].isMain = true;
            
            // 重新渲染預覽
            renderImagePreviews();
            
            // 如果在表格列表中，局部更新商品圖片列表
            if (typeof apiGetList === 'function' && typeof renderTable === 'function') {
                // 可以選擇性地重新載入資料，也可以直接更新前端狀態
                // 這裡保持前端狀態同步即可，無需重新整理頁面
            }
            
        } catch (error) {
            console.error('設定主圖錯誤:', error);
            alert(`設定主圖時發生錯誤：${error.message}`);
        }
    }
    
    /**
     * 刪除圖片
     * @param {number} index 圖片索引
     */
    async function deleteImage(index) {
        if (!confirm('確定要刪除此圖片？')) return;
        
        console.log('嘗試刪除圖片，索引:', index);
        
        try {
            if (index < 0 || index >= uploadedImages.length) {
                console.error('圖片索引超出範圍:', index, '總數:', uploadedImages.length);
                alert('刪除失敗：找不到此圖片');
                return;
            }
            
            const image = uploadedImages[index];
            console.log('要刪除的圖片:', image);
            
            if (!image) {
                console.error('無法找到索引', index, '的圖片');
                return;
            }
            
            if (!image.id) {
                console.log('圖片沒有 ID，僅從本地移除');
                // 若沒有 ID，只從本地移除
                uploadedImages.splice(index, 1);
                renderImagePreviews();
                return;
            }
            
            console.log('發送 API 請求刪除圖片, ID:', image.id);
            
            // 發送 API 請求刪除圖片
            const response = await fetch(`${API_BASE}/delete/${image.id}`, {
                method: 'DELETE'
            });
            
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`刪除圖片失敗：${response.status} ${errorText}`);
            }
            
            console.log('成功刪除圖片, ID:', image.id);
            
            // 從本地列表移除
            uploadedImages.splice(index, 1);
            
            // 重新渲染預覽
            renderImagePreviews();
            
            // 如果需要設定新的主圖
            if (image.isMain && uploadedImages.length > 0) {
                console.log('刪除的是主圖，將第一張圖設為新主圖');
                // 可以在這裡增加自動設定新主圖的邏輯
                // 例如呼叫 setMainImage(0)
            }
            
            // 如果在表格列表中，局部更新商品圖片列表
            if (typeof apiGetList === 'function' && typeof renderTable === 'function') {
                // 可以選擇性地重新載入資料，也可以直接更新前端狀態
                console.log('保持前端狀態同步，不重新整理頁面');
            }
            
        } catch (error) {
            console.error('刪除圖片錯誤:', error);
            alert(`刪除圖片時發生錯誤：${error.message}`);
        }
    }
    
    /**
     * 上傳所有本地圖片到指定產品
     * @param {number} productId 產品ID
     * @returns {Promise<Array>} 上傳後的圖片資訊
     */
    async function uploadAllImages(productId) {
        if (!productId) {
            console.error('無效的產品ID:', productId);
            return [];
        }
        
        if (!imageFiles.length) {
            console.log('沒有需要上傳的圖片檔案');
            return [];
        }
        
        console.log('開始上傳', imageFiles.length, '張圖片到產品ID:', productId);
        
        try {
            // 預處理檔案，檢查是否有問題
            const validFiles = imageFiles.filter(file => {
                // 檢查檔案大小 (限制為 5MB)
                if (file.size > 5 * 1024 * 1024) {
                    console.warn(`檔案 ${file.name} 超過 5MB 大小限制，已略過`);
                    return false;
                }
                
                // 檢查副檔名
                const extension = file.name.split('.').pop().toLowerCase();
                if (!['jpg', 'jpeg', 'png', 'gif', 'webp'].includes(extension)) {
                    console.warn(`檔案 ${file.name} 格式不支援，已略過`);
                    return false;
                }
                
                return true;
            });
            
            console.log('有效檔案數量:', validFiles.length);
            
            // 先確保我們正在處理的是正確的商品
            // 對於新增商品的情況，我們應該只上傳選擇的新圖片
            console.log(`確保圖片只會上傳到商品 ID: ${productId}`);
            
            const uploadPromises = validFiles.map(file => {
                const formData = new FormData();
                formData.append('file', file);
                // 明確指定要上傳到哪個商品
                formData.append('productId', productId);
                
                return fetch(`${API_BASE}/upload/${productId}`, {
                    method: 'POST',
                    body: formData
                })
                .then(res => {
                    if (!res.ok) throw new Error(`上傳失敗：${res.status}`);
                    return res.json();
                });
            });
            
            // 等待所有上傳完成
            const results = await Promise.all(uploadPromises);
            console.log('上傳結果:', results);
            
            // 清空本地檔案列表
            imageFiles = [];
            
            // 將新上傳的圖片加入已上傳列表
            let addedCount = 0;
            results.forEach(result => {
                // 確保上傳的圖片屬於正確的商品
                if (String(result.productId) === String(productId)) {
                    // 檢查是否已經存在相同路徑的圖片（避免重複添加）
                    const exists = uploadedImages.some(img => img.path === result.imagePath);
                    if (!exists) {
                        uploadedImages.push({
                            id: result.id,
                            path: result.imagePath,
                            isMain: result.isMainImage
                        });
                        addedCount++;
                    } else {
                        console.log('圖片已存在，略過:', result.imagePath);
                    }
                } else {
                    console.warn(`跳過圖片，因為它屬於商品 ${result.productId}，而非 ${productId}`);
                }
            });
            
            console.log(`新增了 ${addedCount} 張圖片，當前共有 ${uploadedImages.length} 張圖片`);
            
            // 重新渲染預覽
            renderImagePreviews();
            
            return results;
            
        } catch (error) {
            console.error('上傳圖片錯誤:', error);
            alert(`上傳圖片時發生錯誤：${error.message}`);
            return [];
        }
    }
    
    /**
     * 載入產品的圖片資訊
     * @param {number} productId 產品ID
     */
    async function loadProductImages(productId) {
        if (!productId) {
            console.warn('無法載入圖片：未提供產品ID');
            return;
        }
        
        try {
            console.log('開始載入產品 ID:', productId, '的圖片');
            
            // 先重置現有的圖片列表，確保不會混合不同商品的圖片
            uploadedImages = [];
            
            // 從產品 API 獲取圖片資訊
            const response = await fetch(`${API_BASE}/product/${productId}`, {
                headers: { 'Accept': 'application/json' }
            });
            
            if (!response.ok) {
                throw new Error(`獲取產品圖片失敗：${response.status}`);
            }
            
            const images = await response.json();
            console.log('從API獲取的圖片資料:', images);
            
            // 處理圖片資訊
            if (Array.isArray(images)) {
                // 過濾確保只載入屬於指定產品的圖片
                const productImages = images.filter(img => 
                    img && String(img.productId) === String(productId)
                );
                
                console.log(`過濾後屬於產品 ${productId} 的圖片數量:`, productImages.length);
                
                // 從API獲取的數據是物件陣列，包含完整的圖片資訊
                productImages.forEach(img => {
                    // 檢查是否已經存在相同路徑的圖片
                    const exists = uploadedImages.some(u => u.path === img.imagePath);
                    if (!exists) {
                        uploadedImages.push({
                            id: img.id,
                            path: img.imagePath,
                            isMain: img.isMainImage,
                            productId: img.productId // 保存產品ID，方便之後檢查
                        });
                    }
                });
                
                console.log('處理後的圖片列表:', uploadedImages);
                
                // 重新渲染預覽
                renderImagePreviews();
            } else {
                console.warn('API 返回的圖片資料不是陣列格式:', images);
            }
            
        } catch (error) {
            console.error('載入產品圖片錯誤:', error);
        }
    }
    
    /**
     * 取得已上傳的圖片路徑列表
     * @returns {Array} 圖片路徑列表
     */
    function getUploadedImagePaths() {
        return uploadedImages.map(img => img.path);
    }
    
    /**
     * 檢查是否有未上傳的圖片
     * @returns {boolean}
     */
    function hasUnsavedImages() {
        return imageFiles.length > 0;
    }
    
    /**
     * 重置上傳器狀態，清空所有暫存的圖片
     */
    function resetUploader() {
        console.log('重置圖片上傳器狀態');
        // 清空本地暫存的檔案
        imageFiles = [];
        // 清空已上傳的圖片列表
        uploadedImages = [];
        // 清空預覽區域
        const previewContainer = document.getElementById('imagePreviewList');
        if (previewContainer) {
            previewContainer.innerHTML = '';
        }
    }
    
    // 對外公開的方法
    return {
        init: function(imageInputSelector) {
            // 綁定圖片選擇事件
            const imageInput = document.querySelector(imageInputSelector);
            if (imageInput) {
                imageInput.addEventListener('change', handleImageSelect);
            }
            return this;
        },
        initImages: initUploadedImages,
        upload: uploadAllImages,
        loadImages: loadProductImages,
        getImagePaths: getUploadedImagePaths,
        hasUnsaved: hasUnsavedImages,
        reset: resetUploader // 新增重置函數
    };
})();

// 預設導出
window.ProductImageUploader = ProductImageUploader;
