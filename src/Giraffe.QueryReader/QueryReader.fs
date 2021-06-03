namespace Giraffe.QueryReader

open System
open Giraffe
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks
open System.Threading.Tasks
open System.Globalization

[<AutoOpen>]
module internal Extensions =

    let internal context (contextMap : HttpContext -> HttpHandler) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext)  ->
            let createdHandler = contextMap ctx
            createdHandler next ctx

    let internal request (requestMap : HttpRequest -> HttpHandler) : HttpHandler =
        context (fun ctx -> requestMap ctx.Request)

    let internal isOption (typeInfo: Type) = typeInfo.FullName.StartsWith("Microsoft.FSharp.Core.FSharpOption`1")

    let internal extractValue (name: string) (values: IQueryCollection) (typeInfo: Type) : Result<obj, string> =
        if not (values.ContainsKey(name)) && isOption typeInfo
        then Ok (box None)
        elif not (values.ContainsKey(name)) && not (isOption typeInfo)
        then Error (sprintf "Required query string parameter '%s' was not found" name)
        else
            let value = values.[name]
            match typeInfo.FullName, value.Count with
            | "System.Boolean", 0 -> Ok (box true)
            | "System.Boolean", n when String.IsNullOrWhiteSpace(value.[0]) -> Ok (box true)
            | "System.Boolean", n ->
                match value.[0] with
                | "true" -> Ok (box true)
                | "false" -> Ok (box false)
                | _ -> Error (sprintf "Value '%s' of query string parameter '%s' was not valid, boolean required" value.[0] name)
            | "System.String", 0 ->
                Error (sprintf "Required value for query parameter '%s' was not found" name)
            | "System.String", n when String.IsNullOrWhiteSpace(value.[0])  ->
                Error (sprintf "Required value for query parameter '%s' was not found" name)
            | "System.String", n ->
                Ok (box value.[0])
            | "System.Int32", 0 ->
                Error (sprintf "Required value query string parameter '%s' was not found" name)
            | "System.Int32", n when String.IsNullOrWhiteSpace(value.[0])  ->
                Error (sprintf "Required value query string parameter '%s' was not found" name)
            | "System.Int32", n ->
                match Int32.TryParse(value.[0]) with
                | true, intValue -> Ok (box intValue)
                | false, _ -> Error (sprintf "Value of query string parameter '%s' was not a valid integer" name)
            | "System.Int64", 0 ->
                Error (sprintf "Required value query string parameter '%s' was not found" name)
            | "System.Int64", n when String.IsNullOrWhiteSpace(value.[0])  ->
                Error (sprintf "Required value query string parameter '%s' was not found" name)
            | "System.Int64", n ->
                match Int64.TryParse(value.[0]) with
                | true, intValue -> Ok (box intValue)
                | false, _ -> Error (sprintf "Value of query string parameter '%s' was not a valid integer" name)
            | "System.Double", 0 ->
                Error (sprintf "Required value query string parameter '%s' was not found" name)
            | "System.Double", n when String.IsNullOrWhiteSpace(value.[0]) ->
                Error (sprintf "Required value query string parameter '%s' was not found" name)
            | "System.Double", n ->
                match System.Double.TryParse(value.[0], NumberStyles.Number, CultureInfo.InvariantCulture) with
                | true, floatValue -> Ok (box floatValue)
                | false, _ -> Error (sprintf "Value of query string parameter '%s' was not a valid number" name)
            | "System.Guid", 0 ->
                Error (sprintf "Required value query string parameter '%s' was not found" name)
            | "System.Guid", n when String.IsNullOrWhiteSpace(value.[0]) ->
                Error (sprintf "Required value query string parameter '%s' was not found" name)
            | "System.Guid", n ->
                match Guid.TryParse(value.[0]) with
                | true, guidValue -> Ok (box guidValue)
                | false, _ -> Error (sprintf "Value of query string parameter '%s' was not a valid guid" name)
            | otherwise when isOption typeInfo ->
                let optionalType = typeInfo.GetGenericArguments().[0]
                match optionalType.FullName, value.Count with
                | "System.Boolean", 0 -> Ok (box (Some true))
                | "System.Boolean", n when String.IsNullOrWhiteSpace(value.[0]) -> Ok (box (Some true))
                | "System.Boolean", n ->
                    match value.[0] with
                    | "true" -> Ok (box (Some true))
                    | "false" -> Ok (box (Some false))
                    | _ -> Error (sprintf "Value '%s' of query string parameter '%s' was not valid, boolean required" value.[0] name)
                | "System.String", 0 -> Ok (box None)
                | "System.String", n when String.IsNullOrWhiteSpace(value.[0]) -> Ok (box None)
                | "System.String", n -> Ok (box (Some value.[0]))
                | "System.Int32", 0 -> Ok (box None)
                | "System.Int32", n when String.IsNullOrWhiteSpace(value.[0]) -> Ok (box None)
                | "System.Int32", n ->
                    match Int32.TryParse(value.[0]) with
                    | true, intValue -> Ok (box (Some intValue))
                    | false, _ -> Error (sprintf "Value of query string parameter '%s' was not a valid integer" name)
                | "System.Int64", 0 -> Ok (box None)
                | "System.Int64", n when String.IsNullOrWhiteSpace(value.[0]) -> Ok (box None)
                | "System.Int64", n ->
                    match Int64.TryParse(value.[0]) with
                    | true, intValue -> Ok (box (Some intValue))
                    | false, _ -> Error (sprintf "Value of query string parameter '%s' was not a valid integer" name)
                | "System.Double", 0 -> Ok (box None)
                | "System.Double", n when String.IsNullOrWhiteSpace(value.[0]) -> Ok (box None)
                | "System.Double", n ->
                    match System.Double.TryParse(value.[0], NumberStyles.Number, CultureInfo.InvariantCulture) with
                    | true, floatValue -> Ok (box (Some floatValue))
                    | false, _ -> Error (sprintf "Value of query string parameter '%s' was not a valid number" name)
                | "System.Guid", 0 -> Ok (box None)
                | "System.Guid", n when String.IsNullOrWhiteSpace(value.[0]) -> Ok (box None)
                | "System.Guid", n ->
                    match System.Guid.TryParse(value.[0]) with
                    | true, guidValue -> Ok (box (Some guidValue))
                    | false, _ -> Error (sprintf "Value of query string parameter '%s' was not a valid guid" name)

                | typeName, _ -> Error (sprintf "Could not convert value of '%s' to type '%s'" name typeName)
            | typeName, _ -> Error (sprintf "Could not convert value of '%s' to type '%s'" name typeName)

    let badRequest (msg: string) =
        setStatusCode 400
        >=> json (dict [ "message", msg ])

