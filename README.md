# Giraffe.QueryReader [![Build Status](https://travis-ci.org/Zaid-Ajaj/Giraffe.QueryReader.svg?branch=master)](https://travis-ci.org/Zaid-Ajaj/Giraffe.QueryReader) [![Nuget](https://img.shields.io/nuget/v/Giraffe.QueryReader.svg?colorB=green)](https://www.nuget.org/packages/Giraffe.QueryReader)

HttpHandler for better working with query string paramters within [Giraffe](https://github.com/giraffe-fsharp/Giraffe) applications. Easily extract values from query string in a type-safe manner without defining intermediate types for model binding. The library can parse primitive values and can handle their optional counter parts.

# Install
```bash
# using nuget client
dotnet add package Giraffe.QueryReader
# using Paket
.paket/paket.exe add Giraffe.QueryReader --project path/to/Your.fsproj
```

The library code is actually only a single-file: `QueryReader.fs` so you can add it manually to your project and modify however you want. 

# Usage

Namespace `Giraffe.QueryReader` is opened in all examples below

`Query.read` is the only function is this library, it is overloaded so takes a number of strings as the first parameters being the names of the parameters in the query string (for now up to 5 parameters! need more? PRs are welcome) and the last parameter is a function that transforms the parsed query string parameter into another HttpHandler

### Basis use case: reading *required* parameter as string
```fs
GET 
  >=> route "/sayHello"
  >=> Query.read("to", sprintf "Hello %s" >> text)
```
Notice that because we used `%s` the type of the parameter was inferred to be `string`. This implies that `to` is a required query string parameter and if omitted, a bad request (400) response is returned to the client, i.e.
```bash
curl /sayHello?to=John 
OK (200) "Hello John"

curl /sayHello 
Bad Request (400) '{ "message": "Required query string parameter to was not found" }'
```

### Optional query string parameters
```fs
GET 
  >=> route "/greet"
  >=> Query.read("name", 
        function 
        | Some name -> text (sprintf "Hello %s" name)
        | None -> text "Hello World")
```
Since the input type now has been inferred to be `Option<string>` the query string parameter can be omitted:
```bash
curl /greet?name=John
OK (200) "Hello John"

curl /greet
OK (200) "Hello World"
```
### Use multiple query string parameters
```fs
GET 
  >=> route "/data"
  >=> Query.read("fromDate", "toDate", "format", 
        fun fromDate toDate format -> 
            let data = sprintf "Data from %s to %s as %s" fromDate toDate format
            text data)
```
Notice here we are not reading the values as `DateTime` but using `string` instead. This library doesn't try to do everything and for parsing complex structures like `DateTime` or similar, just read as string and parse/validate yourself. 

### Use *optional* boolean flags
Mix and match different parameters of different types
```fs
GET 
  >=> route "/ablums"
  >=> Query.read("search", "includeArtist", 
        fun search includeArtist -> 
            match includeArtist with
            | Some true -> text (sprintf "Search '%s' and include artist" search)
            | Some false -> text (sprintf "Search '%s' and do not include artist" search)
            | None -> text (sprintf "Just searching '%s', includeArtist omitted")
        )           
```
Here we are using multiple query string parameters: `search` and `includeArtist` where the latter is an optional boolean flag (implied by the `Option<bool>` type):
```bash
# implicit includeArtist=true because flag is present 
curl /albums?search=Metallica&includeArtist
OK (200) "Search 'Metallica' and include artist"

# explicit boolean value
curl /albums?search=Metallica&includeArtist=true
OK (200) "Search 'Metallica' and include artist"

curl /albums?search=Metallica&includeArtist=false
OK (200) "Search 'Metallica' and do not include artist"

curl /albums?search=Metallica
OK (200) "Just searching 'Metallica', includeArtist omitted"
```

### Parsing integers and floating numbers
```fs
GET 
  >=> route "/int-sum"
  >=> Query.read("x", "y", 
        fun x y -> 
            let sum = x + y
            let result = sprintf "x + y = %d" sum
            text result)
```
Here the parameters are inferred to be of type `int` because of using `+` and `%d`.
```bash
curl /int-sum?x=10&y=5 
OK (200) "x + y = 15"
```
if you want to use `float` then just give a hint at the compiler:
```fs
GET 
  >=> route "/number-sum"
  >=> Query.read("x", "y", 
        fun (x:float) y -> 
            let sum = x + y
            let result = sprintf "x + y = %.1f" sum
            text result)
``` 
then use 
```bash
curl /number-sum?x=1.5&y=2.5
OK (200) "x + y = 4.0"
```
Notice here that numbers are parsed with an `CultureInfo.InvariantCulture` format provider. If you want to use a different format provider, then read the query string parameter as string then parse yourself. 

### Supported types
 - `string` 
 - `int`
 - `bool`
 - `float`
 - `Option<string>`
 - `Option<int>`
 - `Option<bool>`
 - `Option<float>`

Using optional types implies that the query string parameter can be omitted, in which case, `None` will be passed to the function. 

## Builds

![Build History](https://buildstats.info/travisci/chart/Zaid-Ajaj/Giraffe.QueryReader)


### Building


Make sure the following **requirements** are installed in your system:

* [dotnet SDK](https://www.microsoft.com/net/download/core) 2.0 or higher
* [Mono](http://www.mono-project.com/) if you're on Linux or macOS.

```
> build.cmd // on windows
$ ./build.sh  // on unix
```

### Watch Tests

The `WatchTests` target will use [dotnet-watch](https://github.com/aspnet/Docs/blob/master/aspnetcore/tutorials/dotnet-watch.md) to watch for changes in your lib or tests and re-run your tests on all `TargetFrameworks`

```
./build.sh WatchTests
```