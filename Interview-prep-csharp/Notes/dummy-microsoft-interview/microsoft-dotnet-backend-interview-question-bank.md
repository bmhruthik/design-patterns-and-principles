# Microsoft-Style .NET Backend Interview: Complete Preparation Question Bank

**Target role:** Software Engineer II / Backend Engineer, C#/.NET, 4+ years experience.

This is a rigorous preparation bank for the interview loop we will run later. In a real Microsoft interview, the exact number and subject of conversations vary by role and team; this document is a comprehensive simulation, not a promise that every question appears in a single real loop.

When we conduct the interview later, I will select from this bank, ask one round at a time, introduce follow-ups based on your answers, and assess you at a senior product-company bar.

---

# Round 0 — Recruiter and Experience Screen

1. Walk me through your background and why you chose backend/.NET engineering.
2. What type of backend role are you seeking, and why does Microsoft interest you?
3. Describe the most technically challenging system you have built or materially improved.
4. What was your specific ownership in that project? Distinguish your work from the team’s work.
5. Tell me about a production incident you handled. How did you detect, mitigate, and prevent recurrence?
6. Describe a project where requirements were ambiguous. What did you do first?
7. What is the most important technical decision you have made in the last year?
8. What feedback have you received that changed how you work?
9. What do you want to learn or do next that you cannot do in your current role?
10. Explain one project from your résumé to a non-specialist stakeholder.

---

# Round 1 — C# Language Fundamentals

## Type system, OOP, and language features

1. Explain the difference between a `class`, `struct`, `record`, and `record struct`. When would you choose each?
2. Explain value types versus reference types. What happens when each is assigned or passed to a method?
3. What are boxing and unboxing? Give a performance-sensitive example.
4. Compare `const`, `readonly`, and `static readonly`.
5. Explain `ref`, `out`, `in`, and `ref return`. What constraints or risks do they introduce?
6. Compare `virtual`, `abstract`, `override`, `sealed`, and interface default implementations.
7. Explain composition versus inheritance. Give an example where inheritance is the wrong choice.
8. Explain access modifiers, including `internal`, `protected internal`, and `private protected`.
9. What does `init` do? How does it relate to immutable design?
10. How do nullable reference types work? What problems do they solve and what do they not solve?

## Interfaces, generics, delegates, and events

11. Explain generic type constraints. When would you use `where T : class`, `struct`, `new()`, or an interface constraint?
12. What are covariance and contravariance? Explain `IEnumerable<out T>` and `Action<in T>` with an example.
13. What is the difference between a delegate, an event, and a callback interface?
14. Why should an event usually not be exposed as a publicly settable delegate?
15. Explain lambda expressions, closures, and a common closure-related bug.
16. What are extension methods? When do they improve a codebase, and when can they make it harder to understand?
17. Explain expression trees. Where might a framework such as EF Core use them?
18. What is `dynamic`? Compare its behavior and trade-offs with `object`, interfaces, and generics.

## Collections, strings, LINQ, and iteration

19. Compare array, `List<T>`, `LinkedList<T>`, `HashSet<T>`, `Dictionary<TKey,TValue>`, `Queue<T>`, and `Stack<T>` by access and insertion complexity.
20. What makes a good dictionary key? What happens if a mutable object used as a key changes?
21. Compare `IEnumerable<T>`, `ICollection<T>`, `IList<T>`, `IReadOnlyCollection<T>`, and `IQueryable<T>`.
22. Explain deferred execution in LINQ. Show an example where it surprises a developer.
23. Explain `Select` versus `SelectMany`, `First` versus `FirstOrDefault`, and `Single` versus `SingleOrDefault`.
24. What is the difference between LINQ-to-Objects and LINQ providers such as EF Core?
25. Explain `yield return`. When is an iterator useful, and when can it hold resources too long?
26. Why is `string` immutable? What is string interning, and when might `StringBuilder` be appropriate?
27. How would you compare strings safely for identifiers, user-facing text, or security-sensitive tokens?

## Exceptions and resource management

28. What is the difference between `throw;` and `throw ex;`?
29. When should you use an exception instead of a result/error return type?
30. Explain `try`, `catch`, `finally`, exception filters, and custom exception types.
31. What does `IDisposable` mean? Explain the `using` statement/declaration.
32. Explain the dispose pattern. When do you need a finalizer, and why is it usually avoided?
33. What is `IAsyncDisposable` and when is `await using` necessary?

