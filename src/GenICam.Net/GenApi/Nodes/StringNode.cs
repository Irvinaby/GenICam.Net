namespace GenICam.Net.GenApi;

using System.Text;

/// <summary>
/// Concrete string node implementation.
/// </summary>
public class StringNode : ValueNode, IString
{
    private string _value = string.Empty;
    private bool _registerCacheDirty = true;

    public string Value
    {
        get
        {
            if (Port is not null && RegisterAddress.HasValue)
            {
                if (_registerCacheDirty)
                {
                    _value = ReadFromRegister();
                    _registerCacheDirty = false;
                }
            }

            return _value;
        }
        set
        {
            if (MaxLength > 0 && value.Length > MaxLength)
                throw new ArgumentException($"String length {value.Length} exceeds maximum {MaxLength}.", nameof(value));

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

    public long MaxLength { get; internal set; }

    /// <summary>Register address for StringReg nodes.</summary>
    internal long? RegisterAddress { get; set; }

    /// <summary>Register length in bytes for StringReg nodes.</summary>
    internal long RegisterLength { get; set; }

    /// <summary>Port for register access.</summary>
    internal IPort? Port { get; set; }

    /// <summary>Marks the cached register value as stale so the next read fetches from the device.</summary>
    internal void InvalidateCache() => _registerCacheDirty = true;

    public override string ValueAsString
    {
        get => Value;
        set => Value = value;
    }

    internal void SetValueDirect(string value) => _value = value;

    private string ReadFromRegister()
    {
        var length = RegisterLength > 0 ? RegisterLength : MaxLength;
        if (length <= 0)
            return _value;

        var data = Port!.Read(RegisterAddress!.Value, length);
        var stringLength = Array.IndexOf(data, (byte)0);
        if (stringLength < 0)
            stringLength = data.Length;

        return Encoding.ASCII.GetString(data, 0, stringLength).TrimEnd();
    }

    private void WriteToRegister(string value)
    {
        var length = RegisterLength > 0 ? RegisterLength : MaxLength;
        var bytes = Encoding.ASCII.GetBytes(value);
        var data = new byte[Math.Max(length, bytes.Length + 1)];
        bytes.CopyTo(data, 0);

        Port!.Write(RegisterAddress!.Value, data);
        _value = value;
        _registerCacheDirty = false;
    }
}
