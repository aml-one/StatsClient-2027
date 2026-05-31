# 3Shape DCM parsing and STL export (StatsClient)

This document describes how **StatsClient** reads 3Shape **DCM** (Dental Case Model) files and how to export the resulting meshes as **STL**. The implementation lives in the **DCM Viewer** module (`StatsClient/DcmViewer/`).

**Primary types**

| Type | File | Role |
|------|------|------|
| `DcmParser` | `Services/DcmParser.cs` | Decode HPS geometry, optional CE decryption, scene transforms |
| `ParsedMeshData` | `Services/DcmParser.cs` | Parse result: `MeshSnapshot`, bounds, properties |
| `MeshSnapshot` | `Services/MeshSnapshot.cs` | Vertex array + triangle index array |
| `MeshExportService` | `Services/MeshExportService.cs` | Write binary STL (and ASCII PLY) |

---

## 1. What is a DCM file?

A **DCM** file is a 3Shape case artifact. In practice it is usually:

1. An **XML document** (often with a `.dcm` extension) rooted at `<HPS>`, or  
2. The same XML inside a **ZIP** container (some tools unzip first; **StatsClient reads the file as XML directly** via `XmlReader` / `XDocument`).

The mesh is not stored as STL inside the file. It is stored in the **HIMSA Packed Scan (HPS)** section:

```xml
<HPS version="1.0">
  <Packed_geometry>
    <Schema>CE</Schema>
    <Properties>
      <Property name="EKID" value="1"/>
      <Property name="SourceApp" value="..."/>
    </Properties>
    <Binary_data>
      <CE version="1.0">
        <Vertices vertex_count="133744" check_value="..." base64_encoded_bytes="...">
          <!-- base64 binary: float32 xyz × vertex_count, optionally encrypted -->
        </Vertices>
        <Facets facet_count="131902" base64_encoded_bytes="...">
          <!-- base64 binary: compressed triangle index stream -->
        </Facets>
      </CE>
    </Binary_data>
  </Packed_geometry>
  <Annotations>
    <Annotation type="CoordinateTransform">
      <String name="TransformID" value="Focus2FinalTrans"/>
      <Matrix4x4 name="TransformMatrix"
        m00="..." m01="..." ... m03="..."
        m10="..." ... m23="..."
        m30="0" m31="0" m32="0" m33="1"/>
    </Annotation>
  </Annotations>
</HPS>
```

**Schemas** (value of `<Schema>`):

| Schema | Meaning in StatsClient |
|--------|-------------------------|
| **CA** / **CC** | Uncompressed vertices; facets compressed |
| **CE** | Encrypted vertices (Blowfish); same facet encoding after decrypt |

Metadata (`EKID`, `PackageLockList`, `IntegrityCheck`, `SourceApp`, etc.) is collected from `<Property name="..." value="..."/>` nodes and drives decryption and fallbacks.

---

## 2. End-to-end parse pipeline

`DcmParser.ParseFile` is the single entry point:

```csharp
var parser = new DcmParser();
ParsedMeshData result = parser.ParseFile(
    filePath,
    mode: CoordinateDecodingMode.Auto,
    allowThreeShapeFallback: true,
    applySceneTransform: true,
    sceneTransformKind: SceneTransformKind.Scan);
```

### 2.1 High-level flow

```
ParseFile(path)
  │
  ├─ .stl extension? ──► ParseStlFile (pass-through)
  │
  ├─ ReadMetadata (Schema + Properties)
  │
  ├─ Streaming XmlReader: <Vertices> + <Facets> pairs
  │     ├─ Base64 or plain-text decode
  │     ├─ CE: DecryptCeVertices (Blowfish)
  │     ├─ DecodeVertices (float absolute/delta, checksum)
  │     └─ DecodeFacets (compressed fan, multi-candidate scoring)
  │
  ├─ Fallback: DOM walk (TryPopulateGeometryFromDocument)
  ├─ Fallback: embedded HPS payloads inside XML
  ├─ Optional: ThreeShapeNativeMeshLoader → temp STL → re-parse
  ├─ Optional: ThreeShapeDecryptor (installed 3Shape app) → decrypted DCM → re-parse
  │
  ├─ ApplyThreeShapeSceneTransforms (best-fit CoordinateTransform)
  ├─ DcmParserSanitizer (connectivity cleanup)
  │
  └─ MeshSnapshot.FromLists → ParsedMeshData
```

### 2.2 Vertex decoding (`DecodeVertices`)

1. **Input**: raw bytes from `<Vertices>` (base64-decoded or plain floats).
2. **CE path**: if `Schema == CE` or `PackageLockList` is present (and bytes are not plain-text), `DecryptCeVertices` runs:
   - Blowfish key from built-in CE key, `PackageLockList` (MD5-derived), subscription passwords from environment, etc.
   - Block endianness swap (`preSwap` / `postSwap`) aligned with 3Shape / reference decoders.
   - **Adler32**-style `check_value` on the attribute when present.
