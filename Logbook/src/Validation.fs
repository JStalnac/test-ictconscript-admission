module Logbook.Validation

type ValidationResult<'Error> =
    | Valid
    | Invalid of 'Error list

let (<&>) a b =
    match a, b with
    | Valid, Valid -> Valid
    | Valid, Invalid errs -> Invalid errs
    | Invalid errs, Valid -> Invalid errs
    | Invalid errs1, Invalid errs2 -> Invalid (errs1 @ errs2)

[<RequireQualifiedAccess>]
module ValidationResult =
    let mapResult f mapError res =
        match res with
        | Ok v -> f v
        | Error err -> Invalid [ mapError err ]

    let notNone f err opt =
        match opt with
        | Some v -> f v
        | None -> Invalid [ err ]

    let ofBool err =
        function
        | true -> Valid
        | false -> Invalid [ err ]

