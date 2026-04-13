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

            var target = NodeMap.GetNode(RegisterNodeName);
            return target switch
            {
                IRegister reg => !IsRegisterValue(reg, CommandValue),
                IInteger intNode => intNode.Value != CommandValue,
                _ => true
            };
        }
    }

    public void Execute()
    {
        if (AccessMode is AccessMode.NA or AccessMode.NI)
            throw new InvalidOperationException($"Command '{Name}' is not available (AccessMode={AccessMode}).");

        if (NodeMap is null || RegisterNodeName is null)
            return;

        var target = NodeMap.GetNode(RegisterNodeName);
        switch (target)
        {
            case IRegister reg:
                WriteToRegister(reg, CommandValue);
                break;
            case IInteger intNode:
                intNode.Value = CommandValue;
                break;
        }
    }

    private static bool IsRegisterValue(IRegister reg, long expected)
    {
        var data = reg.Get(reg.Length);
        var value = reg.Length switch
        {
            4 => BinaryPrimitives.ReadInt32BigEndian(data),
            _ => 0L
        };
        return value == expected;
    }

    private static void WriteToRegister(IRegister reg, long value)
    {
        var data = new byte[reg.Length];
        switch (reg.Length)
        {
            case 4:
                BinaryPrimitives.WriteInt32BigEndian(data, (int)value);
                break;
            case 8:
                BinaryPrimitives.WriteInt64BigEndian(data, value);
                break;
            default:
                data[0] = (byte)value;
                break;
        }
        reg.Set(data);
    }
}
