using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Web.Data;
using SalesHierarchy.Web.Models;
using SalesHierarchy.Web.ViewModels;

namespace SalesHierarchy.Web.Controllers;

public class HomeController : Controller
{
    private readonly ISalesPersonRepository _salesPersonRepository;
    private readonly IPODStationRepository _podStationRepository;
    private readonly IPODRepository _podRepository;
    private readonly IDealerRepository _dealerRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(
        ISalesPersonRepository salesPersonRepository,
        IPODStationRepository podStationRepository,
        IPODRepository podRepository,
        IDealerRepository dealerRepository,
        IAuditLogRepository auditLogRepository,
        UserManager<ApplicationUser> userManager)
    {
        _salesPersonRepository = salesPersonRepository;
        _podStationRepository = podStationRepository;
        _podRepository = podRepository;
        _dealerRepository = dealerRepository;
        _auditLogRepository = auditLogRepository;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return View(new HomeViewModel());
        }

        var isAdmin = User.IsInRole("SystemAdmin");
        var model = new HomeViewModel
        {
            IsAuthenticated = true,
            IsAdmin = isAdmin,
            RoleLabel = isAdmin ? "System administrator" : "Sales person"
        };

        var user = await _userManager.GetUserAsync(User);
        var email = user?.Email ?? User.Identity?.Name ?? string.Empty;
        model.DisplayName = string.IsNullOrWhiteSpace(email) ? "there" : email.Split('@')[0];

        try
        {
            var people = _salesPersonRepository.GetAll().ToList();
            var peopleById = people.ToDictionary(p => p.ID);
            var pods = _podRepository.GetAll().ToList();
            var stations = _podStationRepository.GetAll()
                .GroupBy(s => s.ID)
                .ToDictionary(g => g.Key, g => g.First());

            var currentPerson = !isAdmin && user?.SalesPersonId is int salesPersonId &&
                                peopleById.TryGetValue(salesPersonId, out var person)
                ? person
                : null;
            var visibleIds = isAdmin
                ? people.Select(p => p.ID).ToHashSet()
                : GetSubtreeIds(people, currentPerson?.ID);
            var visiblePeople = people.Where(p => visibleIds.Contains(p.ID)).ToList();
            var visiblePods = pods.Where(p => p.salespersonId is int ownerId && visibleIds.Contains(ownerId)).ToList();

            model.DisplayName = currentPerson?.Name ?? model.DisplayName;
            model.EmployeeCount = visiblePeople.Count;
            model.DealerCount = isAdmin ? _dealerRepository.GetAll().Count() : 0;
            model.StationCount = stations.Count;
            model.PodCount = isAdmin ? pods.Count : visiblePods.Count;
            model.AssignedPods = isAdmin ? pods.Count(p => p.salespersonId.HasValue) : visiblePods.Count;
            model.Levels = CountLevels(visiblePeople);

            var directReports = people
                .Where(p => p.ManagerId.HasValue)
                .GroupBy(p => p.ManagerId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());
            model.RecentEmployees = visiblePeople
                .OrderByDescending(p => directReports.GetValueOrDefault(p.ID))
                .ThenBy(p => p.Name)
                .Take(5)
                .ToList();

            model.Pods = (isAdmin ? pods : visiblePods)
                .Take(6)
                .Select(pod => new PodRow
                {
                    Pod = pod,
                    StationName = stations.TryGetValue(pod.PODStationId, out var station) ? station.Name : "Unassigned station",
                    SalesPersonName = pod.salespersonId is int ownerId && peopleById.TryGetValue(ownerId, out var owner)
                        ? owner.Name
                        : null
                })
                .ToList();

            var emails = string.IsNullOrWhiteSpace(email) ? Array.Empty<string>() : new[] { email };
            model.Activity = (isAdmin
                ? _auditLogRepository.GetRecent(6)
                : _auditLogRepository.GetForTeam(visibleIds, emails, 6)).Take(6).ToList();

            var growthLogs = isAdmin
                ? _auditLogRepository.GetRecent(500, "SalesPerson")
                : _auditLogRepository.GetForTeam(visibleIds, emails, 500)
                    .Where(l => string.Equals(l.EntityName, "SalesPerson", StringComparison.OrdinalIgnoreCase));

            model.Growth = BuildGrowth(growthLogs);
            model.NewEmployeeCount = growthLogs.Count(l =>
                (string.Equals(l.Action, "Create", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(l.Action, "Import", StringComparison.OrdinalIgnoreCase)) &&
                l.OccurredUtc >= DateTime.UtcNow.AddDays(-30));
        }
        catch
        {
            // The overview should still render before the database or audit table is deployed.
            model.Growth = BuildGrowth(Array.Empty<AuditLog>());
        }

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private static HashSet<int> GetSubtreeIds(IReadOnlyCollection<SalesPerson> people, int? rootId)
    {
        var ids = new HashSet<int>();
        if (!rootId.HasValue)
        {
            return ids;
        }

        ids.Add(rootId.Value);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var person in people)
            {
                if (person.ManagerId.HasValue && ids.Contains(person.ManagerId.Value) && ids.Add(person.ID))
                {
                    changed = true;
                }
            }
        }

        return ids;
    }

    private static int CountLevels(IReadOnlyCollection<SalesPerson> people)
    {
        if (people.Count == 0)
        {
            return 0;
        }

        var byId = people.ToDictionary(p => p.ID);
        var depths = new Dictionary<int, int>();

        int Depth(SalesPerson person, HashSet<int> path)
        {
            if (depths.TryGetValue(person.ID, out var known))
            {
                return known;
            }

            if (!person.ManagerId.HasValue ||
                !byId.TryGetValue(person.ManagerId.Value, out var manager) ||
                !path.Add(manager.ID))
            {
                depths[person.ID] = 1;
                return 1;
            }

            var depth = Depth(manager, path) + 1;
            depths[person.ID] = depth;
            return depth;
        }

        return people.Max(p => Depth(p, new HashSet<int> { p.ID }));
    }

    private static IReadOnlyList<GrowthPoint> BuildGrowth(IEnumerable<AuditLog> logs)
    {
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-11);
        var relevant = logs
            .Where(l => l.OccurredUtc >= start &&
                        (string.Equals(l.Action, "Create", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(l.Action, "Import", StringComparison.OrdinalIgnoreCase)))
            .GroupBy(l => (l.OccurredUtc.Year, l.OccurredUtc.Month))
            .ToDictionary(g => g.Key, g => g.Count());

        return Enumerable.Range(0, 12)
            .Select(offset => start.AddMonths(offset))
            .Select(month => new GrowthPoint
            {
                Label = month.ToString("MMM"),
                Value = relevant.TryGetValue((month.Year, month.Month), out var count) ? count : 0
            })
            .ToList();
    }
}
