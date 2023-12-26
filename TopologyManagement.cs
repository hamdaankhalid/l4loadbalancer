using System;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.Threading;
using StackExchange.Redis;

namespace loadbalancer;

public class TopologyManagement
{
    // retirement time period
    private static readonly int TimePeriod = 2000;

    private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();

    private List<string> _serverPool = new List<string>();

    private readonly List<string> _retirementZone = new List<string>();

    private readonly string _redisString;

    private readonly ConnectionMultiplexer _redis;

    private readonly IDatabase _db;

    public TopologyManagement(string redisConnString)
    {
        this._redisString = redisConnString;
        // connect to 6379 port default
        this._redis = ConnectionMultiplexer.Connect(this._redisString);
        this._db = this._redis.GetDatabase();
    }

    public async Task InitServerPool()
    {
        string? serverPoolStr = await this._db.StringGetAsync("serverPool");
        if (serverPoolStr == null)
        {
            throw new Exception("Failed to get serverPool");
        }
        List<string>? serverPool = JsonSerializer.Deserialize<List<string>>(serverPoolStr);
        if (serverPool == null)
        {
            throw new Exception("failed to deser serverPool");
        }
        this._serverPool = serverPool;
    }

    public async Task WatchTopology()
    {
        // Attempting to make everything stack based 
        ISubscriber sub = this._redis.GetSubscriber();

        RedisChannel addServerChan = RedisChannel.Literal("addServer");
		
		Action<RedisChannel, RedisValue> addServerHandler = (_, message) =>
        {
            this._rwLock.EnterWriteLock();
            try
            {
                string msgServerAddr = message.ToString();
                Console.WriteLine($"Topology changing, server being added {msgServerAddr}");
                this._serverPool.Add(msgServerAddr);
                this._db.StringSet("serverPool", JsonSerializer.Serialize(this._serverPool));
            }
            finally
            {
                this._rwLock.ExitWriteLock();
                Console.WriteLine($"Topology changed, server added {message.ToString()}");
            }
        };

        await sub.SubscribeAsync(addServerChan, addServerHandler);

        RedisChannel rmServerChan = RedisChannel.Literal("removeServer");

		Action<RedisChannel, RedisValue> rmServerHandler = (_, message) =>
        {
            this._rwLock.EnterWriteLock();
            try
            {
                string msgServerAddr = message.ToString();
                Console.WriteLine($"Topology changing, server being removed {msgServerAddr}");
                // check active server pool
                for (int i = 0; i < this._serverPool.Count; i++)
                {
                    if (this._serverPool[i] == msgServerAddr)
                    {
                        this._serverPool.RemoveAt(i);
                        this._db.StringSet("serverPool", JsonSerializer.Serialize(this._serverPool));
                        Console.WriteLine("Topology changed, server removed from Server Pool");
                        return;
                    }
                }

                // check retirement zone 
                for (int i = 0; i < this._retirementZone.Count; i++)
                {
                    if (this._retirementZone[i] == msgServerAddr)
                    {
                        this._retirementZone.RemoveAt(i);
                        Console.WriteLine("Topology changed, server removed from Retirement Zone");
                        return;
                    }
                }
            }
            finally
            {
                this._rwLock.ExitWriteLock();
            }
        };

        await sub.SubscribeAsync(rmServerChan, rmServerHandler);
	}

    // Read locking is only there to safeguard against contention
    public ReadOnlyCollection<string> GetPool()
    {
        try
        {
            this._rwLock.EnterReadLock();
            return this._serverPool.AsReadOnly();
        }
        finally
        {
            this._rwLock.ExitReadLock();
        }
    }


    public void Retire(string serverAddr)
    {
        try
        {
            bool found = false;
            this._rwLock.EnterWriteLock();
            // find the serverAddr in active pool
            for (int i = 0; i < this._serverPool.Count; i++)
            {
                if (this._serverPool[i] == serverAddr)
                {
                    this._serverPool.RemoveAt(i);
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                return;
            }

            this._retirementZone.Add(serverAddr);

            // add it to retirement zone for a little time
            // after which it should be added again into the active pool
            TimerCallback timerCallback = async (_serverAddr) =>
            {
                try
                {
                    this._rwLock.EnterWriteLock();
                    string serverAddr = (string)_serverAddr!;
                    int foundRecommisionAt = -1;
                    // find the index of the serverAddr in Retirementr zone
                    // if index is found then we need to remove it from retirement
                    // and bring it into server pool
                    for (int i = 0; i < this._retirementZone.Count; i++)
                    {
                        if (this._retirementZone[i] == serverAddr)
                        {
                            foundRecommisionAt = i;
                            break;
                        }
                    }
                    // this is possible if redis published an event that removed
                    // the server from our list of servers.
                    if (foundRecommisionAt == -1)
                    {
                        return;
                    }

                    if (foundRecommisionAt != -1)
                    {
                        this._retirementZone.RemoveAt(foundRecommisionAt);
                        this._serverPool.Add(serverAddr);
                        await this._db.StringSetAsync("serverPool", JsonSerializer.Serialize(this._serverPool));
                    }
                }
                finally
                {
                    this._rwLock.ExitWriteLock();
                }
            };

            Timer timer = new Timer(timerCallback, serverAddr, TopologyManagement.TimePeriod, Timeout.Infinite);
            this._db.StringSet("serverPool", JsonSerializer.Serialize(this._serverPool));
        }
        finally
        {
            this._rwLock.ExitWriteLock();
        }
    }
}
