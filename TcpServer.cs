using System;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

using loadbalancer.Policy;

namespace loadbalancer;

class TcpServer
{
    private string _ipAddr;

    private int _port;

    private IPolicy _policy;

    private TopologyManagement _topology;

    public TcpServer(string ipAddr, int port, IPolicy policy, TopologyManagement topology)
    {
        this._ipAddr = ipAddr;
        this._port = port;
        this._policy = policy;
        this._topology = topology;
    }

    public async Task Listen()
    {
        // Proxy local Server endpoint
        IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(this._ipAddr), this._port);

        TcpListener tcpListener = new TcpListener(endpoint);

        tcpListener.Start();

        Console.WriteLine($"Load Balancer listening for incoming connections {tcpListener.LocalEndpoint}...");

        try
        {
            while (true)
            {
                // wait to recv connection
                TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();
                // kick off a nonblocking task to handle connection		
                this._handleConnection(tcpClient);
            }
        }
        finally
        {
            tcpListener.Stop();
        }
    }

    private async Task _handleConnection(TcpClient client)
    {
        // setup non blocking forwarding of data from client to target server and from target server to client
        // use task based concurrency for the above described setup.

        TcpClient? target = null;
        try
        {
            // connect the target TcpClient
            target = await this._failsafeTargetConnection();
            if (target == null)
            {
                // nothing available close the client conn
                client.Close();
                return;
            }

            using (NetworkStream clientStream = client.GetStream())
            using (NetworkStream targetStream = target.GetStream())
            {
                // pipe / read write connect the streams
                Task clientToTarget = clientStream.CopyToAsync(targetStream);
                Task targetToClient = targetStream.CopyToAsync(clientStream);

                // when either tasks terminate we need to close the proxy channel
                await Task.WhenAny(clientToTarget, targetToClient);
            }
        }
        catch (Exception)
        {
            // Explicit signal ignore in fire and forget task to avoid process crash
        }
        finally
        {
            // safely dispose TcpClients
            client.Close();
            if (target != null)
            {
                target.Close();
            }
        }
    }

    /*
	 * given a tcp client attempt connection, if it 
	 * fails try to forward the connection to a 
	 * different target server attempt re-picking from 
	 * pool multiple times.
	 * */
    private async Task<TcpClient?> _failsafeTargetConnection()
    {
        int maxAttempts = 5;
        int attempts = 0;
        TcpClient tcpClient;
        string? targetServerAddr = null;
        while (attempts < maxAttempts)
        {
            try
            {
                targetServerAddr = this._policy.GetTarget();

                // we have run out of server pool
                if (targetServerAddr == null)
                {
                    Console.WriteLine("Failure: Policy found no suitable server in the backend pool");
                    return null;
                }

                // create an connect client to target server TCP conn
                IPEndPoint targetEndpoint = IPEndPoint.Parse(targetServerAddr);
                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(targetEndpoint);

                Console.WriteLine($"Target server tcp client connected with address: {targetServerAddr}");

                return tcpClient;
            }
            catch (SocketException)
            {
                if (targetServerAddr != null)
                {
                    Console.WriteLine($"Retiring server from active pool: {targetServerAddr}");
                    this._topology.Retire(targetServerAddr);
                }
            }
            finally
            {
                attempts++;
            }
        }

        // shame on the application server pool, literally none of them were connectable
        return null;
    }
}
