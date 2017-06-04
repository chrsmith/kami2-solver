module PuzzleLoader.Program

open PuzzleLoader.Analysis

// F# black magic for invoking implicit conversions.
// https://stackoverflow.com/questions/10719770/is-there-anyway-to-use-c-sharp-implicit-operators-from-f
(*
// Use a SKRectI where an SKRect is required. In C# this would work via implicit
// conversion. In F# you need to cast this spell.
canvas.DrawRect(
        !> (new SKRectI(32, 32, 32 + 8, 32 + 8)),
        paint
    )
*)
let inline (!>) (x:^a) : ^b = ((^a or ^b) : (static member op_Implicit : ^a -> ^b) x) 


[<EntryPoint>]
let main argv =

    let filePath = "./test/puzzle.jpg"
    let puzzle = ExtractPuzzle filePath

    printfn "Puzzle has %d colors and %d regions" puzzle.NumColors puzzle.Regions.Count
    for region in puzzle.Regions do
        printf "Region %d [%d] -> " region.ID region.Color
        for adjRegion in region.AdjacentRegions do
            printf "%d " adjRegion
        printfn ""

    printfn "Done"

    0