---

# Round 2 — Advanced C#, .NET Runtime, Concurrency, and Performance

## Runtime fundamentals

1. Describe the relationship between CLR, CTS, CLS, JIT compilation, assemblies, and the .NET runtime.
2. What is the difference between JIT and ahead-of-time (AOT) compilation? What trade-offs does AOT create?
3. What is stored in an assembly? What is metadata used for?
4. What are NuGet package transitive dependencies and version conflicts? How do you manage package security?
5. Explain garbage collection generations. Why do long-lived objects and large allocations matter?
6. What is the Large Object Heap? How can allocation patterns affect latency?
7. How would you diagnose a memory leak in a .NET service?
8. What are `Span<T>` and `ReadOnlySpan<T>`? Why are they fast, and what restrictions do they have?
9. What are `Memory<T>` and `ReadOnlyMemory<T>`? When do they fit where `Span<T>` does not?
10. Explain `ArrayPool<T>`. What bugs can careless pooling cause?

## Async and threading

11. Explain what the compiler produces conceptually for an `async` method.
12. Compare `Task`, `ValueTask`, `Thread`, and thread-pool work items.
13. Why can `.Result` or `.Wait()` be harmful? Explain deadlock and thread-pool starvation.
14. Compare `Task.WhenAll`, sequential `await`, and `Parallel.ForEachAsync`.
15. When would `ValueTask` help, and when can it make code worse?
16. How should a `CancellationToken` be used and propagated?
17. What happens if a caller cancels a request after your service has already sent a payment request?
18. Explain `ConfigureAwait`. Does it usually matter in ASP.NET Core application code?
19. How do you safely run CPU-bound work without blocking request threads?
20. What is an async stream (`IAsyncEnumerable<T>`)? Give a useful API scenario.

## Synchronization and correctness

21. Explain a race condition you have seen or could create in C#.
22. Compare `lock`, `Monitor`, `SemaphoreSlim`, `Mutex`, `ReaderWriterLockSlim`, `Interlocked`, and `Channel<T>`.
23. Why should you avoid `lock` around an `await`?
24. Implement or describe a thread-safe counter. Which primitive would you use and why?
25. Design bounded concurrent processing of 100,000 jobs while permitting at most 50 external API calls at a time.
26. How would you implement a producer-consumer pipeline with backpressure in .NET?
27. What is a deadlock? How do you diagnose and prevent one?
28. How would you profile CPU, allocations, lock contention, and async delays in a production .NET process?

---

# Round 3 — .NET, ASP.NET Core, and Web APIs

## Framework and application architecture

1. Describe the lifecycle of an HTTP request in ASP.NET Core.
2. What is middleware? Explain why registration order matters.
3. Implement global exception handling that returns stable RFC 7807 problem responses.
4. Compare Minimal APIs and MVC controllers. When would you choose each?
5. What is dependency injection? Explain singleton, scoped, and transient lifetimes with examples.
6. What bugs can occur if a singleton depends on a scoped service?
7. Explain the options pattern: `IOptions<T>`, `IOptionsSnapshot<T>`, and `IOptionsMonitor<T>`.
8. How do configuration providers compose, and how should secrets be handled across environments?
9. What are hosted/background services? How do you safely stop a long-running worker?
10. Compare middleware, MVC filters, endpoint filters, and action filters.

## HTTP, REST, validation, and API evolution

11. What makes an API RESTful in practical terms? Which REST ideas are often misapplied?
12. How would you choose status codes for creation, asynchronous processing, validation failure, conflict, and rate limiting?
13. Explain model binding and validation in ASP.NET Core.
14. What is overposting? Demonstrate a safe request DTO design.
15. How would you validate cross-field or database-dependent rules?
16. How do you design cursor pagination? Why is it preferable to offset pagination at scale?
17. What are ETags and conditional requests? How can they support optimistic concurrency/caching?
18. How would you version a public REST API and safely deprecate a breaking version?
19. What belongs in OpenAPI/Swagger documentation beyond endpoint paths?
20. How do JSON serialization settings and contract changes break clients?

## Security

