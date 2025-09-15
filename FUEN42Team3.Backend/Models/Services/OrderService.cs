using FUEN42Team3.Backend.Models.DTOs;
using FUEN42Team3.Models.Interfaces;
using FUEN42Team3.Models.Repositories;

namespace FUEN42Team3.Models.Services
{
    public class OrderService
    {
        private readonly IOrderRepository _repo;
        public OrderService(IOrderRepository repo) => _repo = repo;

        public Task<List<OrderDto>> GetOrdersAsync() => _repo.GetAllAsync();
        public Task<OrderDto?> GetOrderByIdAsync(int id) => _repo.GetByIdAsync(id);

        public async Task<(bool ok, string message, int newId)> CreateAsync(OrderDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.RecipientName) ||
                string.IsNullOrWhiteSpace(dto.Phone) ||
                string.IsNullOrWhiteSpace(dto.Address))
                return (false, "收件資料不完整", 0);

            if (dto.TotalAmount < 0 || dto.ShippingFee < 0)
                return (false, "金額/運費需為非負數", 0);

            var id = await _repo.CreateAsync(dto);
            return (true, "建立成功", id);
        }

        public async Task<(bool ok, string message)> UpdateAsync(OrderDto dto)
        {
            if (dto.Id <= 0) return (false, "訂單ID不合法");
            if (string.IsNullOrWhiteSpace(dto.RecipientName) ||
                string.IsNullOrWhiteSpace(dto.Phone) ||
                string.IsNullOrWhiteSpace(dto.Address))
                return (false, "收件資料不完整");

            var ok = await _repo.UpdateAsync(dto);
            return ok ? (true, "更新成功") : (false, "更新失敗");
        }

        public async Task<(bool ok, string message)> DeleteAsync(int id)
        {
            if (id <= 0) return (false, "訂單ID不合法");
            var ok = await _repo.SoftDeleteAsync(id);
            return ok ? (true, "刪除成功") : (false, "刪除失敗");
        }


        public async Task<(bool ok, string message)> UpdateStatusAsync(int id, string status)
        {
            // 透過 repository 取得所有有效狀態
            var validStatuses = await _repo.GetValidStatusesAsync();

            // 檢查提供的狀態是否在資料庫中存在
            var valid = validStatuses.Contains(status);
            if (id <= 0 || !valid) return (false, "狀態或ID不合法");

            var ok = await _repo.UpdateStatusAsync(id, status);
            return ok ? (true, "狀態更新成功") : (false, "狀態更新失敗");
        }

        public async Task<List<PaymentMethodDto>> GetAllPaymentMethodsAsync()
        {
            return await _repo.GetAllPaymentMethodsAsync();
        }

        public List<CategoryDto> GetOrderCategories()
        {
            return _repo.GetOrderCategories();
        }

        public async Task<List<DeliveryMethodDto>> GetAllDeliveryMethodsAsync()
        {
            return await _repo.GetAllDeliveryMethodsAsync();
        }

        public Task<List<string>> GetValidStatusesAsync()
        {
            return _repo.GetValidStatusesAsync();
        }

        public async Task<(List<OrderDto> Orders, int TotalCount, int TotalPages, int CurrentPage, int PageSize)> GetOrdersPagedAsync(int pageNumber, int pageSize)
        {
            return await _repo.GetOrdersPagedAsync(pageNumber, pageSize);
        }
    }
}
