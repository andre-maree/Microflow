using System.Threading.Tasks;

namespace Microflow.FlowControl
{
    public interface IMicroflowContext
    {
        Task RunMicroflow();
    }
}