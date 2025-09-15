// product-test-button.js
// 提供測試數據填充功能

(function() {
    console.log("product-test-button.js loaded v2025-08-31-2");
    
    // 測試數據
    const testData = {
        productNames: ["超級瑪利歐公仔", "星際大戰 - 達斯維達模型", "復仇者聯盟 - 鋼鐵人手辦", "七龍珠 - 悟空動作人偶", "假面騎士模型", "數碼寶貝公仔"],
        descriptions: ["這個公仔非常精緻，每一個細節都設計得栩栩如生。品質上乘，是收藏的絕佳選擇！", 
                      "限量版商品，數量有限。購買後請愛惜保管，避免曝曬於陽光下。", 
                      "此款商品為預購商品，需要2-3週的備貨時間，請耐心等待。",
                      "獨家授權商品，全球限量發售，手感極佳，適合收藏。",
                      "高品質材料製作，色彩鮮明，細節精緻，是粉絲們必收藏的經典之作。"],
    };
    
    // 隨機選擇一個數組中的元素
    function getRandomItem(array) {
        return array[Math.floor(Math.random() * array.length)];
    }
    
    // 隨機生成一個範圍內的整數
    function getRandomInt(min, max) {
        min = Math.ceil(min);
        max = Math.floor(max);
        return Math.floor(Math.random() * (max - min + 1)) + min;
    }
    
    // 將網絡圖片轉換為 File 對象，以便模擬真實文件上傳
    async function getImageAsFile(imageUrl, fileName) {
        try {
            // 獲取完整的圖片 URL
            const fullUrl = new URL(imageUrl, window.location.origin).href;
            console.log('嘗試獲取圖片:', fullUrl);
            
            // 獲取圖片資料
            const response = await fetch(fullUrl);
            if (!response.ok) {
                throw new Error(`無法獲取圖片: ${response.status} ${response.statusText}`);
            }
            
            // 將圖片轉換為 Blob
            const blob = await response.blob();
            
            // 從 URL 中獲取副檔名
            const extension = imageUrl.split('.').pop();
            
            // 創建 File 對象
            const file = new File([blob], fileName || `test-image-${Date.now()}.${extension}`, { 
                type: blob.type 
            });
            
            return file;
        } catch (error) {
            console.error('獲取圖片作為文件時出錯:', error);
            return null;
        }
    }
    
    // 隨機選擇 n 張已上傳的產品圖片，並將它們轉換為文件對象
    async function getRandomProductImages(count) {
        try {
            // 可用的產品圖片列表
            const defaultImages = [
                '/uploads/products/aventador01.jpg',
                '/uploads/products/aventador02.jpg',
                '/uploads/products/ai04.jpg',
                '/uploads/products/ai05.jpg',
                '/uploads/products/ai06.jpg',
                '/uploads/products/aqua01.jpg',
                '/uploads/products/aqua02.jpg',
                '/uploads/products/aqua05.jpg',
                '/uploads/products/blocks01.jpg',
                '/uploads/products/spidey04.jpg'
            ];
            
            // 隨機選擇指定數量的圖片
            const selectedImageUrls = [];
            const availableIndices = [...Array(defaultImages.length).keys()];
            
            for (let i = 0; i < count; i++) {
                if (availableIndices.length === 0) break;
                
                const randomIndex = Math.floor(Math.random() * availableIndices.length);
                const selectedIndex = availableIndices[randomIndex];
                availableIndices.splice(randomIndex, 1);
                
                selectedImageUrls.push(defaultImages[selectedIndex]);
            }
            
            // 將選中的圖片 URL 轉換為 File 對象
            const imageFiles = [];
            for (let i = 0; i < selectedImageUrls.length; i++) {
                const imageUrl = selectedImageUrls[i];
                const fileName = `test-image-${i + 1}-${Date.now()}.${imageUrl.split('.').pop()}`;
                const file = await getImageAsFile(imageUrl, fileName);
                if (file) {
                    imageFiles.push(file);
                }
            }
            
            console.log('已獲取', imageFiles.length, '個圖片檔案');
            return imageFiles;
        } catch (error) {
            console.error('獲取隨機產品圖片時出錯:', error);
            return [];
        }
    }
    
    // 填充表單字段
    async function fillTestData() {
        try {
            const form = document.getElementById('productForm');
            if (!form) {
                console.error('找不到表單元素');
                return;
            }
            
            // 填充基本信息
            form.ProductName.value = getRandomItem(testData.productNames) + " " + getRandomInt(1, 100);
            form.BasePrice.value = getRandomInt(1000, 5000);
            form.SalePrice.value = getRandomInt(500, 999);
            form.Description.value = getRandomItem(testData.descriptions);
            
            // 設定上架狀態
            form.IsActive.value = "1";
            
            // 設定日期
            const today = new Date();
            const futureDate = new Date(today);
            futureDate.setMonth(today.getMonth() + 1);
            
            // 將日期轉換為YYYY-MM-DD格式
            const formatDate = (date) => {
                const yyyy = date.getFullYear();
                const mm = String(date.getMonth() + 1).padStart(2, '0');
                const dd = String(date.getDate()).padStart(2, '0');
                return `${yyyy}-${mm}-${dd}`;
            };
            
            // 將日期時間轉換為datetime-local格式（YYYY-MM-DDTHH:MM）
            const formatDatetime = (date) => {
                const yyyy = date.getFullYear();
                const mm = String(date.getMonth() + 1).padStart(2, '0');
                const dd = String(date.getDate()).padStart(2, '0');
                const hh = String(date.getHours()).padStart(2, '0');
                const mi = String(date.getMinutes()).padStart(2, '0');
                return `${yyyy}-${mm}-${dd}T${hh}:${mi}`;
            };
            
            // 設定預計發售日
            if (form.EstimatedReleaseDate) {
                form.EstimatedReleaseDate.value = formatDate(futureDate);
            }
            
            // 設定特價期間
            const specialPriceStartDate = document.querySelector('input[name="SpecialPriceStartDate"]');
            const specialPriceEndDate = document.querySelector('input[name="SpecialPriceEndDate"]');
            
            if (specialPriceStartDate && specialPriceEndDate) {
                const startDate = new Date(today);
                const endDate = new Date(futureDate);
                specialPriceStartDate.value = formatDatetime(startDate);
                specialPriceEndDate.value = formatDatetime(endDate);
            }
            
            // 填充尺寸和重量信息
            if (form.MinimumOrderQuantity) form.MinimumOrderQuantity.value = getRandomInt(1, 3);
            if (form.MaximumOrderQuantity) form.MaximumOrderQuantity.value = getRandomInt(5, 10);
            if (form.Quantity) form.Quantity.value = getRandomInt(50, 200);
            if (form.Weight) form.Weight.value = (getRandomInt(100, 500) / 100).toFixed(2);
            if (form.Length) form.Length.value = getRandomInt(10, 30);
            if (form.Width) form.Width.value = getRandomInt(10, 30);
            if (form.Height) form.Height.value = getRandomInt(10, 30);
            
            // 隨機選擇分類、品牌和狀態
            const categorySelect = document.getElementById('CategoryId');
            const brandSelect = document.getElementById('BrandId');
            const statusSelect = document.getElementById('StatusId');
            
            if (categorySelect && categorySelect.options.length > 1) {
                const optionsLength = categorySelect.options.length;
                categorySelect.selectedIndex = getRandomInt(1, optionsLength - 1);
            }
            
            if (brandSelect && brandSelect.options.length > 1) {
                const optionsLength = brandSelect.options.length;
                brandSelect.selectedIndex = getRandomInt(1, optionsLength - 1);
            }
            
            if (statusSelect && statusSelect.options.length > 1) {
                const optionsLength = statusSelect.options.length;
                statusSelect.selectedIndex = getRandomInt(1, optionsLength - 1);
            }
            
            // 處理圖片 - 獲取圖片檔案並通過文件輸入框添加它們
            try {
                // 獲取 6 張隨機產品圖片作為檔案對象
                const imageFiles = await getRandomProductImages(6);
                
                if (imageFiles.length > 0) {
                    console.log('獲取了', imageFiles.length, '張測試圖片檔案');
                    
                    // 獲取文件輸入元素
                    const fileInput = document.querySelector('input[name="ProductImages"]');
                    if (!fileInput) {
                        console.error('找不到圖片上傳輸入元素');
                        return;
                    }
                    
                    // 重置 ProductImageUploader
                    if (window.ProductImageUploader && typeof window.ProductImageUploader.reset === 'function') {
                        window.ProductImageUploader.reset();
                    }
                    
                    // 創建一個新的 DataTransfer 對象，用於將文件添加到 input 元素
                    const dataTransfer = new DataTransfer();
                    
                    // 將所有圖片添加到 DataTransfer 對象
                    imageFiles.forEach(file => {
                        dataTransfer.items.add(file);
                    });
                    
                    // 將 DataTransfer 的檔案列表賦值給文件輸入元素
                    fileInput.files = dataTransfer.files;
                    
                    // 手動觸發 change 事件，讓 ProductImageUploader 處理這些文件
                    const changeEvent = new Event('change', { bubbles: true });
                    fileInput.dispatchEvent(changeEvent);
                    
                    console.log('已將', dataTransfer.files.length, '個檔案添加到文件輸入元素，並觸發 change 事件');
                } else {
                    console.warn('無法獲取任何測試圖片檔案');
                }
            } catch (error) {
                console.error('處理測試圖片時出錯:', error);
            }
            
            console.log('測試數據填充完成');
            
        } catch (error) {
            console.error('填充測試數據時出錯:', error);
            alert('填充測試數據時出錯: ' + error.message);
        }
    }
    
    // 為測試按鈕添加點擊事件
    document.addEventListener('DOMContentLoaded', function() {
        const testButton = document.getElementById('btnTest');
        if (testButton) {
            testButton.addEventListener('click', fillTestData);
        } else {
            console.warn('找不到測試按鈕元素');
        }
    });
    
})();
