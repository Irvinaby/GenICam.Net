namespace GenICam.Net.GenApi;

/// <summary>
/// Concrete command node implementation.
/// </summary>
public class CommandNode : NodeBase, ICommand
{
    /// <summary>The value written to the register when the command executes.</summary>
    internal long CommandValue { get; set; }

    /// <summary>The register node to write to.</summary>
    internal string? RegisterNodeName { get; set; }

    /// <summary>Reference to the node map for resolving register references.</summary>
    internal NodeMap? NodeMap { get; set; }

    public bool IsDone
    {
        get
        {
            // In a real implementation, this would read the register and compare.
            return true;
        }
    }

    public void Execute()
    {
        if (AccessMode is AccessMode.NA or AccessMode.NI)
            throw new InvalidOperationException($"Command '{Name}' is not available (AccessMode={AccessMode}).");

        // In a full implementation, this would write CommandValue to the target register via the port.
    }
}
