using System.Buffers.Binary;
using System.Globalization;

namespace GenICam.Net.GenApi;

/// <summary>
/// Concrete integer node implementation.
/// </summary>
public class IntegerNode : ValueNode, IInteger
{
    private long _value;
    private bool _registerCacheDirty = true;

    public long Value
    {
        get
        {
            if (PValueNode is IInteger linked)
                return linked.Value;
            if (Port is not null && RegisterAddress.HasValue)
            {
                if (_registerCacheDirty)
                {
                    _value = ReadFromRegister();
                    _registerCacheDirty = false;
                }
                return _value;
            }
            return _value;
        }
        set
        {
            if (value < Min || value > Max)
                throw new ArgumentOutOfRangeException(nameof(value), $"Value {value} is out of range [{Min}, {Max}].");
            if (Increment != 0 && (value - Min) % Increment != 0)
                throw new ArgumentException($"Value {value} is not a valid increment step from {Min} with increment {Increment}.", nameof(value));

            if (PValueNode is IInteger linked)
            {
                linked.Value = value;
                OnValueChanged();
                return;
            }
            if (Port is not null && RegisterAddress.HasValue)
            {
                WriteToRegister(value);
                OnValueChanged();
                return;
            }
            _value = value;
            OnValueChanged();
        }
    }

    public long Min { get; internal set; } = long.MinValue;
    public long Max { get; internal set; } = long.MaxValue;
    public long Increment { get; internal set; } = 1;
    public Representation Representation { get; internal set; } = Representation.PureNumber;
    public string Unit { get; internal set; } = string.Empty;

    /// <summary>Marks the cached register value as stale so the next read fetches from the device.</summary>
    internal void InvalidateCache() => _registerCacheDirty = true;

    /// <summary>Expression formula (if this node is a SwissKnife/IntSwissKnife).</summary>
    internal string? Formula { get; set; }

    /// <summary>Variables used in the formula, mapping variable name to node name.</summary>
    internal Dictionary<string, string> FormulaVariables { get; } = new();

    /// <summary>Register address for IntReg nodes.</summary>
    internal long? RegisterAddress { get; set; }

    /// <summary>Register length in bytes for IntReg nodes (default 4).</summary>
    internal long RegisterLength { get; set; } = 4;

    /// <summary>Byte order for register access.</summary>
    internal Endianness Endianness { get; set; } = Endianness.BigEndian;

    /// <summary>Port for register access.</summary>
    internal IPort? Port { get; set; }

    /// <summary>Name of the pValue reference node.</summary>
    internal string? PValueNodeName { get; set; }

    /// <summary>Resolved pValue reference node.</summary>
    internal INode? PValueNode { get; set; }

    public override string ValueAsString
    {
        get => Value.ToString(CultureInfo.InvariantCulture);
        set => Value = long.Parse(value, CultureInfo.InvariantCulture);
    }

    internal void SetValueDirect(long value) => _value = value;

    private long ReadFromRegister()
    {
        var data = Port!.Read(RegisterAddress!.Value, RegisterLength);
        return RegisterLength switch
        {
            1 => data[0],
            2 => Endianness == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt16BigEndian(data)
                : BinaryPrimitives.ReadInt16LittleEndian(data),
            4 => Endianness == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(data)
                : BinaryPrimitives.ReadInt32LittleEndian(data),
            8 => Endianness == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt64BigEndian(data)
                : BinaryPrimitives.ReadInt64LittleEndian(data),
            _ => BinaryPrimitives.ReadInt32BigEndian(data),
        };
    }

    private void WriteToRegister(long value)
    {
        var data = new byte[RegisterLength];
        switch (RegisterLength)
        {
            case 1:
                data[0] = (byte)value;
                break;
            case 2:
                if (Endianness == Endianness.BigEndian)
                    BinaryPrimitives.WriteInt16BigEndian(data, (short)value);
                else
                    BinaryPrimitives.WriteInt16LittleEndian(data, (short)value);
                break;
            case 4:
                if (Endianness == Endianness.BigEndian)
                    BinaryPrimitives.WriteInt32BigEndian(data, (int)value);
                else
                    BinaryPrimitives.WriteInt32LittleEndian(data, (int)value);
                break;
            case 8:
                if (Endianness == Endianness.BigEndian)
                    BinaryPrimitives.WriteInt64BigEndian(data, value);
                else
                    BinaryPrimitives.WriteInt64LittleEndian(data, value);
                break;
        }
        Port!.Write(RegisterAddress!.Value, data);
        _value = value;
        _registerCacheDirty = false;
    }
}
