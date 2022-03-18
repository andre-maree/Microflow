using Azure.Data.Tables;
using System.Collections.Generic;

namespace MicroflowModels
{
    public interface IStepEntity : ITableEntity
    {
        Dictionary<string, string> MergeFields { get; set; }
        string SubSteps { get; set; }
    }
}