21. Explain authentication versus authorization.
22. Explain secure JWT validation: issuer, audience, signature, expiry, keys, scopes, and clock skew.
23. What risks come with storing bearer tokens in browser local storage, cookies, or URLs?
24. How would you implement resource-based authorization for `GET /orders/{id}`?
25. How do CORS, CSRF, XSS, and HTTPS relate to an API/backend engineer?
26. How do you protect an API from brute force, abuse, and denial-of-service pressure?
27. How would you secure service-to-service communication in a microservice environment?

## Reliability and observability

28. What are idempotent endpoints? Why do orders and payments need them?
29. Design an idempotent `POST /orders` endpoint that supports client retries.
30. How do you set timeouts, retries, circuit breakers, and concurrency limits for outbound HTTP calls?
31. Which errors should never be retried automatically?
32. Explain structured logging. Why is string interpolation often inferior for logs?
33. Explain correlation IDs, trace IDs, distributed tracing, logs, metrics, and alerts.
34. Which API metrics would you put on a production dashboard?

---

# Round 4 — Coding Interview

You should be prepared to code in C# without relying on framework magic. In every problem, expect follow-ups on edge cases, tests, time/space complexity, naming, and production constraints.

1. Given an integer array and a target, return indices of two values whose sum equals the target.
2. Find the first non-repeating character in a string.
3. Validate whether a string of parentheses/brackets is balanced.
4. Reverse a singly linked list, then detect whether it has a cycle.
5. Merge overlapping intervals.
6. Given a binary tree, return a level-order traversal.
7. Find the lowest common ancestor of two nodes in a binary tree.
8. Implement an LRU cache with `Get` and `Put` in expected O(1) time.
9. Given a stream of events, return the top K most frequent event names.
10. Find the longest substring without repeating characters.
11. Given sorted arrays, merge them with limited additional memory.
12. Design a rate limiter. Start in memory, then discuss distributed deployment.
13. Design a bounded asynchronous work queue with cancellation and failure handling.
14. Given a list of API calls, execute no more than N concurrently and return all results/errors.
15. Parse a large log file stream and report the top N error codes without loading the entire file in memory.

### Typical coding follow-ups

1. What is the complexity? Can you prove it?
2. What input breaks your first solution?
3. What unit tests would you write first?
4. How would this change if input does not fit in memory?
5. How would you make it thread safe?
6. Is the code readable and idiomatic C#?
7. Can you explain the trade-off between a simple and an optimized solution?

---

# Round 5 — Database, SQL, and Entity Framework Core

## SQL and data modeling

1. Explain inner, left, right, full outer, cross, and self joins. When does a left join accidentally behave as an inner join?
2. Write a query to return each customer’s most recent order.
3. Write a query to find the top three products by sales in each category using a window function.
4. Explain CTEs. When would a recursive CTE be useful?
5. Explain normalization. When is controlled denormalization justified?
6. Design tables for users, roles, subscriptions, invoices, payments, and payment events.
7. How would you model a many-to-many relationship with attributes on the relationship?
8. How do foreign keys, unique constraints, check constraints, and defaults enforce business invariants?
9. Why should money use decimal/numeric values and currency codes rather than floating point?
10. What is a soft delete? What complications does it introduce for uniqueness and queries?

## Indexes and query performance

11. How do B-tree indexes work conceptually? What operations benefit from them?
12. Design indexes for: customer order history, pending worker jobs, order lookup, and date-range reporting.
13. Why does composite index column order matter?
14. What is a covering index? What is the cost of adding too many indexes?
15. How do you use an execution plan to diagnose a slow query?
16. What are table scans, index seeks, key lookups, cardinality estimates, and parameter sniffing?
17. Explain offset versus keyset pagination from a database-performance perspective.
18. How would you investigate a database CPU spike caused by one API endpoint?

## Transactions, concurrency, and reliability

19. Explain ACID.
20. Explain read committed, repeatable read, snapshot/MVCC, and serializable isolation. What anomalies do they allow/prevent?
21. What causes a deadlock? How do you reduce and handle deadlocks?
22. Compare optimistic and pessimistic concurrency.
23. How do EF Core concurrency tokens or SQL Server `rowversion` work?
24. How would you reserve inventory without overselling under concurrent purchases?
25. How do you prevent duplicate order creation when an HTTP response times out and the client retries?
26. Why should you not keep a database transaction open while calling a remote payment provider?
27. Explain the transactional outbox pattern. What failure does it prevent?
28. When might you use a stored procedure, raw SQL, or Dapper instead of EF Core?

