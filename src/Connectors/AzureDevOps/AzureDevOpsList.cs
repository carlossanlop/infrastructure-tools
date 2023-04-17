using System.Collections.Generic;

namespace InfrastructureTools.Connectors.AzureDevOps;

public class AzureDevOpsList<T>
{
    public AzureDevOpsList() { }

    public int Count { get; set; }

    public List<T> Value { get; set; } = new List<T>();
}
