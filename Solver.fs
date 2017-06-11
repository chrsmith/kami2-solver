module PuzzleLoader.Solver

open System.Collections.Generic

open PuzzleLoader.Types


// Color a region in the given puzzle, returning the new puzzle state.
// This is done by essentially "deleting" any existing adjacent regions
// that hve the newColor as a color. And merge them into regionId
let colorRegion puzzle regionId newColor =
    let regionToColor = Map.find regionId puzzle.Regions

    // First, we create a new region and adjacent neighbor list.

    // Partition that region's neighbors into those that match the new color
    // and those that don't.
    let colorMatchingNeighbors, nonColorMatchingNeighbors =
        regionToColor.AdjacentRegions
        |> Set.partition (fun regionId ->
            let region = puzzle.Regions.[regionId]
            (region.Color = newColor))

    // The non-color matching neighbors are just fine as-is.
    //
    // As for the color matching neighbors, we need to union all of their adjacent
    // neighbors, as they are now neighbors of the larger, newly colored region.
    let newSetOfNeighbors =
        colorMatchingNeighbors
        |> Set.toSeq
        |> Seq.map (fun regionId -> puzzle.Regions.[regionId])
        |> Seq.map (fun region -> region.AdjacentRegions)
        |> Set.unionMany

    // Now we replace the target region in the puzzle.
    let newlyColoredRegion = {
        regionToColor with 
            Color = newColor
            AdjacentRegions =
                Set.union newSetOfNeighbors nonColorMatchingNeighbors
                // The region being colored is a neighbor of colorMatchingNeighbors,
                // but self-refential regions gum up the gears.
                |> Set.remove regionId
    }

    // Second, we remove all references to the subsumed regions. This is done
    // by updating AdjacentRegions sets for all regions. But as an optimization,
    // we only consider the regions for newSetOfNeighbors, since those are the
    // only regions that would have a reference to the subsumed regions.
    //
    // So we generate the puzzle by taking all regions, and partitioning to
    // those that are adjacent/are the newly colored region. Or are not.
    let adjacentToNewlyColoredRegion, notAdjacent =
        puzzle.Regions
        |> Map.toSeq |> Seq.map snd
        // Filter out the original node.
        |> Seq.filter (fun region -> region.ID <> regionId)
        // Filter out colorMatchingNeighbors (nodes being subsumed by coloring).
        |> Seq.filter (fun region -> not <| Set.contains region.ID colorMatchingNeighbors)
        // No seq.partition :'( Split on if they are in the newlyColored object's
        // neighbors.
        |> Seq.toArray
        |> Array.partition (fun region ->
            Set.contains region.ID newlyColoredRegion.AdjacentRegions)

    // Adjacent to the newly colored region need their adjancent IDs updated,
    // since the previously adjacent node might no longer exist. (Replace ID with
    // that of regionId.)
    let adjacentToNewlyColoredRegion =
        adjacentToNewlyColoredRegion
        |> Seq.map (fun region ->
            { region with
                AdjacentRegions =
                    Set.difference region.AdjacentRegions colorMatchingNeighbors
                    |> Set.add regionId
            })

    {
        Regions =
            seq {
                yield newlyColoredRegion
                yield! adjacentToNewlyColoredRegion
                yield! notAdjacent
            }
            |> Seq.map (fun region -> region.ID, region)
            |> Map.ofSeq
    }

// Brute force kami2 puzzle solving to a max depth.
let enumerateAllMoves (puzzleStep : Kami2PuzzleStep) =
    let colorsUsed = new HashSet<int>()
    do puzzleStep.Regions
       |> Map.toSeq
       |> Seq.map snd
       |> Seq.iter (fun region -> colorsUsed.Add(region.Color) |> ignore)

    let possibleMoves = 
        puzzleStep.Regions
        |> Map.toSeq
        |> Seq.map (fun (_,region) ->
            colorsUsed
            |> Seq.filter (fun colorId -> colorId <> region.Color)
            |> Seq.map (fun colorId -> (region.ID, colorId)))
        |> Seq.concat
    possibleMoves

let mutable bruteForceSteps = 0
let rec bruteForceStep (puzzleStep : Kami2PuzzleStep) movesList currentDepth maxDepth =
    // printfn ">> bruteForceStep %A %d/%d" movesList currentDepth maxDepth

    bruteForceSteps <- bruteForceSteps + 1
    if puzzleStep.IsSolved then
        // printfn "\tSolution Found!"
        Some(movesList)
    elif currentDepth >= maxDepth then
        // printfn "\tMax depth hit"
        None
    else
        let foundSolution =
            enumerateAllMoves puzzleStep
            |> Seq.map (fun (regionToColor, newColor) ->
                // printfn "Coloring region %d color %d" regionToColor newColor
                // printfn "-----\nBEFORE\n-----"
                // puzzleStep.DebugPrint()

                let updatedPuzzle = colorRegion puzzleStep regionToColor newColor
                // printfn "-----\nAFTER\n-----"
                // updatedPuzzle.DebugPrint()

                bruteForceStep updatedPuzzle ((regionToColor, newColor) :: movesList) (currentDepth + 1) maxDepth)
            |> Seq.tryFind (fun results -> Option.isSome results)
        match foundSolution with
        | Some(sln) ->
            // printfn "\tReturning solution"
            sln
        | None -> 
            // printfn "\tDone recursing"
            None
        

let BruteForce (kami2Puzzle : Kami2Puzzle) maxDepth =
    let convertedRegions =
        kami2Puzzle.Regions
        |> Seq.map (fun region -> region.Convert())

    let startingPuzzle : Kami2PuzzleStep = {
        Regions = convertedRegions
                  |> Seq.map (fun region -> region.ID, region)
                  |> Map.ofSeq
    }

    bruteForceSteps <- 0
    let result = bruteForceStep startingPuzzle [] 0 maxDepth
    printfn "brugeForceStepset = %d, Results = %A" bruteForceSteps result

    // Render each move as a graph for debugging purposes.