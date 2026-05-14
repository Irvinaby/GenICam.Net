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
            if (Formula is not null && NodeMap is not null)
                return (long)FormulaEvaluator.Evaluate(Formula, FormulaVariables, NodeMap);
            if (PValueNode is IInteger linked)
                return linked.Value;
            if (Port is not null && HasRegisterAddress)
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
            if (Port is not null && HasRegisterAddress)
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

    /// <summary>Name of the pAddress reference node.</summary>
    internal string? PAddressNodeName { get; set; }

    /// <summary>Resolved pAddress reference node.</summary>
    internal INode? PAddressNode { get; set; }

    /// <summary>Register length in bytes for IntReg nodes (default 4).</summary>
    internal long RegisterLength { get; set; } = 4;

    /// <summary>Sign interpretation for integer register access.</summary>
    internal Sign Sign { get; set; } = Sign.Signed;

    /// <summary>Byte order for register access.</summary>
    internal Endianness Endianness { get; set; } = Endianness.BigEndian;

    /// <summary>Single bit position for MaskedIntReg nodes.</summary>
    internal int? Bit { get; set; }

    /// <summary>Least significant bit for MaskedIntReg nodes.</summary>
    internal int? Lsb { get; set; }

    /// <summary>Most significant bit for MaskedIntReg nodes.</summary>
    internal int? Msb { get; set; }

    /// <summary>Port for register access.</summary>
    internal IPort? Port { get; set; }

    /// <summary>Name of the pValue reference node.</summary>
    internal string? PValueNodeName { get; set; }

    /// <summary>Resolved pValue reference node.</summary>
    internal INode? PValueNode { get; set; }

    /// <summary>Node map used to resolve formula variables.</summary>
    internal NodeMap? NodeMap { get; set; }

    internal bool HasRegisterAddress => RegisterAddress.HasValue || PAddressNode is IInteger;

    public override string ValueAsString
    {
        get => Value.ToString(CultureInfo.InvariantCulture);
        set => Value = long.Parse(value, CultureInfo.InvariantCulture);
    }

    internal void SetValueDirect(long value) => _value = value;

    private long ReadFromRegister()
    {
        var data = Port!.Read(GetRegisterAddress(), RegisterLength);
        if (Sign == Sign.Unsigned)
        {
            return ApplyMask(RegisterLength switch
            {
                1 => data[0],
                2 => Endianness == Endianness.BigEndian
                    ? BinaryPrimitives.ReadUInt16BigEndian(data)
                    : BinaryPrimitives.ReadUInt16LittleEndian(data),
                4 => Endianness == Endianness.BigEndian
                    ? BinaryPrimitives.ReadUInt32BigEndian(data)
                    : BinaryPrimitives.ReadUInt32LittleEndian(data),
                8 => CheckedUInt64ToInt64(Endianness == Endianness.BigEndian
                    ? BinaryPrimitives.ReadUInt64BigEndian(data)
                    : BinaryPrimitives.ReadUInt64LittleEndian(data)),
                _ => BinaryPrimitives.ReadUInt32BigEndian(data),
            });
        }

        return ApplyMask(RegisterLength switch
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
        });
    }

    private void WriteToRegister(long value)
    {
        var data = new byte[RegisterLength];
        if (Sign == Sign.Unsigned)
        {
            switch (RegisterLength)
            {
                case 1:
                    data[0] = checked((byte)value);
                    break;
                case 2:
                    if (Endianness == Endianness.BigEndian)
                        BinaryPrimitives.WriteUInt16BigEndian(data, checked((ushort)value));
                    else
                        BinaryPrimitives.WriteUInt16LittleEndian(data, checked((ushort)value));
                    break;
                case 4:
                    if (Endianness == Endianness.BigEndian)
                        BinaryPrimitives.WriteUInt32BigEndian(data, checked((uint)value));
                    else
                        BinaryPrimitives.WriteUInt32LittleEndian(data, checked((uint)value));
                    break;
                case 8:
                    if (Endianness == Endianness.BigEndian)
                        BinaryPrimitives.WriteUInt64BigEndian(data, checked((ulong)value));
                    else
                        BinaryPrimitives.WriteUInt64LittleEndian(data, checked((ulong)value));
                    break;
            }
        }
        else switch (RegisterLength)
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
        Port!.Write(GetRegisterAddress(), data);
        _value = value;
        _registerCacheDirty = false;
    }

    private long GetRegisterAddress()
    {
        if (PAddressNode is IInteger addressNode)
            return addressNode.Value + RegisterAddress.GetValueOrDefault();

        if (RegisterAddress.HasValue)
            return RegisterAddress.Value;

        throw new InvalidOperationException($"Integer node '{Name}' does not have a register address.");
    }

    private long ApplyMask(long value)
    {
        if (Bit.HasValue)
            return (value >> Bit.Value) & 1;

        if (Lsb.HasValue && Msb.HasValue)
        {
            var width = Msb.Value - Lsb.Value + 1;
            if (width <= 0)
                throw new InvalidOperationException($"MaskedIntReg '{Name}' has an invalid bit range.");

            var mask = width >= 63 ? long.MaxValue : (1L << width) - 1;
            return (value >> Lsb.Value) & mask;
        }

        return value;
    }

    private static long CheckedUInt64ToInt64(ulong value)
    {
        if (value > long.MaxValue)
            throw new OverflowException($"Unsigned 64-bit register value {value} cannot be represented as Int64.");

        return (long)value;
    }
}
