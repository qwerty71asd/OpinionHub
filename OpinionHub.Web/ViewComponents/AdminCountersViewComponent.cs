using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Data;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.ViewComponents;

public class AdminCountersViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;

    public AdminCountersViewComponent(ApplicationDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!User.IsInRole("Admin"))
            return Content(string.Empty);

        var reports = await _db.Reports.CountAsync(r => r.Status == ReportStatus.Pending);
        var appeals = await _db.Appeals.CountAsync(a => a.Status == AppealStatus.Pending);

        return View(new AdminCountersVm { PendingReports = reports, PendingAppeals = appeals });
    }

    public class AdminCountersVm
    {
        public int PendingReports { get; set; }
        public int PendingAppeals { get; set; }
        public int Total => PendingReports + PendingAppeals;
    }
}
