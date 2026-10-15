using CsvHelper;
using CsvHelper.Configuration;
using DataLoaderAPI.Models;
using DataLoaderAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DataLoaderAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataController : ControllerBase
    {
        private readonly IWorkItemService _workItemService;

        public DataController(IWorkItemService workItemService)
        {
            _workItemService = workItemService;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 200)
        {
            var filters = new Dictionary<string, List<string>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var queryParam in Request.Query)
            {
                if (string.Equals(
                        queryParam.Key, "page",
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        queryParam.Key, "pageSize",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var values = queryParam.Value
                    .Where(value => value != null)
                    .Select(value => value!)
                    .ToList();

                filters[queryParam.Key] = values;
            }

            return await SearchAndReturnCsv(filters, page, pageSize);
        }

        [HttpPost("search")]
        public async Task<IActionResult> Search(
            [FromBody] GenericSearchRequest request,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 200)
        {
            var filters = new Dictionary<string, List<string>>(
                StringComparer.OrdinalIgnoreCase);

            if (request.Filters != null)
            {
                foreach (var filter in request.Filters)
                {
                    filters[filter.Key] =
                        ConvertFilterValue(filter.Value);
                }
            }

            // Support the existing Salesforce accountNumbers payload.
            // Use it only when generic filters were not supplied.
            if (filters.Count == 0
                && request.AccountNumbers is { Count: > 0 })
            {
                filters["Account Number"] = request.AccountNumbers;
            }

            return await SearchAndReturnCsv(filters, page, pageSize);
        }

        private async Task<IActionResult> SearchAndReturnCsv(
            Dictionary<string, List<string>> filters,
            int page,
            int pageSize)
        {
            if (page < 1)
            {
                return BadRequest("Page must be greater than 0.");
            }

            if (pageSize < 1)
            {
                return BadRequest(
                    "Page size must be greater than 0.");
            }

            PagedResult<Dictionary<string, string>> result;

            try
            {
                result = await _workItemService.SearchAsync(
                    filters, page, pageSize);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }

            Response.Headers["X-Page"] =
                result.Page.ToString(CultureInfo.InvariantCulture);

            Response.Headers["X-Page-Size"] =
                result.PageSize.ToString(CultureInfo.InvariantCulture);

            Response.Headers["X-Total-Count"] =
                result.TotalCount.ToString(CultureInfo.InvariantCulture);

            Response.Headers["X-Has-More"] =
                result.HasMore.ToString().ToLowerInvariant();

            var csvConfig = new CsvConfiguration(
                CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                HasHeaderRecord = false
            };

            using var writer = new StringWriter(
                CultureInfo.InvariantCulture);

            using (var csv = new CsvWriter(writer, csvConfig))
            {
                // Preserve the original behavior: the first page
                // includes the CSV header row.
                if (result.Page == 1)
                {
                    foreach (var header in result.Headers)
                    {
                        csv.WriteField(header);
                    }

                    await csv.NextRecordAsync();
                }

                foreach (var record in result.Items)
                {
                    foreach (var header in result.Headers)
                    {
                        record.TryGetValue(header, out var value);
                        csv.WriteField(value ?? string.Empty);
                    }

                    await csv.NextRecordAsync();
                }

                await csv.FlushAsync();
            }

            var csvContent = writer.ToString();

            return Content(
                csvContent,
                "text/csv",
                Encoding.UTF8);
        }

        private static List<string> ConvertFilterValue(
            JsonElement element)
        {
            var values = new List<string>();

            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    AddIfNotEmpty(values, element.GetString());
                    break;

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    AddIfNotEmpty(values, element.GetRawText());
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        switch (item.ValueKind)
                        {
                            case JsonValueKind.String:
                                AddIfNotEmpty(values, item.GetString());
                                break;

                            case JsonValueKind.Number:
                            case JsonValueKind.True:
                            case JsonValueKind.False:
                                AddIfNotEmpty(
                                    values, item.GetRawText());
                                break;

                            case JsonValueKind.Null:
                                break;

                            default:
                                throw new ArgumentException(
                                    "Filter arrays can contain only " +
                                    "strings, numbers, booleans, or null.");
                        }
                    }
                    break;

                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    break;

                default:
                    throw new ArgumentException(
                        "Filter values must be scalars or arrays " +
                        "of scalars.");
            }

            return values;
        }

        private static void AddIfNotEmpty(
            List<string> values,
            string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value.Trim());
            }
        }
    }
}
