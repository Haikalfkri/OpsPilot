using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Common.Models
{
    public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
    {
        public int TotalPges => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

        public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) =>
            new(Items.Select(map).ToList(), Page, PageSize, TotalCount);
    }
}
