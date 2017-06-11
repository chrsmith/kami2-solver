module Kami2Solver.Program

open System.Diagnostics
open System.IO

open Kami2Solver.Types
open Kami2Solver.Analysis
open Kami2Solver.Export
open Kami2Solver.Solver


[<EntryPoint>]
let main argv =
    printfn "Kami2 Puzzle Solver"

    let puzzleImages = Directory.GetFiles("./puzzles/", "*.jpg")
    for puzzleImagePath in puzzleImages do
        if puzzleImagePath.Contains("1743") then
            printfn "Solving puzzle [%s]" (Path.GetFileName(puzzleImagePath))

            printfn "Analyzing..."
            let puzzle = Analysis.ExtractPuzzle puzzleImagePath

            printfn "Puzzle has %d colors and %d regions" puzzle.Colors.Length (Seq.length puzzle.Regions)
            (*
            for region in puzzle.Regions do
                printf "Region %d [%d, #%s] [%d triangles]-> " region.ID region.Color region.ColorCode region.Size
                for adjRegion in region.AdjacentRegions do
                    printf "%d " adjRegion
                printfn ""
            *)

            // Print the dot file graph version.
            let sourceImageDir = Path.GetDirectoryName(puzzleImagePath)
            let sourceImageName = Path.GetFileNameWithoutExtension(puzzleImagePath)
            let graphImagePath = Path.Combine(sourceImageDir, sourceImageName + ".graph.png")
            Export.RenderAsGraph
                puzzle.Regions
                graphImagePath

            // Solve it.
            printfn "Solving..."
            let solution = Solver.BruteForce puzzle 4
            printfn "Solution = %A" solution

        printfn "Done"

    0
