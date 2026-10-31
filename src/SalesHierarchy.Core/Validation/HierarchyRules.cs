using SalesHierarchy.Core.Models;

namespace SalesHierarchy.Core.Validation
{
    /// <summary>
    /// Pure business rules for the reporting hierarchy. Kept free of data-access concerns
    /// so the same rules can be applied by controllers, bulk import and tests.
    /// </summary>
    public static class HierarchyRules
    {
        public const string CxoLevel = "CXO";
        public const string HeadLevel = "Head";
        public const string ManagerLevel = "Manager";
        public const string OfficerLevel = "Officer";

        private static readonly IReadOnlyDictionary<string, string> DesignationLevels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["cxo"] = CxoLevel,
                ["head"] = HeadLevel,
                ["sales manager"] = ManagerLevel,
                ["supervisor"] = ManagerLevel,
                ["sales manager/supervisor"] = ManagerLevel,
                ["sales manager / supervisor"] = ManagerLevel,
                ["sales officer"] = OfficerLevel
            };

        /// <summary>
        /// Returns the verified hierarchy level for a designation, or null when the
        /// designation is not part of the CXO/Head/Manager/Officer ladder. Null levels
        /// are treated as legacy/custom designations and are not restricted.
        /// </summary>
        public static string? GetDesignationLevel(string? designation)
        {
            var normalized = NormalizeDesignation(designation);
            return normalized != null && DesignationLevels.TryGetValue(normalized, out var level)
                ? level
                : null;
        }

        public static IReadOnlyDictionary<string, string> GetDesignationLevelMap()
        {
            return DesignationLevels;
        }

        public static IReadOnlyList<string> GetExpectedManagerLevels(string? designation)
        {
            return GetDesignationLevel(designation) switch
            {
                CxoLevel => Array.Empty<string>(),
                HeadLevel => new[] { CxoLevel },
                ManagerLevel => new[] { HeadLevel },
                OfficerLevel => new[] { ManagerLevel },
                _ => Array.Empty<string>()
            };
        }

        public static bool IsManagerLevelAllowed(string? designation, string? managerDesignation)
        {
            var level = GetDesignationLevel(designation);
            if (level == null)
            {
                // Custom/legacy designations keep working; there is no verified rule to enforce.
                return true;
            }

            if (level == CxoLevel)
            {
                return false;
            }

            var managerLevel = GetDesignationLevel(managerDesignation);
            return managerLevel != null && GetExpectedManagerLevels(designation).Contains(managerLevel);
        }

        /// <summary>
        /// Validates a create/edit manager assignment. Returns a user-facing error
        /// message, or null when the assignment is valid.
        /// </summary>
        public static string? ValidateAssignment(
            int salesPersonId,
            string? designation,
            int? managerId,
            IEnumerable<SalesPerson> allPeople)
        {
            var people = allPeople as IReadOnlyCollection<SalesPerson> ?? allPeople.ToList();
            var byId = people.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
            var level = GetDesignationLevel(designation);

            if (!managerId.HasValue)
            {
                if (level == CxoLevel)
                {
                    return null;
                }

                if (level != null)
                {
                    var expected = string.Join(" or ", GetExpectedManagerLevels(designation));
                    return $"{designation} must report to a {expected}.";
                }

                return null;
            }

            if (level == CxoLevel)
            {
                return "A CXO is a top-level role and cannot have a reporting manager.";
            }

            if (!byId.TryGetValue(managerId.Value, out var manager))
            {
                return "The selected reporting manager does not exist.";
            }

            if (salesPersonId > 0 && managerId.Value == salesPersonId)
            {
                return "A salesperson cannot report to themselves.";
            }

            if (salesPersonId > 0 && CreatesCycle(salesPersonId, managerId, people))
            {
                return "This manager would create a circular reporting relationship.";
            }

            if (level != null && !IsManagerLevelAllowed(designation, manager.Designation))
            {
                var expected = string.Join(" or ", GetExpectedManagerLevels(designation));
                return $"{designation} must report to a {expected}, not a {manager.Designation ?? "person with no designation"}.";
            }

            return null;
        }

        private static string? NormalizeDesignation(string? designation)
        {
            if (string.IsNullOrWhiteSpace(designation))
            {
                return null;
            }

            return string.Join(
                ' ',
                designation.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .ToLowerInvariant();
        }

        /// <summary>
        /// Returns true when assigning <paramref name="managerId"/> as the manager of
        /// <paramref name="salesPersonId"/> would create a cycle (including self-reporting).
        /// </summary>
        public static bool CreatesCycle(int salesPersonId, int? managerId, IEnumerable<SalesPerson> allPeople)
        {
            if (managerId == null)
            {
                return false;
            }

            if (managerId.Value == salesPersonId)
            {
                return true;
            }

            var byId = allPeople.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
            var visited = new HashSet<int>();
            var current = managerId;

            while (current != null && current.Value > 0)
            {
                if (!visited.Add(current.Value))
                {
                    // Existing data already contains a cycle; refuse the change.
                    return true;
                }

                if (current.Value == salesPersonId)
                {
                    return true;
                }

                if (!byId.TryGetValue(current.Value, out var person))
                {
                    return false;
                }

                current = person.ManagerId;
            }

            return false;
        }

        /// <summary>
        /// Returns all descendants (direct and indirect reports) of the given manager.
        /// Cycles in existing data are tolerated by tracking visited identifiers.
        /// </summary>
        public static IReadOnlyList<SalesPerson> GetTeamMembers(int managerId, IEnumerable<SalesPerson> allPeople)
        {
            var all = allPeople.ToList();
            var results = new List<SalesPerson>();
            var visited = new HashSet<int> { managerId };
            var queue = new Queue<int>();
            queue.Enqueue(managerId);

            while (queue.Count > 0)
            {
                var currentManager = queue.Dequeue();
                foreach (var report in all.Where(p => p.ManagerId == currentManager))
                {
                    if (!visited.Add(report.Id))
                    {
                        continue;
                    }

                    results.Add(report);
                    queue.Enqueue(report.Id);
                }
            }

            return results;
        }

        /// <summary>
        /// Returns true when <paramref name="candidateId"/> is the team owner or one of
        /// their direct/indirect reports. This is the server-side scope check used by
        /// manager screens; callers must still load the current hierarchy from the database.
        /// </summary>
        public static bool IsInTeam(int rootId, int candidateId, IEnumerable<SalesPerson> allPeople)
        {
            return rootId == candidateId
                || GetTeamMembers(rootId, allPeople).Any(p => p.Id == candidateId);
        }

        /// <summary>
        /// Returns the chain of managers above the given salesperson, nearest first.
        /// </summary>
        public static IReadOnlyList<SalesPerson> GetManagers(int salesPersonId, IEnumerable<SalesPerson> allPeople)
        {
            var all = allPeople.ToList();
            var byId = all.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
            var results = new List<SalesPerson>();
            var visited = new HashSet<int> { salesPersonId };

            if (!byId.TryGetValue(salesPersonId, out var person))
            {
                return results;
            }

            var current = person.ManagerId;
            while (current != null && visited.Add(current.Value) && byId.TryGetValue(current.Value, out var manager))
            {
                results.Add(manager);
                current = manager.ManagerId;
            }

            return results;
        }
    }
}
