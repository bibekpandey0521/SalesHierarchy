
namespace SalesHierarchy.Core.Validation
{
    public sealed class ReportingHierarchyImportRow
    {
        public int RowNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Designation { get; set; }
        public string? ManagerName { get; set; }
    }

    public sealed class ReportingHierarchyImportIssue
    {
        public int? RowNumber { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsError { get; set; }
    }

    public sealed class ReportingHierarchyImportReport
    {
        public List<ReportingHierarchyImportIssue> Issues { get; } = new();
        public Dictionary<string, string?> ManagerNameByPerson { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public bool HasErrors => Issues.Any(i => i.IsError);
        public IReadOnlyList<ReportingHierarchyImportIssue> Errors =>
            Issues.Where(i => i.IsError).ToList();
        public IReadOnlyList<ReportingHierarchyImportIssue> Warnings =>
            Issues.Where(i => !i.IsError).ToList();
    }

    /// <summary>
    /// Validates parsed workbook rows before any ManagerId is written. This is the
    /// data-independent half of the hierarchy import: it detects duplicate names,
    /// conflicting designations/managers, missing managers, self-reporting and cycles.
    /// The workbook parser must supply the verified Name/Designation/ManagerName values.
    /// </summary>
    public static class HierarchyImportValidator
    {
        public static ReportingHierarchyImportReport Analyze(IEnumerable<ReportingHierarchyImportRow> rows)
        {
            var report = new ReportingHierarchyImportReport();
            var normalizedRows = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Name))
                .Select(r => new ReportingHierarchyImportRow
                {
                    RowNumber = r.RowNumber,
                    Name = NormalizeName(r.Name),
                    Designation = NormalizeText(r.Designation),
                    ManagerName = NormalizeNameOrNull(r.ManagerName)
                })
                .ToList();

            var byName = normalizedRows
                .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var group in byName.Values)
            {
                var designations = group
                    .Select(r => r.Designation)
                    .Where(d => d != null)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (designations.Count > 1)
                {
                    report.Issues.Add(new ReportingHierarchyImportIssue
                    {
                        RowNumber = group[0].RowNumber,
                        IsError = true,
                        Message = $"'{group[0].Name}' appears with conflicting designations: {string.Join(", ", designations)}."
                    });
                }

                var managers = group
                    .Select(r => r.ManagerName)
                    .Where(m => m != null)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (managers.Count > 1)
                {
                    report.Issues.Add(new ReportingHierarchyImportIssue
                    {
                        RowNumber = group[0].RowNumber,
                        IsError = true,
                        Message = $"'{group[0].Name}' appears with conflicting managers: {string.Join(", ", managers)}."
                    });
                }

                if (group.Count > 1)
                {
                    report.Issues.Add(new ReportingHierarchyImportIssue
                    {
                        RowNumber = group[0].RowNumber,
                        IsError = designations.Count > 1 || managers.Count > 1,
                        Message = $"'{group[0].Name}' appears {group.Count} times in the source."
                    });
                }
            }

            // Use the first occurrence for validation; duplicate conflicts are already reported.
            var canonical = byName.Values
                .Select(g => g[0])
                .OrderBy(r => r.RowNumber)
                .ToList();

            foreach (var person in canonical)
            {
                var level = HierarchyRules.GetDesignationLevel(person.Designation);
                if (string.IsNullOrWhiteSpace(person.Designation))
                {
                    report.Issues.Add(Error(person, "Designation is required."));
                    continue;
                }

                if (level == null)
                {
                    report.Issues.Add(Warning(person, $"Designation '{person.Designation}' is not part of the verified CXO/Head/Manager/Officer ladder; manager rules were not enforced."));
                }

                if (level == HierarchyRules.CxoLevel)
                {
                    if (!string.IsNullOrWhiteSpace(person.ManagerName))
                    {
                        report.Issues.Add(Error(person, "A CXO must not have a reporting manager."));
                    }

                    report.ManagerNameByPerson[person.Name] = null;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(person.ManagerName))
                {
                    if (level != null)
                    {
                        report.Issues.Add(Error(person, $"The expected reporting manager for '{person.Name}' is blank."));
                    }

                    report.ManagerNameByPerson[person.Name] = null;
                    continue;
                }

                if (string.Equals(person.Name, person.ManagerName, StringComparison.OrdinalIgnoreCase))
                {
                    report.Issues.Add(Error(person, "A salesperson cannot report to themselves."));
                    continue;
                }

                if (!byName.TryGetValue(person.ManagerName, out var managerRows))
                {
                    report.Issues.Add(Error(person, $"Reporting manager '{person.ManagerName}' was not found in the source."));
                    continue;
                }

                var manager = managerRows[0];
                if (level != null &&
                    !HierarchyRules.IsManagerLevelAllowed(person.Designation, manager.Designation))
                {
                    report.Issues.Add(Error(
                        person,
                        $"{person.Designation} cannot report to {manager.Designation ?? "a person with no designation"}."));
                    continue;
                }

                report.ManagerNameByPerson[person.Name] = manager.Name;
            }

            DetectCycles(canonical, report);
            return report;
        }

        /// <summary>
        /// Cycle detection runs on the raw manager-name graph rather than the accepted
        /// hierarchy edges, because a cycle that also violates the designation ladder
        /// must still be reported (the import must never write circular ManagerId data).
        /// </summary>
        private static void DetectCycles(
            IReadOnlyList<ReportingHierarchyImportRow> rows,
            ReportingHierarchyImportReport report)
        {
            var byName = rows
                .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var parentByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.ManagerName) ||
                    string.Equals(row.Name, row.ManagerName, StringComparison.OrdinalIgnoreCase) ||
                    !byName.ContainsKey(row.ManagerName))
                {
                    continue;
                }

                parentByName[row.Name] = row.ManagerName;
            }

            // 0/absent = unvisited, 1 = on the current walk, 2 = fully explored.
            var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var reportedCycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var start in byName.Keys)
            {
                if (state.ContainsKey(start))
                {
                    continue;
                }

                var walk = new List<string>();
                var current = start;
                while (current != null && byName.ContainsKey(current))
                {
                    if (state.TryGetValue(current, out var currentState))
                    {
                        if (currentState == 1)
                        {
                            var cycleStart = walk.FindIndex(
                                name => string.Equals(name, current, StringComparison.OrdinalIgnoreCase));
                            if (cycleStart >= 0)
                            {
                                var cycleKey = string.Join(
                                    "|",
                                    walk.Skip(cycleStart).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
                                if (reportedCycles.Add(cycleKey))
                                {
                                    report.Issues.Add(Error(
                                        byName[current],
                                        "Circular reporting relationship detected in the source."));
                                }
                            }
                        }

                        break;
                    }

                    state[current] = 1;
                    walk.Add(current);
                    current = parentByName.TryGetValue(current, out var parent) ? parent : null;
                }

                foreach (var name in walk)
                {
                    state[name] = 2;
                }
            }
        }

        private static ReportingHierarchyImportIssue Error(ReportingHierarchyImportRow row, string message) =>
            new() { RowNumber = row.RowNumber, Message = message, IsError = true };

        private static ReportingHierarchyImportIssue Warning(ReportingHierarchyImportRow row, string message) =>
            new() { RowNumber = row.RowNumber, Message = message, IsError = false };

        private static string NormalizeName(string value)
        {
            return string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string? NormalizeNameOrNull(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : NormalizeName(value);
        }

        private static string? NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