## EF Core

29. Explain tracked queries versus `AsNoTracking` and `AsNoTrackingWithIdentityResolution`.
30. What is the N+1 query problem? How would you identify it in production?
31. Compare `Include`, projection using `Select`, explicit loading, and lazy loading.
32. What is cartesian explosion from multiple `Include` calls? When might `AsSplitQuery` help?
33. How do EF Core migrations work? Describe a safe zero-downtime schema migration.
34. How would you implement a retry strategy for transient database failures without duplicating business effects?
35. How do you inspect EF-generated SQL and decide whether a query needs improvement?
36. What are common causes of DbContext lifetime/memory issues in a web service?

---

# Round 6 — System Design

In each system-design question, clarify requirements before proposing components. You will be evaluated on requirements, capacity reasoning, data model, API design, scale, reliability, security, observability, cost, and trade-offs.

## Primary design prompts

1. Design a high-volume order-processing platform: orders, inventory reservation, payments, and confirmation notifications.
2. Design a URL-shortening service with custom aliases, redirects, expiry, analytics, and abuse prevention.
3. Design a multi-channel notification platform for email, SMS, push, and in-app notifications.
4. Design a real-time chat platform with direct messages, group messages, history, offline delivery, and read state.
5. Design a distributed API rate-limiting platform.
6. Design a file-upload and processing service for large documents/images.
7. Design an audit-log platform for security-sensitive enterprise actions.
8. Design a subscription billing service with recurring payment and webhook handling.
9. Design a background-job platform used by multiple internal services.
10. Design a feature-flag/configuration service with low-latency reads and safe rollouts.

## Common deep-dive questions

1. What are your functional and non-functional requirements?
2. Estimate average and peak RPS/messages per second, storage growth, and bandwidth.
3. Which data requires strong consistency, and which can be eventually consistent?
4. Why choose SQL, NoSQL, Redis, a search index, or object storage for each data type?
5. How would you design APIs, schema/indexes, and pagination?
6. When should work happen synchronously versus through a queue?
7. Compare Kafka, RabbitMQ, Azure Service Bus, and SQS conceptually. Which qualities matter for this design?
8. How do you implement retries, poison-message handling, dead-letter queues, and replay?
9. How do you handle at-least-once delivery and make consumers idempotent?
10. What does “exactly once” mean in practice, and why is it rarely end-to-end achievable?
11. What happens when the queue, cache, database, or external provider is unavailable?
12. How do you prevent a duplicate payment/side effect after a timeout?
13. How do you scale ASP.NET Core API nodes and background workers independently?
14. What limits throughput: CPU, database connections, partitions, network, or downstream rate limits?
15. Which metrics, traces, logs, SLOs, and alerts would you implement?
16. How do you secure data, secrets, service-to-service calls, PII, and tenant boundaries?
17. How do you deploy, roll back, migrate data, backfill, and recover from a regional failure?
18. How would this design evolve from a modular monolith to services, and when should it not?
19. What is the CAP trade-off in this particular design?
20. What would you build first, and what would you defer until scale proves it necessary?

---

# Round 7 — Architecture, Design Patterns, and Engineering Judgment

## Architecture

1. Explain Clean Architecture, Onion Architecture, and Hexagonal Architecture. What problem are they trying to solve?
2. What does dependency inversion mean in a real ASP.NET Core service?
3. How would you structure a .NET solution for a medium-sized order domain?
4. What is a bounded context in DDD? Give examples in an e-commerce domain.
5. Distinguish entities, value objects, aggregates, domain services, and domain events.
6. When is CQRS useful? What complexity does it add?
7. When is event sourcing appropriate, and why is it often the wrong default?
8. How would you split a monolith into independently deployable services?
9. What are the failure modes and organizational costs of microservices?
10. How do you decide service boundaries and data ownership?

## Patterns and practical design

