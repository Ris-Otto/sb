namespace Sb.Core.Storage;

/// <summary>
/// A filesystem change paired with the action that undoes it. <see cref="AtomicOperation"/> only
/// calls <see cref="Undo"/> when <see cref="Do"/> returns successfully but the paired database action
/// then fails — if <see cref="Do"/> itself throws, <see cref="Undo"/> never runs (there is, from the
/// caller's perspective, nothing to undo). When <see cref="Do"/> performs more than one filesystem
/// step, it must therefore leave the filesystem back in its original state before rethrowing on any
/// step after the first — e.g. by wrapping the later steps in their own try/catch that reverses the
/// earlier ones — rather than relying on <see cref="Undo"/> to clean up a partial attempt.
/// </summary>
public readonly record struct ReversibleAction(Action Do, Action Undo);
