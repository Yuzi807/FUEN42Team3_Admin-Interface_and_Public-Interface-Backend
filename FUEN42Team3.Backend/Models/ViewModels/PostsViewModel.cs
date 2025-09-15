using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace FUEN42Team3.Backend.Models.ViewModels
{
    public class PostsViewModel
    {
        [Key]
        [Display(Name = "文章ID")]
        public int Id { get; set; }

        [Display(Name = "標題")]
        public string Title { get; set; }

        [Display(Name = "作者")]
        public string Member { get; set; }


        [Display(Name = "分類")]
        public string Category { get; set; }

        [Display(Name = "發文時間")]
        public DateTime? PostTime { get; set; }

        [Display(Name = "讚數")]
        public int NumOfGoods { get; set; }

        [Display(Name = "點閱")]
        public int NumOfHits { get; set; }

        [Display(Name = "狀態")]//1 公開、2 隱藏、3 草稿
        public string StatusName { get; set; }

    }
}
