module Kami2Solver.Program

open System
open System.Diagnostics
open System.IO
open System.Threading
open System.Threading.Tasks

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
    let argPuzzleName = None  // e.g. Some("1733")
    let argPrintRegionInfo = false
    let argSaveMarkupImage = false
    let argSaveGraphImage = false

    let puzzleImages = Directory.GetFiles(argPuzzlesDir, "*.jpg")
    for puzzleImagePath in puzzleImages do
        let solvePuzzle = match argPuzzleName with
                          | None -> true
                          | Some(name) -> puzzleImagePath.Contains(name)
        if solvePuzzle then
            printf "Solving puzzle [%s]...\t" (Path.GetFileName(puzzleImagePath))

            let puzzle = Analysis.ExtractPuzzle puzzleImagePath argSaveMarkupImage

            if argPrintRegionInfo then
                printfn "Puzzle has %d colors and %d regions" puzzle.Colors.Length (Seq.length puzzle.Regions)
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

            // Solve it. Put execution in a task so we can provide a timeout.
            let mutable executionResult = ""
            let stopwatch = Stopwatch.StartNew()

            let cts = new CancellationTokenSource()
            let solution = lazy (Solver.BruteForce puzzle 10 cts.Token)

            let solverTask = Task.Run((fun () -> solution.Force()), cts.Token)
            if not <| solverTask.Wait(TimeSpan.FromSeconds(10.0)) then
                cts.Cancel()

            let solution = solution.Force()  // wart
            let timeResult =
                if cts.Token.IsCancellationRequested then
                    // Take up as much space as TimeSpan.ToString
                    // TODO(chrsmith): Format specifier in printfn?
                    "Timeout          "
                else
                    stopwatch.Elapsed.ToString()

            printfn "%s\tEvaluated %d nodes" timeResult solution.NodesEvaluated
            if Option.isSome solution.Moves then
                printfn "\tSolution %O" <| Option.get solution.Moves

    0
