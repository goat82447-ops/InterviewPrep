# How two people stay separate

Each browser gets its own id (a cookie called `sid`).
The server keeps a separate chat for each `sid`, so answers never mix.

```mermaid
flowchart TD
    You["You<br/>cookie sid: AAA"] -->|question| Server["Server<br/>reads the sid"]
    Friend["Friend<br/>cookie sid: BBB"] -->|question| Server
    Server -->|sid AAA| ChatA["Chat for AAA"]
    Server -->|sid BBB| ChatB["Chat for BBB"]
```

**Key point:** each browser has its own `sid`, so answers never mix between users.

See also the picture version: [multi-user-isolation.svg](multi-user-isolation.svg)
