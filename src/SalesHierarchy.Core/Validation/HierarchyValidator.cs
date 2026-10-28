using SalesHierarchy.Core.Models;

namespace SalesHierarchy.Core.Validation
{
    /// <summary>
    /// Pure business rules for the reporting hierarchy. Kept free of data-access concerns
    /// so the same rules can be applied by controllers, bulk import and tests.
    /// </summary>
    public static class HierarchyValidator
    {
        /// <summary>
        /// Returns true when assigning <paramref name="managerId"/> as the manager of
        /// <paramref name="salesPersonId"/> would create a cycle (including self-reporting).
        /// </summary>
        public static bool WouldCreateCycle(int salesPersonId, int? managerId, IEnumerable<SalesPerson> allPeople)
        {
            if (managerId == null)
            {
                return false;
            }

            if (managerId.Value == salesPersonId)
            {
                return true;
            }

            var byId = allPeople.GroupBy(p => p.ID).ToDictionary(g => g.Key, g => g.First());
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
        public static IReadOnlyList<SalesPerson> GetDescendants(int managerId, IEnumerable<SalesPerson> allPeople)
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
                    if (!visited.Add(report.ID))
                    {
                        continue;
                    }

                    results.Add(report);
                    queue.Enqueue(report.ID);
                }
            }

            return results;
        }

        /// <summary>
        /// Returns true when <paramref name="candidateId"/> is the team owner or one of
        /// their direct/indirect reports. This is the server-side scope check used by
        /// manager screens; callers must still load the current hierarchy from the database.
        /// </summary>
        public static bool IsInSubtree(int rootId, int candidateId, IEnumerable<SalesPerson> allPeople)
        {
            return rootId == candidateId
                || GetDescendants(rootId, allPeople).Any(p => p.ID == candidateId);
        }

        /// <summary>
        /// Returns the chain of managers above the given salesperson, nearest first.
        /// </summary>
        public static IReadOnlyList<SalesPerson> GetManagerChain(int salesPersonId, IEnumerable<SalesPerson> allPeople)
        {
            var all = allPeople.ToList();
            var byId = all.GroupBy(p => p.ID).ToDictionary(g => g.Key, g => g.First());
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
