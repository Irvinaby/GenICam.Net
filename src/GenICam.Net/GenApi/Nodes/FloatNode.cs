using System.Buffers.Binary;
using System.Globalization;

namespace GenICam.Net.GenApi;

/// <summary>
/// Concrete float node implementation.
/// </summary>
public class FloatNode : ValueNode, IFloat
{
    private double _value;

    public double Value
    {
        get
        {
            if (PValueNode is IFloat linked)
                return linked.Value;
            if (PValueNode is IInteger iLinked)
                return iLinked.Value;
            if (Port is not null && RegisterAddress.HasValue)
                return ReadFromRegister();
            return _value;
        }
        set
        {
            if (value < Min || value > Max)
                throw new ArgumentOutOfRangeException(nameof(value), $"Value {value} is out of range [{Min}, {Max}].");

            if (PValueNode is IFloat linked)
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

    public double Min { get; internal set; } = double.MinValue;
    public double Max { get; internal set; } = double.MaxValue;
    public bool HasIncrement { get; internal set; }
    public double Increment { get; internal set; }
    public Representation Representation { get; internal set; } = Representation.PureNumber;
    public string Unit { get; internal set; } = string.Empty;

    public long IntValue
    {
        get => (long)Value;
        set => Value = value;
    }

    /// <summary>Expression formula (if this node is a SwissKnife).</summary>
    internal string? Formula { get; set; }

    /// <summary>Variables used in the formula, mapping variable name to node name.</summary>
    internal Dictionary<string, string> FormulaVariables { get; } = new();

    /// <summary>Register address for FloatReg nodes.</summary>
    internal long? RegisterAddress { get; set; }

    /// <summary>Register length in bytes for FloatReg nodes (default 4).</summary>
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
        get => Value.ToString("R", CultureInfo.InvariantCulture);
        set => Value = double.Parse(value, CultureInfo.InvariantCulture);
    }

    internal void SetValueDirect(double value) => _value = value;

    private double ReadFromRegister()
    {
        var data = Port!.Read(RegisterAddress!.Value, RegisterLength);
        return RegisterLength switch
        {
            4 => Endianness == Endianness.BigEndian
                ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(data))
                : BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data)),
            8 => Endianness == Endianness.BigEndian
                ? BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(data))
                : BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(data)),
            _ => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(data)),
        };
    }

    private void WriteToRegister(double value)
    {
        var data = new byte[RegisterLength];
        switch (RegisterLength)
        {
            case 4:
                var intBits = BitConverter.SingleToInt32Bits((float)value);
                if (Endianness == Endianness.BigEndian)
                    BinaryPrimitives.WriteInt32BigEndian(data, intBits);
                else
                    BinaryPrimitives.WriteInt32LittleEndian(data, intBits);
                break;
            case 8:
                var longBits = BitConverter.DoubleToInt64Bits(value);
                if (Endianness == Endianness.BigEndian)
                    BinaryPrimitives.WriteInt64BigEndian(data, longBits);
                else
                    BinaryPrimitives.WriteInt64LittleEndian(data, longBits);
                break;
        }
        Port!.Write(RegisterAddress!.Value, data);
        _value = value;
    }
}
