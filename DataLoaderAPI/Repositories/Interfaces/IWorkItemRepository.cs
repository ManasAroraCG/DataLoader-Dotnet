
using DataLoaderAPI.Models;

namespace DataLoaderAPI.Repositories.Interfaces
{
    public interface IWorkItemRepository
    {
        Task<PagedResult<Dictionary<string, string>>> SearchAsync(
            Dictionary<string, List<string>> filters,
            int page,
            int pageSize);
    }
}
