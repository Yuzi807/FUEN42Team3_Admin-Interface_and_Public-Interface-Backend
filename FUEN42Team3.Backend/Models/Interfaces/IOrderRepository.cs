using FUEN42Team3.Backend.Models.DTOs;
using FUEN42Team3.Backend.Models.ViewModels;
using FUEN42Team3.Models.ViewModel;

namespace FUEN42Team3.Models.Interfaces
{
    public interface IOrderRepository
    {
        Task<List<OrderDto>> GetAllAsync();
        Task<OrderDto?> GetByIdAsync(int id);
        Task<int> CreateAsync(OrderDto dto);
        Task<bool> UpdateAsync(OrderDto dto);
        Task<bool> SoftDeleteAsync(int id);
        Task<bool> UpdateStatusAsync(int id, string status);
        Task<List<string>> GetValidStatusesAsync();
        Task<List<PaymentMethodDto>> GetAllPaymentMethodsAsync();
        List<CategoryDto> GetOrderCategories();
        Task<List<DeliveryMethodDto>> GetAllDeliveryMethodsAsync();
        Task<(List<OrderDto> Orders, int TotalCount, int TotalPages, int CurrentPage, int PageSize)> GetOrdersPagedAsync(int pageNumber, int pageSize);
    }
}