11. Explain Repository and Unit of Work. When are they useful, and when do they merely wrap EF Core without value?
12. Explain Factory, Strategy, Decorator, Adapter, Builder, and Mediator patterns with .NET examples.
13. Design a payment gateway abstraction supporting multiple providers. Which pattern(s) apply?
14. Design a notification service supporting email, SMS, and push with provider-specific retry behavior.
15. How do you avoid a dependency-injection container becoming a service locator anti-pattern?
16. How would you introduce a breaking API/event-contract change without disrupting clients?
17. What is a shared library appropriate for? When does it create distributed-monolith coupling?
18. What makes code easy to test? What makes it difficult to test?
19. Review a pull request that works today but adds coupling, duplicate business logic, or hidden failure modes.
20. Describe a technical decision you reversed. What evidence changed your decision?

---

# Round 8 — Performance, Production Debugging, and Operations

1. An API’s p99 latency doubled after a release while average latency remained normal. How do you investigate?
2. CPU is low but request latency and timeout rates are high. What could be happening?
3. A .NET service memory footprint grows continually. What data do you collect and what hypotheses do you test?
4. How do you distinguish a managed memory leak, unmanaged memory leak, cache growth, and normal GC behavior?
5. What causes thread-pool starvation and how would it appear in telemetry?
6. A database has high CPU and many slow queries. How do you find the highest-value fix?
7. A Redis cache is intermittently unavailable. How should API behavior degrade?
8. A cache key expires and thousands of requests hit the database simultaneously. How do you prevent/contain a cache stampede?
9. A message queue’s oldest-message age is rising. What do you inspect before simply adding consumers?
10. How do you tune worker concurrency without overwhelming a database or third-party API?
11. A deployment has raised error rate from 0.1% to 3%. What is your incident process?
12. Define SLI, SLO, SLA, error budget, MTTR, and change-failure rate.
13. What should a production runbook contain?
14. How do you design a safe canary deployment and rollback trigger?
15. Describe how you would run a load test for a new API before launch.
16. Which benchmarks are misleading, and how do you make benchmarking representative?
17. How do you reduce allocations in a hot .NET code path without making code needlessly complex?
18. When should you use compression, pagination, streaming, or response caching?

---

# Round 9 — Behavioral, Collaboration, and Leadership

Use STAR(R): Situation, Task, Action, Result, Reflection. Be concrete about your personal contribution and measurable outcome.

1. Tell me about a time you took ownership of a problem outside your formal responsibility.
2. Describe a technical disagreement with a peer or senior engineer. How did you resolve it?
3. Tell me about a failed project, deployment, or decision. What did you learn and change?
4. Describe a time you had to deliver under an aggressive deadline. What trade-offs did you make?
5. Tell me about a time you improved a system proactively.
6. Describe a production incident you led or significantly contributed to resolving.
7. Tell me about a time you made an ambiguous requirement actionable.
8. Describe a time you received difficult feedback. What did you do afterward?
9. Tell me about a time you gave difficult feedback to someone else.
10. Describe a time you mentored or unblocked another engineer.
11. Describe a time you advocated for customer impact over a technically elegant solution.
12. Tell me about a time you used data to change a technical or product decision.
13. How do you balance delivery speed, quality, security, and reliability?
14. Describe a time you earned trust across teams or disciplines.
15. What would your manager say is your biggest strength and biggest development area?

---

# Round 10 — Hiring Committee Review

There are no candidate questions in this internal simulated round. The committee evaluates accumulated evidence against the role:

| Dimension | Evidence considered |
| --- | --- |
| Problem solving | Clarifies requirements, finds correct solutions, explains complexity, handles follow-ups. |
| C#/.NET depth | Uses language/runtime/framework concepts accurately and pragmatically. |
| Backend design | Designs APIs/data/storage with sound correctness and scale trade-offs. |
| Production excellence | Anticipates failure, security, observability, performance, and recovery. |
| Collaboration/ownership | Communicates clearly, learns from feedback, and shows accountable behavior. |
| Hiring bar | Consistent evidence of operating independently at the targeted level. |

## Preparation advice

Do not memorize perfect scripts. Practice stating assumptions, thinking aloud, identifying the critical invariant, and explaining trade-offs. For example: “We must never charge twice,” “only members can access this conversation,” or “the request is acknowledged only after durable acceptance.” That is stronger than listing technologies without explaining why they protect the system.
