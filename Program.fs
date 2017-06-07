module PuzzleLoader.Program

open System.IO

open PuzzleLoader.Analysis
open PuzzleLoader.Solver


[<EntryPoint>]
let main argv =

    let puzzleImages = Directory.GetFiles("./puzzles/")
    for imageFilePath in puzzleImages do
        if not <| imageFilePath.Contains("analyzed") then 
            printfn "Extracing data from image %s..." imageFilePath
            let puzzle = ExtractPuzzle imageFilePath

            printfn "Puzzle has %d colors and %d regions" puzzle.NumColors puzzle.Regions.Count
            for region in puzzle.Regions do
                printf "Region %d [%d, #%s] [%d triangles]-> " region.ID region.Color region.ColorCode region.Size
                for adjRegion in region.AdjacentRegions do
                    printf "%d " adjRegion
                printfn ""

        printfn "Done"

    0
