// product-form-handler.js
// 用於處理產品表單提交並保存到資料庫

(function() {
    console.log("product-form-handler.js loaded v2025-08-31-1");
    
    // 將日期時間轉換為台灣時區 (UTC+8)
    function convertToTaiwanTime(dateTimeString) {
        if (!dateTimeString) return null;
        
        const date = new Date(dateTimeString);
        // 確保日期有效
        if (isNaN(date)) return null;
        
        // 將本地時間轉換為UTC，然後再轉換為台灣時間 (UTC+8)
        // 計算當前時區和UTC的差異（分鐘）
        const localTimezoneOffset = date.getTimezoneOffset();
        // 轉換到UTC時間
        const utcTime = new Date(date.getTime() + localTimezoneOffset * 60 * 1000);
        // 轉換到台灣時間 (UTC+8)
        const taiwanTime = new Date(utcTime.getTime() + (8 * 60 * 60 * 1000));
        
        return taiwanTime.toISOString();
    }

    // 表單提交處理
    async function handleFormSubmit(event) {
        event.preventDefault();
        
        // 獲取表單元素
        const form = event.target;
        const formData = new FormData(form);
        
        // 檢查必填欄位
        const requiredFields = [
            { name: 'ProductName', label: '商品名稱' },
            { name: 'CategoryId', label: '分類' },
            { name: 'BrandId', label: '品牌' },
            { name: 'StatusId', label: '狀態' },
            { name: 'BasePrice', label: '原價' }
        ];
        
        for (const field of requiredFields) {
            const value = formData.get(field.name);
            if (!value || (typeof value === 'string' && value.trim() === '')) {
                alert(`請填寫${field.label}`);
                const element = form.elements[field.name];
                if (element) element.focus();
                return;
            }
        }
        
        // 處理特價與特價期間
        const basePrice = Number(formData.get('BasePrice') || 0);
        const salePriceRaw = formData.get('SalePrice') || '';
        const salePrice = salePriceRaw.trim() === '' ? null : Number(salePriceRaw);
        
        // 特價需小於原價的驗證
        if (salePrice !== null && salePrice >= basePrice) {
            alert('特價必須小於原價');
            const element = form.elements['SalePrice'];
            if (element) element.focus();
            return;
        }
        
        // 特價期間驗證
        const specialStartDate = formData.get('SpecialPriceStartDate') || '';
        const specialEndDate = formData.get('SpecialPriceEndDate') || '';
        
        if ((specialStartDate && !specialEndDate) || (!specialStartDate && specialEndDate)) {
            alert('特惠開始與結束時間需同時填寫或都不填');
            return;
        }
        
        if (specialStartDate && specialEndDate) {
            const startDate = new Date(specialStartDate);
            const endDate = new Date(specialEndDate);
            
            if (startDate >= endDate) {
                alert('特惠開始時間必須早於結束時間');
                return;
            }
            
            // 若有填寫特價期間但沒有特價，提示使用者
            if (salePrice === null) {
                if (!confirm('您已設定特惠期間但未填寫特價，確定要繼續嗎？')) {
                    return;
                }
            }
        }
        
        try {
            // 顯示處理中訊息
            const submitBtn = form.querySelector('button[type="submit"]');
            const originalBtnText = submitBtn.textContent;
            submitBtn.disabled = true;
            submitBtn.textContent = '處理中...';
            
            // 準備要提交的數據
            const payload = {
                ProductName: formData.get('ProductName'),
                CategoryId: Number(formData.get('CategoryId')),
                BrandId: Number(formData.get('BrandId')),
                StatusId: Number(formData.get('StatusId')),
                BasePrice: basePrice,
                SpecialPrice: salePrice,
                IsActive: formData.get('IsActive') === '1',
                Description: formData.get('Description') || '',
                
                // 日期時間 - 將本機時間轉換為台灣時區儲存
                SpecialPriceStartDate: specialStartDate ? convertToTaiwanTime(specialStartDate) : null,
                SpecialPriceEndDate: specialEndDate ? convertToTaiwanTime(specialEndDate) : null,
                EstimatedReleaseDate: formData.get('EstimatedReleaseDate') ? convertToTaiwanTime(formData.get('EstimatedReleaseDate')) : null,
                
                // 規格資料
                Quantity: getNumberOrNull(formData.get('Quantity')),
                MinimumOrderQuantity: getNumberOrNull(formData.get('MinimumOrderQuantity')),
                MaximumOrderQuantity: getNumberOrNull(formData.get('MaximumOrderQuantity')),
                Weight: getNumberOrNull(formData.get('Weight')),
                Length: getNumberOrNull(formData.get('Length')),
                Width: getNumberOrNull(formData.get('Width')),
                Height: getNumberOrNull(formData.get('Height'))
            };
            
            console.log('準備提交的數據:', payload);
            
            const productId = formData.get('ProductID');
            let response;
            
            if (!productId) {
                // 新增產品
                response = await fetch('/api/ProductsAPI', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Accept': 'application/json'
                    },
                    body: JSON.stringify(payload)
                });
            } else {
                // 更新產品
                response = await fetch(`/api/ProductsAPI/${productId}`, {
                    method: 'PUT',
                    headers: {
                        'Content-Type': 'application/json',
                        'Accept': 'application/json'
                    },
                    body: JSON.stringify(payload)
                });
            }
            
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`伺服器回應錯誤：${response.status} ${errorText}`);
            }
            
            const result = await response.json();
            console.log('伺服器回應:', result);
            
            // 上傳圖片（如果有）
            if (window.ProductImageUploader && result.id) {
                console.log('檢查是否有未儲存的圖片...');
                if (window.ProductImageUploader.hasUnsaved()) {
                    console.log('開始上傳圖片到產品 ID:', result.id);
                    try {
                        await window.ProductImageUploader.upload(result.id);
                        console.log('圖片上傳成功');
                    } catch (imageError) {
                        console.error('圖片上傳過程中發生錯誤:', imageError);
                        // 不中斷主流程，只記錄錯誤
                    }
                } else {
                    console.log('沒有需要儲存的新圖片');
                }
            }
            
            // 成功提示
            alert(productId ? '商品更新成功！' : '商品新增成功！');
            
            // 關閉Modal並重新載入商品列表
            const modalInstance = bootstrap.Modal.getInstance(document.getElementById('productModal'));
            if (modalInstance) {
                modalInstance.hide();
            } else {
                console.warn('無法找到產品編輯視窗的實例');
            }
            
            // 由 product.api.js 處理重新載入商品列表，這裡不需重複處理
            console.log('商品保存成功，UI 更新將由表單提交事件處理');
            
        } catch (error) {
            console.error('商品保存錯誤:', error);
            alert(`商品保存失敗：${error.message}`);
        } finally {
            // 恢復按鈕狀態
            const submitBtn = form.querySelector('button[type="submit"]');
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.textContent = originalBtnText || '儲存';
            }
        }
    }
    
    // 將空字串或無效數字轉為 null 的輔助函數
    function getNumberOrNull(value) {
        if (value === null || value === undefined || value === '') return null;
        const num = Number(value);
        return isNaN(num) ? null : num;
    }
    
    // 當文檔載入完成後綁定事件
    document.addEventListener('DOMContentLoaded', function() {
        console.log('product-form-handler.js: 表單提交事件由 product.api.js 處理');
        // 不再綁定表單提交事件，避免重複處理
    });
})();
