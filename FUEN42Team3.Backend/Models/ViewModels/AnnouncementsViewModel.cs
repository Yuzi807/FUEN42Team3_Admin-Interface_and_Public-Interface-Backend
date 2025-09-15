using System.ComponentModel.DataAnnotations;

namespace FUEN42Team3.Backend.Models.ViewModels
{
    public class AnnouncementsViewModel
    {
        [Display(Name = "ID")]
        public int Id { get; set; }

            [Display(Name = "標題")]
            public string Title { get; set; }

            [Display(Name = "發布者")]
            public string Supervisor { get; set; }

            [Display(Name = "發布時間")]
            public DateTime PostTime { get; set; }


            [Display(Name = "最後修改人員")]
            public string? LastEditor { get; set; }

            [Display(Name = "最後編輯時間")]
            public DateTime? LastEditTime { get; set; }

            [Display(Name = "狀態")]
            public string StatusName { get; set; }

        

    }
}
