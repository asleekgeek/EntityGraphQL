---
sidebar_position: 2
---

# Getting Started

## Installation

If you are working with ASP.NET then install [EntityGraphQL.AspNet](https://www.nuget.org/packages/EntityGraphQL.AspNet) [![Nuget](https://img.shields.io/nuget/dt/EntityGraphQL.AspNet)](https://www.nuget.org/packages/EntityGraphQL.AspNet) via Nuget. It will allow you to:

- Quickly get started with ASP.NET
- Integrate with ASP.NET policy authorization

You can install the core [EntityGraphQL](https://www.nuget.org/packages/EntityGraphQL) [![Nuget](https://img.shields.io/nuget/dt/EntityGraphQL)](https://www.nuget.org/packages/EntityGraphQL) package if you do not need ASP.NET.

## Create a data model

_Note: There is no dependency on Entity Framework. Queries are compiled to `IQueryable` or `IEnumberable` LINQ expressions. EF is not a requirement - any ORM working with `LinqProvider` or an in-memory object will work - this example uses EF._

```cs
public class DemoContext : DbContext
{
    public DbSet<Movie> Movies { get; set; }
    public DbSet<Person> People { get; set; }
    public DbSet<Actor> Actors { get; set; }
}

public class Movie
{
    public uint Id { get; set; }
    public string Name { get; set; }
    public Genre Genre { get; set; }
    public DateTime Released { get; set; }
    public List<Actor> Actors { get; set; }
    public List<Writer> Writers { get; set; }
    public Person Director { get; set; }
    public uint? DirectorId { get; set; }
    public double Rating { get; internal set; }
}

public class Actor
{
    public uint PersonId { get; set; }
    public Person Person { get; set; }
    public uint MovieId { get; set; }
    public Movie Movie { get; set; }
}
public class Writer
{
    public uint PersonId { get; set; }
    public Person Person { get; set; }
    public uint MovieId { get; set; }
    public Movie Movie { get; set; }
}

public enum Genre
{
    Action,
    Drama,
    Comedy,
    Horror,
    Scifi,
}

public class Person
{
    public uint Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime Dob { get; set; }
    public List<Actor> ActorIn { get; set; }
    public List<Writer> WriterOf { get; set; }
    public List<Movie> DirectorOf { get; set; }
    public DateTime? Died { get; set; }
    public bool IsDeleted { get; set; }
}
```

## Create the API

Using what ever .NET API library you wish you can receive a query, execute it and return the data. Here is an example with ASP.NET.

You will need to install [EntityGraphQL.AspNet](https://www.nuget.org/packages/EntityGraphQL.AspNet) to use `MapGraphQL<>()` and `AddGraphQLSchema()`. You can also build your own endpoint, see below.

```cs
using EntityGraphQL.AspNet;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<DemoContext>(opt => opt.UseInMemoryDatabase("Demo"));
builder.Services.AddGraphQLSchema<DemoContext>();

var app = builder.Build();

app.MapGraphQL<DemoContext>();

app.Run();
```

This sets up a `HTTP` `POST` end point at `/graphql` where the body of the post is expected to be a GraphQL query. You can change the path with the `path` argument in `MapGraphQL<T>()`

:::tip
Version 5.6 introduced an argument to `MapGraphQL` called `followSpec`. When set to true it will follow [this GraphQL over HTTP spec](https://github.com/graphql/graphql-over-http/blob/main/spec/GraphQLOverHTTP.md). This will be the default behavior in version 6 so if you are starting a new project we suggest setting it to `true` now.

```cs
app.MapGraphQL<DemoContext>(followSpec: true);
```

:::

_You can authorize the route how ever you wish using ASP.NET. See the Authorization section for more details._

You can also expose any endpoint over any protocol you'd like. We'll use HTTP/S for these examples.

## Securing the route in ASP.NET core

When using `MapGraphQL()`, the route is added with the `IEndpointRouteBuilder.MapPost` method. The `.MapPost()` method can be chained with `.RequireAuthorization()` where an array of Policy Names can be passed. The policy names are ANDed together with `.RequireAuthorization()`.

To add one or more security policies when using `MapGraphQL()` you can pass a configure function that has access to the `IEndpointConventionBuilder` from the created `.MapPost()`.

```cs
//in ConfigureServices
services.AddAuthentication()
services.AddAuthorization(options =>
{
    options.AddPolicy("authorized", policy => policy.RequireAuthenticatedUser();
});


//in Configure
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
  {
      // defaults to /graphql endpoint
    endpoints.MapGraphQL<DemoContext>(configureEndpoint: (endpoint) => {
      endpoint.RequireAuthorization("authorized");
      // do other things with endpoint
    });
  });

```

## Query your API

You can now make a request to your API via any HTTP tool/library.

For example

```json
POST localhost:5000/graphql
{
  "query": "{
    movies { id name }
  }",
  "variables": null
}
```

Will return the following result (depending on the data in you DB).

```json
{
  "data": {
    "movies": [
      {
        "id": 11,
        "name": "Inception"
      },
      {
        "id": 12,
        "name": "Star Wars: Episode IV - A New Hope"
      }
    ]
  }
}
```

Maybe you only want a specific property **(request body only from now on)**

```graphql
{
  movie(id: 11) {
    id
    name
  }
}
```

Will return the following result.

```json
{
  "data": {
    "movie": {
      "id": 11,
      "name": "Inception"
    }
  }
}
```

If you need other fields or relations, just ask

```graphql
{
  movies {
    id
    name
    director {
      name
    }
    writers {
      name
    }
  }
}
```

Will return the following result.

```json
{
  "data": {
    "movies": [
      {
        "id": 11,
        "name": "Inception",
        "director": {
          "name": "Christopher Nolan"
        },
        "writers": [
          {
            "name": "Christopher Nolan"
          }
        ]
      },
      {
        "id": 12,
        "name": "Star Wars: Episode IV - A New Hope",
        "director": {
          "name": "George Lucas"
        },
        "writers": [
          {
            "name": "George Lucas"
          }
        ]
      }
    ]
  }
}
```

## Custom Controller / Manual Execution

You can execute GraphQL queries in your own controller or outside of ASP.NET. Below gives an example.

### Build the GraphQL schema

We can use the helper method `SchemaBuilder.FromObject<T>>()` to build the schema from the .NET object model we defined above. Create this once at startup and register it with your services.

```cs
var schema = SchemaBuilder.FromObject<DemoContext>();

services.AddSingleton<SchemaProvider<DemoContext>>();
```

_See the [Schema Creation](./schema-creation) section to learn more about `SchemaBuilder.FromObject<T>>()`_

### Executing a Query in a custom controller

Here is an example of a controller that receives a `QueryRequest` and executes the query. This logic could easily be applied to other web frameworks.

```cs
[Route("graphql")]
public class QueryController : Controller
{
    private readonly DemoContext _dbContext;
    private readonly SchemaProvider<DemoContext> _schemaProvider;

    public QueryController(DemoContext dbContext, SchemaProvider<DemoContext> schemaProvider)
    {
        this._dbContext = dbContext;
        this._schemaProvider = schemaProvider;
    }

    [HttpPost]
    public async Task<object> Post([FromBody]QueryRequest query)
    {
        var results = await _schemaProvider.ExecuteRequestWithContextAsync(query, _dbContext, HttpContext.RequestServices, null);
        // gql compile errors show up in results.Errors
        return results;
    }
}
```

### Executing a Query in azure functions (isolated)

Here is an example of a function that receives a `HttpRequestData`, from its Body property `QueryRequest` is deserialized and graphQL query is executed.

```cs
public class GraphQLFunctions
{
    private readonly DemoContext _dbContext;
    public GraphQLFunctions(DemoContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function(nameof(GraphQL))]
    public async Task<HttpResponseData> GraphQL([HttpTrigger(AuthorizationLevel.Function, "post", Route = "graphql")] HttpRequestData req)
    {
        var query = await req.GetJsonBody<QueryRequest>(); //helper method to deserialize QueryRequest from req.Body
        var schemaProvider = SchemaBuilder.FromObject<DemoContext>();
        var results = await schemaProvider.ExecuteRequestWithContextAsync(query, _dbContext, null, null);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(results);
        return response;
    }
}
```

## Configuring System.Text.Json

If you use your own controller/method to execute GraphQL and use `System.Text.Json`, it is best to configure it like below for best compatibility with other tools.

```cs
services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Use enum field names instead of numbers
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // EntityGraphQL internally builds types with fields
        opts.JsonSerializerOptions.IncludeFields = true;
        // The fields internally built already are named with fieldNamer (defaults to camelCase). This is
        // for the properties on QueryResult (Data, Errors) to match what most tools etc expect (camelCase)
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
```

## Deserialization of QueryRequest & QueryVariables

If you are using you're own controller/method to execute GraphQL and deserializing a GraphQL request like below into the `GraphQLRequest` object. You need to be aware of how your serializer handles nested `Dictionary<string, object>`.

_Sample incoming json request_

```json
{
  "query": "mutation Mutate($var: ComplexInputType){ doUpdate($var) }",
  "variables": {
    "var": {
      "name": "Lisa",
      "lastName": "Simpson"
    }
  }
}
```

Many deserializer libraries will deserialize this into the `QueryRequest.Variables` object with the value of `var` as a `JsonElement` (`System.Text.Json`) or a `JObject` (`Newtonsoft.Json`). e.g.

```cs
var gql = JsonSerializer.Deserialize<QueryRequest>(query);
Assert.True(gql.Variables["var"].GetType() == typeof(JsonElement));
```

What we want is a nested `Dictionary<string, object>`. `EntityGraphQL` handles `System.Text.Json`'s `JsonElement` itself. However if you are using `Newtonsoft.Json` or another library (that doesn't deserialize to nested dictionaries) you will have to provide a custom type converter.

See the serialization tests for [an example with Newtonsoft.Json](https://github.com/EntityGraphQL/EntityGraphQL/blob/master/src/tests/EntityGraphQL.Tests/SerializationTests.cs).

## Threading and async execution

`EntityGraphQL` executes each request (`schemaProvider.ExecuteRequest(...)`) in a single thread. First, `EntityGraphQL` compiles the whole GraphQL request document then selects the operation to execute. For a mutation operation all top-level mutations individually, in the order it appears in the document, as [required by GraphQL](https://graphql.org/learn/queries/#multiple-fields-in-mutations). For a query operation, `EntityGraphQL` starts each query in the order it appears in the document. Finally, it awaits all queries, the async portion of which is allowed to execute in parallel.

Since a GraphQL request is processed with a single thread, database contexts can be scoped services like they do for ordinary web services. Likewise, queries (and mutations) that call external web services can safely use the single-threaded `HttpContext` accessor to access `HttpContext.RequestAborted` to cancel the dependent request if the GraphQL request is aborted.

## Tracking Argument Values: IArgumentsTracker

EntityGraphQL provides a way to help you determine if an argument or input property was explicitly set by the user in a query or mutation, or if it is just the default .NET value. This is useful for distinguishing between "not provided" and "provided as null/default".

### IArgumentsTracker

If your argument or input class implements the `IArgumentsTracker` interface (or inherits from the provided `ArgumentsTracker` base class), you can check if a property was set by the user:

```csharp
public class MyInput : ArgumentsTracker {
    public string? Name { get; set; }
    public int? Age { get; set; }
}

// For a field
schema.Query().AddField("test", new MyInput(), (db, args) => db.People.WhereWhen(p => args.Name == p.Name, args.IsSet(nameof(MyInput.Name))), "test field");

// In your mutation or subscriptions
public string MyMutation(MyInput input) {
    if (input.IsSet(nameof(MyInput.Name))) {
        // Name was provided in the query
    }
    if (!input.IsSet(nameof(MyInput.Age))) {
        // Age was not provided
    }
    ...
}
```

This works for both inline arguments and variables.

For simple argument lists (e.g. method parameters), you can add an `IArgumentsTracker` parameter to your mutation or query method. This allows you to check if a specific argument was set:

```csharp
public string MyMutation(Guid? id, string? name, IArgumentsTracker argsTracker) {
    if (argsTracker.IsSet(nameof(id))) {
        // id was provided
    }
    if (!argsTracker.IsSet(nameof(name))) {
        // name was not provided
    }
    ...
}
```

This is especially useful for distinguishing between omitted arguments and those set to null/default.
