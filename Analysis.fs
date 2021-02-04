﻿module Kami2Solver.Analysis

open System
open System.IO
open System.Collections.Generic

open SkiaSharp

open Kami2Solver.Types
open Kami2Solver.Export


let kBlack = new SKColor(0uy, 0uy, 0uy, 255uy)
let kMagenta = new SKColor(200uy, 0uy, 200uy, 255uy)
let kWhite = new SKColor(255uy, 255uy, 255uy, 255uy)


// Array of solid colors. Used to have a set of pre-defined
// colors for annotating distinct regions of the puzzle.
let solidColors = [|
    for r = 0 to 2 do
        for g = 0 to 2 do
            for b = 0 to 2 do
                let byteValue x = 127uy * (byte x)
                yield new SKColor(byteValue r, byteValue g, byteValue b, 255uy)
|]


// Values in pixels for the dimensions of things. These are hard-coded
// to the images I'm generating on my phone, which might vary between
// iPhone models. (Sourced from 1242 × 2208, but exported as JPG at 665x1182.)
let kImageWidth = 1080.0f
let kImageHeight = 2267.0f
// Height of the "footer" with the color palet, moves remaining, etc.
let kGameFooter = 132.0f
// Width of the color swatches widget in the bottom right.
let kColorPaletWidth = 400.0f

let kGridWidth = kImageWidth
let kGridHeight = kImageHeight - kGameFooter
let kColorPaletXOffset = kGridWidth - kColorPaletWidth

// Width/height of the triangle grid.
let kColWidth = kGridWidth / 10.0f
let kRowHeight = kGridHeight / 28.0f

// Similiarty between two colors to be considered a match.
// BUG? This seems unreasonablly high, but is necessary in practice.
let kColorMatchThreshold = 0.99

// Returns the approximate center of the triangle at the col and row.
// The result are pixels into the original image.
let getTrianglePoint col row =
    // First we map the col/row to a coordinate on the input image.
    // We shift along the X axis to account for the trangular grid.
    let colShift = if (col + row) % 2 = 0 then -kColWidth / 5.0f else kColWidth / 5.0f
    // Shift the first/last row to avoid reading pixels off the grid.
    let rowShift =
        if row = 0 then     6.0f
        elif row = 28 then -6.0f
        else                0.0f

    let x = (col |> float32) * kColWidth + kColWidth / 2.0f + colShift
    let y = (row |> float32) * kRowHeight + rowShift
    (x, y)


// Returns the approximate center of the i'th color swatch, if there are n swatches
// in total.
let getPuzzleColorPoint i numColors =
    let colorSwatchWidth = kColorPaletWidth / (float32 numColors)
    let x = kColorPaletXOffset + (float32 i) * colorSwatchWidth + colorSwatchWidth / 2.0f
    let y = kGridHeight + kGameFooter / 2.0f
    (x, y)


// Returns the col/row positions of adjacent triangles. If the game
// were a square grid, it would be col and col +/- 1. Instead, we
// need to do some work.
let getAdjacentTriangles col row =
    seq {
        // You always have a triangle above/below you, except on edges.
        if row > 0 then yield (col, row - 1)
        if row < 28 then yield (col, row + 1)
        // You only have one neighbor to the left or right, depending
        // on which row/col you are in.
        let a = row + col % 2
        if a % 2 = 0 && col > 0 then yield (col - 1, row)
        if a % 2 = 1 && col < 9 then yield (col + 1, row)
    }


// Computes the dot product of the two colors as if they were
// normalized vectors.
let dotProduct colorA colorB =
    let colorToNormVec (c : SKColor) =
        let valueArray =
            [| c.Red; c.Green; c.Blue; c.Alpha |]
            |> Array.map float
        let w, x, y, z = (valueArray.[0], valueArray.[1], valueArray.[2], valueArray.[3])
        let length = Math.Sqrt(w * w + x * x + y * y + z * z)
        Array.map (fun x -> (float x) / length) valueArray
    let vecA = colorToNormVec colorA
    let vecB = colorToNormVec colorB    
    Array.fold2 (fun acc x y -> acc + x * y) 0.0 vecA vecB


// Returns whether or not every color in the sequence is unique within a tolerance.
let allUnique (colors : SKColor[]) =
    if colors = null || colors.Length = 0 then failwith "???"
    let mutable allColorsUnique = true
    for i = 0 to colors.Length - 2 do
        for j = i + 1 to colors.Length - 1 do
            let similarity = dotProduct colors.[i] colors.[j]
            if similarity > kColorMatchThreshold then
                allColorsUnique <- false
    allColorsUnique


