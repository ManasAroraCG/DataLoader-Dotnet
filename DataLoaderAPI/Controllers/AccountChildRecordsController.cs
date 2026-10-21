using Azure.Storage.Blobs;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;

namespace DataLoaderAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountChildRecordsController : ControllerBase
    {
        private readonly BlobClient _blobClient;

        public AccountChildRecordsController(
            IConfiguration configuration,
            BlobServiceClient blobServiceClient)
        {
            var containerName =
                configuration["AccountChildDataSource:ContainerName"]
                ?? throw new InvalidOperationException(
                    "Missing AccountChildDataSource:ContainerName.");

            var blobName =
                configuration["AccountChildDataSource:BlobName"]
                ?? throw new InvalidOperationException(
                    "Missing AccountChildDataSource:BlobName.");

            _blobClient = blobServiceClient
                .GetBlobContainerClient(containerName)
                .GetBlobClient(blobName);
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 200)
        {
            if (page < 1 || pageSize < 1)
            {
                return BadRequest(
                    "Page and pageSize must be greater than zero.");
            }

            var download = await _blobClient.DownloadStreamingAsync();

            var csvConfig = new CsvConfiguration(
                CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                PrepareHeaderForMatch =
                    args => args.Header.Trim()
            };

            List<string> headers;
            List<string[]> records = new();

            using (var reader =
                new StreamReader(download.Value.Content))
            using (var csv = new CsvReader(reader, csvConfig))
            {
                if (!await csv.ReadAsync())
                {
                    return BadRequest("The child CSV file is empty.");
                }

                csv.ReadHeader();

                headers = csv.HeaderRecord?
                    .Select(header => header.Trim())
                    .ToList()
                    ?? new List<string>();

                if (headers.Count == 0)
                {
                    return BadRequest(
                        "The child CSV file has no headers.");
                }

                if (headers.Distinct(
                    StringComparer.OrdinalIgnoreCase).Count()
                    != headers.Count)
                {
                    return BadRequest(
                        "The child CSV contains duplicate headers.");
                }

                while (await csv.ReadAsync())
                {
                    var row = new string[headers.Count];

                    for (int i = 0; i < headers.Count; i++)
                    {
                        row[i] = csv.GetField(i) ?? string.Empty;
                    }

                    records.Add(row);
                }
            }

            var totalCount = records.Count;

            var pageRecords = records
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            Response.Headers["X-Page"] =
                page.ToString(CultureInfo.InvariantCulture);

            Response.Headers["X-Page-Size"] =
                pageSize.ToString(CultureInfo.InvariantCulture);

            Response.Headers["X-Total-Count"] =
                totalCount.ToString(CultureInfo.InvariantCulture);

            Response.Headers["X-Has-More"] =
                ((long)page * pageSize < totalCount)
                    .ToString()
                    .ToLowerInvariant();

            using var writer =
                new StringWriter(CultureInfo.InvariantCulture);

            var outputConfig = new CsvConfiguration(
                CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                HasHeaderRecord = false
            };

            using (var outputCsv =
                new CsvWriter(writer, outputConfig))
            {
                // Include headers on every page so each response
                // is a self-contained CSV file.
                foreach (var header in headers)
                {
                    outputCsv.WriteField(header);
                }

                await outputCsv.NextRecordAsync();

                foreach (var row in pageRecords)
                {
                    foreach (var value in row)
                    {
                        outputCsv.WriteField(value);
                    }

                    await outputCsv.NextRecordAsync();
                }

                await outputCsv.FlushAsync();
            }

            return Content(
                writer.ToString(),
                "text/csv",
                Encoding.UTF8);
        }
    }
}
