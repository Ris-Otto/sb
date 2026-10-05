namespace Sb.Core.Storage;

/// <summary>A filesystem change paired with the action that undoes it.</summary>
public readonly record struct ReversibleAction(Action Do, Action Undo);
