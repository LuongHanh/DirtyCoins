using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public class ErrorController : Controller
{
    [Route("Error")]
    public IActionResult Index()
    {
        var exceptionHandler = HttpContext.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionHandler?.Error;

        ViewBag.Message = exception?.Message ?? "Đã xảy ra lỗi không xác định.";
        ViewBag.StatusCode = 500;

        return View("~/Views/Shared/Error.cshtml");
    }

    [Route("Error/{statusCode}")]
    public IActionResult HttpStatusHandler(int statusCode)
    {
        ViewBag.StatusCode = statusCode;

        switch (statusCode)
        {
            case 404:
                ViewBag.Message = "Trang bạn yêu cầu không tồn tại.";
                break;
            case 403:
                ViewBag.Message = "Bạn không có quyền truy cập trang này.";
                break;
            default:
                ViewBag.Message = "Đã xảy ra lỗi không xác định.";
                break;
        }

        return View("~/Views/Shared/Error.cshtml");
    }
}
