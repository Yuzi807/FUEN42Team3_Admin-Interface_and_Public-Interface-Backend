using Microsoft.AspNetCore.Mvc.Rendering;

namespace FUEN42Team3.Backend.RazorHelper
{
    public static class HtmlHelpers
    {
        public static string IsActive(this IHtmlHelper htmlHelper, string controller, string action = null)
        {
            // 確認現在的路由資料
            var routeData = htmlHelper.ViewContext.RouteData;
            var currentController = routeData.Values["Controller"]?.ToString();
            var currentAction = routeData.Values["Action"]?.ToString();
            // 如果沒有提供 action，則使用當前的 action
            if (!string.Equals(controller, currentController, StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            // 如果提供了 action，則檢查是否匹配
            if (action != null && !string.Equals(action, currentAction, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return "active";
        }

        public static string IsExpanded(this IHtmlHelper htmlHelper, string controller)
        {

            var routeData = htmlHelper.ViewContext.RouteData;
            var currentController = routeData.Values["Controller"]?.ToString();

            if (!string.Equals(controller, currentController, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return "show";
        }
    }
}
