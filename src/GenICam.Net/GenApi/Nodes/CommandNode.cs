using System.Buffers.Binary;

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
            if (NodeMap is null || RegisterNodeName is null)
                return true;

            var register = NodeMap.GetNode(RegisterNodeName) as IRegister;
            if (register is null)
                return true;

            var data = register.Get(register.Length);
            var value = register.Length switch
            {
                4 => BinaryPrimitives.ReadInt32BigEndian(data),
                _ => 0L
            };
            return value != CommandValue;
        }
    }

    public void Execute()
    {
        if (AccessMode is AccessMode.NA or AccessMode.NI)
            throw new InvalidOperationException($"Command '{Name}' is not available (AccessMode={AccessMode}).");

        if (NodeMap is null || RegisterNodeName is null)
            return;

        var register = NodeMap.GetNode(RegisterNodeName) as IRegister;
        if (register is null)
            return;

        var data = new byte[register.Length];
        switch (register.Length)
        {
            case 4:
                BinaryPrimitives.WriteInt32BigEndian(data, (int)CommandValue);
                break;
            case 8:
                BinaryPrimitives.WriteInt64BigEndian(data, CommandValue);
                break;
            default:
                data[0] = (byte)CommandValue;
                break;
        }

        register.Set(data);
    }
}
