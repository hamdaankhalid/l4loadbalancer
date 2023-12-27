using loadbalancer.Policy;

namespace loadbalancer;

class Program
{
    static async Task Main(string[] args)
    {
        TopologyManagement topology = new TopologyManagement("localhost");

        // use redis and get init server pool
        await topology.InitServerPool();

        // watch for changes in server pool config in redis
        await topology.WatchTopology();

        // TODO: should pick the policy from config file
        RoundRobinPolicy lbPolicy = new RoundRobinPolicy(topology);

        // TODO: should pick proxy IP and Port from config file
        TcpServer proxy = new TcpServer("127.0.0.1", 8080, lbPolicy, topology);

        await proxy.Listen();
    }
}
