using FUEN42Team3.Backend.Models.EfModels;
using FUEN42Team3.Backend.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FUEN42Team3.Backend.Models.Services
{
    public class NewsService
    {
        private readonly AppDbContext _context;

        public NewsService(AppDbContext context)
        {
            _context = context;
        }

        // ���o�Ҧ����i���@�Τ�k
        public async Task<List<NewsViewModel>> GetAllNewsAsync()
        {
            return await _context.News
                .Include(n => n.Category)
                .Include(n => n.User)
                .Include(n => n.Status)
                .OrderByDescending(n => n.IsPinned) // ���̸m���Ƨ�
                .ThenByDescending(n => n.PublishedAt) // �A�̵o���ɶ��Ƨ�
                .Select(n => new NewsViewModel
                {
                    IsPinned = n.IsPinned,
                    Id = n.Id,
                    Title = n.Title,
                    CategoryName = n.Category.CategoryName,
                    UserName = n.User.UserName,
                    PublishedAt = n.PublishedAt,
                    UpdatedAt = n.UpdatedAt,
                    ViewCountToday = n.ViewCountToday,
                    ViewCountTotal = n.ViewCountTotal,
                    Status = n.Status.Name
                })
                .ToListAsync();
        }

        // ���o��@���i���@�Τ�k
        public async Task<NewsViewModel> GetNewsByIdAsync(int id)
        {
            var news = await _context.News
                .Include(x => x.Category)
                .Include(x => x.User)
                .Include(x => x.Status)
                .FirstOrDefaultAsync(nw => nw.Id == id);

            if (news == null) return null;

            return new NewsViewModel
            {
                IsPinned = news.IsPinned,
                Id = news.Id,
                Title = news.Title,
                CategoryName = news.Category.CategoryName,
                UserName = news.User.UserName,
                PublishedAt = news.PublishedAt,
                UpdatedAt = news.UpdatedAt,
                ViewCountToday = news.ViewCountToday,
                ViewCountTotal = news.ViewCountTotal,
                Status = news.Status.Name
            };
        }

        // ���o�Ҧ��������@�Τ�k
        public async Task<List<NewsCategoriesViewModel>> GetAllCategoriesAsync()
        {
            return await _context.NewsCategories
                .Select(c => new NewsCategoriesViewModel
                {
                    Id = c.Id,
                    CategoryName = c.CategoryName,
                    IconPath = c.Icon,
                    IsVisible = c.IsVisible
                })
                .ToListAsync();
        }
    }
}