using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;

namespace loadbalancer.Policy;

/*
 * Selects servers on a round robin basis.
 * */
public class RoundRobinPolicy : IPolicy
{

    private int _counter = 0;

    private TopologyManagement _topologyManagement;

    public RoundRobinPolicy(TopologyManagement topologyManagement)
    {
        this._topologyManagement = topologyManagement;
    }

    public string? GetTarget()
    {
        ReadOnlyCollection<string> serverPool = this._topologyManagement.GetPool();
        if (serverPool.Count < 1)
        {
            return null;
        }
        this._counter = (this._counter + 1) % serverPool.Count;
        string server = serverPool[this._counter];
        return server;
    }
}
