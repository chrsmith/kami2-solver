module Kami2Solver.Program

open System.Diagnostics
open System.IO

open Kami2Solver.Types
open Kami2Solver.Analysis
open Kami2Solver.Export
open Kami2Solver.Solver

// TODO(chrsmith): Review and apply F# style guidelines:
// http://fsharp.org/specs/component-design-guidelines/

[<EntryPoint>]
let main argv =
    printfn "Kami2 Puzzle Solver"

    // TODO(chrsmith): Wire in some command-line processing.
    // Perhaps transliterating Mono.Options.cs to F#.
    // https://twitter.com/migueldeicaza/status/877714014226202624
    let argPuzzlesDir = "./puzzles/"
    let argPuzzleName = Some("1733")
    let argPrintRegionInfo = false
    let argSaveMarkupImage = true
    let argSaveGraphImage = true

    let puzzleImages = Directory.GetFiles(argPuzzlesDir, "*.jpg")
    for puzzleImagePath in puzzleImages do
        let solvePuzzle = match argPuzzleName with
                          | None -> true
                          | Some(name) -> puzzleImagePath.Contains(name)
        if solvePuzzle then
            printfn "Solving puzzle [%s]" (Path.GetFileName(puzzleImagePath))

            printfn "Analyzing..."
            let puzzle = Analysis.ExtractPuzzle puzzleImagePath argSaveMarkupImage

            printfn "Puzzle has %d colors and %d regions" puzzle.Colors.Length (Seq.length puzzle.Regions)
            if argPrintRegionInfo then
                for region in puzzle.Regions do
                    printf "Region %d [%d, #%s] [%d triangles]-> " region.ID region.Color region.ColorCode region.Size
                    for adjRegion in region.AdjacentRegions do
                        printf "%d " adjRegion
                    printfn ""

            // Print the dot file graph version.
            if argSaveGraphImage then
                let sourceImageDir = Path.GetDirectoryName(puzzleImagePath)
                let sourceImageName = Path.GetFileNameWithoutExtension(puzzleImagePath)
                let graphImagePath = Path.Combine(sourceImageDir, sourceImageName + ".graph.png")
                Export.RenderAsGraph
                    puzzle.Regions
                    graphImagePath

            // Solve it.
            printfn "Solving..."
            let solution = Solver.BruteForce puzzle 10
            printfn "Solution = %A" solution

        printfn "Done"

    0
