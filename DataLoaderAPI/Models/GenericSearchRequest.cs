using System.Text.Json;

namespace DataLoaderAPI.Models
{
    public class GenericSearchRequest
    {
        public Dictionary<string, JsonElement>? Filters { get; set; }

        // Backward compatibility with the existing Salesforce request.
        public List<string>? AccountNumbers { get; set; }
    }
}
