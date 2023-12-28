# LoadBalancer

1. Client Conn -> Proxy -> [Backend pool selected server]
2. Client Conn <- Proxy <- [Backend pool selected server]

Server handles connection forwarding to pool, based on policy.

## Policies
1. Round robin - Done
2. Least Active connection policy
3. Location Based Routing

## Features
- Layer 4 TCP LB
- Runtime addition and removal of servers from server pool
- Retiring and bringing back servers on transient deaths into active backend pool.

## How it works?
- Topology is managed by using pub sub with redis. This was a workaround for writing a management operations API server for the Load balancer,and being able to use prebuilt clients such as telnet for managing the load balancer.
- Every incoming connection is a fire and forget task on the thread pool.
- Each connection uses a policy to find an active server in the backend pool before proxying traffic.

## How to use it!?

1. Run a Redis Server For config Management on Localhost 6379
2. Use telnet or another Redis Client REPL to manage addition, removal, and reading the servers in the backend pool for the load balancer.
3. Create an initial key value in Redis for servePool (I use telnet):
```
set serverPool '["127.0.0.1:6000","127.0.0.1:3000","127.0.0.1:4000"]'
```
4. Run the Load balancer as it's own process.
5. Now during runtime to add a new server
```
publish addServer 127.0.0.1:5000
```
6. To remove a serve during runtime.
```
publish removeServer 127.0.0.1:5000
```
7. To see the actively being used serverPool during runtime.
```
get serverPool
```
