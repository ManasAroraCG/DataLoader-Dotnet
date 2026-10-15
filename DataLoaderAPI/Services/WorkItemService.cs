using DataLoaderAPI.Models;
using DataLoaderAPI.Repositories.Interfaces;
using DataLoaderAPI.Services.Interfaces;

namespace DataLoaderAPI.Services
{
    public class WorkItemService : IWorkItemService
    {
        private readonly IWorkItemRepository _workItemRepository;

        public WorkItemService(IWorkItemRepository workItemRepository)
        {
            _workItemRepository = workItemRepository;
        }

        public Task<PagedResult<Dictionary<string, string>>> SearchAsync(
            Dictionary<string, List<string>> filters,
            int page,
            int pageSize)
        {
            return _workItemRepository.SearchAsync(
                filters,
                page,
                pageSize);
        }
    }
}
