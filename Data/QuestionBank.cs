using InterviewPrep.Models;

namespace InterviewPrep.Data;

/// <summary>
/// A built-in bank of real technical interview questions across common topics.
/// Each question has a strong model answer, a short "say it simply" version in
/// easy English, and the key points a good answer covers. Add more freely.
/// </summary>
public static class QuestionBank
{
    private static readonly List<Question> All = Build();

    // AI-generated questions live here (keyed by Id) so the /answer scorer can
    // find them by Id just like the built-in bank. Ids start high to never clash
    // with the built-in questions.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, Question> Generated = new();
    private static int _generatedId = 100_000;

    public static IReadOnlyList<Question> Questions => All;

    public static IReadOnlyList<string> Topics =>
        All.Select(q => q.Topic).Distinct().OrderBy(t => t).ToList();

    public static IReadOnlyList<Question> ForTopic(string topic) =>
        All.Where(q => q.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase)).ToList();

    public static Question? ById(int id) =>
        All.FirstOrDefault(q => q.Id == id)
        ?? (Generated.TryGetValue(id, out var g) ? g : null);

    /// <summary>Stores an AI-generated question and returns it with a fresh Id.</summary>
    public static Question RegisterGenerated(
        string topic, Level level, string prompt, string answer, string simple, IReadOnlyList<string> points)
    {
        var id = System.Threading.Interlocked.Increment(ref _generatedId);
        var q = new Question(id, topic, level, prompt, answer, simple, points);
        Generated[id] = q;
        return q;
    }

    private static List<Question> Build()
    {
        var list = new List<Question>();
        var id = 1;

        void Add(string topic, Level level, string prompt, string answer, string simple, params string[] points) =>
            list.Add(new Question(id++, topic, level, prompt, answer, simple, points));

        // ---------------- C# ----------------
        Add("C#", Level.Easy,
            "What is the difference between a DLL and an EXE in C#?",
            "Both are compiled .NET assemblies, but an EXE is an executable program with an entry point (the Main method) that the OS can start and run on its own. A DLL (Dynamic Link Library) is a library of reusable code that has no entry point, so it cannot run by itself; it is loaded into a host process (an EXE or another DLL) and its types/methods are called from there. You build an EXE when you need a runnable application, and a DLL when you want to share code across many applications. An EXE runs as its own process; a DLL runs inside the process that loads it, so many programs can share one DLL.",
            "An EXE is a program you can run - it has a Main entry point. A DLL is a library of shared code with no entry point, so it can't run alone; another program loads and uses it. I make an EXE to run an app, a DLL to reuse code.",
            "assembly", "entry point", "main", "executable", "library", "reusable", "process");

        Add("C#", Level.Easy,
            "What is the difference between a class and a struct in C#?",
            "A class is a reference type stored on the heap; variables hold a reference, so copies share the same instance. A struct is a value type usually stored inline/on the stack; assigning it copies the whole value. Use structs for small, short-lived data; use classes for most objects, especially when you need inheritance or shared identity.",
            "Class is a reference type - copies point to the same object. Struct is a value type - copying makes a full new copy. I use structs for small data, classes for most things.",
            "reference type", "value type", "heap", "stack", "copy", "inheritance");

        Add("C#", Level.Medium,
            "What is the difference between 'IEnumerable' and 'IQueryable'?",
            "IEnumerable runs queries in memory with LINQ-to-Objects: the data is already loaded and filtering happens on the client. IQueryable builds an expression tree that a provider (like EF Core) turns into a query (e.g. SQL) run at the data source, so filtering happens on the server and only matching rows come back. Use IQueryable for database queries.",
            "IEnumerable filters data already in memory. IQueryable sends the filter to the database, so only matching rows come back. For database work I use IQueryable.",
            "in memory", "expression tree", "provider", "sql", "server");

        Add("C#", Level.Medium,
            "Explain 'async' and 'await'. What problem do they solve?",
            "async/await let you write non-blocking asynchronous code that reads like synchronous code. When you await a Task, the method returns control to the caller until the operation completes, freeing the thread instead of blocking it. This improves scalability, especially for I/O like network or disk. The compiler builds a state machine to resume after the await.",
            "async and await let the app do other work while waiting, instead of blocking the thread. It's mainly for slow things like network or disk, so the app stays responsive and scales better.",
            "non-blocking", "task", "thread", "i/o", "scalability", "state machine");

        Add("C#", Level.Hard,
            "What is the difference between 'Task' and 'Thread'?",
            "A Thread is a low-level OS construct you manage directly, and it always uses a thread. A Task is a higher-level unit of work that may run on a thread-pool thread, may complete asynchronously, and can return a result. Tasks support composition (await, ContinueWith), cancellation, and exception handling, and for I/O they often use no dedicated thread.",
            "A thread is a low-level OS thread I manage myself. A Task is higher level - it represents work, can return a result, supports await and cancellation, and often uses the thread pool. I prefer Tasks.",
            "thread pool", "abstraction", "async", "result", "cancellation", "i/o-bound");

        // ---------------- .NET ----------------
        Add(".NET", Level.Easy,
            "What is the difference between .NET Framework, .NET Core, and .NET 5+?",
            ".NET Framework is the older, Windows-only platform. .NET Core was the cross-platform, open-source rewrite. From .NET 5 onward it's unified simply as '.NET', built on Core, cross-platform, with one SDK and yearly releases. New apps should target modern .NET like .NET 8.",
            ".NET Framework is old and Windows-only. .NET Core made it cross-platform. From .NET 5 it's just called .NET - one modern, cross-platform version. New projects use .NET 8.",
            "windows-only", "cross-platform", "open source", "unified", "modern");

        Add(".NET", Level.Medium,
            "What is dependency injection and why does .NET use it?",
            "Dependency injection means a class receives its dependencies from outside (usually the constructor) instead of creating them itself. .NET has a built-in DI container that registers services with a lifetime and resolves them automatically. Benefits: loose coupling, easy testing by swapping mocks, and central configuration.",
            "Dependency injection means a class is given what it needs from outside, instead of creating it. .NET has a built-in container for this. It makes code loosely coupled and easy to test.",
            "constructor", "container", "lifetime", "testing", "loose coupling");

        Add(".NET", Level.Medium,
            "Explain the service lifetimes: singleton, scoped, and transient.",
            "Singleton: one instance for the whole app. Scoped: one instance per scope - in ASP.NET Core, one per HTTP request. Transient: a new instance every time it's requested. Choose based on state and thread-safety: per-request work like a DbContext should be scoped.",
            "Singleton is one instance for the whole app. Scoped is one per web request. Transient is a new one each time. A DbContext is usually scoped.",
            "singleton", "scoped", "transient", "per request", "instance");

        // ---------------- SQL ----------------
        Add("SQL", Level.Easy,
            "What is the difference between an INNER JOIN and a LEFT JOIN?",
            "An INNER JOIN returns only rows with a match in both tables. A LEFT JOIN returns all rows from the left table plus matching rows from the right; where there's no match, the right columns are NULL. Use LEFT JOIN to keep every row from the primary table even if related data is missing.",
            "INNER JOIN gives only rows that match in both tables. LEFT JOIN keeps all rows from the left table and puts NULL where there is no match on the right.",
            "match in both", "all rows from left", "null", "outer");

        Add("SQL", Level.Medium,
            "What is an index and what is the trade-off of adding one?",
            "An index is a data structure (often a B-tree) that finds rows quickly without scanning the whole table, speeding up SELECT, WHERE, JOIN and ORDER BY. The trade-off: indexes use storage and must be updated on every INSERT/UPDATE/DELETE, which slows writes. Index columns you filter or join on often, but avoid over-indexing.",
            "An index makes reads faster because the database doesn't scan the whole table. But it uses space and makes writes a bit slower, so I only index columns I search or join on often.",
            "b-tree", "faster reads", "scan", "storage", "slower writes");

        Add("SQL", Level.Medium,
            "What is the difference between WHERE and HAVING?",
            "WHERE filters individual rows before grouping and can't use aggregates. HAVING filters groups after GROUP BY and aggregation, so it can use aggregates like COUNT() or SUM(). Typical order: WHERE narrows rows, GROUP BY groups them, HAVING filters the grouped results.",
            "WHERE filters rows before grouping. HAVING filters after grouping, so it can use things like COUNT or SUM. WHERE first, then GROUP BY, then HAVING.",
            "before grouping", "after group by", "aggregate", "rows", "groups");

        Add("SQL", Level.Hard,
            "What are transactions and the ACID properties?",
            "A transaction groups statements into one unit that fully commits or fully rolls back. ACID: Atomicity (all-or-nothing), Consistency (data stays valid), Isolation (concurrent transactions don't corrupt each other), Durability (committed changes survive crashes). They keep data correct under failures and concurrency.",
            "A transaction is a group of steps that all succeed or all fail together. ACID means Atomic, Consistent, Isolated, Durable - it keeps data correct even with crashes or many users.",
            "atomicity", "consistency", "isolation", "durability", "commit", "rollback");

        // ---------------- OOP ----------------
        Add("OOP", Level.Easy,
            "What are the four pillars of OOP?",
            "Encapsulation (hide internal data behind methods/properties), Inheritance (a class reuses and extends another), Polymorphism (the same call behaves differently depending on the object's type), and Abstraction (expose only the essential details and hide complexity). Together they make code reusable and easier to maintain.",
            "The four pillars are Encapsulation, Inheritance, Polymorphism, and Abstraction. They help me reuse code and hide complexity so it's easier to maintain.",
            "encapsulation", "inheritance", "polymorphism", "abstraction");

        Add("OOP", Level.Medium,
            "What is the difference between an abstract class and an interface?",
            "An interface is a pure contract: it declares members with no implementation (though modern C# allows default methods), and a class can implement many interfaces. An abstract class can have both abstract members and real implementation plus fields/state, but a class can inherit only one. Use an interface for a capability; use an abstract class to share common base behavior.",
            "An interface is just a contract with no code, and a class can implement many. An abstract class can include real code and state, but you can inherit only one. Interface for a capability, abstract class for shared base code.",
            "contract", "implementation", "multiple", "one", "state");

        Add("OOP", Level.Medium,
            "Explain polymorphism with a simple example.",
            "Polymorphism means one interface, many behaviors. For example, a base class Shape has a virtual Area() method, and Circle and Square override it. If you loop over a list of Shape and call Area(), each object runs its own version. The caller doesn't need to know the concrete type.",
            "Polymorphism means the same method call does different things depending on the object. Like a Shape.Area() where Circle and Square each calculate area their own way, but I just call Area().",
            "override", "virtual", "base", "same call", "different behavior");

        // ---------------- REST APIs ----------------
        Add("REST", Level.Easy,
            "What is a REST API and what are the main HTTP methods?",
            "A REST API exposes resources over HTTP using standard methods: GET (read), POST (create), PUT (replace/update), PATCH (partial update), and DELETE (remove). It's stateless - each request carries all it needs - and typically uses JSON. URLs represent resources, like /api/users/5.",
            "A REST API lets clients work with resources over HTTP. GET reads, POST creates, PUT updates, DELETE removes. It's stateless and usually uses JSON.",
            "http", "get", "post", "put", "delete", "stateless", "resource");

        Add("REST", Level.Medium,
            "What do the status codes 200, 201, 400, 401, 404, and 500 mean?",
            "200 OK (success), 201 Created (a new resource was made), 400 Bad Request (invalid input from the client), 401 Unauthorized (not authenticated), 404 Not Found (resource doesn't exist), 500 Internal Server Error (something failed on the server). 2xx is success, 4xx is client error, 5xx is server error.",
            "200 is success, 201 means created, 400 is bad input, 401 is not logged in, 404 is not found, 500 is a server error. 4xx is the client's fault, 5xx is the server's.",
            "200", "201", "400", "401", "404", "500", "client error", "server error");

        Add("REST", Level.Medium,
            "What is the difference between PUT and PATCH?",
            "PUT replaces the entire resource with the data you send - it should be idempotent, so repeating it gives the same result. PATCH applies a partial update, changing only the fields you provide. Use PUT for a full replace, PATCH when you only want to change a few fields.",
            "PUT replaces the whole resource. PATCH updates only some fields. If I only change one field, I use PATCH.",
            "replace", "partial", "idempotent", "fields");

        // ---------------- Azure ----------------
        Add("Azure", Level.Easy,
            "What is the difference between Azure App Service, Azure Functions, and a VM?",
            "A VM gives full control of the OS but you manage everything. App Service is a managed platform (PaaS) for hosting web apps/APIs without managing servers. Azure Functions is serverless - small event-driven pieces of code that scale automatically and you pay per execution. Pick based on how much control vs. how little management you want.",
            "A VM is full control but I manage it all. App Service is managed hosting for web apps. Functions is serverless - small code that runs on events and scales by itself. Less control means less to manage.",
            "vm", "app service", "paas", "functions", "serverless", "managed");

        Add("Azure", Level.Medium,
            "How do you keep secrets like connection strings out of code in Azure?",
            "Don't hard-code secrets. Use Azure Key Vault to store them, and give the app a Managed Identity so it can read the vault without storing credentials. For simpler cases, use App Service configuration/environment variables. Locally, keep secrets in user secrets or a git-ignored file. Never commit secrets to source control.",
            "I never put secrets in code. I store them in Azure Key Vault and let the app read them using a Managed Identity. Locally I use environment variables or a git-ignored file.",
            "key vault", "managed identity", "environment", "not in code", "git-ignored");

        Add("Azure", Level.Medium,
            "What is a Managed Identity in Azure?",
            "A Managed Identity is an automatically managed identity in Entra ID that an Azure resource (like an App Service) uses to authenticate to other services - such as Key Vault or Storage - without storing any credentials in code or config. Azure handles the credentials and rotation for you.",
            "A Managed Identity lets my Azure app prove who it is to other Azure services without any password in code. Azure manages the credentials for me.",
            "identity", "no credentials", "authenticate", "key vault", "rotation");

        // ---------------- CI/CD ----------------
        Add("CI/CD", Level.Easy,
            "What is CI/CD?",
            "CI (Continuous Integration) means developers merge code frequently and every change automatically builds and runs tests, catching problems early. CD (Continuous Delivery/Deployment) means the tested build is automatically prepared for, or released to, environments. Together they make releases faster, safer, and repeatable.",
            "CI means every code change is automatically built and tested. CD means that tested build is automatically deployed. It makes releases fast and safe.",
            "continuous integration", "build", "tests", "continuous delivery", "deploy", "automatic");

        Add("CI/CD", Level.Medium,
            "What are the typical stages of a CI/CD pipeline?",
            "A common pipeline: trigger on commit/PR, restore dependencies, build, run automated tests, produce an artifact (e.g. a container image), then deploy to environments (dev -> staging -> production), often with approvals and health checks. If any stage fails, the pipeline stops and notifies the team.",
            "A pipeline usually does: build, run tests, package an artifact, then deploy to dev, staging, and production. If a step fails, it stops and tells us.",
            "trigger", "build", "test", "artifact", "deploy", "stages", "approval");

        Add("CI/CD", Level.Medium,
            "What is the difference between blue-green and rolling deployments?",
            "Blue-green runs two identical environments; you deploy to the idle one (green), test it, then switch traffic over instantly, with easy rollback by switching back. Rolling updates replace instances a few at a time so there's no full duplicate environment, but rollback is slower. Both aim for zero-downtime releases.",
            "Blue-green keeps two environments and switches traffic to the new one all at once, with easy rollback. Rolling replaces servers a few at a time. Both avoid downtime.",
            "blue-green", "two environments", "switch traffic", "rolling", "rollback", "zero downtime");

        // ---------------- Production Support ----------------
        Add("Production Support", Level.Easy,
            "A production issue is reported. What are your first steps?",
            "First, acknowledge and assess impact and severity (who/what is affected). Check monitoring, logs, and recent changes/deployments. Communicate status to stakeholders. Try to stabilize quickly - roll back or apply a mitigation - before a full fix. After recovery, do a root-cause analysis and add a fix or safeguard to prevent recurrence.",
            "First I check the impact and severity, then look at logs, monitoring, and recent changes. I keep people informed, stabilize fast - often by rolling back - and later do a root-cause analysis to stop it happening again.",
            "impact", "severity", "logs", "monitoring", "recent changes", "rollback", "communicate", "root cause");

        Add("Production Support", Level.Medium,
            "What is the difference between logging, monitoring, and alerting?",
            "Logging records what happened (events, errors) for later investigation. Monitoring continuously tracks metrics and health (CPU, latency, error rate) and shows dashboards. Alerting notifies the team automatically when a metric crosses a threshold, so you act before users are badly affected. You need all three together.",
            "Logging records what happened. Monitoring watches metrics like errors and latency all the time. Alerting pings us when something crosses a limit. I use all three.",
            "logging", "records", "monitoring", "metrics", "alerting", "threshold", "notify");

        Add("Production Support", Level.Medium,
            "What is a root cause analysis (RCA) and why does it matter?",
            "An RCA is a structured review after an incident to find the underlying cause - not just the symptom - usually by asking 'why' repeatedly and reviewing timeline, logs, and changes. It produces action items (a real fix, tests, alerts, or process changes) so the same problem doesn't happen again. It should be blameless, focusing on the system.",
            "RCA means finding the real reason an incident happened, not just the symptom, and adding fixes so it doesn't repeat. It should be blameless - about the system, not people.",
            "underlying cause", "symptom", "why", "action items", "prevent", "blameless");

        // ============================================================
        // Top-company deep-dive questions (the harder, follow-up style
        // asked at product companies). These probe HOW and WHY, not just
        // definitions - the same way a real senior interviewer digs in.
        // ============================================================

        // ---- C# / .NET internals ----
        Add("C#", Level.Hard,
            "How does garbage collection work in .NET, and what are generations?",
            "The .NET GC automatically frees managed objects that are no longer reachable. It's generational: Gen 0 holds new, short-lived objects and is collected most often and cheaply; survivors are promoted to Gen 1, then Gen 2 for long-lived objects, which is collected rarely. Large objects go on the Large Object Heap. Because most objects die young, collecting Gen 0 frequently is very efficient. As a developer I reduce pressure by avoiding needless allocations, reusing buffers, and disposing unmanaged resources.",
            "The GC frees objects nothing is using anymore. It groups them by age: Gen 0 is new and cleaned often and cheaply, Gen 2 is old and cleaned rarely. Most objects die young, so this is fast. I help by allocating less and disposing properly.",
            "reachable", "generational", "gen 0", "gen 2", "promoted", "large object heap", "allocations");

        Add("C#", Level.Hard,
            "What can cause a memory leak in a garbage-collected language like C#?",
            "Even with a GC, objects stay alive if something still references them. Common causes: event handlers that are never unsubscribed keep the subscriber alive; static collections or caches that grow forever; long-lived objects holding references to short-lived ones; and undisposed unmanaged resources like file or socket handles. I find them with a memory profiler, look at what's rooting the objects, unsubscribe events, bound caches, and implement IDisposable.",
            "In C# a leak happens when something still points to objects you don't need - like events you never unsubscribe, or a static list that keeps growing. I use a profiler to see what holds them, then unsubscribe, bound caches, and dispose properly.",
            "still referenced", "event handlers", "unsubscribe", "static", "cache grows", "undisposed", "profiler");

        // ---- Concurrency ----
        Add("C#", Level.Hard,
            "What is a deadlock and how do you prevent one?",
            "A deadlock is when two or more threads each hold a lock the other needs, so none can proceed and they wait forever. The classic cause is acquiring multiple locks in different orders. To prevent it: always acquire locks in a consistent global order, keep critical sections small, use timeouts (e.g. Monitor.TryEnter), prefer higher-level concurrent collections or async coordination, and avoid blocking on async code with .Result or .Wait which can deadlock on a captured context.",
            "A deadlock is when two threads each wait for a lock the other one holds, so both freeze forever. I prevent it by always taking locks in the same order, keeping locked sections small, using timeouts, and not blocking on async code with .Result.",
            "hold and wait", "lock order", "circular", "timeout", "small critical section", "avoid .result");

        // ---- SQL performance ----
        Add("SQL", Level.Hard,
            "A query is slow in production. How do you diagnose and fix it?",
            "I start by looking at the actual execution plan to see where time goes - full table scans, expensive sorts, or key lookups. I check whether the right indexes exist for the WHERE and JOIN columns and whether the query is sargable (no functions wrapped around indexed columns). I look at row counts and statistics, reduce returned columns instead of SELECT *, and consider covering indexes. I also check for parameter sniffing, blocking/locks, and whether the data volume simply grew. Then I add or adjust an index or rewrite the query and re-measure.",
            "First I read the execution plan to see what's slow, like a full table scan. I check indexes on the filter and join columns, avoid SELECT *, and make sure the query can actually use the index. Then I add the right index or rewrite it and measure again.",
            "execution plan", "table scan", "index", "sargable", "statistics", "covering index", "measure again");

        Add("SQL", Level.Hard,
            "What is a clustered vs a non-clustered index?",
            "A clustered index defines the physical order of the rows in the table, so a table can have only one - usually on the primary key. A non-clustered index is a separate structure with a copy of the key columns and a pointer back to the row; you can have many. Non-clustered indexes can 'cover' a query if they include all needed columns, avoiding an extra lookup. Clustered is great for range scans on the key; non-clustered helps targeted lookups on other columns.",
            "A clustered index sets the actual order of rows, so there's only one, usually the primary key. A non-clustered index is a separate lookup structure and you can have many. If it includes all the columns a query needs, it avoids an extra trip to the row.",
            "physical order", "one clustered", "primary key", "separate structure", "many", "covering", "lookup");

        // ---- System design lite (very common at top companies) ----
        Add("REST", Level.Hard,
            "How would you design an API to handle high traffic and stay reliable?",
            "I'd keep the service stateless so I can scale it horizontally behind a load balancer. I'd add caching for hot reads (in-memory or a distributed cache like Redis), use pagination and reasonable payloads, and put rate limiting and timeouts in place to protect the backend. For resilience I'd add retries with backoff, circuit breakers, and health checks, and make write operations idempotent so retries are safe. I'd offload slow work to a queue and process it asynchronously, and watch it all with metrics, logging, and alerts.",
            "I keep the service stateless so I can run many copies behind a load balancer. I cache hot data, add rate limits and timeouts, use retries with circuit breakers, make writes idempotent, push slow work to a queue, and monitor everything.",
            "stateless", "horizontal scale", "load balancer", "cache", "rate limit", "retry", "circuit breaker", "idempotent", "queue");

        Add("REST", Level.Medium,
            "What is idempotency and why does it matter for APIs?",
            "An idempotent operation gives the same result no matter how many times it's repeated. GET, PUT and DELETE are naturally idempotent; POST usually isn't. It matters because networks retry - if a client resends a request after a timeout, a non-idempotent create could charge a customer twice. I make critical writes idempotent using an idempotency key the server records, so a repeat with the same key returns the original result instead of doing the work again.",
            "Idempotent means doing it again gives the same result. It matters because clients retry on timeouts - without it you could create or charge twice. I use an idempotency key so a repeat request is safe.",
            "same result", "repeat", "retry", "timeout", "duplicate", "idempotency key");

        Add("REST", Level.Medium,
            "How do you handle authentication and authorization in a REST API?",
            "Authentication is proving who you are; authorization is what you're allowed to do. A common approach is token-based auth with JWTs: the client logs in, gets a signed token, and sends it in the Authorization header on each request; the API validates the signature and expiry. Authorization is then enforced with roles or claims/policies on endpoints. I always use HTTPS, keep tokens short-lived with refresh tokens, and never trust the client for permission checks.",
            "Authentication is who you are; authorization is what you can do. I usually use JWT tokens: log in, get a signed token, send it on each request. The API checks it and then checks roles for access. Always over HTTPS.",
            "authentication", "authorization", "jwt", "token", "authorization header", "roles", "claims", "https");

        // ---- Caching ----
        Add("Azure", Level.Hard,
            "When would you add caching, and what problems does it introduce?",
            "I add caching when the same data is read far more than it changes and reads are expensive, to cut latency and load - for example a distributed cache like Redis in front of a database. The hard part is invalidation: stale data if you don't refresh it, and complexity keeping cache and source in sync. I use TTLs, cache-aside (load on miss, write-through or invalidate on update), and I'm careful about cache stampedes and about caching per-user or sensitive data.",
            "I cache data that's read a lot but changes rarely, to make it faster and reduce load - like Redis in front of the database. The tricky part is stale data, so I use expiry times and clear or update the cache when the data changes.",
            "read heavy", "expensive reads", "latency", "redis", "invalidation", "stale", "ttl", "cache-aside");

        // ---- Distributed / behavioral-technical ----
        Add("Production Support", Level.Hard,
            "A service works locally but fails intermittently in production. How do you approach it?",
            "Intermittent means I need data, not guesses. I check whether it correlates with load, a specific instance, a dependency, or time. I look at logs and traces across services with a correlation id, and at metrics for latency spikes, errors, timeouts, memory, or thread-pool exhaustion. Common culprits are race conditions, connection-pool limits, downstream timeouts, retries amplifying load, or config differences between environments. I reproduce under load if I can, add targeted logging, apply a mitigation, and confirm with monitoring before closing it out.",
            "Intermittent bugs need data. I check if it lines up with high load, one server, or a slow dependency, and follow the logs and traces with a correlation id. Often it's a race condition, connection limits, or timeouts. I add logging, fix it, and confirm with monitoring.",
            "correlate", "load", "logs", "traces", "correlation id", "race condition", "connection pool", "timeouts", "config difference");

        Add("Production Support", Level.Medium,
            "How do you decide whether to roll back or fix forward during an incident?",
            "The priority is restoring service fast with the least risk. If a recent deploy clearly caused it and rollback is quick and safe, I roll back first and investigate calmly afterwards. If rollback isn't possible - for example a database migration already ran - or the fix is small and well understood, I fix forward. I weigh blast radius, how confident I am in the cause, and how reversible each option is, and I keep stakeholders updated throughout.",
            "The goal is to restore service safely and fast. If a recent deploy caused it and rollback is quick, I roll back and investigate later. If rollback isn't safe or the fix is small and clear, I fix forward. I weigh risk, confidence, and how reversible each choice is.",
            "restore fast", "recent deploy", "rollback quick", "cannot rollback", "fix forward", "blast radius", "reversible");

        // ---------------- System Design ----------------
        Add("System Design", Level.Medium,
            "How would you design a URL shortener like bit.ly?",
            "The core is mapping a short code to a long URL. I'd generate a unique short code - either a base62 encoding of an auto-increment id or a hash - and store the mapping in a database. Reads hugely outnumber writes, so I'd cache hot mappings in Redis and put the service behind a load balancer, keeping it stateless to scale horizontally. Redirects use a 301/302 to the long URL. I'd add analytics via an async queue so click tracking never slows the redirect, and handle custom aliases and expiry.",
            "I map a short code to the long URL and store it in a database. I make the code with base62 or a hash. Since reads are far more than writes, I cache popular links in Redis and run many stateless servers behind a load balancer. Click tracking goes through a queue so it doesn't slow the redirect.",
            "short code", "base62", "mapping", "database", "cache", "redis", "stateless", "load balancer", "redirect", "queue");

        Add("System Design", Level.Medium,
            "What is horizontal vs vertical scaling, and when do you use each?",
            "Vertical scaling means making one machine bigger - more CPU, RAM. It's simple but has a hard ceiling and a single point of failure. Horizontal scaling means adding more machines and spreading load across them with a load balancer; it scales much further and adds redundancy, but the app must be stateless and you deal with distributed concerns like shared state and data consistency. I scale vertically for quick wins and stateful stores, and horizontally for stateless web/API tiers that need to grow.",
            "Vertical scaling is a bigger machine - easy but limited. Horizontal scaling is more machines behind a load balancer - it grows further and adds redundancy, but the app must be stateless. I use vertical for quick wins and horizontal for the web tier.",
            "vertical", "bigger machine", "ceiling", "horizontal", "more machines", "load balancer", "stateless", "redundancy");

        Add("System Design", Level.Hard,
            "What is the CAP theorem?",
            "CAP says a distributed data store can guarantee at most two of Consistency, Availability, and Partition tolerance at the same time. Since network partitions are unavoidable in practice, the real choice under a partition is between consistency and availability. A CP system (like a strongly consistent database) may reject requests to stay correct; an AP system (like many NoSQL stores) stays available but may return stale data and reconcile later. I pick based on whether the use case needs strict correctness or maximum uptime.",
            "CAP says in a distributed system you can only fully have two of Consistency, Availability, and Partition tolerance. Partitions will happen, so you really choose between staying consistent or staying available. Banking leans consistent; a social feed leans available.",
            "distributed", "consistency", "availability", "partition tolerance", "two of three", "cp", "ap", "trade-off");

        Add("System Design", Level.Medium,
            "How do you handle communication between microservices?",
            "Two main styles. Synchronous, request/response over REST or gRPC, is simple and immediate but couples services and can cascade failures, so I add timeouts, retries, and circuit breakers. Asynchronous, via a message broker like Kafka or a queue, decouples services and absorbs load spikes - a service publishes an event and others react - which is great for resilience and scale but is eventually consistent and harder to trace. I use sync for direct queries and async events for workflows and decoupling.",
            "Services talk either synchronously with REST or gRPC, or asynchronously through a broker like Kafka. Sync is simple but couples them, so I add timeouts and circuit breakers. Async decouples them and handles spikes, but it's eventually consistent. I mix both.",
            "synchronous", "rest", "grpc", "asynchronous", "message broker", "kafka", "decouple", "eventually consistent", "circuit breaker");

        // ---------------- Kafka ----------------
        Add("Kafka", Level.Easy,
            "What is Apache Kafka and when would you use it?",
            "Kafka is a distributed event-streaming platform - a durable, high-throughput log where producers publish messages to topics and consumers read them. It decouples systems: the producer doesn't wait for consumers, and messages are stored so consumers can read at their own pace or replay history. I use it for event-driven architectures, streaming pipelines, decoupling microservices, and absorbing bursts of load between fast producers and slower consumers.",
            "Kafka is a system for streaming events. Producers send messages to topics and consumers read them. It stores messages, so it decouples services and handles big bursts of data. I use it for event-driven systems and pipelines.",
            "event streaming", "distributed log", "producer", "topic", "consumer", "durable", "decouple", "high throughput");

        Add("Kafka", Level.Medium,
            "Explain topics, partitions, and consumer groups in Kafka.",
            "A topic is a named stream of messages, split into partitions for parallelism and scale. Each partition is an ordered, append-only log, and order is guaranteed only within a partition. Producers pick a partition, often by a key so related messages stay ordered together. A consumer group lets many consumers share the work: each partition is read by exactly one consumer in the group, so adding consumers (up to the partition count) increases throughput. Different groups each get their own full copy of the messages.",
            "A topic is a stream, split into partitions so it scales. Order is kept only inside a partition, and a key keeps related messages together. In a consumer group, each partition goes to one consumer, so more consumers means more parallel work. Different groups each read everything.",
            "topic", "partition", "parallelism", "ordered", "key", "consumer group", "one consumer per partition", "offset");

        Add("Kafka", Level.Medium,
            "How does Kafka guarantee messages aren't lost, and what are delivery semantics?",
            "Durability comes from replication: each partition has a leader and followers, and producers can require acks=all so a write is confirmed only after replicas have it. Consumers track their position with offsets and commit them once processed. Delivery semantics: at-most-once (commit before processing - can lose messages), at-least-once (process then commit - can duplicate, the common default), and exactly-once using idempotent producers and transactions. Because at-least-once can duplicate, I make consumers idempotent.",
            "Kafka replicates each partition, and with acks=all a write is safe only after replicas have it. Consumers commit offsets after processing. You get at-most-once, at-least-once, or exactly-once. At-least-once can duplicate, so I make my consumers idempotent.",
            "replication", "leader", "acks=all", "offset", "commit", "at-least-once", "exactly-once", "idempotent consumer");

        // ---------------- Redis ----------------
        Add("Redis", Level.Easy,
            "What is Redis and what is it commonly used for?",
            "Redis is an in-memory key-value data store, so it's extremely fast - typically sub-millisecond. It supports rich data types like strings, hashes, lists, sets, and sorted sets, and can optionally persist to disk. Common uses: caching hot data in front of a database, session storage, rate limiting, leaderboards with sorted sets, and simple pub/sub or queues. I reach for it when I need very fast reads on data that's read far more than it changes.",
            "Redis is an in-memory key-value store, so it's very fast. It has data types like strings, lists, and sorted sets. I use it for caching, sessions, rate limiting, and leaderboards - anything that needs quick reads.",
            "in-memory", "key-value", "fast", "data types", "cache", "session", "rate limiting", "leaderboard");

        Add("Redis", Level.Medium,
            "How do you use Redis as a cache, and what is a TTL and eviction?",
            "The common pattern is cache-aside: on a read, check Redis first; on a miss, load from the database, store it in Redis, and return it. On updates I invalidate or refresh the key. I set a TTL - a time-to-live - so entries expire automatically and don't serve stale data forever. When memory fills up, Redis uses an eviction policy such as LRU (evict least-recently-used) or LFU. I also watch for cache stampedes when many keys expire at once and use jittered TTLs or locks.",
            "I use cache-aside: check Redis first, and on a miss load from the database and store it. I set a TTL so keys expire and don't go stale. When memory is full, Redis evicts old keys, often least-recently-used. I add jitter to TTLs to avoid stampedes.",
            "cache-aside", "miss", "load from db", "invalidate", "ttl", "expire", "eviction", "lru", "stampede");

        Add("Redis", Level.Medium,
            "Is Redis single-threaded, and how does it scale?",
            "Redis executes commands on a single thread, which avoids locks and makes each operation atomic and predictable; it's fast because it's in-memory and uses efficient I/O. To scale reads I add replicas (primary-replica replication). To scale writes and data beyond one machine I use Redis Cluster, which shards keys across nodes by hash slot. For high availability, Redis Sentinel or Cluster handles failover by promoting a replica.",
            "Yes, Redis runs commands on one thread, so each command is atomic and there are no locks - and it's still fast because it's in memory. I scale reads with replicas and scale writes with Redis Cluster, which shards data across nodes. Sentinel or Cluster handles failover.",
            "single-threaded", "atomic", "in-memory", "replica", "read scale", "cluster", "shard", "sentinel", "failover");

        // ---------------- Angular ----------------
        Add("Angular", Level.Easy,
            "What is Angular and how is a component structured?",
            "Angular is a TypeScript-based front-end framework for building single-page applications. The building block is a component: a TypeScript class with an @Component decorator that ties together an HTML template, styles, and logic. The class exposes data and methods the template binds to. Components form a tree, communicate via @Input and @Output, and use services (injected via DI) for shared logic and data. Angular also gives you routing, forms, and HttpClient out of the box.",
            "Angular is a TypeScript framework for single-page apps. The main piece is a component - a class with a template, styles, and logic. Components pass data with Input and Output and use services for shared logic. Angular includes routing, forms, and HTTP.",
            "typescript", "spa", "component", "decorator", "template", "input", "output", "service", "dependency injection");

        Add("Angular", Level.Medium,
            "What is data binding in Angular and what are its types?",
            "Data binding connects the component class and the template. There are four kinds: interpolation {{ value }} and property binding [prop] send data from the class to the view; event binding (click)=\"handler()\" sends events from the view to the class; and two-way binding [(ngModel)] keeps a value in sync both ways, which is really property plus event binding together. This keeps the UI and data automatically consistent.",
            "Data binding connects the class and the template. Interpolation and property binding send data to the view, event binding sends events back, and two-way binding with ngModel keeps a value in sync both ways. It keeps the UI and data matching.",
            "interpolation", "property binding", "event binding", "two-way", "ngmodel", "class to view", "view to class");

        Add("Angular", Level.Medium,
            "What is the difference between an Observable and a Promise in Angular?",
            "A Promise handles a single async value and runs once, eagerly. An Observable, from RxJS, is a stream that can emit many values over time, is lazy (nothing happens until you subscribe), and is cancellable by unsubscribing. Angular uses Observables widely - HttpClient returns one, and so do router and form events - and you compose them with operators like map, filter, and switchMap. For a single HTTP call either works, but Observables shine for streams and cancellation.",
            "A Promise gives one value and runs once. An Observable is a stream that can emit many values, is lazy until you subscribe, and can be cancelled. Angular's HttpClient returns Observables, and you use operators like map and switchMap. Observables are more powerful for streams.",
            "promise single", "eager", "observable", "rxjs", "stream", "many values", "lazy", "subscribe", "cancellable", "operators");

        Add("Angular", Level.Medium,
            "How does dependency injection work in Angular?",
            "Angular has a hierarchical DI system. You mark a class with @Injectable and register it - usually providedIn: 'root' for an app-wide singleton, or in a component's providers for a scoped instance. Components and services declare dependencies in their constructor and Angular's injector creates and supplies them. This gives loose coupling, easy testing by swapping providers with mocks, and controlled sharing of state through services.",
            "Angular injects dependencies for you. You mark a service with @Injectable and provide it, usually in root for one shared instance. Classes ask for it in their constructor and Angular supplies it. It makes code loosely coupled and easy to test.",
            "injectable", "providedin root", "singleton", "constructor", "injector", "provider", "loose coupling", "testing");

        // ---------------- JavaScript / TypeScript ----------------
        Add("JavaScript", Level.Easy,
            "What is the difference between let, const, and var?",
            "var is function-scoped and hoisted, which leads to surprising bugs, so I avoid it. let and const are block-scoped and only usable after declaration. let allows reassignment; const does not - though for objects and arrays the reference is fixed while the contents can still change. My rule is: use const by default, use let when you truly need to reassign, and avoid var.",
            "var is old and function-scoped, so I avoid it. let and const are block-scoped. let can be reassigned, const cannot - but a const object can still change inside. I use const by default and let only when I must reassign.",
            "var function-scoped", "hoisting", "block scope", "let reassign", "const", "reference fixed");

        Add("JavaScript", Level.Medium,
            "Explain closures in JavaScript with why they are useful.",
            "A closure is a function that remembers the variables from the scope where it was created, even after that outer function has returned. It works because the inner function keeps a reference to those variables. Closures are useful for data privacy - keeping state hidden in a factory function - for callbacks that need context, and for things like counters or once-only functions. They're also how hooks and event handlers hold onto values.",
            "A closure is a function that remembers variables from where it was created, even after the outer function finished. It's useful for keeping private state and for callbacks that need to remember something.",
            "remembers scope", "outer function returned", "reference", "data privacy", "state", "callback");

        Add("JavaScript", Level.Medium,
            "What is the event loop, and how do the call stack, microtasks, and macrotasks fit in?",
            "JavaScript runs on a single thread with an event loop. Synchronous code runs on the call stack. Async callbacks are queued: promises and things like queueMicrotask go on the microtask queue, while timers and I/O callbacks go on the macrotask queue. After each synchronous run finishes, the loop drains all microtasks first, then takes one macrotask, and repeats. That's why a resolved promise's .then runs before a setTimeout of 0.",
            "JavaScript is single-threaded and uses an event loop. Sync code runs first on the call stack. Then promises (microtasks) run before timers (macrotasks). That's why a promise callback runs before setTimeout zero.",
            "single thread", "event loop", "call stack", "microtask", "promise", "macrotask", "settimeout", "order");

        Add("JavaScript", Level.Medium,
            "What does TypeScript add over JavaScript, and why use it?",
            "TypeScript is JavaScript with static typing. You add types to variables, parameters, and return values, and the compiler catches type errors before you run the code. It gives great editor support - autocomplete, refactoring, inline docs - and features like interfaces, generics, and enums. It compiles down to plain JavaScript. On larger teams and codebases it prevents a whole class of bugs and makes the code self-documenting, which is why most big front-end projects use it.",
            "TypeScript is JavaScript plus types. The compiler catches type mistakes before running, and the editor gives better autocomplete and refactoring. It compiles to normal JavaScript. On big projects it prevents bugs and makes code clearer.",
            "static typing", "compiler catches errors", "editor support", "interfaces", "generics", "compiles to javascript");

        // ---------------- Docker / Kubernetes ----------------
        Add("Docker", Level.Easy,
            "What is Docker and how is a container different from a VM?",
            "Docker packages an app with its dependencies into an image that runs as a container - a lightweight, isolated process. The key difference from a VM: a VM virtualizes hardware and runs a full guest OS, so it's heavy and slow to start, while containers share the host's OS kernel and isolate at the process level, so they're small, start in seconds, and pack densely. Containers give consistent 'runs the same everywhere' environments, which is why they're the standard for deployment.",
            "Docker packs an app and its dependencies into an image that runs as a container. Unlike a VM, which runs a whole operating system, containers share the host's kernel, so they're small and start in seconds. They make the app run the same everywhere.",
            "image", "container", "dependencies", "vm full os", "share kernel", "lightweight", "fast start", "consistent");

        Add("Docker", Level.Medium,
            "What is the difference between a Docker image and a container, and what is a Dockerfile?",
            "An image is a read-only template - the packaged app, its dependencies, and config - built in layers. A container is a running instance of an image; you can start many containers from one image, and each has its own writable layer. A Dockerfile is the recipe: a set of instructions (FROM a base image, COPY files, RUN commands, set the ENTRYPOINT) that Docker builds into the image. Images are stored in a registry like Docker Hub or ACR.",
            "An image is a read-only template of the app and its dependencies. A container is a running copy of that image. A Dockerfile is the recipe that builds the image - the base, the files to copy, and the command to run.",
            "image template", "read-only", "layers", "container running instance", "dockerfile", "recipe", "registry");

        Add("Kubernetes", Level.Medium,
            "What is Kubernetes and what problems does it solve?",
            "Kubernetes orchestrates containers across a cluster of machines. Instead of running containers by hand, you declare the desired state - how many replicas, which image, resources - and Kubernetes makes it real and keeps it that way. It handles scheduling containers onto nodes, self-healing by restarting failed ones, scaling up and down, rolling updates and rollbacks, service discovery, and load balancing. It solves running containerized apps reliably at scale without manual babysitting.",
            "Kubernetes runs and manages containers across many machines. I declare what I want - how many copies, which image - and it makes it happen, restarts failures, scales, and does rolling updates. It keeps containerized apps running reliably at scale.",
            "orchestration", "cluster", "desired state", "replicas", "self-healing", "scaling", "rolling update", "service discovery");

        Add("Kubernetes", Level.Medium,
            "What is a Pod, and what is a Deployment and a Service in Kubernetes?",
            "A Pod is the smallest deployable unit - usually one container (sometimes a few tightly-coupled ones) sharing network and storage. Pods are ephemeral and can be replaced. A Deployment manages a set of identical Pods: it keeps the desired replica count, and handles rolling updates and rollbacks. A Service gives a stable network endpoint and load-balances across the matching Pods, since Pod IPs change. So Deployment manages lifecycle, Service manages stable access.",
            "A Pod is the smallest unit, usually one container. A Deployment keeps a set of identical Pods running and handles updates and rollbacks. A Service gives a stable address and load-balances across those Pods, because Pods come and go.",
            "pod smallest unit", "container", "ephemeral", "deployment", "replicas", "rolling update", "service", "stable endpoint", "load balance");

        // ---------------- Entity Framework ----------------
        Add("Entity Framework", Level.Easy,
            "What is Entity Framework Core and what is an ORM?",
            "EF Core is Microsoft's ORM for .NET. An ORM - object-relational mapper - lets you work with the database using C# classes and LINQ instead of hand-written SQL; it maps your entities to tables and translates queries into SQL. EF Core handles change tracking, generating INSERT/UPDATE/DELETE, migrations for schema changes, and relationships. It boosts productivity and keeps data access strongly typed, though for hot paths you sometimes drop to raw SQL.",
            "EF Core is the .NET ORM. An ORM lets me use C# classes and LINQ instead of writing SQL, and it maps objects to tables. It tracks changes, generates the SQL, and handles migrations. It's productive and type-safe.",
            "orm", "object-relational", "linq", "maps classes to tables", "change tracking", "migrations", "generates sql");

        Add("Entity Framework", Level.Medium,
            "What is the difference between eager, lazy, and explicit loading?",
            "They control when related data is loaded. Eager loading uses Include to pull related data in the same query up front - good when you know you need it. Lazy loading fetches related data automatically when you first access the navigation property - convenient but it can fire many hidden queries. Explicit loading loads related data on demand with a specific call. The danger is the N+1 problem from lazy loading in a loop; I prefer eager Include or a projection to avoid it.",
            "It's about when related data loads. Eager loading with Include gets it up front. Lazy loading gets it when you first touch the property, which can cause many hidden queries. Explicit loading is on demand. I use Include to avoid the N+1 problem.",
            "eager", "include", "lazy", "navigation property", "explicit", "on demand", "n+1 problem");

        Add("Entity Framework", Level.Medium,
            "What are migrations in EF Core and why are they useful?",
            "Migrations are versioned, incremental changes to the database schema generated from your model. When you change your entities, you add a migration, and EF Core creates code describing the schema delta (and how to reverse it). Applying migrations updates the database to match the model, and because they're in source control and ordered, the whole team and every environment stay in sync. They also give a safe, repeatable path to evolve the schema and roll back if needed.",
            "Migrations are step-by-step schema changes EF Core generates from my model. When I change my classes, I add a migration and apply it to update the database. They're in source control, so every environment and teammate stays in sync, and I can roll back.",
            "schema changes", "versioned", "generated from model", "add migration", "apply", "source control", "in sync", "rollback");

        // ============================================================
        // HR ROUND — the classic "get to know you" questions. Answer
        // honestly and adapt these to your own real story.
        // ============================================================
        Add("HR Round", Level.Easy,
            "Tell me about yourself.",
            "Keep it a 60-90 second story in three parts: present, past, future. Present: your current role and what you do. Past: one or two relevant achievements that led you here. Future: why this role is the logical next step. Example: 'I'm a .NET developer with about two years building web APIs and Angular front-ends. In my current team I own a payments module and I recently cut a report's load time from 8 seconds to under 1 by fixing the SQL and adding caching. I enjoy solving performance and reliability problems, and I'm looking for a role like this one where I can take on more ownership and work on larger-scale systems.'",
            "Tell a short story in three parts: what I do now, one or two proud achievements from my past, and why this job is my next step. I keep it under about 90 seconds and tie it to the role.",
            "present past future", "current role", "relevant achievement", "why this role", "keep it short", "tie to the job");

        Add("HR Round", Level.Easy,
            "Why should we hire you?",
            "Match your strengths to their needs, back it with proof, and show fit. Example: 'You need someone who can build reliable .NET APIs and also help in production. I've done exactly that - I've shipped APIs used by thousands of users and I've been on-call, so I'm comfortable debugging live issues under pressure. I also learn fast and communicate clearly with non-technical people. I'm confident I can add value from early on and grow with the team.' The key is to name their need, give evidence you meet it, and show you'll fit the culture.",
            "I connect my strengths to what they need and give proof. I say what problem I solve for them, one real example that shows it, and that I learn fast and fit the team. Short, confident, and specific.",
            "match their needs", "give proof", "real example", "fit the team", "confident", "add value");

        Add("HR Round", Level.Easy,
            "What are your strengths and weaknesses?",
            "For strengths, pick two that matter for the role and give a quick example - e.g. 'I'm strong at debugging tricky production issues; I once traced an intermittent failure to a connection-pool limit and fixed it.' For weaknesses, be honest about a real one and show how you're actively improving it - e.g. 'I used to take on too much myself instead of delegating. I've been consciously sharing work and writing clearer docs so the team isn't dependent on me.' Never use a fake weakness like 'I work too hard'; interviewers see through it.",
            "For strengths I name two that fit the job with a quick example. For weaknesses I give a real one and how I'm improving it - like I used to not delegate enough, so now I share work and write better docs. I avoid fake answers.",
            "two relevant strengths", "example", "real weakness", "how you improve", "no fake weakness", "self-aware");

        Add("HR Round", Level.Easy,
            "Why do you want to leave your current job? / Why are you looking for a change?",
            "Stay positive - never bad-mouth your current employer. Focus on what you're moving toward, not running from. Example: 'I've learned a lot and I'm grateful for it, but I've grown as far as I can in my current scope. I'm looking for bigger technical challenges and more ownership - like the larger-scale systems this role involves - which my current company can't offer right now.' Growth, scope, learning, and alignment with the new role are all safe, genuine reasons.",
            "I stay positive and never criticize my current company. I focus on what I want next - bigger challenges, more ownership, learning - and how this role offers it. I frame it as moving toward growth, not running away.",
            "stay positive", "no bad-mouthing", "moving toward", "growth", "more ownership", "fits new role");

        Add("HR Round", Level.Medium,
            "What are your salary expectations?",
            "Do research first and give a range based on your market value, or deflect politely early in the process. Example: 'Based on my experience and the market for this role, I'm looking in the range of X to Y, but I'm flexible and most interested in the overall opportunity and growth. What range has the team budgeted for this position?' Turning it into a range and asking about their budget keeps you from underselling yourself while staying collaborative.",
            "I research the market first, then give a range instead of one number and say I'm flexible and care about the whole opportunity. I also ask what range they've budgeted, so I don't undersell myself.",
            "research market", "give a range", "flexible", "whole opportunity", "ask their budget", "don't undersell");

        Add("HR Round", Level.Medium,
            "Where do you see yourself in five years?",
            "Show ambition that aligns with the company, and focus on growth rather than a specific title. Example: 'In five years I want to be a senior engineer who owns significant parts of a system and mentors juniors. I want deep technical expertise but also the ability to lead a feature end-to-end. I see this role as a strong step toward that because of the scale and ownership involved.' Avoid answers that sound like you'll leave soon or want the interviewer's exact job.",
            "I show ambition that fits the company - growing into a senior engineer who owns big pieces and mentors others - and I connect it to how this role helps me get there. I avoid sounding like I'll leave soon.",
            "ambition", "aligns with company", "growth not just title", "own more", "mentor", "connect to role");

        Add("HR Round", Level.Medium,
            "Do you have any questions for us?",
            "Always say yes - having no questions signals low interest. Ask thoughtful ones about the team, the work, and growth. Good examples: 'What does success look like in this role in the first six months?', 'How does the team handle code review and deployments?', 'What are the biggest technical challenges the team is facing right now?', and 'What does growth and mentorship look like here?' Avoid asking only about salary and leave; show genuine curiosity about the work.",
            "I always have questions ready - it shows interest. I ask about what success looks like in the first months, how the team works and deploys, the biggest challenges, and growth. I don't ask only about pay and time off.",
            "always ask", "shows interest", "success in first months", "how the team works", "challenges", "growth", "not just salary");

        // ============================================================
        // BEHAVIORAL — use the STAR method: Situation, Task, Action,
        // Result. Prepare real stories from your own experience.
        // ============================================================
        Add("Behavioral", Level.Easy,
            "How do you answer any behavioral question? (the STAR method)",
            "Use STAR: Situation (set the context briefly), Task (what you were responsible for), Action (what YOU specifically did - the biggest part), and Result (the outcome, ideally with a number). Keep it about 90 seconds, use 'I' not 'we' when describing your actions, and pick a real story. Preparing 5-6 flexible STAR stories - a success, a failure, a conflict, a tight deadline, and a leadership moment - lets you answer almost any behavioral question.",
            "I use STAR: briefly the Situation and my Task, then mostly the Action I took, and end with the Result and a number. I say 'I' not 'we', keep it short, and prepare a few real stories I can reuse.",
            "situation", "task", "action", "result", "use I not we", "number in result", "prepare stories");

        Add("Behavioral", Level.Medium,
            "Tell me about a challenging problem you solved.",
            "Pick a real, specific problem and walk through it with STAR. Example: 'Our API had intermittent timeouts in production (Situation). I was asked to find and fix the root cause (Task). I added correlation-id logging and traced it to connection-pool exhaustion under load; I increased the pool sensibly, fixed a query holding connections too long, and added an alert (Action). Timeouts dropped to zero and p95 latency improved by 40% (Result).' Emphasize your reasoning and the measurable result.",
            "I pick one real problem and use STAR - what the problem was, that I owned it, the specific steps I took to diagnose and fix it, and the measurable result. I focus on my reasoning and end with a number.",
            "specific problem", "star", "your reasoning", "steps you took", "measurable result", "root cause");

        Add("Behavioral", Level.Medium,
            "Tell me about a time you failed or made a mistake.",
            "Show honesty, ownership, and learning - interviewers want growth, not perfection. Example: 'I once pushed a change on a Friday without enough testing and it broke a report over the weekend (Situation/Task). I owned it immediately, rolled it back, and fixed it Monday (Action). More importantly, I added tests and pushed the team to adopt a no-Friday-deploy and staging-check rule (Result/Learning).' Never say you've never failed, and don't blame others - focus on what you learned and changed.",
            "I share a real mistake, take full ownership, and focus on what I learned and changed afterward. Like a rushed deploy that broke something - I rolled it back, fixed it, and added tests and a rule so it wouldn't repeat. I never blame others.",
            "honesty", "own it", "no blaming others", "what you learned", "what you changed", "growth");

        Add("Behavioral", Level.Medium,
            "Tell me about a conflict with a coworker and how you handled it.",
            "Show maturity and focus on resolution, not who was 'right'. Example: 'A teammate and I disagreed on whether to refactor now or ship first (Situation). I set up a quick chat to understand their view, and I realized their concern was the deadline (Task/Action). We agreed to ship a clean minimal version now and schedule the refactor right after, and I wrote it into the backlog (Action). We hit the deadline and did the refactor the next sprint, and our working relationship actually got stronger (Result).' Emphasize listening, empathy, and a shared goal.",
            "I stay calm and focus on solving it, not winning. I listen to understand their real concern, find common ground, and agree on a practical plan. Like agreeing to ship first and refactor next sprint. It's about the shared goal, not who's right.",
            "stay calm", "listen first", "understand their view", "common ground", "shared goal", "resolution not winning");

        Add("Behavioral", Level.Medium,
            "Tell me about a time you had a tight deadline or too much work.",
            "Show prioritization, communication, and delivery under pressure. Example: 'We had a release in two days but three big tasks (Situation/Task). I listed them by impact and risk, flagged early to my lead that all three couldn't be fully done, and proposed shipping the two critical ones plus a safe stub for the third (Action). We delivered on time with no incidents, and finished the third the next sprint (Result).' The key traits: prioritize by impact, communicate early, and don't silently miss the date.",
            "I prioritize by impact, tell my lead early if everything can't fit, and propose what to ship now versus later. Like shipping the two critical items on time and finishing the third next sprint. I never go quiet and miss the date.",
            "prioritize by impact", "communicate early", "propose a plan", "deliver critical first", "no surprises", "meet the date");

        // ============================================================
        // MANAGERIAL / LEADERSHIP — asked in manager or senior rounds.
        // Show ownership, people skills, and good judgment.
        // ============================================================
        Add("Managerial", Level.Medium,
            "How do you handle disagreement with your manager or a technical decision?",
            "Show that you can disagree respectfully, back it with reasoning, and then commit. Example: 'If I disagree, I raise it privately with data - the trade-offs, risks, and an alternative - and I listen to their context, which is often wider than mine. If they still decide differently, I disagree and commit: I support the decision fully. Example: I preferred one caching approach; my manager chose another for operational simplicity, and once I understood the on-call cost I agreed and made it work.' The balance is honest input plus loyal execution.",
            "I raise my disagreement respectfully and with data - the trade-offs and an alternative - and I listen to their wider context. If they still decide differently, I disagree and commit and support it fully. Honest input, then loyal execution.",
            "disagree respectfully", "use data", "listen to context", "disagree and commit", "support the decision", "honest but loyal");

        Add("Managerial", Level.Medium,
            "How do you prioritize when everything seems important?",
            "Use a clear framework and communicate. Example: 'I rank work by impact and urgency - what unblocks others or affects customers goes first - and I weigh effort versus value. I make the priorities visible, confirm them with stakeholders so we agree, and I say no or 'later' to low-value work explicitly rather than quietly dropping it. For production issues, severity and customer impact always jump the queue.' The key is a repeatable method plus transparent communication.",
            "I rank by impact and urgency - customer impact and things that unblock others go first - and weigh effort versus value. I make priorities visible, agree them with stakeholders, and clearly say no or later to low-value work.",
            "impact and urgency", "unblock others", "effort vs value", "make it visible", "agree with stakeholders", "say no explicitly");

        Add("Managerial", Level.Medium,
            "How do you mentor or help junior team members?",
            "Show that you grow others, not just yourself. Example: 'I pair with juniors on tricky tasks and explain the why, not just the fix. In code review I'm specific and kind, and I point to resources so they learn. I give them ownership of real features with a safety net, and I encourage questions so they never feel stuck alone. Seeing someone I mentored ship a feature independently is one of the most rewarding parts of the job.' Emphasize patience, sharing context, and building their confidence.",
            "I pair with juniors and explain the why, not just the answer. I give kind, specific code reviews, hand them real work with support, and encourage questions. I focus on building their confidence and independence.",
            "explain the why", "pair programming", "kind specific reviews", "give real ownership", "encourage questions", "build confidence");

        Add("Managerial", Level.Medium,
            "How do you handle an underperforming team member or missed deadlines?",
            "Show empathy first, then a structured, fair approach. Example: 'I'd talk privately to understand the cause - it could be unclear expectations, a skill gap, blockers, or something personal. I set clear, specific goals and offer support like pairing or training, and I follow up regularly with honest feedback. Most people improve with clarity and help. If it continued despite real support, I'd escalate through the proper process. The goal is to help them succeed, not to blame.' Balance compassion with accountability.",
            "First I talk privately to find the real cause - unclear goals, a skill gap, blockers, or personal issues. Then I set clear goals, offer support like pairing or training, and follow up honestly. I help them improve; only if nothing changes do I escalate properly.",
            "understand the cause", "talk privately", "clear expectations", "offer support", "follow up", "empathy plus accountability");

        Add("Managerial", Level.Medium,
            "How do you give and receive feedback?",
            "Show that feedback is normal, specific, and kind both ways. Example: 'When giving feedback I'm specific and timely, focus on the behavior and impact not the person, and I balance it - praise good work publicly and correct privately. When receiving it, I listen without getting defensive, thank them, ask clarifying questions, and actually act on it. I treat feedback as a gift that helps me improve.' Emphasize a growth mindset and psychological safety.",
            "Giving feedback, I'm specific and timely, focus on the behavior and its impact, praise publicly and correct privately. Receiving it, I listen without getting defensive, thank them, and act on it. I treat feedback as a gift.",
            "specific and timely", "behavior not person", "praise publicly correct privately", "listen without defensiveness", "act on it", "growth mindset");

        // ---------------- Investment Banking ----------------
        Add("Investment Banking", Level.Easy,
            "What does an investment bank actually do?",
            "An investment bank helps companies, governments, and large investors raise money and make big financial decisions. Its main jobs are: underwriting (helping a company issue new shares or bonds to raise capital), advising on mergers and acquisitions (M&A) when one company buys or merges with another, and trading or selling securities like stocks and bonds. It sits between those who need money (companies) and those who have money (investors), earning fees for advice and for arranging the deals.",
            "An investment bank helps companies and governments raise money and do big deals. It helps them sell shares or bonds, advises on buying or merging with other companies, and trades securities. It connects people who need money with people who have money, and earns fees.",
            "raise capital", "underwriting", "mergers and acquisitions", "securities", "advisory", "fees");

        Add("Investment Banking", Level.Easy,
            "What is the difference between the buy side and the sell side?",
            "The sell side creates and sells financial products and services - investment banks, brokers, and market makers that underwrite securities, publish research, and execute trades for clients. The buy side is the investors who buy and hold those products to earn returns - hedge funds, pension funds, mutual funds, and asset managers. In short: the sell side makes and sells the investments; the buy side buys and manages them.",
            "The sell side makes and sells investments - like investment banks and brokers. The buy side buys and holds them to make money - like hedge funds, pension funds, and asset managers. Sell side sells, buy side buys.",
            "sell side", "buy side", "brokers", "hedge funds", "asset managers", "investors");

        Add("Investment Banking", Level.Medium,
            "What is an IPO and why does a company do one?",
            "An IPO (Initial Public Offering) is when a private company sells its shares to the public for the first time and becomes listed on a stock exchange. Companies do it to raise a large amount of capital for growth, to let early investors and founders cash out some of their stake, and to gain public prestige and a market value for their shares. The investment bank underwrites the IPO: it helps set the price, markets the shares to investors (the 'roadshow'), and guarantees the sale. The trade-off is more regulation, public reporting, and pressure from shareholders.",
            "An IPO is when a private company sells shares to the public for the first time and gets listed on a stock market. It does this to raise money for growth and let early investors cash out. The bank helps set the price and sell the shares. The downside is more rules and public reporting.",
            "initial public offering", "go public", "raise capital", "underwriting", "roadshow", "regulation");

        Add("Investment Banking", Level.Medium,
            "What is the difference between debt financing and equity financing?",
            "Debt financing means borrowing money (like issuing bonds or taking a loan) that must be repaid with interest; the lender does not own part of the company, and interest is usually tax-deductible, but the debt must be paid back on schedule. Equity financing means raising money by selling ownership (shares); there is no repayment, but the new owners share in profits and get voting rights, diluting existing owners. Debt keeps ownership but adds fixed repayment risk; equity avoids repayment but gives away part of the company.",
            "Debt financing is borrowing money that you pay back with interest - the lender owns nothing. Equity financing is selling ownership shares - no repayment, but new owners share the profits and control. Debt keeps ownership but must be repaid; equity gives away part of the company.",
            "debt", "equity", "bonds", "shares", "interest", "ownership dilution");

        // ---------------- Battery / Battery Bank ----------------
        Add("Battery", Level.Easy,
            "What is a battery bank?",
            "A battery bank is a group of individual batteries connected together to store more energy or deliver more power than a single battery could. Connecting batteries in series increases the total voltage, while connecting them in parallel increases the total capacity (amp-hours) and runtime. Battery banks are used in solar power systems, data-center backup (UPS), telecom towers, and electric vehicles to provide reliable stored energy that can be used when the main supply is off or during peak demand.",
            "A battery bank is many batteries joined together to store more energy or give more power than one battery. Joining in series raises the voltage; joining in parallel raises the capacity. They are used in solar systems, backup power, and electric vehicles.",
            "batteries connected", "series", "parallel", "voltage", "capacity", "energy storage");

        Add("Battery", Level.Easy,
            "What is the difference between connecting batteries in series and in parallel?",
            "In a series connection you join the positive terminal of one battery to the negative of the next; this adds up the voltages while the capacity (amp-hours) stays the same - for example two 12V 100Ah batteries in series give 24V 100Ah. In a parallel connection you join all the positive terminals together and all the negative terminals together; this keeps the voltage the same but adds up the capacity - two 12V 100Ah batteries in parallel give 12V 200Ah. Series is for higher voltage; parallel is for longer runtime.",
            "Series joins positive to negative and adds the voltage, keeping capacity the same (two 12V become 24V). Parallel joins all positives and all negatives together and adds the capacity, keeping voltage the same (100Ah becomes 200Ah). Series for more voltage, parallel for more runtime.",
            "series", "parallel", "voltage adds", "capacity adds", "amp-hours", "runtime");

        Add("Battery", Level.Medium,
            "What is a Battery Management System (BMS) and why is it needed?",
            "A Battery Management System (BMS) is the electronic control system that protects and manages a battery bank, especially lithium-ion. It monitors each cell's voltage, current, and temperature, and it balances the cells so they charge and discharge evenly. It prevents dangerous conditions like overcharging, over-discharging, overheating, and short circuits by disconnecting the battery when needed. A BMS is needed because lithium cells can be damaged or even catch fire if pushed outside safe limits, and balancing extends the pack's life and usable capacity.",
            "A BMS is the electronics that protect a battery bank. It watches each cell's voltage, current, and temperature, keeps the cells balanced, and cuts off the battery if it overcharges, over-discharges, or overheats. It is needed to keep lithium batteries safe and to make them last longer.",
            "cell monitoring", "balancing", "overcharge protection", "temperature", "safety", "battery life");

        Add("Battery", Level.Medium,
            "What is the difference between energy (Wh) and power (W) in a battery?",
            "Power, measured in watts (W), is the rate at which a battery delivers or absorbs energy at a given moment - how fast it can work. Energy, measured in watt-hours (Wh), is the total amount of work stored - how much it can do over time. Energy is roughly power multiplied by time (Wh = W x hours). For example, a battery delivering 100W for 5 hours uses 500Wh. When sizing a battery bank you need enough power to run the load at once and enough energy to run it for the required duration.",
            "Power (watts) is how fast a battery gives energy right now. Energy (watt-hours) is the total amount it can give over time. Energy is roughly power times hours - 100W for 5 hours is 500Wh. You need enough power to run the load and enough energy to run it long enough.",
            "watts", "watt-hours", "rate vs total", "power = rate", "energy = capacity", "sizing");

        return list;
    }
}
