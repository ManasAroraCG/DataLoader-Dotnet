
using CsvHelper;
using CsvHelper.Configuration;
using DataLoaderAPI.Models;
using DataLoaderAPI.Repositories.Interfaces;
using Microsoft.AspNetCore.Hosting;
using System.Globalization;

namespace DataLoaderAPI.Repositories
{
    public class WorkItemRepository : IWorkItemRepository
    {

        private readonly string _filepath;

        public WorkItemRepository(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var configuredPath = configuration["DataSource:Path"]
                ?? throw new InvalidOperationException("Missing configuration key 'DataSource:Path'.");

            _filepath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath);
        }


        public Task<PagedResult<WorkItem>> GetAllWorkItemsAsync(
            int page,
            int pageSize,
            string? brandCode,
            string? brandName)
        {
            if (!File.Exists(_filepath))
            {
                throw new FileNotFoundException(
                    $"The file {_filepath} does not exist.");
            }

            using var reader = new StreamReader(_filepath);

            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = "\t",
                HasHeaderRecord = true
            };

            using var csv = new CsvReader(reader, csvConfig);

            csv.Context.RegisterClassMap<WorkItemMap>();

            var records = csv.GetRecords<WorkItem>().ToList();

            // Filter by Brand Code.
            if (!string.IsNullOrWhiteSpace(brandCode))
            {
                records = records
                    .Where(x => string.Equals(
                        x.BrandCode?.Trim(),
                        brandCode.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Filter by Brand Name.
            if (!string.IsNullOrWhiteSpace(brandName))
            {
                records = records
                    .Where(x => x.BrandName != null &&
                        x.BrandName.Contains(
                            brandName.Trim(),
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Count records after applying filters.
            var totalCount = records.Count;

            var items = records
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var hasMore = page * pageSize < totalCount;

            return Task.FromResult(new PagedResult<WorkItem>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                HasMore = hasMore
            });
        }

        
        public Task<PagedResult<WorkItem>> GetByAccountNumbersAsync(
            List<string> accountNumbers,
            int page,
            int pageSize)
        {
            if (!File.Exists(_filepath))
            {
                throw new FileNotFoundException(
                    $"The file {_filepath} does not exist.");
            }

            using var reader = new StreamReader(_filepath);

            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = "\t",
                HasHeaderRecord = true
            };

            using var csv = new CsvReader(reader, csvConfig);

            csv.Context.RegisterClassMap<WorkItemMap>();

            var requestedNumbers = new HashSet<string>(
                accountNumbers
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var records = csv.GetRecords<WorkItem>()
                .Where(x => requestedNumbers.Contains(x.AccountNumber.Trim()))
                .ToList();

            var totalCount = records.Count;

            var items = records
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var hasMore = page * pageSize < totalCount;

            return Task.FromResult(new PagedResult<WorkItem>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                HasMore = hasMore
            });
        }

    }
}
