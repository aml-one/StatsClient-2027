namespace DCMViewer.Services;

/// <summary>Information captured while loading a DCM so sculpted meshes can be written back.</summary>
public sealed record DcmMeshWriteProfile(
    int VertexCount,
    string Schema,
    bool UseDeltaEncoding,
    bool IsEncrypted,
    CeEncryptionProfile? Encryption,
    SceneTransformProfile? SceneTransform,
    bool VerticesStoredAsBase64);

public sealed record CeEncryptionProfile(byte[] Key, bool PreSwap, bool PostSwap);

public sealed record SceneTransformProfile(
    double M00, double M01, double M02, double M03,
    double M10, double M11, double M12, double M13,
    double M20, double M21, double M22, double M23,
    bool AppliedInverse,
    bool UseColumnMajor);
