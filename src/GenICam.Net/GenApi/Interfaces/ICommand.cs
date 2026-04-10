namespace GenICam.Net.GenApi;

/// <summary>
/// Represents an executable command as defined by the GenICam GenApi standard.
/// Commands trigger device actions such as starting/stopping acquisition or saving a user set.
/// </summary>
/// <remarks>
/// Common examples: AcquisitionStart, AcquisitionStop, TriggerSoftware, UserSetSave.
/// Commands are write-only by nature; <see cref="IsDone"/> indicates completion.
/// </remarks>
public interface ICommand : INode
{
    /// <summary>
    /// Executes the command by writing the command value to the target register.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the command is not available (<see cref="INode.AccessMode"/> is <see cref="AccessMode.NA"/> or <see cref="AccessMode.NI"/>).
    /// </exception>
    void Execute();

    /// <summary>
    /// Indicates whether the command has finished executing.
    /// Poll this property after calling <see cref="Execute"/> for asynchronous commands.
    /// </summary>
    bool IsDone { get; }
}
