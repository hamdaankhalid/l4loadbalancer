# LoadBalancer

1. Client Conn -> Proxy -> [Backend pool selected server]
2. Client Conn <- Proxy <- [Backend pool selected server]

Server handles connection forwarding to pool, based on policy.

## Policies
1. Round robin
2. Least Active connection policy
3. Location Based Routing

## Features
- Layer 4 TCP LB
- Runtime addition and removal of servers from server pool