type Query() =
    static member read<'t>(name: string, map: 't -> HttpHandler) =
        request <| fun req ->
            let typeInfo = typeof<'t>
            let query = req.Query
            match extractValue name query typeInfo with
            | Ok value -> map (unbox<'t> value)
            | Error errorMsg -> badRequest errorMsg

    static member read<'t, 'u>(firstName: string, secondName: string, map: 't -> 'u -> HttpHandler) : HttpHandler =
        request <| fun req ->
            let firstTypeInfo = typeof<'t>
            let query = req.Query
            match extractValue firstName query firstTypeInfo with
            | Error errorMsg -> badRequest errorMsg
            | Ok firstValue ->
                let secondTypeInfo = typeof<'u>
                match extractValue secondName query secondTypeInfo with
                | Error errorMsg -> badRequest errorMsg
                | Ok secondValue -> map (unbox<'t> firstValue) (unbox<'u> secondValue)

    static member read<'t, 'u, 'v>(firstName: string, secondName: string, thirdName: string, map: 't -> 'u -> 'v -> HttpHandler) : HttpHandler =
        request <| fun req ->
            let firstTypeInfo = typeof<'t>
            let query = req.Query
            match extractValue firstName query firstTypeInfo with
            | Error errorMsg -> badRequest errorMsg
            | Ok firstValue ->
                let secondTypeInfo = typeof<'u>
                match extractValue secondName query secondTypeInfo with
                | Error errorMsg -> badRequest errorMsg
                | Ok secondValue ->
                    let thirdTypeInfo = typeof<'v>
                    match extractValue thirdName query thirdTypeInfo with
                    | Error errorMsg -> badRequest errorMsg
                    | Ok thirdValue -> map (unbox<'t> firstValue) (unbox<'u> secondValue) (unbox<'v> thirdValue)

    static member read<'t, 'u, 'v, 'w>(firstName: string, secondName: string, thirdName: string, forthName: string, map: 't -> 'u -> 'v -> 'w -> HttpHandler) : HttpHandler =
        request <| fun req ->
            let firstTypeInfo = typeof<'t>
            let query = req.Query
            match extractValue firstName query firstTypeInfo with
            | Error errorMsg -> badRequest errorMsg
            | Ok firstValue ->
                let secondTypeInfo = typeof<'u>
                match extractValue secondName query secondTypeInfo with
                | Error errorMsg -> badRequest errorMsg
                | Ok secondValue ->
                    let thirdTypeInfo = typeof<'v>
                    match extractValue thirdName query thirdTypeInfo with
                    | Error errorMsg -> badRequest errorMsg
                    | Ok thirdValue ->
                        let forthTypeInfo = typeof<'w>
                        match extractValue forthName query forthTypeInfo with
                        | Error errorMsg -> badRequest errorMsg
                        | Ok forthValue ->
                            map (unbox<'t> firstValue) (unbox<'u> secondValue) (unbox<'v> thirdValue) (unbox<'w> forthValue)

    static member read<'t, 'u, 'v, 'w, 'z>(firstName: string, secondName: string, thirdName: string, forthName: string, fifthName: string, map: 't -> 'u -> 'v -> 'w -> 'z -> HttpHandler) : HttpHandler =
        request <| fun req ->
            let firstTypeInfo = typeof<'t>
            let query = req.Query
            match extractValue firstName query firstTypeInfo with
            | Error errorMsg -> badRequest errorMsg
            | Ok firstValue ->
                let secondTypeInfo = typeof<'u>
                match extractValue secondName query secondTypeInfo with
                | Error errorMsg -> badRequest errorMsg
                | Ok secondValue ->
                    let thirdTypeInfo = typeof<'v>
                    match extractValue thirdName query thirdTypeInfo with
                    | Error errorMsg -> badRequest errorMsg
                    | Ok thirdValue ->
                        let forthTypeInfo = typeof<'w>
                        match extractValue forthName query forthTypeInfo with
                        | Error errorMsg -> badRequest errorMsg
                        | Ok forthValue ->
                            let fifthTypeInfo = typeof<'z>
                            match extractValue fifthName query fifthTypeInfo with
                            | Error errorMsg -> badRequest errorMsg
                            | Ok fifthValue ->
                                map (unbox<'t> firstValue) (unbox<'u> secondValue) (unbox<'v> thirdValue) (unbox<'w> forthValue) (unbox<'z> fifthValue)

    static member read<'t, 'u, 'v, 'w, 'z, 'q>(firstName: string, secondName: string, thirdName: string, forthName: string, fifthName: string, sixthName: string, map: 't -> 'u -> 'v -> 'w -> 'z -> 'q -> HttpHandler) : HttpHandler =
        request <| fun req ->
            let firstTypeInfo = typeof<'t>
            let query = req.Query
            match extractValue firstName query firstTypeInfo with
            | Error errorMsg -> badRequest errorMsg
            | Ok firstValue ->
                let secondTypeInfo = typeof<'u>
                match extractValue secondName query secondTypeInfo with
                | Error errorMsg -> badRequest errorMsg
                | Ok secondValue ->
                    let thirdTypeInfo = typeof<'v>
                    match extractValue thirdName query thirdTypeInfo with
                    | Error errorMsg -> badRequest errorMsg
                    | Ok thirdValue ->
                        let forthTypeInfo = typeof<'w>
                        match extractValue forthName query forthTypeInfo with
                        | Error errorMsg -> badRequest errorMsg
                        | Ok forthValue ->
                            let fifthTypeInfo = typeof<'z>
                            match extractValue fifthName query fifthTypeInfo with
                            | Error errorMsg -> badRequest errorMsg
                            | Ok fifthValue ->
                                let sixthTypeInfo = typeof<'q>
                                match extractValue sixthName query fifthTypeInfo with
                                | Error errorMsg -> badRequest errorMsg
                                | Ok sixthValue ->
                                    map (unbox<'t> firstValue)
                                        (unbox<'u> secondValue)
                                        (unbox<'v> thirdValue)
                                        (unbox<'w> forthValue)
                                        (unbox<'z> fifthValue)
                                        (unbox<'q> sixthValue)
