using System.Net.Sockets;

namespace loadbalancer.Policy;

public interface IPolicy
{
    // if available target then send target else null
    string? GetTarget();
}
