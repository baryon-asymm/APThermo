namespace AerospacePropellantThermodynamics.Execution.Chunks;

/// <summary>What happens to a chunk buffer around a launch.</summary>
internal enum ChunkTransfer
{
    /// <summary>Copied from its host array before the launch.</summary>
    Input,

    /// <summary>Copied to its host array after the launch.</summary>
    Output,

    /// <summary>Zeroed before the launch and copied to its host array after it: the kernel writes only the slots of the cases it solved.</summary>
    ClearedOutput,

    /// <summary>Working memory of the kernel: never moved, only sized.</summary>
    Scratch,

    /// <summary>The same for every case and every chunk: uploaded once, when the buffers are allocated.</summary>
    Constant,
}
