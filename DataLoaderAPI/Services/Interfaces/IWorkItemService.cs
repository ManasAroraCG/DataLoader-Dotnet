
using DataLoaderAPI.Models;

namespace DataLoaderAPI.Services.Interfaces
{
    public interface IWorkItemService
    {
        Task<PagedResult<Dictionary<string, string>>> SearchAsync(
            Dictionary<string, List<string>> filters,
            int page,
            int pageSize);
    }
}
