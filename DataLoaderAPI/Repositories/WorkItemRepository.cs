
using CsvHelper;
using CsvHelper.Configuration;
using DataLoaderAPI.Models;
using DataLoaderAPI.Repositories.Interfaces;
using System.Globalization;

namespace DataLoaderAPI.Repositories
{
    public class WorkItemRepository : IWorkItemRepository
    {
        private readonly string _filePath;

        public WorkItemRepository(
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            var configuredPath = configuration["DataSource:Path"]
                ?? throw new InvalidOperationException(
                    "Missing configuration key 'DataSource:Path'.");

            _filePath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(
                    environment.ContentRootPath,
                    configuredPath);
        }

        public Task<PagedResult<Dictionary<string, string>>> SearchAsync(
            Dictionary<string, List<string>> filters,
            int page,
            int pageSize)
        {
            if (page < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(page), "Page must be greater than 0.");
            }

            if (pageSize < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pageSize), "Page size must be greater than 0.");
            }

            if (!File.Exists(_filePath))
            {
                throw new FileNotFoundException(
                    $"The configured data file was not found: {_filePath}");
            }

            var csvConfig = new CsvConfiguration(
                CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                PrepareHeaderForMatch = args => args.Header.Trim()
            };

            List<Dictionary<string, string>> records = new();
            List<string> headers;

            using (var reader = new StreamReader(_filePath))
            using (var csv = new CsvReader(reader, csvConfig))
            {
                if (!csv.Read())
                {
                    throw new InvalidDataException(
                        "The CSV file is empty.");
                }

                csv.ReadHeader();

                headers = csv.HeaderRecord?
                    .Select(header => header.Trim())
                    .ToList()
                    ?? new List<string>();

                if (headers.Count == 0)
                {
                    throw new InvalidDataException(
                        "The CSV file does not contain headers.");
                }

                if (headers.Distinct(
                    StringComparer.OrdinalIgnoreCase).Count()
                    != headers.Count)
                {
                    throw new InvalidDataException(
                        "The CSV file contains duplicate column headers.");
                }

                // Read every row using the actual CSV headers.
                while (csv.Read())
                {
                    var record = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase);

                    for (int i = 0; i < headers.Count; i++)
                    {
                        record[headers[i]] =
                            csv.GetField(i)?.Trim() ?? string.Empty;
                    }

                    records.Add(record);
                }
            }

            // Match incoming filter names to CSV headers.
            // Ignore spaces and capitalization.
            var normalizedFilters =
                new Dictionary<string, List<string>>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var filter in filters)
            {
                if (string.IsNullOrWhiteSpace(filter.Key))
                {
                    throw new ArgumentException(
                        "Filter field names cannot be empty.");
                }

                var normalizedFilterName =
                    NormalizeFieldName(filter.Key);

                var header = headers.FirstOrDefault(
                    h => NormalizeFieldName(h) == normalizedFilterName);

                if (header == null)
                {
                    throw new ArgumentException(
                        $"Unknown filter field '{filter.Key}'. " +
                        "Available fields: " +
                        string.Join(", ", headers));
                }

                var values = filter.Value?
                    .Where(value =>
                        !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                    ?? new List<string>();

                if (values.Count > 0)
                {
                    normalizedFilters[header] = values;
                }
            }

            // Different fields use AND.
            // Multiple values for one field use OR.
            IEnumerable<Dictionary<string, string>> filtered = records;

            foreach (var filter in normalizedFilters)
            {
                var fieldName = filter.Key;
                var acceptedValues = filter.Value;

                filtered = filtered.Where(record =>
                    acceptedValues.Any(value =>
                        string.Equals(
                            record[fieldName],
                            value,
                            StringComparison.OrdinalIgnoreCase)));
            }

            var matchingRecords = filtered.ToList();
            var totalCount = matchingRecords.Count;

            var items = matchingRecords
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(
                new PagedResult<Dictionary<string, string>>
                {
                    Items = items,
                    Headers = headers,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    HasMore = (long)page * pageSize < totalCount
                });
        }

        private static string NormalizeFieldName(string fieldName)
        {
            return new string(
                fieldName
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToLowerInvariant)
                    .ToArray());
        }
    }
}
