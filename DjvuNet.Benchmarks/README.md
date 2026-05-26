# DjvuNet Benchmarks

This project contains performance microbenchmarks for the `DjvuNet` library, utilizing `BenchmarkDotNet`.

## Benchmark Artifacts

Test images and data files used by the benchmarks are stored in the root `/artifacts` directory. 

### Raw Binary Artifacts (`.bin`)

Certain microbenchmarks (specifically those testing color space conversions like `YCbCr2RgbUnified`) require highly specific memory layouts to test SIMD vectorization and padding correctness. 

Standard image formats like TIFF or JPEG either compress the data, use chroma subsampling (4:2:0 instead of 4:4:4), or trigger automatic OS-level color conversions (e.g., GDI+ converting YCbCr back to RGB upon load). To avoid introducing third-party imaging libraries solely for benchmarking, we use raw `.bin` files for these specific scenarios.

**File Naming Convention:**
To avoid hardcoding dimensions in the benchmark code, binary artifacts encode their geometry in the filename:
`<Name>-<Width>x<Height>-<Format>.bin`

Example: `TitanIR-5447x3686-24bpp-YCbCr.bin`
- **5447**: Width in pixels.
- **3686**: Height in pixels.
- **24bpp-YCbCr**: The data format. 24bpp indicates 3 bytes per pixel. In this specific format, the bytes are interleaved natively for DjvuNet's `Pixel` struct (where Blue=Y, Green=Cb, Red=Cr). 
- **Stride / Padding**: The benchmark code automatically calculates the required 4-byte aligned GDI+ stride based on the width and 24bpp format. The binary file contains the exact padded rows.

To generate or regenerate these binary artifacts from source images, see the scripts in `eng/scripts/` (e.g., `Generate-YCbCrArtifact.ps1`).