# c# dot net developer role

Created: June 22, 2026 12:43 PM

Here’s a realistic top-tier C#/.NET interview loop, progressing from fundamentals to senior-level engineering judgment.

1. Recruiter / Background Round
    - Tell me about a .NET project you owned end-to-end.
    - Why C# and .NET? What kinds of systems do you think they are especially good for?
    - Describe a difficult production issue you solved.
    - What are you looking for in your next role?
2. C# Fundamentals Round
    - Explain the difference between `class`, `struct`, `record`, and `record struct`. When would you use each?
    - What is the difference between a value type and a reference type?
    - Explain `const`, `readonly`, and `static readonly`.
    - What do `ref`, `out`, and `in` mean?
    - What is boxing and unboxing? Why can it affect performance?
    - Explain the differences between `IEnumerable<T>`, `ICollection<T>`, `IList<T>`, and `IQueryable<T>`.
    - What does `using` do? How is `await using` different?
    - Explain exception filters and when you would avoid catching a general `Exception`.
3. Object-Oriented Design & Clean Code
    - Explain encapsulation, inheritance, polymorphism, and abstraction using a practical .NET example.
    - What are SOLID principles? Show an example of violating and then fixing the Single Responsibility Principle.
    - When would you favor composition over inheritance?
    - How would you design a notification service supporting email, SMS, and push notifications?
    - What makes an API or class difficult to test?
    - How do dependency injection lifetimes—singleton, scoped, transient—work in ASP.NET Core?
4. Coding / Data Structures & Algorithms

Typically done in a shared editor, with attention to correctness, clarity, complexity, and tests.

- Given an array of integers, return the indices of two numbers that add up to a target.
- Find the first non-repeating character in a string.
- Merge overlapping intervals.
- Implement an LRU cache.
- Given a stream of events, return the top K most frequent event types.
- Detect a cycle in a linked list.
- Design a thread-safe in-memory rate limiter.

Follow-up questions:

- What is the time and space complexity?
- How would your solution behave with millions of items?
- What test cases would you add?
- Can you make it safe under concurrent access?
1. Async, Concurrency & Runtime Round
    - Explain how `async`/`await` works under the hood.
    - What is the difference between `Task`, `ValueTask`, and `Thread`?
    - What problems can arise from calling `.Result` or `.Wait()` on a task?
    - Explain `Task.WhenAll` versus awaiting tasks one by one.
    - When would you use `CancellationToken`, and how should cancellation be propagated?
    - What is a race condition? Show how you would fix one in C#.
    - Compare `lock`, `Monitor`, `SemaphoreSlim`, `Mutex`, and `Interlocked`.
    - How would you process 100,000 messages concurrently while limiting calls to an external API to 50 at a time?
    - What is thread-pool starvation, and how would you diagnose it?
2. ASP.NET Core / Web API Round
    - Describe the lifecycle of an HTTP request in ASP.NET Core.
    - What is middleware, and how would you implement global exception handling?
    - How do authentication and authorization differ?
    - How would you implement JWT authentication securely?
    - How do you version a public REST API?
    - How do you prevent overposting and validate request models?
    - What are idempotent endpoints? Why do they matter for payment or order APIs?
    - How would you make an API resilient to retries, timeouts, and downstream failures?
    - How do you add structured logging, tracing, and correlation IDs to an API?
3. Database & Entity Framework Core Round
    - Explain tracking versus no-tracking queries in EF Core.
    - What is the N+1 query problem? How do you identify and fix it?
    - Compare `Include`, projection with `Select`, explicit loading, and lazy loading.
    - How would you design indexes for a frequently queried `Orders` table?
    - Explain database transactions and isolation levels.
    - How would you prevent duplicate order creation if a client retries a request?
    - When would you use raw SQL or Dapper instead of EF Core?
    - Design a schema for users, roles, subscriptions, and payments.
4. System Design Round

Example prompt: “Design a URL-shortening service, notification platform, or high-volume order-processing system using .NET.”

Expected discussion:

- Clarify requirements: scale, latency, consistency, availability, security.
- Define APIs and data model.
- Choose SQL versus NoSQL and explain the trade-off.
- Design caching with Redis.
- Use queues such as Kafka, Azure Service Bus, RabbitMQ, or SQS for asynchronous work.
- Handle retries, dead-letter queues, duplicate delivery, and idempotency.
- Scale ASP.NET Core services horizontally.
- Design observability: metrics, logs, traces, alerts.
- Explain deployment, rollback, disaster recovery, and data migration.

Deep-dive questions:

- How would you process 10 million events per day?
- What happens when the queue is unavailable?
- How do you prevent a duplicate message from charging a customer twice?
- How would you evolve the system from one service to microservices, and when should you not?
1. Architecture & Senior Engineering Round
    - How would you split a monolith into independently deployable services?
    - What are the downsides of microservices?
    - How do you decide whether a shared library is appropriate?
    - How do you introduce a breaking API change without disrupting clients?
    - Describe a technical decision you reversed. What evidence changed your mind?
    - How do you balance delivery speed, code quality, security, and reliability?
    - How would you review a pull request that “works” but creates long-term maintenance risk?
    - What engineering metrics do you use to understand system health and team effectiveness?
2. Behavioral / Leadership Round

Top companies assess this as rigorously as coding.

- Tell me about a time you disagreed with a technical decision. What did you do?
- Describe a failure you were responsible for and how you handled it.
- Tell me about a time you improved a system without being asked.
- Describe a situation where requirements were unclear.
- How have you mentored a less experienced engineer?
- Tell me about a time you prioritized customer impact over an elegant technical solution.
- Describe how you handle feedback from peers or managers.
- What would your previous teammates say is your biggest strength and growth area?

A strong interview usually evaluates four things throughout: C#/.NET depth, problem solving, production engineering judgment, and communication. For mid-to-senior roles, the advanced rounds matter most: concurrency, distributed systems, system design, trade-offs, and ownership.