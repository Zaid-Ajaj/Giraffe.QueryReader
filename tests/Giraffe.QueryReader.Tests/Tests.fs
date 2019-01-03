module Tests

open Expecto
open Giraffe
open Giraffe.QueryReader
open System
open System.IO
open System.Linq
open System.Net.Http
open System.Collections.Generic
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open System.Net

let testWebApp : HttpHandler =
  choose [
    GET >=> route "/" >=> text "Index"

    // required parameter as string
    route "/sayHello" >=> Query.read("to", sprintf "Hello %s" >> text)

    // optional parameter as string
    route "/greet" >=> Query.read("name",
      function
      | Some name -> text (sprintf "Hello %s" name)
      | None -> text "Hello World")

    // required query parameters as integers
    route "/sum" >=> Query.read("a", "b",
      fun a b ->
        let sum = a + b
        text (sprintf "a + b = %d" sum))

    // optional flags
    route "/logical" >=> Query.read("value",
      function
      | Some true -> text "value is true"
      | Some false -> text "value is false"
      | None -> text "value is omitted")

    // required boolean flags
    route "/required-boolean" >=> Query.read("value",
      function
      | true -> text "true"
      | false -> text "false")

    // max and match optional numbers
    route "/numbers" >=> Query.read("x", "y",
      fun (x: float) (y: Option<float>) ->
        let sum = x + defaultArg y 0.0
        text (sprintf "x + y = %.1f" sum))

    route "/int64" >=> Query.read("x", "y",
        fun (x : int64)  (y : int64)  ->
            let sum = x + y
            text (sprintf "x + y = %d" sum))

    route "/int64Optional" >=> Query.read("x", "y",
        fun (x : int64)  (y : Option<int64>)  ->
            let sum = x + defaultArg y 0L
            text (sprintf "x + y = %d" sum))

    route "/byId" >=> Query.read<Guid>("id",
        fun guid ->
            text (guid.ToString("n"))
    )
    
    route "/byIdOptional" >=> Query.read<Option<Guid>>("id",
        function
        | Some guid ->
            text (guid.ToString("n"))
        | None ->
            text (Guid.Empty.ToString("n"))
    )

    setStatusCode 404 >=> text "Not Found"
  ]

let pass() = Expect.isTrue true "Passed"
let fail() = Expect.isTrue false "Failed"

let rnd = System.Random()

let appBuilder (app: IApplicationBuilder) =
  app.UseGiraffe testWebApp

let configureServices (services: IServiceCollection) =
  services.AddGiraffe()
  |> ignore

let createHost() =
    WebHostBuilder()
        .UseContentRoot(Directory.GetCurrentDirectory())
        .Configure(Action<IApplicationBuilder> appBuilder)
        .ConfigureServices(Action<IServiceCollection> configureServices)

let runTask task =
    task
    |> Async.AwaitTask
    |> Async.RunSynchronously

let httpGet (path : string) (client : HttpClient) =
    path
    |> client.GetAsync
    |> runTask

let isStatus (code : HttpStatusCode) (response : HttpResponseMessage) =
    Expect.equal response.StatusCode code "Status code is wrong"
    response

let ensureSuccess (response : HttpResponseMessage) =
    if not response.IsSuccessStatusCode
    then response.Content.ReadAsStringAsync() |> runTask |> failwithf "%A"
    else response

let readText (response : HttpResponseMessage) =
    response.Content.ReadAsStringAsync()
    |> runTask

let readTextEqual content (response : HttpResponseMessage) =
    response.Content.ReadAsStringAsync()
    |> runTask
    |> fun result -> Expect.equal result content "The expected and actual response content are not equal"