// Returns the average SKPixel value for those near the given point.
let getColorAverage (image : SKBitmap) x y =
    let x, y = (int x), (int y)
    let colorTotals = [| 0; 0; 0; 0 |]
    let addAtIdx i x = colorTotals.[i] <- colorTotals.[i] + (int x)
    let mutable samples = 0
    let kRange = 5
    for xOffset in [-kRange..kRange] do
        for yOffset in [-kRange..kRange] do
            let color = image.GetPixel(x + xOffset, y + yOffset)
            samples <- samples + 1
            addAtIdx 0 color.Red
            addAtIdx 1 color.Green
            addAtIdx 2 color.Blue
            addAtIdx 3 color.Alpha
    let averages = colorTotals |> Array.map (fun total -> total / samples) |> Array.map byte
    new SKColor(averages.[0], averages.[1], averages.[2], averages.[3])


let loadKamiPuzzleImage filePath =
    use fileStream = File.OpenRead(filePath)
    use skiaStream = new SKManagedStream(fileStream)
    SKBitmap.Decode(skiaStream)


// Analyze a screen shot of a Kami2 puzzle and convert it into a RawKami2Puzzle
// object. Also creates an annotated copy of the image for debugging purposes.
let AnalyzePuzzleImage (bitmap : SKBitmap) (debugImage : AnalysisDebugImage) =
    // Draw guiding lines for rows and columns.
    for col = 0 to 9 do
        let colf = col |> float32
        let colStartX = colf * kColWidth
        debugImage.DrawLine(colStartX, 0.0f,
                        colStartX, kGridHeight,
                        kBlack)

    for row = 0 to 28 do
        let rowf = row |> float32
        let rowStartY = rowf * kRowHeight
        debugImage.DrawLine(0.0f, rowStartY,
                        kGridWidth, rowStartY,
                        kBlack)

    // Determine the number of colors used by the puzzle. We assume there are 5, and
    // whittle that number down if we detect duplicates. Marking the regions we used
    // for determiniation along the way.
    let puzzleColors : SKColor[] =
        seq { 5 .. -1 .. 2 }
        |> Seq.map (fun numColors ->
            let getColorAtIdx idx =
                let (x, _) = getPuzzleColorPoint idx numColors
                // Adjust the X value so it isn't inbetween two different colors.
                let x = x - 10.0f  // BUG: Hard-coded relative to image size.
                // Adjust the Y value so that debugging annotations don't overlap.
                let y = kGridHeight + (kGameFooter / 4.0f) * (float32 (5 - numColors)) + 40.0f
                let color = getColorAverage bitmap x y
                debugImage.AddCircle(x, y, color)
                debugImage.AddCircleOutline(x, y, solidColors.[numColors])
                color
            Array.init numColors getColorAtIdx)
        |> Seq.tryFind allUnique
        |> (function
            | Some(uniqueColors) -> uniqueColors
            | None -> [| kMagenta (* couldn't determine colors *) |])

    // Get the inferred index of the color correspoding to the triangle at the
    // given column and row.
    let getTriangleColor col row =
        let x, y = getTrianglePoint col row
        let triangleColor = getColorAverage bitmap x y

        let (colorIdx, similarity) =
            puzzleColors
            |> Seq.mapi (fun idx puzzleColor -> (idx, dotProduct triangleColor puzzleColor))
            |> Seq.maxBy snd

        // If the triangle's color doesn't closely match a puzzle color, assume it is
        // fixed. (i.e. blank space in some puzzles.)
        if similarity > kColorMatchThreshold then
            debugImage.AddCircle(x, y, triangleColor)
            colorIdx
        else
            -1

    {
        NumColors = puzzleColors.Length
        PuzzleColors = puzzleColors
                       |> Array.map (fun c -> {Red = c.Red; Green = c.Green; Blue = c.Blue})
        Triangles = Array2D.init 10 29 getTriangleColor
    }


// Mutable variant of the Region type, which is exported publicly.
type RegionBuilder = {
    ID: int
    Color: int
    Position: int * int
    // Hex value of the RGB value of the color index.
    ColorCode: string
    // Number of triangles in the region.
    mutable Size: int
    // IDs of adjacent regions
    AdjacentRegions: HashSet<int>
} with
    member this.AddAdjacentRegion(adjacentRegion : RegionBuilder) =
        if adjacentRegion.ID <> this.ID then
            this.AdjacentRegions.Add(adjacentRegion.ID) |> ignore
            adjacentRegion.AdjacentRegions.Add(this.ID) |> ignore

    static member ConvertRegion(rb : RegionBuilder) =
        {
            Region.ID = rb.ID
            Color = rb.Color
            Position = rb.Position
            ColorCode = rb.ColorCode
            Size = rb.Size
            AdjacentRegions = Set.ofSeq rb.AdjacentRegions
        }
        

// Extract a Kami2Puzzle object from an in-game screenshot.
let ExtractPuzzle imageFilePath saveDebugImage =
    use bitmap = loadKamiPuzzleImage imageFilePath
    use debugImage = new AnalysisDebugImage(bitmap)

    // Get the colors of each triangle.
    let rawData = AnalyzePuzzleImage bitmap debugImage

    // Convert individual triangles into "regions". This is done by:
    // 1.) Build a parallel array mapping each triangle to its Region.
    //     Nulled out to start.
    // 2.) Pick the first triangle that doesn't have a region associated
    //     with it. Create a new region.
    // 3.) For all adjacent triangles, if it is the same color, add it to
    //     the curent region. If it is different, ignore. But, if that
    //     ignored triangle has a region associated with it, update both
    //     region's adjacency lists.
    // 4.) Go back to #2 until all triangles have a region.
    let knownRegions = new List<RegionBuilder>()
    let triangleRegions : RegionBuilder option[,] = Array2D.zeroCreate 10 29

    let rec floodFillRegion col row (region : RegionBuilder) =
        match triangleRegions.[col, row] with
        // If the triangle's neighbor is already known, mark as neighbor and stop.
        | Some(adjacentRegion) ->
            region.AddAdjacentRegion(adjacentRegion)
        // If the adjacent triangle does not have a region, then we merge it
        // into the region being flood filled if it has the same color,
        // otherwise we ignore it. (And will get to it later.)
        | None ->
            let triangleColor = rawData.Triangles.[col, row]
            if triangleColor = region.Color then
                triangleRegions.[col, row] <- Some(region)
                region.Size <- region.Size + 1

                // Mark it on the debug image.
                let x, y = getTrianglePoint col row
                let regionColor = solidColors.[region.ID % solidColors.Length]
                debugImage.AddCircleOutline(x, y, regionColor)

                // Recurse
                getAdjacentTriangles col row
                |> Seq.iter (fun (col', row') -> floodFillRegion col' row' region)
            else
                // Ignore the diff colored neighbor, as it will be a part
                // of a different region constructed later.
                ()

    // Check each triangle and ensure it is apart of a region.
    for col = 0 to 9 do
        for row = 0 to 28 do
            let triangleColorIdx = rawData.Triangles.[col, row]
            match triangleRegions.[col, row] with
            // Ignore triangles already associated with a region or
            // not part of the puzzle.
            | Some(_) -> ()
            | _ when triangleColorIdx = -1 -> ()
            | None ->
                let triangleColor = rawData.PuzzleColors.[triangleColorIdx]
                let newRegion = {
                    RegionBuilder.ID = knownRegions.Count
                    RegionBuilder.Color = triangleColorIdx
                    RegionBuilder.Position = (col, row)
                    RegionBuilder.ColorCode = sprintf "#%02x%02x%02x" triangleColor.Red triangleColor.Green triangleColor.Blue
                    RegionBuilder.Size = 0  // Updated in flood fill.
                    RegionBuilder.AdjacentRegions = new HashSet<int>()
                }
                knownRegions.Add(newRegion)
                floodFillRegion col row newRegion

    // Label region IDs and color indices last, as to not get overwritten.
    knownRegions
    |> Seq.iter (fun region ->
        let (x,y) = getTrianglePoint (fst region.Position) (snd region.Position)
        debugImage.AddText(region.ID.ToString(), x, y, kMagenta))

    for i = 0 to rawData.NumColors - 1 do
        let (x, y) = getPuzzleColorPoint i rawData.NumColors
        debugImage.AddText(i.ToString(), x, y, kMagenta)

    if saveDebugImage then
        let sourceImageDir = Path.GetDirectoryName(imageFilePath)
        let sourceImageName = Path.GetFileNameWithoutExtension(imageFilePath)
        debugImage.Save(Path.Combine(sourceImageDir, sourceImageName + ".analyzed.png"))

    {
        Colors = rawData.PuzzleColors
        Regions = knownRegions |> Seq.map RegionBuilder.ConvertRegion
    }
