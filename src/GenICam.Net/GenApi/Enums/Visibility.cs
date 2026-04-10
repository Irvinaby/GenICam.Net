namespace GenICam.Net.GenApi;

/// <summary>
/// Visibility of a node as defined by the GenICam GenApi standard.
/// </summary>
public enum Visibility
{
    /// <summary>Visible to beginners.</summary>
    Beginner,

    /// <summary>Visible to experts.</summary>
    Expert,

    /// <summary>Visible to gurus only.</summary>
    Guru,

    /// <summary>Not visible (internal).</summary>
    Invisible,
}
