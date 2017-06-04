module PuzzleLoader.Analysis

open System
open System.IO
open System.Collections.Generic

open SkiaSharp


let kBlack = new SKColor(0uy, 0uy, 0uy, 255uy)
let kMagenta = new SKColor(200uy, 0uy, 200uy, 255uy)
let kWhite = new SKColor(255uy, 255uy, 255uy, 255uy)


// Values in pixels for the dimensions of things. These are hard-coded
// to the images I'm generating on my phone, which might vary between
// iPhone models. (Sourced from 1242 × 2208.)
let kImageWidth = 1242.0f
let kImageHeight = 2208.0f

let kGameFooter = 206.0f  // Size in px of the game's footer
let kGridWidth = kImageWidth
let kGridHeight = kImageHeight - kGameFooter

// Dimensions of the color swatches in the bottom right.
let kColorPaletWidth = 745.0f
let kColorPaletXOffset = kGridWidth - kColorPaletWidth

// Width/height of the triangle grid.
let kColWidth = kGridWidth / 10.0f
let kRowHeight = kGridHeight / 28.0f

// Size of annotations in the debug image.
let kAnnotationSize = 22.0f


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


// Kami2 puzzle image with annotations to aid in debugging.
type AnalysisDebugImage(originalImage : SKBitmap) =
    // Surface for the debugging annotations.
    let surface = SKSurface.Create(originalImage.Width,
                                   originalImage.Height,
                                   SKImageInfo.PlatformColorType,
                                   SKAlphaType.Premul)
    let canvas = surface.Canvas
    let paint = new SKPaint(TextSize = 80.0f,
                            IsAntialias = true,
                            Color = kBlack,
                            StrokeWidth = 5.0f)

    let init() =
        // Draw the original puzzle image.
        canvas.DrawColor(SKColors.White)
        canvas.DrawBitmap(originalImage, 0.0f, 0.0f, paint)

        // Draw a watermark, outlined.
        paint.Color <- kMagenta
        canvas.DrawText("Kami2 Solver", 8.0f, kGridHeight + 128.0f, paint)

        paint.IsStroke <- true
        paint.Color <- kWhite
        canvas.DrawText("Kami2 Solver", 8.0f, kGridHeight + 128.0f, paint)
        paint.IsStroke <- false
    do init()

    member this.DrawLine(x0, y0, x1, y1, color) =
        paint.Color <- color
        canvas.DrawLine(x0, y0, x1, y1, paint)

    member this.AddCircle(x, y, color) =
        paint.IsStroke <- false
        paint.Color <- color
        canvas.DrawCircle(
            x, y, kAnnotationSize,
            paint
        )

    member this.AddCircleOutline(x, y, color) =
        paint.IsStroke <- true
        paint.Color <- color
        canvas.DrawCircle(
            x, y, kAnnotationSize,
            paint
        )

    // Save the image to disk in the PNG format.
    member this.Save(filePath) =
        let finalImage = surface.Snapshot()
        use pngEncodedFile = finalImage.Encode();
        File.WriteAllBytes(filePath, pngEncodedFile.ToArray())

    // Clean up SKia objects.
    interface IDisposable with
        member __.Dispose() =
            paint.Dispose()
            canvas.Dispose()
            surface.Dispose()


// Raw puzzle extracted from a screenshot.
type RawKami2Puzzle = {
    // Number of colors used in the puzzle.
    NumColors: int
    // Index of the color of each individual triagle. See other comments for
    // translating coordinates to triangles, and adjacency rules.
    Triangles: int[,]
}


// Analyze a screen shot of a Kami2 puzzle and convert it into a RawKami2Puzzle
// object. Also creates an annotated copy of the image for debugging purposes.
let AnalyzePuzzleImage filePath =
    // Load the puzzle image.
    use bitmap = loadKamiPuzzleImage filePath
   
    use debugImage = new AnalysisDebugImage(bitmap)

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

    // Get the puzzle's colors and mark them on the analyzed image.
    let puzzleColors : SKColor[] =
        // TODO(chrsmith): Do some better logic here. Maybe start with 10
        // colors and whittle them down till all are unique?
        let puzzleColorsUsed = 5
        let colorSwatchWidth = kColorPaletWidth / (float32 puzzleColorsUsed)

        // Get the average SKColor representing the ith puzzle color.
        let getPuzzleColor i =
            let x = kColorPaletXOffset + (float32 i) * colorSwatchWidth + colorSwatchWidth / 2.0f
            let y = kGridHeight + kGameFooter / 2.0f
            let color = getColorAverage bitmap x y
            debugImage.AddCircle(x, y, color)
            debugImage.AddCircleOutline(x, y, kMagenta)
            color

        Array.init puzzleColorsUsed getPuzzleColor

    // Get the inferred index of the color correspoding to the triangle at the
    // given column and row.
    let getTriangleColor col row =
        let x, y = getTrianglePoint col row
        let triangleColor = getColorAverage bitmap x y
        debugImage.AddCircle(x, y, triangleColor)

        puzzleColors
        |> Seq.mapi (fun idx puzzleColor -> (idx, dotProduct triangleColor puzzleColor))
        |> Seq.maxBy snd
        |> fst

    let puzzleInfo = {
        NumColors = puzzleColors.Length
        Triangles = Array2D.init 10 29 getTriangleColor
    }

    debugImage.Save("demo.png")
    puzzleInfo
