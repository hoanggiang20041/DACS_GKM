using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Chamsoc.Filters
{
    public class CustomAuthorizationFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Kiểm tra nếu có lỗi phân quyền (Access Denied)
            if (context.Result is ChallengeResult || context.Result is ForbidResult)
            {
                // Chuyển hướng đến view AccessDenied
                context.Result = new ViewResult
                {
                    ViewName = "~/Views/Shared/AccessDenied.cshtml"
                };
            }
        }
    }
}