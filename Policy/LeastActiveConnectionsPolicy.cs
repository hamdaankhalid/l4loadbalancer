namespace loadbalancer.Policy;

public class LeastActiveConnectionsPolicy : IPolicy
{
    private TopologyManagement _topologyManagement;

    public LeastActiveConnectionsPolicy(TopologyManagement topologyManagement)
    {
        this._topologyManagement = topologyManagement;
    }

    public string? GetTarget()
    {
        return "";
    }
}
