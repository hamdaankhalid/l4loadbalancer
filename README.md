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
