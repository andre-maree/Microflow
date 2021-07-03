using Microsoft.Azure.Cosmos.Table;
using System.Collections.Generic;

namespace Microflow.Models
{
    public interface IStepEntity : ITableEntity
    {
        Dictionary<string, string> MergeFields { get; set; }
        string SubSteps { get; set; }
    }
}