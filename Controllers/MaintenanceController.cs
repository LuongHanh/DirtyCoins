using DirtyCoins.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

public class MaintenanceController : Controller
{
    private readonly IWebHostEnvironment _env;

    public MaintenanceController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpGet("/Maintenance/Notice")]
    public IActionResult Notice()
    {
        var filePath = Path.Combine(_env.ContentRootPath, "maintenance.json");

        if (!System.IO.File.Exists(filePath))
            return RedirectToAction("Index", "Home");

        var json = System.IO.File.ReadAllText(filePath);
        var info = JsonSerializer.Deserialize<MaintenanceInfo>(json);

        ViewBag.Start = info?.StartTime.ToString("HH:mm dd/MM/yyyy");
        ViewBag.End = info?.EndTime.ToString("HH:mm dd/MM/yyyy");
        ViewBag.Reason = info?.Reason ?? "Bảo trì hệ thống";

        return View("Notice");
    }
}