3. **Coordinate mode** (`CoordinateDecodingMode`):
   - **Absolute**: each 12-byte triplet is `float x, y, z`.
   - **Delta**: cumulative sum of deltas (common in packed scans).
   - **Auto**: pick the candidate set with better plausibility score.
4. **Validation**: vertex count coverage, plausible coordinate range; optional lenient path for high coverage on locked (EKID) files.

### 2.3 Facet decoding (`DecodeFacets`)

Facets use a **compressed opcode stream** (per-triangle flags + delta-encoded indices). The parser tries several interpretations (32-bit vs variable-length, nibble high-bit, initial vertex pointer) and scores each candidate by:

- Expected triangle count vs `facet_count`
- Geometry penalty (long edges, needle triangles vs vertex bounds)

The best candidate becomes a list of `(v0, v1, v2)` indices into the vertex list.

### 2.4 Embedded HPS documents

Some DCMs embed additional `<HPS>...</HPS>` XML inside properties or binary payloads. `LoadDocumentHierarchy`:

1. Loads the root `XDocument`.
2. BFS-extracts embedded HPS strings (`ExtractEmbeddedHpsPayloads`).
3. Parses each unique payload as another document.

Transforms and comments from **all** documents in the hierarchy are considered during scene alignment.

### 2.5 Scene transforms (`CoordinateTransform`)

After geometry is decoded, vertices are moved into **case / arch coordinates** (so prep, antagonist, and CAD line up in the viewer).

**Current behavior (aligned with legacy DCMViewer):**

- `ApplyThreeShapeSceneTransforms` calls **`TryApplyBestCoordinateTransform`**.
- For each `CoordinateTransform` in the document hierarchy:
  - Score **forward** and **inverse** application by how well the transformed mesh bounding box fits **Comment `<Origin>`** marker points.
- Apply **at most one** transform (forward or inverse), only if improvement passes a threshold:
  - `bestScore < baselineScore * 0.6` **or** `baselineScore - bestScore > 2.0` (mm-scale heuristic).

**Matrix layout** (attributes `m00`–`m23`, `m30`–`m33`):

- Translation is in **`m03`, `m13`, `m23`** (row 0–2, column 3).
- Application (forward):

  ```
  x' = m00·x + m01·y + m02·z + m03
  y' = m10·x + m11·y + m12·z + m13
  z' = m20·x + m21·y + m22·z + m23
  ```

- Inverse assumes a **rigid** transform: subtract translation, multiply by transpose of 3×3 rotation.

`SceneTransformKind` still exists for diagnostics (`DcmAlignmentCheck` tool); production loading uses **`SceneTransformKind.Scan`** for every DCM via `MainViewModel.ResolveSceneTransformKind`.

**Important:** Transforms are baked into **vertex positions** before rendering. The Helix scene graph does not attach per-file `Transform3D` nodes.

### 2.6 Post-processing (`DcmParserSanitizer`)

Light connectivity cleanup (degenerate slivers, optional component pruning) runs after transforms. Tuned to avoid punching holes in normal crown geometry.

---

## 3. Loading files in the application

### 3.1 Order Info / production cases

`DCMFinder` (`StatsClient/MVVM/Core/DCMFinder.cs`) discovers:

- **Scans** under `{OrderFolder}\Scans\` (preparation, antagonist, preop, etc.)
- **Designed parts** under `{OrderFolder}\CAD\` only (not Anatomy elements / External models)

`MainViewModel.LoadFilesAsync` loads scans first, then CAD, using:

```csharp
_parser.ParseFile(
    filePath,
    decodeMode,
    applySceneTransform: sceneTransformKind != SceneTransformKind.None,
    sceneTransformKind: sceneTransformKind);
```

### 3.2 Standalone DCM Viewer window

Same parser; drag-and-drop accepts `.dcm`, `.stl`, `.xml`.

### 3.3 Environment variables (optional)

| Variable | Effect |
|----------|--------|
| `DCMVIEWER_FORCE_3SHAPE_DECRYPT=1` | Always try 3Shape install decrypt fallback |
| `DCMVIEWER_FORCE_NATIVE_FALLBACK=1` | Force native mesh loader |
| `DCMVIEWER_ENABLE_NATIVE_LOADER=1` | Enable native loader when parse is weak |
| `DCMVIEWER_ENABLE_INT32_VERTEX_FALLBACK=1` | Try int32-scaled vertex decode |
| `DCMVIEWER_CE_EXHAUSTIVE=1` | Exhaustive CE key search (slow) |

---

## 4. Exporting to STL

Parsed meshes are **`MeshSnapshot`** instances: `Point3D[] Positions`, `int[] TriangleIndices` (3 indices per triangle).

### 4.1 From the UI (DCM Viewer)

1. Load one or more DCM/STL files.
2. Toggle visibility so only the meshes you want are shown.
3. Use export commands on `MainViewModel`:
   - **Merged STL** — one file, all visible meshes concatenated (`MeshExportService.Export`).
   - **Separate STLs** — one `.stl` per visible layer (`MeshExportService.ExportSeparateStl`), names like `{base}_{DisplayName}.stl`.

Export uses **binary STL** (80-byte header, uint32 triangle count, 50 bytes per triangle: normal + 3 vertices + attribute).

### 4.2 Programmatic export (same assembly)

`MeshExportService` is `internal` to the DCM Viewer project. From code in **StatsClient**:

```csharp
using DCMViewer.Services;

