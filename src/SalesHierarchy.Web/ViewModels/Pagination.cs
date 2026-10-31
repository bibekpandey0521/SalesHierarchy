using System;
using System.Collections.Generic;
using System.Linq;

namespace SalesHierarchy.Web.ViewModels
{
    /// <summary>
    /// Shared paging metadata used by the admin list screens and the _Pager partial.
    /// </summary>
    public class PaginationViewModel
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = Paging.DefaultPageSize;
        public int TotalItems { get; set; }
        public string Action { get; set; } = string.Empty;
        public Dictionary<string, string> RouteValues { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
        public bool HasPrevious => Page > 1;
        public bool HasNext => Page < TotalPages;
        public int FirstItem => TotalItems == 0 ? 0 : ((Page - 1) * PageSize) + 1;
        public int LastItem => Math.Min(Page * PageSize, TotalItems);
    }

    public static class Paging
    {
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 200;

        public static (List<T> Items, PaginationViewModel Pager) Paginate<T>(
            IEnumerable<T> source,
            int page,
            int pageSize = DefaultPageSize)
        {
            var all = source as IList<T> ?? source.ToList();
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var totalPages = Math.Max(1, (int)Math.Ceiling(all.Count / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var items = all
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (items, new PaginationViewModel
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = all.Count
            });
        }
    }
}