[<Tests>]
let tests =
  testList "Giraffe.QueryReader" [

    testCase "Root path / returns 'Index' as text" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()

      client
      |> httpGet "/"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "Index"

    testCase "Unknown path returns status 404 not found " <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()

      client
      |> httpGet "/non-existent-path"
      |> isStatus HttpStatusCode.NotFound
      |> readTextEqual "Not Found"

    testCase "Basic use-case: required parameter as string" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()

      client
      |> httpGet "/sayHello?to=Zaid"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "Hello Zaid"

      client
      |> httpGet "/sayHello"
      |> isStatus HttpStatusCode.BadRequest
      |> ignore

    testCase "Basic use case: optional query parameter as string" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()

      client
      |> httpGet "/greet"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "Hello World"

      client
      |> httpGet "/greet?name=Zaid"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "Hello Zaid"

    testCase "Required int query parameters can be parsed" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()

      client
      |> httpGet "/sum?a=10&b=5"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "a + b = 15"

    testCase "Required negative int query parameters can be parsed" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()

      client
      |> httpGet "/sum?a=-10&b=-5"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "a + b = -15"

    testCase "Omitting one required int query parameters will shortcircuit as bad request" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()

      client
      |> httpGet "/sum?b=5"
      |> isStatus HttpStatusCode.BadRequest
      |> ignore

    testCase "Required boolean parameters cannot be omitted: otherwise returns bad request" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()

      client
      |> httpGet "/required-boolean"
      |> isStatus HttpStatusCode.BadRequest
      |> ignore

    testCase "Required boolean parameters cannot be omitted: returns OK when value exists" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()

      client
      |> httpGet "/required-boolean?value"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "true"

      client
      |> httpGet "/required-boolean?value=true"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "true"

      client
      |> httpGet "/required-boolean?value=false"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "false"

    testCase "Optional numbers" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()

      client
      |> httpGet "/numbers?x=1.5&y=2.5"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "x + y = 4.0"

      client
      |> httpGet "/numbers?x=1.5"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "x + y = 1.5"

    testCase "Optional boolean flags" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()

      client
      |> httpGet "/logical"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "value is omitted"

      client
      |> httpGet "/logical?value"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "value is true"

      client
      |> httpGet "/logical?value=true" // explicit
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "value is true"

      client
      |> httpGet "/logical?value=false"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "value is false"

    testCase "Guid requires value; bad request when no value exists" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()
      let guid = Guid.NewGuid()

      client
      |> httpGet "/byId"
      |> isStatus HttpStatusCode.BadRequest
      |> ignore

      client
      |> httpGet "/byId?id"
      |> isStatus HttpStatusCode.BadRequest
      |> ignore


    testCase "Guid requires value; Ok when exists" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()
      let guid = Guid.NewGuid()

      client
      |> httpGet (sprintf "/byId?id=%s" (guid.ToString()))
      |> isStatus HttpStatusCode.OK
      |> readTextEqual (guid.ToString("n"))

    testCase "Optional Guid" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()
      let guid = Guid.NewGuid()

      client
      |> httpGet (sprintf "/byIdOptional?id=%s" (guid.ToString()))
      |> isStatus HttpStatusCode.OK
      |> readTextEqual (guid.ToString("n"))

      client
      |> httpGet  "/byIdOptional"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual (Guid.Empty.ToString("n"))
      client
      |> httpGet  "/byIdOptional?id"
      |> isStatus HttpStatusCode.OK
      |> readTextEqual (Guid.Empty.ToString("n"))

    testCase "Int64 requires value; Ok when exists" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()
      let x = 2L
      let y = 3L

      client
      |> httpGet (sprintf "/int64?x=%d&y=%d" x y)
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "x + y = 5"

    testCase "Int64 requires value; bad when no value exists" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()
      let x = 2L
      let y = 3L

      client
      |> httpGet (sprintf "/int64" )
      |> isStatus HttpStatusCode.BadRequest
      |> ignore
      client
      |> httpGet (sprintf "/int64?x=%d" x)
      |> isStatus HttpStatusCode.BadRequest
      |> ignore
      client
      |> httpGet (sprintf "/int64?y=%d" y)
      |> isStatus HttpStatusCode.BadRequest
      |> ignore

    testCase "Optional Int64" <| fun _ ->
      use server = new TestServer(createHost())
      use client = server.CreateClient()
      let x = 2L
      let y = 3L
      client
      |> httpGet (sprintf "/int64Optional?x=%d" x)
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "x + y = 2"
      client
      |> httpGet (sprintf "/int64Optional?x=%d&y=%d" x y)
      |> isStatus HttpStatusCode.OK
      |> readTextEqual "x + y = 5"
  ]
