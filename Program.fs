module PuzzleLoader.Program

open System.Diagnostics
open System.IO

open PuzzleLoader.Analysis
open PuzzleLoader.Solver


[<EntryPoint>]
let main argv =

    let puzzleImages = Directory.GetFiles("./puzzles/")
    for imageFilePath in puzzleImages do
        // Sample puzzle images are jpegs, everything else is a png.
        if imageFilePath.Contains(".jpg") && imageFilePath.Contains("IMG_1743") then 
            printfn "Extracing data from image %s..." imageFilePath
            let puzzle = ExtractPuzzle imageFilePath

            printfn "Puzzle has %d colors and %d regions" puzzle.NumColors puzzle.Regions.Count
            (*
            for region in puzzle.Regions do
                printf "Region %d [%d, #%s] [%d triangles]-> " region.ID region.Color region.ColorCode region.Size
                for adjRegion in region.AdjacentRegions do
                    printf "%d " adjRegion
                printfn ""
            *)

            // Print the dot file graph version.
            let sourceImageDir = Path.GetDirectoryName(imageFilePath)
            let sourceImageName = Path.GetFileNameWithoutExtension(imageFilePath)
            let dotFilePath = Path.Combine(sourceImageDir, sourceImageName + ".graph.dot")
            let dotFileLines = Solver.ConvertToGraph puzzle
            File.WriteAllLines(dotFilePath, dotFileLines)

            // Render the graphs. Obviously assumes `dot` is on the current path.
            let dotFileImagePath = Path.Combine(sourceImageDir, sourceImageName + ".graph.png")
            let args = sprintf "-Tpng %s -o %s"
                                dotFilePath
                                dotFileImagePath
            Process.Start("dot", args) |> ignore

            // Solve it.
            printfn "Solving..."
            Solver.BruteForce puzzle 4

        printfn "Done"

    0
