using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InfrastructureTools.Connectors.AzureDevOps;

public class AzureDevOpsList<T> : IEnumerable<T>
{
    public AzureDevOpsList() { }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("value")]
    public List<T> Value { get; set; } = new List<T>();

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Value).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Value).GetEnumerator();
}
