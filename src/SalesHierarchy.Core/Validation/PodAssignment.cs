
namespace SalesHierarchy.Core.Validation
{
    /// <summary>
    /// Pure, data-access-free helpers for synchronising a salesperson's Pod
    /// assignments. Duplicate and invalid identifiers are handled explicitly by
    /// callers so they can surface a user-facing validation message.
    /// </summary>
    public static class PodAssignment
    {
        public static IReadOnlyList<int> Normalize(IEnumerable<int>? podIds)
        {
            return podIds == null
                ? Array.Empty<int>()
                : podIds.Distinct().OrderBy(id => id).ToArray();
        }

        public static (IReadOnlyList<int> ToAdd, IReadOnlyList<int> ToRemove) Diff(
            IEnumerable<int> existingPodIds,
            IEnumerable<int> submittedPodIds)
        {
            var existing = Normalize(existingPodIds).ToHashSet();
            var submitted = Normalize(submittedPodIds);

            return (
                submitted.Where(id => !existing.Contains(id)).ToArray(),
                existing.Where(id => !submitted.Contains(id)).ToArray());
        }

        /// <summary>
        /// Validates the names that would make up a station's Pod list. The filtered
        /// unique index UX_Pod_Station_Name allows the same Pod name in different
        /// stations but not twice inside one station, so the UI checks the same rule
        /// before writing.
        /// </summary>
        /// <returns>A user-facing message, or <c>null</c> when the names are valid.</returns>
        public static string? FindStationNameConflict(
            IEnumerable<string> selectedPodNames,
            IEnumerable<string> namesStayingInStation)
        {
            var selected = (selectedPodNames ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .ToList();

            var duplicate = selected
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                return $"Two selected Pods are both named '{duplicate.Key}'. A station cannot contain duplicate Pod names.";
            }

            var staying = (namesStayingInStation ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var conflict = selected.FirstOrDefault(staying.Contains);
            return conflict == null
                ? null
                : $"Pod '{conflict}' has the same name as another Pod already in this station. Rename one of them before assigning.";
        }
    }
}
