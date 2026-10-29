
namespace SalesHierarchy.Core.Validation
{
    /// <summary>
    /// Pure, data-access-free helpers for synchronising a salesperson's POD
    /// assignments. Duplicate and invalid identifiers are handled explicitly by
    /// callers so they can surface a user-facing validation message.
    /// </summary>
    public static class PODAssignment
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
    }
}
