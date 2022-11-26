using Microsoft.Azure.Management.Media.Models;

namespace RadioArchive
{
    public interface IStreamingLocatorGenerator
    {
        Task<IDictionary<string, StreamingPath>> Generate(LocatorRequest request);
    }
}

