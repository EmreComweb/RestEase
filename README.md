RestEase
========

RestEase is a little type-safe REST API client library, which aims to make interacting with remote REST endpoints easy, without adding unnecessary compexity.

Almost every aspect of RestEase can be overridden and customized, leading to a large level of flexibility.

To use it, you define an interface which represents the endpoint you wish to communicate with (more on that in a bit), where methods on that interface correspond to requests that can be made on it.
RestEase will then generate an implementation of that interface for you, and by calling the methods you defined, the appropriate requests will be made.

RestEase is heavily inspired by [Paul Betts' Refit](https://github.com/paulcbetts/refit), which in turn is inspired by Retrofit.


Installation
------------

[RestEase is available on NuGet](https://www.nuget.org/packages/RestEase).

Either open the package console and type:

```
PM> Install-Package RestEase
```

Or right-click your project -> Manage NuGet Packages... -> Online -> search for RestEase in the top right.

I also publish symbols on [SymbolSource](http://www.symbolsource.org/Public), so you can use the NuGet package but still have access to Stylet's source when debugging. If you haven't yet set up Visual Studio to use SymbolSource, do that now:

In Visual Studio, go to Debug -> Options and Settings, and make the following changes:

 1. In General
   1. Turn **off** "Enable Just My Code"
   2. Turn **off** "Enable .NET Framework source stepping". Yes, it is misleading, but if you don't, then Visual Studio will ignore your custom server order and only use its own servers.
   3. Turn **on** "Enable source server support". You may have to OK a security warning.
 2. In Symbols
   1. Add "http://srv.symbolsource.org/pdb/Public" to the list. 


Quick Start
-----------

To start, first create an interface which represents the endpoint you wish to make requests to.
Please note that it does have to be public, or you must add RestEase as a friend assembly, see [Interface Accessibility below](#redefining-headers).

```csharp
// Define an interface representing the API
public interface IGithubApi
{
    // All interface methods must return a Task or Task<T>. We'll discuss what sort of T in more detail below.

    // The [Get] attribute marks this method as a GET request
    // The "users" is a relative path the a base URL, which we'll provide later
    [Get("users")]
    Task<List<User>> GetUsersAsync();
}

// Create an implementation of that interface
// We'll pass in the base URL for the API
IGithubApi api = RestClient.For<IGithubApi>("http://api.github.com");

// Now we can simply call methods on it
// Sets a GET request to http://api.github.com/users
List<User> users = await api.GetUsersAsync();
```


Request Types
-------------

See the `[Get("path")]` attribute used above?
That's how you mark that method as being a GET request.
There are a number of other attributes you can use here - in fact, there's one for each type of request: `[Post("path")]`, `[Delete("path")]`, etc.
Use whichever one you need to.

The argument to `[Get]` (or `[Post]`, or whatever) is typically a relative path, and will be relative to the base uri that you provide to `RestClient.For<T>`.
(You *can* specify an absolute path here if you need to, in which case the base uri will be ignored).


Return Types
------------

Your interface methods may return one of the following types:

 - `Task`: This method does not return any data, but the task will complete when the request has completed
 - `Task<T>` (where `T` is not one of the types listed below): This method will deserialize the response into an object of type `T`, using Json.NET (or a custom deserializer, see [Controlling Serialization and Deserialization below](#controlling-serialization-and-deserialization)).
 - `Task<string>`: This method returns the raw response, as a string
 - `Task<HttpResponseMessage>`: This method returns the raw `HttpResponseMessage` resulting from the request. It does not do any deserialiation
 - `Task<Response<T>>`: This method returns a `Response<T>`. A `Response<T>` contains both the deserialied response (of type `T`), but also the `HttpResponseMessage`. Use this when you want to have both the deserialized response, and access to things like the response headers

Non-async methods are not supported (use `.Wait()` or `.Result` as appropriate if you do want to make your request synchronous).


Query Parameters
----------------

It is very common to want to include query parameters in your request (e.g. `/foo?key=value`), and RestEase makes this easy.
Any parameters to a method which are:

 - Decorated with the `[Query]` attribute, or
 - Not decorated at all

will be interpreted as query parameters.

The name of the parameter will be used as the key, unless an argument is passed to `[Query("key")]`, in which case that will be used instead.

For example:

```csharp
public interface IGithubApi
{
	[Get("user")]
    Task<User> FetchUserAsync(int userid);

    // Is the same as

    [Get("user")]
    Task<User> FetchUserAsync([Query] int userid);

    // Is the same as

    [Get("user")]
    Task<User> FetchUserAsync([Query("userid")] int userId);
}

IGithubApi api = RestClient.For<IGithubApi>("http://api.github.com");

// Requests http://api.github.com/user?userId=3
await api.FetchUserAsync(3);
```

Constant query parameters can just be specified in the path:

```csharp
public interface ISomeApi
{
    [Get("users?userid=3")]
    Task<User> GetUserIdThreeAsync();
}
```

You can have duplicate keys if you want:

```csharp
public interface ISomeApi
{
    [Get("search")]
    Task<SearchResult> SearchAsync([Query("filter")] string filter1, [Query("filter")] string filter2);
}

ISomeApi api = RestClient.For<ISomeApi>("http://someendpoint.com");

// Requests http://somenedpoint.com/search?filter=foo&filter=bar
await api.SearchAsync("foo", "bar");
```

You can also have an array of query parameters:

```csharp
public interface ISomeApi
{
	// You can use IEnumerable<T>, or any type which implements IEnumerable<T>

    [Get("search")]
    Task<SearchResult> SearchAsync([Query("filter")] IEnumerable<string> filters);
}

ISomeApi api = RestClient.For<ISomeApi>("http://someendpoint.com");

// Requests http://somenedpint.com/search?filter=foo&filter=bar&filter=baz
await api.SearchAsync(new[] { "foo", "bar", "baz" });
```


Path Parameters
---------------

Sometimes you also want to be able to control some parts of the path itself, rather than just the query parameters.
This is done using placeholders in the path, and corresponding method parameters decorated with `[Path]`.

For example:

```csharp
public interface ISomeApi
{
    [Get("user/{userId}")]
    Task<User> FetchUserAsync([Path] string userId);
}

ISomeApi api = RestClient.For<ISomeApi>("http://example.com");

// Requests http://example.com/user/fred
await api.FetchUserAsync("fred");
```

As with `[Query]`, the name of the placeholder to substitute is determined by the name of the parameter.
If you want to override this, you can pass an argument to `[Query("placeholder")]`, e.g.:

```csharp
public interface ISomeApi
{
    [Get("user/{userId}")]
    Task<User> FetchUserAsync([Path("userId")] string idOfTheUser);
}
```

Every placeholder must have a corresponding parameter, and every parameter must relate to a placeholder.


Body Content
------------

If you're sending a request with a body, you can specify that one of the parameters to your method contains the body you want to send, using the `[Body]` attribute.

```csharp
public interface ISomeApi
{
    [Post("/users/new")]
    Task CreateUserAsync([Body] User user);
}
```

Exactly how this will be serialized depends on the type of parameters:

 - If the type is `Stream`, then the content will be streamed via [`StreamContent`](https://msdn.microsoft.com/en-us/library/system.net.http.streamcontent%28v=vs.118%29.aspx).
 - If the type is `String`, then the string will be used directly as the content (using [`StringContent`](https://msdn.microsoft.com/en-us/library/system.net.http.stringcontent%28v=vs.118%29.aspx)).
 - If the parameter has the attribute `[Body(BodySerializationMethod.UrlEncoded)]`, then the content will be URL-encoded (see below).
 - Otherwise, the parameter will be serialized as JSON (by default, or you can customize this if you want, see TODO REFERENCE).


### URL Encoded bodies

For APIs which take form posts (i.e. serialized as `application/x-www-form-urlencoded`), initialize the `[Body]` attribute with `BodySerializationMethod.UrlEncoded`.
This parameter must implement `IDictionary`.

If any of the values in the `IDictionary` implement `IEnumerable`, then they will be serilaized as an array of values.

For example:

```csharp
public interface IMeasurementProtocolApi
{
    [Post("/collect")]
    Task CollectAsync([Body(BodySerializationMethod.UrlEncoded)] Dictionary<string, object> data);
}

var data = new Dictionary<string, object> {
    {"v", 1}, 
    {"tids", new[] { "UA-1234-5", "UA-1234-6" }, 
    {"cid", new Guid("d1e9ea6b-2e8b-4699-93e0-0bcbd26c206c")}, 
    {"t", "event"},
};

// Serialized as: v=1&tids=UA-1234-5&tids=UA-1234-6&cid=d1e9ea6b-2e8b-4699-93e0-0bcbd26c206c&t=event
await api.CollectAsync(data);
 ```


Cancelling Requests
-------------------

If you want to be able to cancel a request, pass a `CancellationToken` as one of the method paramters.

```csharp
public interface ISomeApi
{
    [Get("very-large-response")]
    Task<LargeResponse> GetVeryLargeResponseAsync(CancellationToken cancellationToken);
}
```


Controlling Serialization and Deserialization
---------------------------------------------

By default, RestEase will use [Json.NET](http://www.newtonsoft.com/json) to deserialize responses, and serialize request bodies.
However, you can change this, either by specifying custom `JsonSerializerSettings`, or by providing your own Deserializer and Serializer.

### Custom `JsonSerializerSettings`

If you want to specify your own `JsonSerializerSettings`, you can do this using the appropriate `RestClient.For<T>` overload, for example:

```csharp
var settings = new JsonSerializerSettings()
{
    ContractResolver = new CamelCasePropertyNamesContractResolver(),
    Converters = {new StringEnumConverter()}
};
var api = RestClient.For<ISomeApi>("http://somebaseaddress.com", settings);
```

If you want to completely customize how responses / requests are deserialized / serialized, then you can provide your own implementations of [`IResponseDeserializer`](https://github.com/canton7/RestEase/blob/master/src/RestEase/IResponseDeserializer.cs) or [`IRequestBodySerializer`](https://github.com/canton7/RestEase/blob/master/src/RestEase/IRequestBodySerializer.cs) respectively.

For example:

```csharp
// This API returns XML

public class XmlResponseDeserializer : IResponseDeserializer
{
    public async Task<T> ReadAndDeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // Consider caching generated serializers
        var serializer = new XmlSerializer(typeof(T));

        var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return (T)serializer.Deserialize(contentStream);
    }
}

// You can define either IResponseDeserializer, or IRequestBodySerializer, or both
// I'm going to do both as an example

public class XmlRequestBodySerializer : IRequestBodySerializer
{
    public string SerializeBody<T>(T body)
    {
        // Consider caching generated serializers
        var serializer = new XmlSerializer(typeof(T));

        using (var stringWriter = new StringWriter())
        {
            serializer.Serialize(stringWriter, body);
            return stringWriter.ToString();
        }
    }
}

// ...

var api = RestClient.For<ISomeApi>("http://somebaseaddress.com", new XmlResponseDeserializer(), new XmlRequestBodySerializer());
```


Headers
-------

Specifying headers is actually a surprisingly large topic, and is broken down into several levels: interface, method, and parameter.

 - Interface headers are constant, and apply to all method in that interface.
 - Method headers are constant, and apply to just that method, and override the interface headers.
 - Parameter headers are dynamic: that is, you can specify their value per-request. They apply to a single method, and override the method headers.

### Static Headers

You can set one or more static request headers for a request by applying a `[Headers]` attribute to the method:

```csharp
[Header("User-Agent: RestEase")]
public interface IGitHubApi
{
   [Get("/users/{user}")]
   Task<User> GetUserAsync([Path] string user);
}
```

Likewise, you can also apply these to all methods, by defining them on the interface itself:

```csharp
[Header("User-Agent: RestEase")]
public interface IGitHubApi
{
   [Get("/users/{user}")]
   Task<User> GetUserAsync([Path] string user);

   [Post("/users/new")]
   Task CreateUserAsync[Body] User user);
}
```

### Dynamic Headers

If you need to, you can also have dynamic headers, but specifying the `[Header]` attribute on a method parameter:

```csharp
public interface IGitHubApi
{
   [Get("/users/{user}")]
   Task<User> GetUserAsync([Path] string user, [Header("Authorization")] string authorization);
}

IGitHubApi api = RestClient.For<IGitHubApi>("http://api.github.com");

// Has the header 'Authorization: token OAUTH-TOKEN'
var user = await api.GetUserAsync("octocat", "token OAUTH-TOKEN");
```

If you've got a header which needs to be specified when the API is created, but also need to be specified for all methods, you can use the `RestClient.For<T>` overload which takes a HttpClient, and use its `DefaultRequestHeaders` property:

```csharp
public interface IGitHubApi
{
   [Get("/users/{user}")]
   Task<User> GetUserAsync([Path] string user);
}

var httpClient = new HttpClient();
httpClient.aseAddress = new Uri("http://api.github.com"),
httpClient.DefaultRequestHeaders.Add("Authorization", "token OAUTH-TOKEN");

IGitHubApi api = RestClient.For<IGitHubApi>(httpClient);
var user = await api.GetUserAsync("octocat");
```

Alternatively, there's a `RestClient.For<T>` overload which lets you specify a delegate which will modify every outgoing request:

```csharp
public interface IGitHubApi
{
   [Get("/users/{user}")]
   Task<User> GetUserAsync([Path] string user);
}

IGitHubApi api = RestClient.For<IGitHubApi>("http://api.github.com", (request, cancellationToken) =>
   {
      request.Headers.Add("Authorization", "token OAUTH-TOKEN");
      return Task.FromResult(0); // Return a completed task
   });
var user = await api.GetUserAsync("octocat");
```

This technique lets you do other fancy stuff per-request, as well.
For example, if you need to refresh an oAuth access token occasionally (using the [ADAL](https://msdn.microsoft.com/en-us/library/azure/jj573266.aspx) library as an example):

```csharp
public interface IMyRestService
{
    [Get("/getPublicInfo")]
    Task<Foobar> SomePublicMethodAsync();

    [Get("/secretStuff")]
    [Headers("Authorization: Bearer")]
    Task<Location> GetLocationOfRebelBaseAsync();
}

AuthenticationContext context = new AuthenticationContext(...);
IGitHubApi api = RestClient.For<IGitHubApi>("http://api.github.com", async (request, cancellationToken) =>
   {
      // See if the request has an authorize header
      var auth = request.Headers.Authorization;
      if (auth != null)
      {
          // The AquireTokenAsync call will prompt with a UI if necessary
          // Or otherwise silently use a refresh token to return a valid access token 
          var token = await context.AcquireTokenAsync("http://my.service.uri/app", "clientId", new Uri("callback://complete")).ConfigureAwait(false);
          request.Headers.Authorization = new AuthenticationHeaderValue(auth.Scheme, token);
      }
   });

```

### Redefining Headers

You've probably noticed that you can specify the same header in multiple places: on the interface, on the method, and as a parameter.

Redefining a header will replace it, in the following order of precidence:

 - Attributes on the interface *(lowest priority)*
 - Attributes on the method
 - Attributes on method parameters *(highest priority)*

```csharp
[Headers("X-Emoji: :rocket:")]
public interface IGitHubApi
{
    [Get("/users/list")]
    Task<List> GetUsersAsync();

    [Get("/users/{user}")]
    [Headers("X-Emoji: :smile_cat:")]
    Task<User> GetUserAsync(string user);

    [Post("/users/new")]
    [Headers("X-Emoji: :metal:")]
    Task CreateUserAsync([Body] User user, [Header("X-Emoji")] string emoji);
}

// X-Emoji: :rocket:
var users = await GetUsersAsync();

// X-Emoji: :smile_cat:
var user = await GetUserAsync("octocat");

// X-Emoji: :trollface:
await CreateUserAsync(user, ":trollface:"); 
```

### Removing Headers

As with defining headers, headers can be removed entirely by `[Header]` declarations on something with higher priority.
For interface and method headers, define the header without a value (and without the colon between the key and the value).
For parameter headers, pass `null` as the header's value.

For example:

```csharp
[Headers("X-Emoji: :rocket:")]
public interface IGitHubApi
{
    [Get("/users/list")]
    [Headers("X-Emoji")] // Remove the X-Emoji header
    Task<List> GetUsersAsync();

    [Get("/users/{user}")]
    [Headers("X-Emoji:")] // Redefine the X-Emoji header as empty
    Task<User> GetUserAsync(string user);

    [Post("/users/new")]
    Task CreateUserAsync([Body] User user, [Header("X-Emoji")] string emoji);
}

// No X-Emoji header
var users = await GetUsersAsync();

// X-Emoji: 
var user = await GetUserAsync("octocat");

// No X-Emoji header
await CreateUserAsync(user, null); 

// X-Emoji: 
await CreateUserAsync(user, ""); 
```


Customizing RestEase
--------------------

RestEase has been written in a way which makes it very easy to customize exactly how it works.
In order to describe this, I'm first going to have to outline its architecture.

Given an API like:

```csharp
public interface ISomeApi
{
    [Get("users/{userId}")]
    Task GetUserAsync([Path] string userId);
}
```

Calling `RestClient.For<ISomeApi>(...)` will cause a class like this to be generated:

```csharp
namespace RestEase.AutoGenerated
{
    public class ISomeApi
    {
        private readonly IRequester requester;

        public ISomeApi(IRequester requester)
        {
            this.requester = requester;
        }

        public Task GetUserAsync(string userId)
        {
            var requestInfo = new RequestInfo(HttpMethod.Get, "users/{userId}");
            requestInfo.AddPatheter<string>("userId", userId);
            return this.requester.RequestVoidAsync(requestInfo);
        }
    }
}
```

Now, you cannot customize what this generated class looks like, but you can see it doesn't actually do very much: it just builds up a `RequestInfo` object, then sends it off to the `IRequester` (which does all of the hard work).
What you *can* do however is to provide your own [`IRequester`](https://github.com/canton7/RestEase/blob/master/src/RestEase/IRequester.cs) implementation, and pass that to an appropriate overload of `RestClient.For<T>`.
In fact, the default implementation of `IRequester`, [`Requester`](https://github.com/canton7/RestEase/blob/master/src/RestEase/Implementation/Requester.cs), has been carefully written so that it's easy to extend: each little bit of functionality is broken out into its own virtual method, so it's easy to replace just the behaviour you need.

Have a read through [`Requester`](https://github.com/canton7/RestEase/blob/master/src/RestEase/Implementation/Requester.cs), figure out what you want to change, subclass it, and provide an instance of that subclass to `RestClient.For<T>`. 


Interface Accessibility
-----------------------

Since RestEase generates an interface implementation in a separate assembly, the interface ideally needs to be public.

If you don't want to do this, you'll need to mark RestEase as being a 'friend' assembly, which allows RestEase to see your internal types.
Add the following line to your `AssemblyInfo.cs`:

```
[assembly: InternalsVisibleTo(RestClient.FactoryAssemblyName)]
```


Using Generic Interfaces
------------------------

When using something like ASP.NET Web API, it's a fairly common pattern to have a whole stack of CRUD REST services. RestEase supports these, allowing you to define a single API interface with a generic type:

```csharp
public interface IReallyExcitingCrudApi<T, in TKey> where T : class
{
    [Post("")]
    Task<T> Create([Body] T paylod);

    [Get("")]
    Task<List<T>> ReadAll();

    [Get("/{key}")]
    Task<T> ReadOne(TKey key);

    [Put("/{key}")]
    Task Update(TKey key, [Body]T payload);

    [Delete("/{key}")]
    Task Delete(TKey key);
}
```

Which can be used like this:

// The "/users" part here is kind of important if you want it to work for more 
// than one type (unless you have a different domain for each type)
var api = RestClient.For<IReallyExcitingCrudApi<User, string>>("http://api.example.com/users"); 


Comparison to Refit
-------------------

RestEase is very heavily inspired by [Paul Betts' Refit](https://github.com/paulcbetts/refit#form-posts).
Refit is a fantastic library, and in my opinion does a lot of things very right.
It was the first C# REST client library that I actually enjoyed using.

I write RestEase for two reasons: 1) there were a couple of things about Refit which I didn't like, and 2) I thought it would be fun.

Here's a brief summary of pros/cons, compared to Refit:

### Pros

 - No autogenerated `RefitStubs.cs`
 - Supports `CancellationToken`s for Task-based methods
 - Supports method overloading
 - Possible to avoid `ApiExceptions` if the API is expected to return a non-success status code
 - Easier to customize:
   - Can specify custom response deserializer
   - Can specify custom request body serializer
   - Can customize almost every aspect of setting up and creating the request (through implementing `IRequester`)

## Cons

 - Interfaces need to be public, or you need to add `[assembly: InternalsVisibleTo(RestClient.FactoryAssemblyName)]` to your `AssemblyInfo.cs`
 - No `IObservable` support
 - Slightly more work done at runtime (but not very much more)