var parser = new DcmParser();
var parsed = parser.ParseFile(
    @"C:\Orders\436\Scans\Upper\PreparationScan.dcm",
    applySceneTransform: true,
    sceneTransformKind: SceneTransformKind.Scan);

// Single mesh
var outPath = @"C:\temp\preparation.stl";
MeshExportService.Export(outPath, new[] { parsed.Mesh });

// Multiple meshes (merged into one STL)
var prep = parser.ParseFile(prepPath, sceneTransformKind: SceneTransformKind.Scan);
var ant = parser.ParseFile(antPath, sceneTransformKind: SceneTransformKind.Scan);
MeshExportService.Export(@"C:\temp\case436-merged.stl", new[] { prep.Mesh, ant.Mesh });

// Separate files
var items = new[]
{
    new MeshExportService.MeshExportItem("prep", prep.Mesh),
    new MeshExportService.MeshExportItem("ant", ant.Mesh),
};
MeshExportService.ExportSeparateStl(@"C:\temp\case436.stl", items);
// Creates C:\temp\case436_prep.stl, C:\temp\case436_ant.stl, etc.
```

**PLY** is also supported if the output path ends with `.ply` (ASCII PLY, merged vertices).

### 4.3 STL format details (our writer)

Implemented in `MeshExportService.WriteBinaryStl`:

- Header: 80 bytes (ASCII label `"DCMViewer STL export"`).
- Triangle count: `uint32` little-endian.
- Per triangle: unit normal (float×3), three vertices (float×3), uint16 attribute = 0.
- Normals computed from vertex positions via cross product.

Coordinates are **millimeters** in the same space as the viewer after `CoordinateTransform` (case space).

### 4.4 Export without scene transforms

For debugging raw HPS coordinates (compare to external tools that do not apply annotations):

```csharp
var raw = parser.ParseFile(path, applySceneTransform: false);
MeshExportService.Export(@"C:\temp\raw.stl", new[] { raw.Mesh });
```

Prep and antagonist will generally **not** align with each other or with CAD in this mode.

---

## 5. Batch / command-line style workflow

There is no separate CLI in StatsClient. Options:

1. **`tools/DcmAlignmentCheck`** — console app referencing `DcmParser`; prints bounds/overlap for order folders (transform mode experiments).
2. **Small script** in a test project referencing `StatsClient` — call `ParseFile` + `MeshExportService` as above.
3. **UI** — load folder via DCM Viewer, export visible meshes.

Example alignment check:

```bat
dotnet run --project tools\DcmAlignmentCheck\DcmAlignmentCheck.csproj -- "\\server\3Shape Dental System Orders\436-..."
```

---

## 6. Troubleshooting

| Symptom | Likely cause |
|---------|----------------|
| `encrypted with 3Shape's proprietary encryption` | CE decrypt failed; need EKID keys, 3Shape install, or valid `PackageLockList` |
| Meshes load but float apart | `applySceneTransform: false` or missing Comment origins (transform skipped) |
| Spiky / bird-nest mesh | Wrong facet decode; sanitizer may reduce; try `DCMVIEWER_FORCE_3SHAPE_DECRYPT` |
| Crown “flies away” from scan | Usually wrong transform policy; production uses best-fit like legacy viewer |
| Empty mesh | File has no decodable Vertices/Facets in XML path; try embedded HPS fallback |

---

## 7. Related files (quick index)

```
StatsClient/DcmViewer/
  Services/
    DcmParser.cs              Parse pipeline, CE decrypt, transforms
    DcmParserSanitizer.cs     Mesh connectivity cleanup
    MeshSnapshot.cs           Positions + indices
    MeshExportService.cs      STL / PLY export
    ThreeShapeDecryptor.cs    Optional 3Shape install decrypt
    ThreeShapeNativeMeshLoader.cs
  ViewModels/
    MainViewModel.cs          LoadFilesAsync, export commands
  MVVM/Core/
    DCMFinder.cs              Order folder scan discovery

tools/DcmAlignmentCheck/
  Program.cs                  Bounds / transform experiments
```

---

## 8. External references

- [HIMSA Packed Scan Standard](https://himsanoah.atlassian.net/wiki/spaces/AD/pages/1309803049/Packed+Scan+Standard) (3Shape / HIMSA documentation)
- [hpsdecode](https://github.com/HeadTriXz/hpsdecode) — Python HPS/DCM geometry decoder (no scene transforms)
- Legacy reference implementation: `C:\Users\ambru\source\repos\DCMViewer` (Helix WPF, same best-fit transform idea)

---

*Last updated to match StatsClient DCM Viewer behavior after legacy transform alignment (cases 364 / 436).*
